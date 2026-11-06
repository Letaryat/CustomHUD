using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ShopCore.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;

namespace CustomHUD.Integrations.ShopPages;

// ShopCore's store as a storefront: item cards with their art, duration, team and price under the
// player's balance, an item's page as a showcase, owned items with the time they have left. Item
// rows carry their ShopItemDefinition as Tag and an item's page carries it as the menu's Tag; names,
// prices, art, the balance and ownership all come from IShopCoreApiV2.
internal sealed class ShopIntegration(IShopCoreApiV2 api) : IPageIntegration
{
    public string Provider => "ShopCore";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPageIntegration? TryCreate(IInterfaceManager interfaces)
    {
        ApiProbe.Require(typeof(IShopCoreApiV2), "ShopCore 1.1.9", nameof(IShopCoreApiV2.GetSellPrice), nameof(IShopCoreApiV2.GetItemState));
        ApiProbe.Require(typeof(ShopItemDefinition), "ShopCore 1.1.9", nameof(ShopItemDefinition.ImageKey));
        return interfaces.TryGetSharedInterface<IShopCoreApiV2>("ShopCore.API.v2", out var api) && api is not null
            ? new ShopIntegration(api)
            : null;
    }

    public bool Owns(IMenu menu) => menu.Id.StartsWith("shopcore.", StringComparison.Ordinal);

    public string? RegionOf(IMenu menu, IMenuComponent component) => null;

    public void Draw(PageDraw page)
    {
        var credits = api.GetCredits(page.Player);
        page.AddHeader($"{Directive.Money(credits)}\t{T(page, "credits")}", "chip=gold;glyph=credits");

        switch (page.Menu.Id)
        {
            case "shopcore.buy.items":
                page.Hint(T(page, "hint.items"));
                for (var k = 0; k < page.Components.Count; k++)
                    if (page.Components[k] is MenuComponentBase { Tag: ShopItemDefinition item })
                        page.Draw(k, $"{Name(page, item)}\t{Directive.Money(item.Price)}", Card(page, item, credits));
                break;

            case "shopcore.buy.item" when page.Menu.Tag is ShopItemDefinition item:
                page.Hint(T(page, item.AllowPreview ? "hint.item" : "hint.item_buy"));
                Showcase(page, item, credits);
                break;

            case "shopcore.inventory.items":
                page.Hint(T(page, "hint.inventory"));
                for (var k = 0; k < page.Components.Count; k++)
                    if (page.Components[k] is MenuComponentBase { Tag: ShopItemDefinition owned })
                        page.Draw(k, Name(page, owned), Owned(page, owned));
                break;

            case "shopcore.inventory.item" when page.Menu.Tag is ShopItemDefinition item:
                Price(page, "+" + Credits(api.GetSellPrice(item)), "+" + Directive.Money(api.GetSellPrice(item)), "sell", "");
                break;
        }
    }

    // Equipped outlined, owned marked with the price struck through, unaffordable in red.
    private string Card(PageDraw page, ShopItemDefinition item, decimal credits)
    {
        var payload = new StringBuilder("submenu;vglyph=credits;").Append(Art(page, item));
        AppendDuration(page, payload, item);
        AppendTeam(page, payload, item);

        var state = api.GetItemState(page.Player, item.Id);
        if (state.Enabled)
            payload.Append(";chosen;note=").Append(T(page, "equipped")).Append(";notetone=accent;tone=faint");
        else if (state.Owned)
            payload.Append(";note=").Append(T(page, "owned")).Append(";notetone=good;tone=faint");
        else if (credits < item.Price)
            payload.Append(";danger;note=").Append(T(page, "need", Directive.Money(item.Price - credits))).Append(";notetone=bad");
        return payload.ToString();
    }

    // The art and name on the left; badges, the price with the balance it leaves, and the facts on the
    // right; Preview and Buy at the foot. ShopCore's own first line is the art's caption, so it folds.
    private void Showcase(PageDraw page, ShopItemDefinition item, decimal credits)
    {
        var info = IndexOf(page, c => c is TextComponent and not ButtonComponent);
        if (info >= 0) page.Decorate(info, "fold");
        page.Add(Name(page, item), $"at=banner;{Art(page, item)};type={Directive.Clean(Leaf(item.Category))}", 0);

        var (duration, _) = DurationOf(page, item);
        var kind = KindOf(page, item);
        if (kind.Length > 0) page.Add(kind, "group=badge");
        if (item.Duration is not null && duration.Length > 0) page.Add(duration, "group=badge;tone=info");
        if (TeamOf(page, item) is { } team) page.Add(team.Text, "group=badge;tone=" + team.Tone);

        var after = credits - item.Price;
        var balance = after >= 0
            ? "sub=" + T(page, "balance", Directive.Money(credits), Directive.Money(after))
            : "sub=" + T(page, "need", Directive.Money(-after)) + ";subtone=bad";
        page.Add(Directive.Money(item.Price), $"group=price;type={T(page, "price")};glyph=credits;{balance}");

        if (duration.Length > 0) page.Add($"{T(page, "fact.duration")}\t{duration}", "group=fact");
        if (kind.Length > 0) page.Add($"{T(page, "fact.type")}\t{kind}", "group=fact");
        if (TeamOf(page, item) is { } side) page.Add($"{T(page, "fact.team")}\t{side.Text}", "group=fact");

        var price = Price(page, Credits(item.Price), Directive.Money(item.Price), "buy", "at=foot;primary;glyph=credits");
        for (var k = 0; k < page.Components.Count; k++)
            if (k != price && page.Components[k] is ButtonComponent)
                page.Decorate(k, "at=foot;glyph=refresh");
    }

