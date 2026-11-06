using System.Runtime.CompilerServices;
using CustomHUD.Config;
using MapChooser.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;

namespace CustomHUD.Integrations.MapPages;

// MapChooser's pages with each map's art, read from IMapChooserApi: every map row carries the map's
// name as Tag, which GetMap resolves to the map the server loads. The end-of-map ballot also gets each
// option's count, a share bar and the player's own pick. After voting, MapChooser closes the ballot;
// CustomHUD then opens a read-only tally in its place, which closes when the vote ends.
internal sealed class MapChooserIntegration : IPageIntegration, IDisposable
{
    private const string TallyId = "mapchooser.eof-vote.tally";

    private readonly IMapChooserApi _api;
    private readonly ISwiftlyCore _core;
    private readonly IntegrationHost _host;
    private readonly Dictionary<int, IMenu> _tallies = [];

    private MapChooserIntegration(IMapChooserApi api, ISwiftlyCore core, IntegrationHost host)
    {
        _api = api;
        _core = core;
        _host = host;
        _api.VoteChanged += OnVoteChanged;
        _api.PlayerVoted += OnPlayerVoted;
    }

    public string Provider => "MapChooser";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces, ISwiftlyCore core, IntegrationHost host)
    {
        ApiProbe.Require(typeof(IMapChooserApi), "MapChooser 1.4.0", nameof(IMapChooserApi.GetMap));
        return interfaces.TryGetSharedInterface<IMapChooserApi>(IMapChooserApi.InterfaceKey, out var api) && api is not null
            ? new MapChooserIntegration(api, core, host)
            : null;
    }

    public bool Owns(IMenu menu) => menu.Id.StartsWith("mapchooser.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        var ballot = page.Menu.Id.StartsWith("mapchooser.eof-vote", StringComparison.Ordinal);
        if (Hint(page.Menu.Id) is { } hint) page.Hint(T(page, hint));

        var total = _api.TotalVotes;
        var mine = ballot ? _api.GetPlayerVote(page.Player.Slot) : null;
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is not MenuComponentBase { Tag: string name }) continue;
            var parts = new List<string>(4);
            var map = _api.GetMap(name);
            if (Art(page, map) is { Length: > 0 } art) parts.Add(art);

            if (ballot)
            {
                var count = _api.GetVoteCount(name);
                var share = total > 0 ? (int)Math.Round(100.0 * count / total, MidpointRounding.AwayFromZero) : 0;
                parts.Add($"text=\t{count};bar={share}");
                if (name == mine) parts.Add("chosen");
            }
            else if (map is not null)
            {
                parts.Add("sub=" + Players(page, map));
            }

            if (parts.Count > 0) page.Decorate(k, string.Join(';', parts));
        }
    }

    public void Dispose()
    {
        _api.VoteChanged -= OnVoteChanged;
        _api.PlayerVoted -= OnPlayerVoted;
        CloseTallies();
    }

    // A stock map shows its screenshot and logo; another map the server has installed shows its
    // prefix (aim_redline: AIM) in the mark, in that prefix's tone; a workshop map, whose own name
    // is not known until it loads, shows the stand-in mark and a Workshop tag.
    private static string? Art(PageDraw page, MapChooserMap? map)
    {
        if (map is null) return null;
        if (map.MapName is { Length: > 0 } loaded)
        {
            var key = loaded.Trim().ToLowerInvariant();
            if (LockerProfiles.StockMaps.Contains(key)) return "image=map/" + key;
            var prefix = key.Split('_')[0];
            return key.Contains('_') && Directive.IsToken(prefix) ? $"mark={prefix.ToUpperInvariant()};tone={prefix}" : null;
        }
        return map.WorkshopId is not null ? $"mark=WS;tag={T(page, "workshop")};tagtone=dark" : null;
    }

    private static string Players(PageDraw page, MapChooserMap map) => (map.MinPlayers, map.MaxPlayers) switch
    {
        ( <= 0, <= 0) => T(page, "players.any"),
        ( > 0, > 0) => T(page, "players.range", map.MinPlayers, map.MaxPlayers),
        ( > 0, _) => T(page, "players.min", map.MinPlayers),
        _ => T(page, "players.max", map.MaxPlayers),
    };

    // What a click does on each page; the read-only tally and the admin pages keep the default.
    private static string? Hint(string menuId) => menuId switch
    {
        TallyId => null,
        _ when menuId.StartsWith("mapchooser.eof-vote", StringComparison.Ordinal) => "hint",
        "mapchooser.votemap" => "hint",
        "mapchooser.nominate" => "hint.nominate",
        "mapchooser.admin-change-map" => "hint.change",
        "mapchooser.set-next-map" => "hint.next",
        _ => null,
    };

    // A ballot's counts change with every other player's vote, so every open board redraws.
    private void OnVoteChanged()
    {
        if (!_api.IsVoteActive)
        {
            CloseTallies();
            return;
        }

        foreach (var player in _core.PlayerManager.GetAllPlayers())
            if (_core.Menu.GetSession(player) is { IsOpen: true } session && session.Menu.Id.StartsWith("mapchooser.eof-vote", StringComparison.Ordinal))
                session.Invalidate();
    }

    // Next tick: MapChooser closes the ballot after raising this, and that close must not take the tally with it.
    private void OnPlayerVoted(int slot, string option) => _core.Scheduler.NextTick(() =>
    {
        if (!_api.IsVoteActive || _core.PlayerManager.GetPlayer(slot) is not { IsValid: true, IsFakeClient: false } player) return;

        var builder = _core.Menu.CreateMenu(TallyId).WithTitle(_host.Text(slot, "customhud.maps.tally"));
        foreach (var ballot in _api.VoteOptions)
        {
            var extend = ballot == IMapChooserApi.ExtendOption;
            builder.Add(new TallyRow(extend ? _host.Text(slot, "customhud.maps.extend") : ballot, ballot, extend));
        }

        var menu = builder.Build();

        // Only this renderer draws counts; any other would show a list of names that does nothing.
        if (!string.Equals(menu.Renderer.Id, _host.RendererId, StringComparison.OrdinalIgnoreCase))
        {
            menu.Dispose();
            return;
        }

        if (_tallies.Remove(slot, out var previous)) previous.Dispose();
        _tallies[slot] = menu;
        menu.Open(player);
    });

    private void CloseTallies()
    {
        foreach (var menu in _tallies.Values)
        {
            menu.CloseAll();
            menu.Dispose();
        }
        _tallies.Clear();
    }

    private static string T(PageDraw page, string key, params object[] args) => page.Text("customhud.maps." + key, args);

    // Read-only: E, Shift and F also act in game, so nothing on the tally can be focused.
    private sealed class TallyRow : MenuComponentBase
    {
        private static readonly MenuTextStyle Map = MenuTextStyle.Default.WithColor("#90EE90");
        private static readonly MenuTextStyle Extend = MenuTextStyle.Default.WithColor("#FFA500");

        private readonly string _text;
        private readonly bool _extend;

        public TallyRow(string text, string option, bool extend)
        {
            _text = text;
            _extend = extend;
            Tag = option;
        }

        public override bool IsFocusable => false;

        // The ballot's own colours, which the board's view keys its tones on.
        public override MenuNode Render(IMenuComponentRenderContext context) => new MenuTextNode(_text, _extend ? Extend : Map);
    }
}
