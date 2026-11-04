using System.Runtime.CompilerServices;
using Ranks.CustomCombos.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;

namespace CustomHUD.Integrations.RankPages;

// Ranks_CustomCombos' list: each landed combo as a tile, named as !combos names it.
internal sealed class CombosIntegration(ICustomCombosApi api, IRankChips? chips) : IPageIntegration
{
    public string Provider => "Ranks_CustomCombos";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces, IRankChips? chips)
    {
        ApiProbe.Require(typeof(ICustomCombosApi), "Ranks_CustomCombos 1.0.2", nameof(ICustomCombosApi.GetComboName));
        return interfaces.TryGetSharedInterface<ICustomCombosApi>(ICustomCombosApi.InterfaceKey, out var api) && api is not null
            ? new CombosIntegration(api, chips)
            : null;
    }

    public bool Owns(IMenu menu) => menu.Id.StartsWith("customcombos.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        chips?.AddChip(page);
        for (var k = 0; k < page.Components.Count; k++)
            if (page.ItemOf(k) is ComboStatRow row)
                page.Draw(k, Directive.Number(row.Count), $"info;type={Directive.Clean(api.GetComboName(row.ComboKey))};glyph=star");
    }
}
