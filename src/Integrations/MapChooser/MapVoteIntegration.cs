using System.Runtime.CompilerServices;
using MapChooser.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;

namespace CustomHUD.Integrations.MapPages;

// MapChooser's end-of-map ballot with each option's count, a share bar and the player's own pick,
// all read from IMapChooserApi; a vote row carries its ballot option as Tag. After voting,
// MapChooser closes the ballot; CustomHUD then opens a read-only tally in its place, which closes
// when the vote ends.
internal sealed class MapVoteIntegration : IPageIntegration, IDisposable
{
    private const string TallyId = "mapchooser.eof-vote.tally";

    private readonly IMapChooserApi _api;
    private readonly ISwiftlyCore _core;
    private readonly IntegrationHost _host;
    private readonly Dictionary<int, IMenu> _tallies = [];

    private MapVoteIntegration(IMapChooserApi api, ISwiftlyCore core, IntegrationHost host)
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
        => interfaces.TryGetSharedInterface<IMapChooserApi>(IMapChooserApi.InterfaceKey, out var api) && api is not null
            ? new MapVoteIntegration(api, core, host)
            : null;

    public bool Owns(IMenu menu) => menu.Id.StartsWith("mapchooser.eof-vote", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        // The tally is read-only, so it keeps the default line.
        if (page.Menu.Id != TallyId) page.Hint(page.Text("customhud.maps.hint"));

        var total = _api.TotalVotes;
        var mine = _api.GetPlayerVote(page.Player.Slot);
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is not MenuComponentBase { Tag: string option }) continue;
            var count = _api.GetVoteCount(option);
            var share = total > 0 ? (int)Math.Round(100.0 * count / total, MidpointRounding.AwayFromZero) : 0;
            page.Decorate(k, $"text=\t{count};bar={share}" + (option == mine ? ";chosen" : ""));
        }
    }

    public void Dispose()
    {
        _api.VoteChanged -= OnVoteChanged;
        _api.PlayerVoted -= OnPlayerVoted;
        CloseTallies();
    }

    // A ballot's counts change with every other player's vote, so every open board redraws.
    private void OnVoteChanged()
    {
        if (!_api.IsVoteActive)
        {
            CloseTallies();
            return;
        }

        foreach (var player in _core.PlayerManager.GetAllPlayers())
            if (_core.Menu.GetSession(player) is { IsOpen: true } session && Owns(session.Menu))
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
