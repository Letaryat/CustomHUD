using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ShopCore.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;

namespace CustomHUD.Integrations.ShopPages;

// ShopCore's store with each price in the page's value column rather than inside its label. Item
// rows carry their ShopItemDefinition as Tag and an item's own page carries it as the menu's Tag;
// names come from GetItemDisplayName and prices from the item and GetSellPrice.
internal sealed class ShopIntegration(IShopCoreApiV2 api) : IPageIntegration
{
    public string Provider => "ShopCore";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces)
    {
        ApiProbe.Require(typeof(IShopCoreApiV2), "ShopCore 1.1.9", nameof(IShopCoreApiV2.GetSellPrice));
        return interfaces.TryGetSharedInterface<IShopCoreApiV2>("ShopCore.API.v2", out var api) && api is not null
            ? new ShopIntegration(api)
            : null;
    }

    public bool Owns(IMenu menu) => menu.Id.StartsWith("shopcore.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        switch (page.Menu.Id)
        {
            case "shopcore.buy.items":
                page.Hint(page.Text("customhud.shop.hint.items"));
                for (var k = 0; k < page.Components.Count; k++)
                    if (page.Components[k] is MenuComponentBase { Tag: ShopItemDefinition item })
                        page.Draw(k, $"{Name(page, item)}\t{Credits(item.Price)}", "submenu");
                break;

            case "shopcore.buy.item" when page.Menu.Tag is ShopItemDefinition item:
                page.Hint(page.Text(item.AllowPreview ? "customhud.shop.hint.item" : "customhud.shop.hint.item_buy"));
                // The item's line names it only: its price would draw in the disabled row's colour, as if locked.
                var info = IndexOf(page, c => c is TextComponent and not ButtonComponent);
                if (info >= 0) page.Draw(info, Name(page, item), "");
                Price(page, Credits(item.Price), "buy");
                break;

            case "shopcore.inventory.item" when page.Menu.Tag is ShopItemDefinition item:
                Price(page, "+" + Credits(api.GetSellPrice(item)), "sell");
                break;
        }
    }

    // The button whose label states the price: its label stays ShopCore's, in the player's
    // language, with the price moved to the value column.
    private static void Price(PageDraw page, string price, string fallback)
    {
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is not ButtonComponent button || !button.Text.Contains(price, StringComparison.Ordinal)) continue;

            var label = Regex.Replace(button.Text, $@"\s*[\[(]?{Regex.Escape(price)}[\])]?\s*", " ").Trim();
            page.Draw(k, $"{(label.Length > 0 ? Directive.Clean(label) : page.Text("customhud.shop." + fallback))}\t{price}", "");
            return;
        }
    }

    private static int IndexOf(PageDraw page, Func<IMenuComponent, bool> match)
    {
        for (var k = 0; k < page.Components.Count; k++)
            if (match(page.Components[k]))
                return k;
        return -1;
    }

    private string Name(PageDraw page, ShopItemDefinition item) => Directive.Clean(api.GetItemDisplayName(page.Player, item));

    // ShopCore's own format, so the price found in a label is the one it wrote there.
    private static string Credits(decimal value) =>
        value == decimal.Truncate(value) ? ((int)value).ToString() : value.ToString("0.##");
}
