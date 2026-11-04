using System.Runtime.CompilerServices;
using System.Text;
using Ranks.WeaponStats.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;

namespace CustomHUD.Integrations.RankPages;

// Ranks_WeaponStats' pages: each weapon as a card with the game's render and its headshot and
// accuracy meters, and hit groups as tiles. Rows are the module's own WeaponStatRow items; the
// hit pages are re-read through IWeaponStatsApi, since their rows are private to the module.
internal sealed class WeaponStatsIntegration(IWeaponStatsApi api, IRankChips? chips) : IPageIntegration
{
    public string Provider => "Ranks_WeaponStats";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces, IRankChips? chips)
        => interfaces.TryGetSharedInterface<IWeaponStatsApi>(IWeaponStatsApi.InterfaceKey, out var api) && api is not null
            ? new WeaponStatsIntegration(api, chips)
            : null;

    public bool Owns(IMenu menu) => menu.Id.StartsWith("weaponstats.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        chips?.AddChip(page);
        switch (page.Menu.Id)
        {
            case "weaponstats.weapons":
                page.Hint(T(page, "hint"));
                DrawWeapons(page);
                break;
            case "weaponstats.hitgroups":
                DrawHits(page, weapon: null);
                break;
            case "weaponstats.weaponhitgroups":
                if (page.Opener is WeaponStatRow opened) DrawHits(page, opened.Weapon);
                break;
        }
    }

    private static void DrawWeapons(PageDraw page)
    {
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.ItemOf(k) is not WeaponStatRow row) continue;

            var headshots = Directive.Percent(row.Headshots, row.Kills);
            var accuracy = Directive.Percent(row.Hits, row.Hits + row.Misses);
            var payload = new StringBuilder("type=").Append(Directive.Clean(WeaponNames.Of(row.Weapon)))
                .Append(";unit=").Append(T(page, "kills"))
                .Append(";image=weapons/base_weapons/").Append(WeaponNames.ImageKey(row.Weapon))
                .Append(";tag=").Append(T(page, "headshots"))
                .Append(";tag2=").Append(T(page, "accuracy"))
                .Append(";mark=").Append(Directive.Decimal(accuracy)).Append('%')
                .Append(";bar=").Append(Directive.Bar(headshots))
                .Append(";bar2=").Append(Directive.Bar(accuracy))
                .Append(";sub=").Append(T(page, "shares", Directive.Decimal(headshots), Directive.Decimal(accuracy)));

            // The module sorts by kills, so the first card of the list is the favourite.
            if (page.PageIndex == 0 && k == 0 && row.Kills > 0) payload.Append(";lead;note=").Append(T(page, "most_kills"));

            page.Draw(k, $"{Directive.Number(row.Kills)}\t{Directive.Decimal(headshots)}%", payload.ToString());
        }
    }

    // The module lists one row per hit group, most hits first; each becomes a tile, drawn only when
    // the API answers with as many groups as there are rows.
    private void DrawHits(PageDraw page, string? weapon)
    {
        var steamId = page.Player.SteamID;
        if (!page.TryLoad("hits", () => Load(steamId, weapon), out var groups) || groups.Count != page.Components.Count) return;

        var total = groups.Sum(g => g.Hits);
        for (var k = 0; k < groups.Count; k++)
        {
            var (group, hits, damage) = groups[k];
            var share = Directive.Percent(hits, total);
            page.Draw(k, Directive.Number(hits),
                $"info;type={HitGroupName(page, group)};glyph={(group is 1 or 8 ? "headshot" : "crosshair")};unit={T(page, "hits")};"
                + $"sub={T(page, "hit_share", Directive.Decimal(share), Directive.Number(damage))};bar={Directive.Bar(share)}");
        }
    }

    private async Task<List<(short Group, long Hits, long Damage)>> Load(ulong steamId, string? weapon)
    {
        var rows = weapon is null
            ? (await api.GetHitGroupsAsync(steamId).ConfigureAwait(false)).Select(r => (r.HitGroup, r.Hits, r.Damage))
            : (await api.GetWeaponHitGroupsAsync(steamId).ConfigureAwait(false))
                .Where(r => string.Equals(r.Weapon, weapon, StringComparison.OrdinalIgnoreCase))
                .Select(r => (r.HitGroup, r.Hits, r.Damage));
        return [.. rows.OrderByDescending(r => r.Hits)];
    }

    // HitGroup_t is not dense: 9 is UNUSED.
    private static string HitGroupName(PageDraw page, short group) => group switch
    {
        0 => T(page, "hitgroup.generic"),
        1 => T(page, "hitgroup.head"),
        2 => T(page, "hitgroup.chest"),
        3 => T(page, "hitgroup.stomach"),
        4 => T(page, "hitgroup.left_arm"),
        5 => T(page, "hitgroup.right_arm"),
        6 => T(page, "hitgroup.left_leg"),
        7 => T(page, "hitgroup.right_leg"),
        8 => T(page, "hitgroup.neck"),
        10 => T(page, "hitgroup.gear"),
        11 => T(page, "hitgroup.special"),
        _ => T(page, "hitgroup.unknown", group),
    };

    private static string T(PageDraw page, string key, params object[] args) => page.Text("customhud.weaponstats." + key, args);
}
