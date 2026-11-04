using System.Runtime.CompilerServices;
using System.Text;
using Ranks.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;

namespace CustomHUD.Integrations.RankPages;

// Ranks' menus (ranks.*) as their designed pages: the rank as a hero card over the rank road,
// counters as tiles, the leaderboard as a podium over a table, settings as a form. The menus are
// Ranks' own and unchanged; every figure drawn here comes from IRanksApi.
internal sealed class RanksIntegration(IRanksApi api) : IPageIntegration, IRankChips
{
    private const int Podium = 3;

    public string Provider => "Ranks";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces)
    {
        ApiProbe.Require(typeof(IRanksApi), "Ranks 1.0.5",
            nameof(IRanksApi.GetLadder), nameof(IRanksApi.GetRankIcon), nameof(IRanksApi.LoadSnapshotAsync));
        ApiProbe.Require(typeof(RanksPlayerSnapshot), "Ranks 1.0.5", nameof(RanksPlayerSnapshot.Headshots));
        return interfaces.TryGetSharedInterface<IRanksApi>(IRanksApi.InterfaceKey, out var api) && api is not null
            ? new RanksIntegration(api)
            : null;
    }

    public bool Owns(IMenu menu) => menu.Id.StartsWith("ranks.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) =>
        menu.Id == "ranks.top" && CollectionItems.Of(component) is RanksLeaderboardEntry { Position: <= Podium } ? "banner" : null;

    public void Draw(PageDraw page)
    {
        switch (page.Menu.Id)
        {
            case "ranks.myrank":
                AddChip(page);
                page.Hint(T(page, "hint.myrank"));
                DrawMyRank(page);
                break;
            case "ranks.ladder":
                AddChip(page);
                DrawLadder(page);
                break;
            case "ranks.stats.main":
                AddChip(page);
                page.Hint(T(page, "hint.stats"));
                if (api.GetSnapshot(page.Player.SteamID) is { } own) DrawStats(page, own);
                break;
            case "ranks.top":
                AddChip(page);
                page.Hint(T(page, "hint.top"));
                DrawTop(page);
                break;
            case "ranks.top.player":
                DrawPlayer(page);
                break;
            case "ranks.settings":
                AddChip(page);
                page.Hint(T(page, "hint.settings"));
                DrawSettings(page);
                break;
            case "ranks.settings.reset":
                AddChip(page);
                DrawReset(page);
                break;
        }
    }

    // The player's rank beside the title, as their scoreboard draws it.
    public void AddChip(PageDraw page)
    {
        if (api.GetSnapshot(page.Player.SteamID) is not { } me) return;
        var tiers = api.GetLadder();
        if (tiers.Count == 0) return;

        var index = IndexFor(tiers, me.Points);
        var icon = api.GetRankIcon(me.Points);

        // A CS Rating badge is the points; an emblem needs them spelled out beside it.
        var caption = Rated(icon) ? "" : $"{Directive.Number(me.Points)} {T(page, "xp")}";
        page.AddHeader($"{Directive.Clean(tiers[index].Name)}\t{caption}", "chip;" + RankArt.For(icon, index, tiers.Count).Payload);
    }

    // Ranks lists the rank, the XP and the progress as three lines and the ladder link last. The
    // first line becomes the hero card; the other two are folded into it, since it shows both.
    // The ladder page draws the same road whole, so its link only earns a place under a road
    // that is windowed.
    private void DrawMyRank(PageDraw page)
    {
        if (api.GetSnapshot(page.Player.SteamID) is not { } me) return;
        var tiers = api.GetLadder();
        if (tiers.Count == 0) return;

        int hero = -1, link = -1;
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is SubmenuComponent)
            {
                if (link < 0) link = k;
                page.Decorate(k, tiers.Count > RoadSpan ? "at=foot" : "fold");
            }
            else if (hero < 0)
            {
                hero = k;
            }
            else
            {
                page.Decorate(k, "fold");
            }
        }

        if (hero >= 0) DrawHero(page, hero, tiers, me.Points);
        AddRoad(page, tiers, me.Points, windowed: true, before: link >= 0 ? link : null);
    }

    private void DrawHero(PageDraw page, int row, IReadOnlyList<RanksTier> tiers, int points)
    {
        var index = IndexFor(tiers, points);
        var tier = tiers[index];
        var next = index + 1 < tiers.Count ? tiers[index + 1] : null;
        var icon = api.GetRankIcon(points);
        var rated = Rated(icon);
        var name = Directive.Clean(tier.Name);

        var payload = new StringBuilder("info;at=banner;")
            .Append(RankArt.For(icon, index, tiers.Count).Payload)
            .Append(";type=").Append(T(page, rated ? "cs_rating" : "current_rank"))
            .Append(";sub=").Append(T(page, rated ? "band" : "position", index + 1, tiers.Count))
            .Append(";bar=").Append(Directive.Bar(Progress(tiers, index, points) * 100))
            .Append(";tag=").Append(name).Append(" · ").Append(Directive.Number(tier.Points));

        if (next is null)
        {
            payload.Append(";note=").Append(T(page, "top_band"));
        }
        else
        {
            var nextName = Directive.Clean(next.Name);
            payload.Append(";tag2=").Append(nextName).Append(" · ").Append(Directive.Number(next.Points))
                .Append(";note=").Append(T(page, "to_next", Directive.Number(next.Points - points), nextName));
        }

        // The badge already shows a CS Rating's points; an emblem gets them as the value.
        page.Draw(row, rated ? name : $"{name}\t{Directive.Number(points)} {T(page, "xp")}", payload.ToString());
    }

    // The page fits ten nodes on a line, so a longer ladder shows the seven around the player's own.
    private const int RoadSpan = 10;

    private void AddRoad(PageDraw page, IReadOnlyList<RanksTier> tiers, int points, bool windowed, int? before)
    {
        var index = IndexFor(tiers, points);
        var window = windowed && tiers.Count > RoadSpan;
        var first = window ? Math.Clamp(index - 3, 0, tiers.Count - 7) : 0;
        var shown = window ? 7 : tiers.Count;

        var count = T(page, "road_count", tiers.Count);
        if (window) count += " · " + T(page, "road_shown", first + 1, first + shown);
        page.Add($"{T(page, "road")}\t{count}", "section", before);

        for (var i = first; i < first + shown; i++)
            page.Add(Directive.Clean(tiers[i].Name), RoadNode(page, tiers, i, index), before);
    }

    // One line per tier, in ladder order: each becomes a node of the full road.
    private void DrawLadder(PageDraw page)
    {
        var tiers = api.GetLadder();
        if (tiers.Count == 0 || page.Components.Count != tiers.Count) return;

        var index = api.GetSnapshot(page.Player.SteamID) is { } me ? IndexFor(tiers, me.Points) : -1;
        page.Add($"{T(page, "road")}\t{T(page, "road_count", tiers.Count)}", "section", 0);
        for (var k = 0; k < tiers.Count; k++)
            page.Draw(k, Directive.Clean(tiers[k].Name), RoadNode(page, tiers, k, index));
    }

    // Reached, current or still locked; with no standing (-1) nothing is marked.
    private string RoadNode(PageDraw page, IReadOnlyList<RanksTier> tiers, int i, int current)
    {
        var icon = api.GetRankIcon(tiers[i].Points);
        var payload = "info;" + RankArt.For(icon, i, tiers.Count).Payload;

        // A CS Rating badge shows the threshold itself.
        if (!Rated(icon)) payload += ";sub=" + Directive.Number(tiers[i].Points);

        if (current >= 0 && i == current) payload += ";chosen;tagtone=accent;tag=" + T(page, "you");
        else if (current >= 0 && i > current) payload += ";disabled;glyph=lock";
        return payload;
    }

    // Ranks prints nine counter lines; each becomes a tile, its figure and caption from the
    // snapshot. Only a page of exactly those lines is drawn so: a loading or empty notice stays.
    private void DrawStats(PageDraw page, RanksPlayerSnapshot s)
    {
        var tiles = Tiles(page, s);
        if (page.Components.Count != tiles.Count) return;
        for (var k = 0; k < tiles.Count; k++)
            page.Draw(k, tiles[k].Value, tiles[k].Payload);
    }

    private List<(string Value, string Payload)> Tiles(PageDraw page, RanksPlayerSnapshot s)
    {
        var kd = s.Deaths == 0 ? s.Kills : Math.Round(s.Kills / (double)s.Deaths, 2);
        var headshotRate = Directive.Percent(s.Headshots, s.Kills);

        // Hits + misses: one shotgun blast is a single trigger pull that can land nine hits.
        var attempts = s.Hits + s.Misses;
        var accuracy = Directive.Percent(s.Hits, attempts);
        var rounds = s.RoundWins + s.RoundLosses;
        var matches = s.MatchWins + s.MatchLosses;

        return
        [
            Tile(page, Directive.Number(s.Kills), "kills", "kill"),
            Tile(page, Directive.Number(s.Deaths), "deaths", "health"),
            Tile(page, Directive.Decimal(kd), "kd", "graph", sub: T(page, "kd_sub")),
            Tile(page, Directive.Number(s.Assists), "assists", "team"),
            Tile(page, Directive.Number(s.Headshots), "headshots", "headshot",
                sub: T(page, "headshots_sub", Directive.Decimal(headshotRate)), share: s.Kills > 0 ? headshotRate : null, meter: "accent"),
            Tile(page, Directive.Decimal(accuracy), "accuracy", "crosshair", unit: "%",
                sub: T(page, "accuracy_sub", Directive.Number(s.Hits), Directive.Number(s.Misses)), share: attempts > 0 ? accuracy : null),
            Tile(page, Directive.Number(s.RoundWins), "rounds", "trophy", unit: T(page, "won"),
                sub: Lost(s.RoundLosses, s.RoundWins, rounds), share: rounds > 0 ? s.RoundWins * 100.0 / rounds : null, meter: "split"),
            Tile(page, Directive.Number(s.MatchWins), "matches", "star", unit: T(page, "won"),
                sub: Lost(s.MatchLosses, s.MatchWins, matches), share: matches > 0 ? s.MatchWins * 100.0 / matches : null, meter: "split"),
            Tile(page, Directive.Number(s.BombsPlanted), "bombs", "bomb", unit: T(page, "planted"),
                sub: T(page, "defused", Directive.Number(s.BombsDefused))),
        ];

        // A record that never played a round has no share to state.
        string Lost(long lost, long won, long total) => total > 0
            ? T(page, "lost_share", Directive.Number(lost), Directive.Bar(won * 100.0 / total))
            : T(page, "lost", Directive.Number(lost));
    }

    // `info`: a counter is a fact, with no key and nothing to click. A meter only comes with a share.
    private static (string, string) Tile(PageDraw page, string value, string caption, string glyph,
        string? unit = null, string? sub = null, double? share = null, string? meter = null)
    {
        var payload = new StringBuilder("info;type=").Append(T(page, caption)).Append(";glyph=").Append(glyph);
        if (unit is not null) payload.Append(";unit=").Append(unit);
        if (sub is not null) payload.Append(";sub=").Append(sub);
        if (share is { } percent)
        {
            payload.Append(";bar=").Append(Directive.Bar(percent));
            if (meter is not null) payload.Append(";metertone=").Append(meter);
        }
        return (value, payload.ToString());
    }

    // The first three stand on the podium, the rest form a table under column heads, and a viewer
    // outside the list is pinned under it once their position has loaded.
    private void DrawTop(PageDraw page)
    {
        var tiers = api.GetLadder();
        if (tiers.Count == 0) return;

        var viewer = page.Player.SteamID;
        var rated = Rated(api.GetRankIcon(0));
        var banner = page.Regions.Contains("banner");

        page.Add(rated
            ? $"#\t{T(page, "cs_rating")}\t{T(page, "col_player")}\t{T(page, "col_tier")}"
            : $"#\t{T(page, "col_rank")}\t{T(page, "col_player")}\t\t{T(page, "xp")}", "head", 0);

        int mainRows = 0, lastPosition = 0;
        bool listed = false, any = false;
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.ItemOf(k) is not RanksLeaderboardEntry entry)
            {
                mainRows++;
                continue;
            }
            any = true;
            var mine = entry.SteamId == viewer;
            listed |= mine;
            lastPosition = Math.Max(lastPosition, entry.Position);
            var podium = entry.Position <= Podium;
            if (!(podium && banner)) mainRows++;

            // As GetTopAsync resolves the tier: a resident record may hold points not yet saved.
            var points = api.IsLoaded(entry.SteamId) ? api.GetPoints(entry.SteamId) : (int)entry.Value;
            var (text, payload) = TopRow(page, tiers, entry.Name, entry.Position, points, podium, mine, rated);
            page.Draw(k, text, payload);
        }

        if (!any || listed || !page.LastPage || api.GetSnapshot(viewer) is not { } me) return;
        if (!page.TryLoad("position", () => api.GetPositionAsync(viewer), out var position) || position <= lastPosition) return;

        var room = page.MainSlots - mainRows;
        if (room >= 2) page.Add("· · ·", "info;group=gap");
        if (room >= 1)
        {
            var (text, payload) = TopRow(page, tiers, me.Name, position, me.Points, podium: false, mine: true, rated);
            page.Add(text, payload);
        }
    }

    private (string Text, string Payload) TopRow(PageDraw page, IReadOnlyList<RanksTier> tiers, string player,
        int position, int points, bool podium, bool mine, bool rated)
    {
        var index = IndexFor(tiers, points);
        var payload = new StringBuilder(podium ? "at=banner;" : "")
            .Append(RankArt.For(api.GetRankIcon(points), index, tiers.Count).Payload)
            .Append(";tag=#").Append(position)
            .Append(";sub=").Append(Directive.Clean(tiers[index].Name));
        if (mine) payload.Append(";chosen");

        // A CS Rating badge is the points; an emblem leaves them to the value column.
        var name = Directive.Clean(player);
        var text = rated ? name
            : podium ? $"{name}\t{Directive.Number(points)} {T(page, "xp")}"
            : $"{name}\t{Directive.Number(points)}";
        return (text, payload.ToString());
    }

    // Opened from a leaderboard row, whose entry says whose counters these are.
    private void DrawPlayer(PageDraw page)
    {
        if (page.Opener is not RanksLeaderboardEntry entry) return;
        if (page.TryLoad("snapshot", () => api.LoadSnapshotAsync(entry.SteamId), out var snapshot) && snapshot is not null)
            DrawStats(page, snapshot);
    }

    // A form: the setting under a heading with a line explaining it, and the reset, always last,
    // fenced off as a card whose button is Ranks' own reset entry.
    private static void DrawSettings(PageDraw page)
    {
        var last = page.Components.Count - 1;
        if (last < 0) return;

        page.Add(T(page, "notifications"), "section", 0);
        if (page.Components[0] is ToggleComponent)
            page.Decorate(0, "sub=" + T(page, "xp_messages_sub"));

        if (page.LastPage && last > 0 && page.Components[last] is SubmenuComponent reset)
        {
            page.Add(Directive.Clean(reset.Text), "group=danger;sub=" + T(page, "reset_sub"), last);
            page.Draw(last, T(page, "reset_button"), "inline;danger;submenu");
        }
    }

    private static void DrawReset(PageDraw page)
    {
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is ButtonComponent) page.Decorate(k, "danger");
            else if (page.Components[k] is TextComponent) page.Decorate(k, "info;group=danger");
        }
    }

    private static bool Rated(RanksRankIcon icon) => icon.Mode == RanksDisplayModes.Premier;

    // Ranks' own rule: the highest threshold the points meet.
    private static int IndexFor(IReadOnlyList<RanksTier> tiers, int points)
    {
        for (var i = tiers.Count - 1; i >= 0; i--)
            if (points >= tiers[i].Points)
                return i;
        return 0;
    }

    private static double Progress(IReadOnlyList<RanksTier> tiers, int index, int points)
    {
        if (index + 1 >= tiers.Count) return 1.0;
        var floor = tiers[index].Points;
        var ceiling = tiers[index + 1].Points;
        return ceiling <= floor ? 1.0 : Math.Clamp((points - floor) / (double)(ceiling - floor), 0.0, 1.0);
    }

    private static string T(PageDraw page, string key, params object[] args) => page.Text("customhud.ranks." + key, args);
}
