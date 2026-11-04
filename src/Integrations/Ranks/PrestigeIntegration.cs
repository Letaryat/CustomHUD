using System.Runtime.CompilerServices;
using Ranks.Prestige.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;

namespace CustomHUD.Integrations.RankPages;

// Ranks_Prestige's page: its two lines become tiles, the level and the points against the next
// level's threshold as a meter.
internal sealed class PrestigeIntegration(IPrestigeApi api, IRankChips? chips) : IPageIntegration
{
    public string Provider => "Ranks_Prestige";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces, IRankChips? chips)
        => interfaces.TryGetSharedInterface<IPrestigeApi>(IPrestigeApi.InterfaceKey, out var api) && api is not null
            ? new PrestigeIntegration(api, chips)
            : null;

    public bool Owns(IMenu menu) => menu.Id.StartsWith("prestige.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        chips?.AddChip(page);

        // The module shows a single notice when prestige is off or unavailable: that stays as it is.
        if (page.Components.Count != 2 || api.GetProgress(page.Player.SteamID) is not { } progress) return;

        page.Draw(0, Directive.Number(progress.Level), $"info;type={T(page, "level")};glyph=crown");

        if (progress.NextLevelPoints is { } required)
        {
            var bar = required <= 0 ? 100 : Math.Clamp(progress.Points * 100L / required, 0, 100);
            page.Draw(1, Directive.Number(progress.Points),
                $"info;type={T(page, "next")};glyph=graph;unit=/ {Directive.Number(required)};bar={bar}");
        }
        else
        {
            // At the top the module's own line says so; it becomes the tile's caption underneath.
            var line = (page.Components[1] as TextComponent)?.Text;
            page.Draw(1, T(page, "max"), $"info;type={T(page, "next")};glyph=graph;sub={Directive.Clean(line)}");
        }
    }

    private static string T(PageDraw page, string key) => page.Text("customhud.prestige." + key);
}