    // How long it was bought for, and how long it has left.
    private string Owned(PageDraw page, ShopItemDefinition item)
    {
        var payload = new StringBuilder("submenu;").Append(Art(page, item));
        AppendDuration(page, payload, item);

        var state = api.GetItemState(page.Player, item.Id);
        if (state.Enabled) payload.Append(";chosen;tag=").Append(T(page, "equipped")).Append(";tagtone=accent");

        if (state.ExpiresAtUnixSeconds is { } expires && item.Duration is { } total && total > TimeSpan.Zero)
        {
            var left = DateTimeOffset.FromUnixTimeSeconds(expires) - DateTimeOffset.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            var share = left.TotalSeconds * 100 / total.TotalSeconds;
            payload.Append(";bar=").Append(Directive.Bar(share)).Append(";note=").Append(T(page, "left", Remaining(page, left)));
            if (share < 15) payload.Append(";metertone=bad;notetone=bad");
        }
        else if (item.Type == ShopItemType.Permanent)
        {
            payload.Append(";note=").Append(T(page, "permanent")).Append(";notetone=gold");
        }
        return payload.ToString();
    }

    // The game's render when the item has one, else its category's icon from the layout's map.
    private static string Art(PageDraw page, ShopItemDefinition item)
    {
        if (item.ImageKey is { Length: > 0 } image) return "image=" + Directive.Clean(image);
        var segments = item.Category.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var s = segments.Length - 1; s >= 0; s--)
            if (page.Icon(segments[s]) is { } icon)
                return "glyph=" + icon;
        return "glyph=shop";
    }

    private static void AppendDuration(PageDraw page, StringBuilder payload, ShopItemDefinition item)
    {
        var (text, tone) = DurationOf(page, item);
        if (text.Length > 0) payload.Append(";sub=").Append(text).Append(";subtone=").Append(tone);
    }

    private static void AppendTeam(PageDraw page, StringBuilder payload, ShopItemDefinition item)
    {
        if (TeamOf(page, item) is { } team) payload.Append(";tag=").Append(team.Text).Append(";tagtone=").Append(team.Tone);
    }

    private static (string Text, string Tone) DurationOf(PageDraw page, ShopItemDefinition item)
        => item.Duration is { } span && span > TimeSpan.Zero ? (Span(page, span), "info")
            : item.Type == ShopItemType.Permanent ? (T(page, "permanent"), "gold")
            : item.Type == ShopItemType.Consumable ? (T(page, "single_use"), "info")
            : ("", "");

    private static string KindOf(PageDraw page, ShopItemDefinition item) => item.Type switch
    {
        ShopItemType.Permanent => T(page, "permanent"),
        ShopItemType.Temporary => T(page, "temporary"),
        ShopItemType.Consumable => T(page, "single_use"),
        _ => "",
    };

    private static (string Text, string Tone)? TeamOf(PageDraw page, ShopItemDefinition item) => item.Team switch
    {
        ShopItemTeam.T => (T(page, "team.t"), "t"),
        ShopItemTeam.CT => (T(page, "team.ct"), "ct"),
        _ => null,
    };

    // "1 hour", "7 days", "90 minutes": the largest unit the duration is a whole number of.
    private static string Span(PageDraw page, TimeSpan span)
    {
        if (span.Ticks % TimeSpan.TicksPerDay == 0) return Unit(page, "day", (long)span.TotalDays);
        if (span.Ticks % TimeSpan.TicksPerHour == 0) return Unit(page, "hour", (long)span.TotalHours);
        return Unit(page, "minute", Math.Max(1, (long)Math.Round(span.TotalMinutes)));
    }

    private static string Unit(PageDraw page, string unit, long count)
        => T(page, $"duration.{unit}.{(count == 1 ? "one" : "other")}", count);

    // Coarse on purpose: the list is drawn once, not every second.
    private static string Remaining(PageDraw page, TimeSpan left)
        => left.TotalDays >= 1 ? T(page, "remaining.days", (int)left.TotalDays, left.Hours)
            : left.TotalHours >= 1 ? T(page, "remaining.hours", (int)left.TotalHours, left.Minutes)
            : T(page, "remaining.minutes", Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));

    // The button whose label states the price: its label stays ShopCore's, in the player's language,
    // with the price moved to the value column. The index it drew, or -1.
    private static int Price(PageDraw page, string inLabel, string shown, string fallback, string payload)
    {
        for (var k = 0; k < page.Components.Count; k++)
        {
            if (page.Components[k] is not ButtonComponent button || !button.Text.Contains(inLabel, StringComparison.Ordinal)) continue;

            var label = Regex.Replace(button.Text, $@"\s*[\[(]?{Regex.Escape(inLabel)}[\])]?\s*", " ").Trim();
            page.Draw(k, $"{(label.Length > 0 ? Directive.Clean(label) : T(page, fallback))}\t{shown}", payload);
            return k;
        }
        return -1;
    }

    private static int IndexOf(PageDraw page, Func<IMenuComponent, bool> match)
    {
        for (var k = 0; k < page.Components.Count; k++)
            if (match(page.Components[k]))
                return k;
        return -1;
    }

    private static string Leaf(string category)
        => category.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? category;

    private string Name(PageDraw page, ShopItemDefinition item) => Directive.Clean(api.GetItemDisplayName(page.Player, item));

    // ShopCore's own format, so the price found in a label is the one it wrote there.
    private static string Credits(decimal value) =>
        value == decimal.Truncate(value) ? ((int)value).ToString() : value.ToString("0.##");

    private static string T(PageDraw page, string key, params object[] args) => page.Text("customhud.shop." + key, args);
}
