# CustomHUD

CS2-themed Panorama renderer for the SwiftlyS2 menu framework.

Consumer plugins need **no code changes and no dependency on this plugin**. A
server owner switches one line of config and every `Core.Menu` menu on the
server is drawn as a Panorama HUD instead of center-screen HTML.

```jsonc
// configs/menus/menus.jsonc
{ "Menus": { "Renderer": "customhud.panorama" } }
```

## Requirements

| | |
|---|---|
| SwiftlyS2 | **1.5.0 or newer** — `IMenuRenderer` does not exist in 1.4.x |
| Client | must subscribe to the CustomHUD Workshop addon (via AddonsManager) |
| Consumer plugins | must use `Core.Menu`, not the deprecated `Core.MenusAPI` |
| [Cookies plugin](https://github.com/SwiftlyS2-Plugins/Cookies) | optional — persists player navigation and scale preferences |

The consumer-plugins row is the real adoption gate. `Core.MenusAPI` is a
separate pipeline that never reaches a renderer, so a plugin still on it is
unaffected by CustomHUD and keeps drawing its own HTML.

## Install

1. Subscribe to the CustomHUD Workshop addon and serve it through AddonsManager.
2. Drop the release into the SwiftlyS2 plugins directory.
3. Set `"Renderer": "customhud.panorama"` in `configs/menus/menus.jsonc`.
4. **Set `MenuLayout.MaxVisibleLines`** to the page size you want. It defaults
   to `5` and is clamped to `1..32`. CustomHUD reads it from `menus.jsonc` and
   splits every menu into fixed pages of at most that many lines (see
   [Pages](#pages)); the mouse layouts are exempt: they draw every row and scroll.

```jsonc
{
  "Menus": { "Renderer": "customhud.panorama" },
  "MenuLayout": { "MaxVisibleLines": 20 }
}
```

A renderer id that is not registered makes a menu **fail to build**. Pointing
the server-wide `Renderer` at CustomHUD and then having it fail to load breaks
every menu on the server — scope the override to a single menu id while testing.

## Navigation modes

Players pick their own with `!setnav`:

| Mode | Input | Handled by |
|---|---|---|
| `wasd` | W / S move, E select, R or Tab close | CustomHUD |
| `buttons` | whatever `menus.jsonc` binds (default Shift / F / E / Tab) | core |
| `numbers` | `1`–`6` select, `7`/`8` page, `9` close (Tab does not close) | CustomHUD |
| `mouse` | click; footer `<` back and `×` close, `‹` / `›` page; a slider or selector steps with its own `‹` `›` (Tab does not close) | CustomHUD |

`buttons` is core's own name for keyboard input — the same word
`menus.jsonc` uses for `"InputMethod"` — so the two configs share one
vocabulary. Its keys are bound there, not here.

`!setnav` applies to every menu that player opens, whatever `menus.jsonc` or
the consuming plugin chose. In `wasd` mode CustomHUD ignores any key the menu's
own keymap already handles, so the two never move the cursor twice. W and S
still move the player as well: core has no input freeze.

### Pages

Outside mouse mode a menu shows one page at a time. A page holds at most
`MaxVisibleLines` lines from `menus.jsonc` and at most the menu's own
`ItemsPerPage` (core's default is `5`; plugins raise it with
`WithItemsPerPage`) and the layout's `RowSlots` — the same limits core
applies. Moving past a page's last row opens the next page, past the first row
the previous one; both ends wrap. `7` / `8` in numbers mode turn whole pages,
and a numbers page never holds more than six choices, the keys there are. A
disabled entry takes no key, so it costs none of the six. The footer shows the
page (`2/5`, with a dot per page) while there is more than one.

Core itself scrolls a window one row at a time instead; CustomHUD cuts fixed
pages from the top. **Chat-input menus** are the exception: core resolves `!N`
against its own window, so that window is drawn unchanged.

`!hudscale 110` sets menu scale. Preferences persist through the optional
SwiftlyS2 Cookies plugin; without it, they are kept only for the current server
process.

**Mouse and numbers input is currently silent.** `MenuSounds` is played by
core's input dispatcher and no public API exposes the configured sound names.

In `mouse` mode the cursor is captured per player while a menu is open.

In `mouse` and `numbers` modes, for Buttons-input menus, CustomHUD removes Tab
from the server's sampled button state after simulation, before SW2 dispatches
menu keys. Tab therefore never closes or reopens the session: focus, chat input
and submenu state stay intact, with no exit sound or close callback. Other
players, renderers and navigation modes keep their normal bindings; client user
commands are not rewritten. If a Tab press still reaches the menu router for a
player CustomHUD masked, the server log gets a warning naming them.

Mouse layouts end in a footer bar: a `<` button at the left closes the current
session normally (returning to its parent, or closing a root menu), and `‹` /
`›` at the right page through selectable rows. Paging buttons appear only when
the menu has more than one page. Back has an orange border, a filled background
and a hover state. The classic list layouts also carry these controls for
mouse-mode overrides; all three start hidden and stay collapsed in WASD,
buttons and numbers modes, leaving the normal hint.

A layout may name a close button instead (`Panels.Close`, the `×` of the lists,
lockers and pages). It leaves every submenu level at once, where Back goes up
one; with one present, Back is hidden wherever it has nowhere to go. A menu
whose plugin unbound Close (`WithAction(MenuActions.Close.Name, MenuKey.None)`,
as MixScrims' picks do) has no way out in any mode: no `×`, no `<`, and R, Tab
and `9` do nothing.

## The list

Every menu without a page of its own (see [Plugin pages](#plugin-pages-mouse))
draws as **the list**: `menu_list` in the keyboard modes, `menu_list_mouse` in
Mouse mode, docked at the left edge. Nothing on it has to be sent for
CustomHUD: each row is drawn from the component behind it.

| Component | Drawn as |
|---|---|
| `ToggleComponent` | a switch |
| `SelectorComponent<T>` | `‹ value ›`; A and D step it |
| `ChoiceComponent<T>` | its value and a cycle mark; E steps it |
| `SliderComponent` | its track and value; `track=wear` bands the track with the five exteriors |
| `InputComponent` | its text in a field, with a pen |
| `SubmenuComponent` | `›` at the right |
| `ProgressBarComponent` | its label, a meter under it and the share at the right |
| a component that cannot be focused | a fact on the panel: no key, no row ground |

The path over the title is the menus above it (`MapDecals Menu › Choose a
decal to edit`). ShopCore, MapChooser, Ranks, VIPCore, Admins and MixScrims
menus that are their own root show their plugin's name there instead, and
MapChooser's and MixScrims' map votes draw each stock map's screenshot.

The footer is a **legend**: the keys that work on the focused row, drawn as
keys, in the menu's own keymap. `W S move · E switch · R back` on a toggle,
`A D change` on a slider or selector, `1-6 pick · 7 8 page · 9 close` in
numbers mode, which also draws each row's key. The verb follows the focused
component (`switch`, `edit`, `change`, `open`, `select`); a plugin can name it
with `verb=` (`pick`, `vote`, `apply` ...). The close key says `back` where it
returns to a parent and is left out where the plugin unbound Close. An
operator's `Hints` entry for the mode replaces the legend with that line.

In Mouse mode there are no keys and no cursor: the row under the pointer is the
bright one, a switch flips where it is clicked, a slider's track sets it where
it is clicked and its `‹` `›` step it, a selector steps with its own `‹` `›`,
and a long list scrolls, paging past 24 rows.

The page directives a plugin sends for its mouse page work in the list too:
`glyph=`, `sub=`, `tag=`, `chip`, `section`, `avatar`, `rating=`, `image=`,
`bar=` (the share fills the row; the leader in gold, the player's own pick in
the accent), and `pips=` / `nays=` with `at=foot` for a turnout under the rows.
Two views change the whole list: `compact` (a rarity stripe for the art, one
line a row) and `ballot` (Yes and No as the two big rows), which MixScrims'
mid-round votes use. Pick either per menu id in `PageViews`. A third, `ranks`,
is Ranks' own and its menus' default: their pages caption meters only the
mouse page draws, and this view leaves the captions out.

> A `config.jsonc` written before the list shipped pinned the classic layouts
> in `Default` and `Mouse`. Delete those two keys, or set them to `null`, to
> get the list; keep them to stay on the classic look. While either is set the
> plugin says so once at load (`config.jsonc sets "Default" (menu.vxml_c) …`).
> The classic `menu.vxml` (`"Default": {}`), its variants and the tile grid
> (`menu_mouse.vxml`, with `<<` / `>>` paging) all still ship.

## Translations

Everything CustomHUD says to players comes from
`resources/translations/<language>.jsonc`, shipped in English (`en`) and
Lithuanian (`lt`): the `!setnav` / `!hudscale` replies (`customhud.prefix`,
`customhud.nav.*`, `customhud.scale.*`), the menu footer hints
(`customhud.hint.<key>`), the list's legend words (`customhud.legend.<verb>`)
and the plugin names over a page's title (`customhud.eyebrow.<plugin>`). SwiftlyS2 picks the language — the player's game
language while `core.UsePlayerLanguage` is on (its default), otherwise
`core.Language` — and falls back to `en` for a missing file or key. To add a
language, copy `en.jsonc` to `<code>.jsonc`. Chat replies take SwiftlyS2
colour tags (`[green]`, `[default]`); hints must not, because Panorama would
draw the control character.

Core's own menu strings, such as an input row's "type in chat" prompt, come
from SwiftlyS2's translation files, exactly as under CenterHTML.

## Capturing native HUD text

CustomHUD can also take over the game's own HUD text — `SendCenter`,
`SendAlert`, `SendChat` and the rest — so messages from plugins that know
nothing about CustomHUD still render in your style.

```jsonc
"NativeText": {
  "Enabled": true,
  "Center": { "Capture": true, "File": "hud_text.vxml_c", "DurationSeconds": 5 },
  "Alert":  { "Capture": true, "File": "hud_alert.vxml_c" }
}
```

Covers every `CUserMessageTextMsg` destination: `Notify` (1), `Console` (2),
`Chat` (3), `Center` (4), `ChatEOT` (5), `Alert` (6).

Off by default, per-kind. `Suppress: false` draws in CustomHUD *and* leaves the
native message visible, which is handy while building a layout.

> **`SendCenterHTML` is not covered.** It is not a `TextMsg` — it is buffered
> and re-sent every tick as a game event, and the same buffer carries the
> legacy `Core.MenusAPI` menu text. See
> [docs/PublicPort.md](docs/PublicPort.md) §6.

## Layouts are configurable

Every panel id, dialog variable and CSS class is a config value, so a different
look is a config edit rather than a fork. See
[`config.example.jsonc`](config.example.jsonc) and
[`docs/PublicPort.md`](docs/PublicPort.md). Every colour the shipped layouts
draw comes from `customhud_tokens.vcss`, one ladder per hue, so editing the
accent's ladder there re-themes every menu, page and panel at once (see
[docs/DesignLanguage.md](docs/DesignLanguage.md#tokens)).

A layout profile is selected per player, per nav mode, with optional per-menu
overrides — so mouse players get a different presentation from keyboard
players, from the same menu.

Mouse resolution checks `MouseLayouts` first (exact id, then longest
trailing-`*` prefix), then the built-in mouse rules, then ordinary exact
`Overrides`, then `Mouse`, then the mouse list. Keyboard resolution checks
exact `Overrides`, then the mode's profile (`Wasd`, `Buttons`, `Numbers`),
then `Default`, then the plugin's built-in list, then the list.

The built-in `weaponskins.*` mouse rule selects
[`menu_skins_mouse.vxml`](resources/panorama/layout/custom_game/customhud/menu_skins_mouse.vxml):
a wide, orange-accented locker with a category sidebar and a five-column
scrolling grid of cards. The rule lives in code rather than in `config.jsonc`,
so an update reaches existing servers; a `MouseLayouts["weaponskins.*"]` entry
replaces it. WeaponSkins continues to use ordinary
`Core.Menu`; no CustomHUD dependency or consumer module is needed.
Each card shows the item's econ thumbnail (`image=`) as its main content, with
the item type (`type=`) and name below it and a `MenuTextStyle.Color` rarity
glow rising from the foot. Weapon and knife lists show the base item, glove
types their first finish, sticker capsules and charm collections their own art.
An item the game ships no art for is a plain card. CustomHUD marks the item
the player has on (a finish, the knife or glove type, an agent, a music kit)
`chosen`, read from WeaponSkins' API, and the locker outlines that card in a
2px orange edge, twice its hover edge; picking another moves the outline. The
art, the captions and that outline come from `IWeaponSkinAPI` (see
[Provider integrations](#provider-integrations)); equipped-item previews and
VIP widgets are not drawn.

The sidebar mirrors the root menu's submenus and dividers and marks the entry
the player is anywhere beneath: the entry whose component id equals that
page's menu id, else whose label equals its title. With `Sidebar.AutoOpen` the
root page is never drawn: opening the menu lands on that entry (the locker's
first, Weapon Skins), and backing out of that level closes the menu. Since
nothing else would then reach them, the root's buttons join the sidebar too,
such as Ranks' `Help`, which runs as it would on the root page. A label's
trailing aside, `Help (shown in chat)`, is dropped there; a count,
`Knives (12)`, still becomes the badge.

Every sidebar entry has an icon. `Sidebar.Icons` maps an entry's component id
(else its label) to a token from
[`customhud_icons.vcss`](resources/panorama/styles/custom_game/customhud/customhud_icons.vcss),
and an entry missing from the map keeps the layout's default icon, so a plugin
nobody has mapped still gets one. The locker maps WeaponSkins' entries: a pistol,
a knife, gloves, an agent and a music kit, and the three properties pages show
their item's icon with a small gear. The icons are the game's own, so the addon
ships none. Tokens are listed in `config.example.jsonc`.

The weapon, knife and glove properties pages are drawn as an inspect card, 668px
wide, in place of the grid: the item's picture, rarity and name, then one field
per entry (name tag, wear, seed, StatTrak™ count with its switch, Souvenir,
Stickers, Charms; gloves take only the wear). Wear has an exact field plus a
bar banded Factory New to Battle-Scarred; a click on the bar sets the wear in
0.02 steps. CustomHUD draws the card through three raw-node directives on
WeaponSkins' own rows, see [Card pages](#card-pages); keyboard modes keep their list.

The mouse layouts set `ShowAllRows`: CustomHUD renders the whole list in
slot-sized chunks instead of pages, so neither `MaxVisibleLines` nor a plugin's
`ItemsPerPage` limits them. They scroll with the mouse wheel or a scrollbar. A
list longer than the layout's slots pages: the mouse list in 24-row chunks and
the tile grid in 60 with the footer's paging buttons, the WeaponSkins locker
(`PageTiles`) through a `«` / `»` card at each end of a 128-card grid, which
only a few sticker capsules need.

`RowSlots` (`1..256`) must match the authored layout. A page never holds more
than `RowSlots` rows, and since `MaxVisibleLines` stops at 32, slots past 32
are only reached with `ShowAllRows`.
`Panels.Back` / `Close` / `Previous` / `Next`, `PageTiles`, the page counter
(`Variables.PagePanel` / `PageVar` / `PageFormat`, default `"{0} / {1}"`),
sidebar addressing (`DividerClass`, `AutoOpen`, `IconPanel`, `IconClass`,
`Icons`) and the
`ColorIcons` hex-colour-to-icon-token mapping are configurable.

Only *presentation* varies per player. A renderer receives an already-composed
frame and cannot change what a plugin put in it — with seven deliberate
exceptions:

- **Title.** Header dividers and runs of rule characters (`---`, `───`, `===`)
  are stripped; the layout draws its own frame around the title.
- **Dividers.** A body `DividerComponent` takes no row, tile or card field: the
  layouts already set their rows apart. Only a sidebar draws one, as a rule.
  CenterHTML and chat draw it as a row of `─` and page a line for it, so a
  divider meant only for the sidebar should override `IsVisible` to return
  `true` only when `session.Menu.Renderer.Id` is `customhud.panorama`, as
  WeaponSkins' does. A `VisibleWhen` provider would keep it out of the sidebar
  too.
- **Footer.** Core's footer is replaced by a short per-mode hint in the
  player's language (see [Translations](#translations)). `{up}` `{down}`
  `{select}` `{close}` expand to the menu's keymap, except that in Mouse mode
  `{close}` names the layout's close button (`×`, or `<` where it has none),
  and `chat` is used for menus whose input method is chat. A menu with
  nothing to select (a vote tally kept open after voting) uses `<mode>.view`
  instead, which only says how to close it.
- **Pages.** Fixed pages replace core's scrolling window (see [Pages](#pages)).
- **Value slots.** A toggle, input, slider or choice puts its value in the
  layout's value slot instead of after a `": "`, so a locker card reads `Wear`
  over `0.15`.
- **Accepted input.** Core shows `Accepted` in place of an input's new value
  for two seconds; CustomHUD shows the value at once. A rejected value keeps
  core's notice.
- **Optional sidebar.** Static, visible root `SubmenuComponent` entries become
  shortcuts to the same consumer actions, with root `DividerComponent`s as
  rules between them. Player-dependent text, enabled and visibility
  providers are not evaluated on the render thread; those entries stay in the
  normal body only.

`Hints` in `config.jsonc` overrides a hint in every language; `""` hides it.
An entry equal to the English default counts as unset, because builds before
translations wrote every default into the file. A mode hint you override keeps
showing on view-only menus until you override its `.view` too, so your text is
never mixed with a translated one.

```jsonc
"Hints": {
  "numbers": "1-6 pick · 7/8 page · 9 exit",
  "numbers.view": "9 exit"
}
```

> A profile pointing at a layout the player's Workshop addon does not contain
> renders **nothing**, and the server cannot detect it. Keep profiles in step
> with the addon version you ship.

> SwiftlyS2 adds new keys to an existing `config.jsonc` but never changes a
> value it already holds, and never adds keys inside `MouseLayouts` or
> `Overrides` — which is why the built-in layouts live in the code instead.
> A config still carrying the old `MouseOverrides` section is warned about on
> load and the section ignored. Profiles that are in the file (your own, or a
> `Default` / `Mouse` an older build wrote) keep the values they were generated
> with: when an update changes a shipped layout's slot count, edit `RowSlots`
> to match (see `config.example.jsonc`) or delete the profile.

## Plugin pages (mouse)

In Mouse mode, ShopCore, MapChooser, Ranks, VIPCore, Admins and MixScrims open
in one page layout, `menu_locker`: the plugin's sections in a sidebar, the open
section's submenus as tabs, chips beside the title, and the page itself.
Built-in mouse rules (`shopcore.*`, `mapchooser.*`, `ranks.*`, `vipcore.*`,
`admins.*`, `mixscrims.*`, and Ranks' modules `weaponstats.*`,
`customcombos.*`, `prestige.*`) send them there, and each menu id draws as a
**view**, a design for that kind of page:

| Plugin | Views |
|---|---|
| ShopCore | `shop-grid` (storefront cards), `shop-list` (a price list), `shop-product`, `shop-inventory` |
| MapChooser | `maps-board` (vote cards), `maps-rows` (poll rows), `maps-pool`, `maps-rtv` |
| Ranks | `ranks-rank`, `ranks-stats`, `ranks-weapons`, `ranks-board`, `ranks-settings` |
| VIPCore | `vip-perks`, `vip-options`, `vip-guns`, `vip-users`, `vip-grant` (and `vip-tiers`, which no VIPCore menu opens yet) |
| Admins | `admins-players`, `admins-ban`, `admins-bans`, `admins-votes`, `admins-staff` |
| MixScrims | `mix-captains`, `mix-draft` (the team pick), `mix-sides` (the knife round's side), `mix-ballot` (the timeout, surrender and kick votes, docked at the side), `mix-control` |

MixScrims' menus are all their own root, so none has a sidebar, and its map
vote is MapChooser's `maps-board`. The team pick and the side choice have no
way out: MixScrims unbinds Close on them, and CustomHUD hides `×` and `<`.

Ranks' modules (WeaponStats, CustomCombos, Prestige) open as tabs under its
Statistics but keep ids of their own, so they have rules of their own that
keep them on the Ranks page: `weaponstats.weapons` draws as `ranks-weapons`,
the modules' other pages as `ranks-stats`, and so does another player's
statistics opened from the leaderboard (`ranks.top.player`).

Ranks (and its modules), ShopCore, MapChooser, WeaponSkins and VIPCore send no
directives: CustomHUD recognises their menus and draws the designed pages from
each plugin's public API (see [Provider integrations](#provider-integrations)):
the rank as a hero card with its road through the ladder, the counters as
tiles, a podium with the player's own row pinned under the table, weapon cards
with the game's renders, the settings as a form with the reset fenced off,
prices in a value column, a live map tally, the VIP membership over its perk
cards, a module's choices as cards (field-of-view dials, chat tags as chat
lines, a gun loadout), and the VIP admin pages as a table beside the selected
player's grants. Every other renderer still draws those plugins' plain rows.

An id no rule names gets `list`, the plain list. Pick another view in config
`PageViews`, by exact id or trailing-`*` prefix; it wins over the built-in
choice and over the plugin's own `view=`:

```jsonc
"PageViews": { "mapchooser.eof-vote*": "maps-rows", "shopcore.buy.items": "shop-list" }
```

### What a page sends

A page is built from the same components as any other menu. These directives,
in a `MenuRawNode("customhud.panorama", "…")` on the entry, place it and say
what it is; other renderers ignore them. Directives are separated by `;`, so a
text value runs to the next one. A value that becomes part of a class name
(`at=`, `view=`, `group=`, the tones, `glyph=`, `swatch=`) must be a plain
token: up to 24 lowercase letters, digits and `-`.

| Directive | Effect |
|---|---|
| `at=<region>` | where the entry goes: `banner` (above the list, 5 slots), `detail` (the side pane, 28), `detailbar` (the pane's buttons, 3), `foot` (under the list, 4), `aside` (a column left of the list, 8), `strip` (a row of small slots over the page, 10). Absent, unknown or full: the list (48) |
| `chip[=tone]` | a chip beside the title instead of a row, up to 3. The text's first segment is its value, the one after a tab its caption |
| `head` | the first entry carrying it is not drawn: its tab-separated text becomes the column heads, up to 6 |
| `section` | a heading across the page that also starts a new line of cards; the text after a tab shows beside it (`Perks\t8 of 10 on`) |
| `group=<token>` | adds `grp-<token>` to the entry. On a `section`, every entry after it carries the group up to the next section, and the section itself does not |
| `view=<token>` | on any entry, the first one wins: draw the page as that view |
| `sub=`, `unit=`, `tag=`, `tag2=`, `note=`, `mark=` | more text: a line under the name, a small word with it (`kills`), two pills, a note at the right, and the text in the mark square |
| `tone=`, `subtone=`, `tagtone=`, `tag2tone=`, `notetone=`, `metertone=` | colours, from the table below |
| `glyph=<icon>` | an icon from `customhud_icons.vcss` (`ico-<icon>`) |
| `vglyph=<icon>` | the value's own icon, such as a price's coin, so `glyph=` can be the entry's. Without it the value shows `glyph=` |
| `swatch=<colour>` | a colour dot: `red`, `blue`, `green`, `yellow`, `purple`, `orange`, `pink`, `cyan`, `white`, `black`, `team` or `random` |
| `avatar[=text]` | the mark square with the name's initial in a colour fixed per name, or the given text |
| `rating=N` | a CS Rating badge in its band colour |
| `pips=K/N` | N pips, K of them lit; N up to 20 |
| `nays=M` | with `pips=`, M more lit after the K as no votes, in red |
| `verb=<word>` | the list's legend word for the entry: `select`, `pick`, `open`, `switch`, `edit`, `change`, `vote` or `apply` |
| `bar2=N` | a second meter, 0–100, in the list |
| `lead` | the leading entry: a gold `Leading` tag unless it has a tag of its own. On MapChooser's pages the single highest `bar=` leads without it |
| `primary` | the page's main action, for views that draw one |
| `info` | the entry is a fact, not an action: drawn as text, with no key and nothing to click, even though core lets the cursor stop on it (a `TextComponent` is focusable so a long page can scroll past it) |
| `fold` | the entry takes no slot on a mouse page, because another entry draws what it says (a hero card showing the lines it sums up). Keyboard pages still draw it, and the cursor still counts it |
| `hint` | on a header entry: its text is the page's footer line for a mouse player (`Click a map to vote`), in place of the mode's hint. Keyboard pages keep their keys |
| `inline` | joins the entry straight before it as a second button on its slot, such as a Sell beside an owned item; in the list, `detail` and `foot` |
| `map` | reads a text entry's label as a map, for its art |
| `submenu` | a `›` at the right, in views that draw one. Added for you on a `SubmenuComponent`; a button that opens a page can send it |

| Part | Directive | Tones in `locker.vcss` |
|---|---|---|
| the value | `tone=` | `good` `bad` `gold` `info` `faint` `main` `ct` `t`; `warn` on a chip |
| the line under the name | `subtone=` | `good` `bad` `gold` `info` `faint` |
| the pills | `tagtone=`, `tag2tone=` | `good` `bad` `gold` `info` `accent` `ct` `t` `dark` `legend` |
| the note | `notetone=` | `good` `bad` `gold` `info` `faint` `accent` |
| the meter | `metertone=` | `good` `bad` `gold` `accent` |

Views add tones of their own and use `tone=` for a whole card: MapChooser's
`extend`, `aim`, `awp` and `surf`, and the rank tiers below.

On MapChooser's pages a label is read as a map on an entry to click, a `bar=`
share or a `map` entry. A stock map (`de_mirage`) shows its screenshot and
logo; any other `prefix_name` label (`awp_lego_2`) shows its prefix, `AWP`, in
the mark square.

The Ranks integration draws a rank the way Ranks' `RankMode` does, and any
page can do the same: `rating=N` for Premier,
and an emblem otherwise, `image=rank/skillgroupN` (0–18) for Competitive,
`image=rank/wingmanN` (0–18) for Wingman and `image=rank/levelN` (1–10) for a
custom ladder. `tone=` colours the tier: `csr0`–`csr6` for Premier's bands,
`r1`–`r7` for the skill groups (Silver to Global Elite), and `l1`, `l2`, `l4`,
`l8`, `l10` for levels 1, 2–3, 4–7, 8–9 and 10.

In a keyboard mode the same Ranks pages draw in the list's `ranks` view, which
drops the captions of meters only the page draws (the hero's two thresholds, a
weapon card's two shares) and reads a `unit=` as a word after its number.

## Provider integrations

A plugin with a public shared-interface API can have its menus drawn as
designed pages without depending on CustomHUD or knowing it exists. Its menus
stay ordinary `Core.Menu` components; CustomHUD finds them by menu id, reads
the figures from the plugin's API, and adds the page directives to the rows
the plugin built. Any other custom renderer can read the same API.

| Plugin | Shared interface | Needs | Menus |
|---|---|---|---|
| Ranks | `Ranks.Api.v1` | 1.0.5 | `ranks.*` |
| Ranks_WeaponStats | `Ranks.WeaponStats.Api.v1` | any | `weaponstats.*` |
| Ranks_CustomCombos | `Ranks.CustomCombos.Api.v1` | 1.0.2 | `customcombos.*` |
| Ranks_Prestige | `Ranks.Prestige.Api.v1` | 1.0.1 | `prestige.*` |
| WeaponSkins | `WeaponSkins.API` | a build whose API has `Agents`, `MusicKits`, `GetImageKey` | `weaponskins.*` |
| ShopCore | `ShopCore.API.v2` | 1.1.9 | `shopcore.*` |
| MapChooser | `MapChooser.Api.v1` | 1.4.0 | `mapchooser.*` |
| VIPCore | `VIPCore.Api.v1` | 1.2.0 (contract 1.1.0) | `vipcore.*` |

What a page needs to be recognised is renderer-agnostic, and the plugins
above follow it:

- **A row's `Tag` is the item it stands for**: a leaderboard entry, a weapon's
  definition index, a finish's paint kit, a shop item, a map's name, a ballot
  option, a VIP perk, choice, user or grant (VIPCore's records name what each
  row is, since its pages are made of plain buttons).
- **An item's own page carries that item as `IMenu.Tag`.** A page opened from
  a row without one (a weapon's finishes) is matched to the row it was opened
  from.
- **The API exposes what a page shows** as read-only members: the ladder and a
  player's snapshot, a sell price, an item's art and whether the player owns
  it, a map's pool entry, the live vote counts.

A plugin that is missing, older than the table says, or publishes another
contract under the same key is skipped with one log line, and its menus draw
as plain rows. A draw that throws falls back to the plugin's own rows for that
page and logs once.

CustomHUD compiles against each plugin's contract DLL in `lib/`
(`Private=false`, never shipped); `scripts/update-contracts.ps1` rebuilds and
copies them. Update a plugin and CustomHUD together when a contract changes.

## Richer rows (optional, no dependency)

A component can address this renderer directly and still degrade cleanly
everywhere else:

```csharp
new MenuRawNode("customhud.panorama", "icon=7;danger")
```

Supported directives: `text=`, `icon=`, `image=`, `type=`, `bar=`, `chosen`,
`danger`, `disabled`, `marquee`, for [card pages](#card-pages) `hero`,
`inline` and `track=`, and for [plugin pages](#what-a-page-sends) the page
directives. Unknown ones are ignored, so a payload aimed at a newer
CustomHUD never breaks an older one.

`text=` adds text that only CustomHUD shows, at the raw node's place in the
row: a component that draws `de_mirage` and then appends `text=\t7` gets `7`
in the value column here, while every other renderer still shows `de_mirage`.

`type=` is a short category label (e.g. `type=Rifle`). It is written to the
profile's `Row.TypeVar` and adds the `has-type` class; layouts without a
`TypeVar` ignore it.

`bar=N` (a whole number, clamped to 0–100) is a share, filled to N%: the list
fills the row behind its text, the classic lists and the mouse grid draw a thin
meter under it, and the WeaponSkins locker shows none. `chosen` marks the
player's own pick, such as their vote, in the accent: the name, and the share
when there is one. The mouse layouts also outline a chosen tile or card in a
2px accent edge, as WeaponSkins does for the item a player has on.

MapChooser's next-map vote gets each map's vote count as `text=\t`, its share
of all votes as `bar=`, and `chosen` on the player's vote, all read from
`IMapChooserApi`, so the menu reads as a live tally that redraws as votes land.
MapChooser closes the ballot once the player votes; CustomHUD then opens a
read-only tally (`mapchooser.eof-vote.tally`) in its place until the vote ends.
On every MapChooser page a map shows its art: menus list a map by the name in
`maps.jsonc` ("Dust II"), and `GetMap` gives the map the server loads
(`de_dust2`), whose screenshot and logo a stock map has. Another installed map
shows its prefix in the mark, a workshop map a `Workshop` tag, and the pool
pages each map's player limits.

ShopCore's prices go the same way, as `text=\t500`, on its buy list and its
Buy and Sell rows; every other renderer still reads them in brackets, as
`Neon Tracer [500]`. The price sits outside the label, so a long item name is
shortened at the row's edge but its price never is. The cards add the coin
(`vglyph=credits`), the item's duration and team, and whether the player has
it on, owns it or cannot afford it, under the balance as a chip. An item's
picture is its `ShopItemDefinition.ImageKey`, which a module sets where the
game ships one (agents, the healthshot, the decoy); any other item shows its
category's icon from `Sidebar.Icons`, so a server with its own categories
maps them there.

`image=` takes a CS2 econ image key — the game's own
`panorama/images/econ/<key>_png.vtex_c` path without prefix and suffix, e.g.
`image=default_generated/weapon_ak47_cu_ak47_asiimov_light`. It shows only in
layouts that declare `Row.ThumbPanel` and include `econ_thumbs.vcss`, plus
`econ_stickers.vcss` for `stickers/` keys. The
image bytes stay in the player's game install; the addon ships one CSS class
per key, regenerated after a CS2 update with
`scripts/generate-econ-thumbs.ps1`.

Without any of that, a row's text still splits on tabs:
`"AK-47\t500 XP\t7"` → label, right-hand value, icon badge.

Per-component text colour (`MenuTextStyle` colour, e.g. rarity-tinted skin
names) is not drawn: rows reach Panorama as plain text plus CSS classes, so
colour comes from the layout. Use `danger` or an icon badge to mark a row.

Text is not cut to core's `MaxWidth` (26 by default), which is sized for
CenterHTML's narrow column. CustomHUD renders every text component whole and
the layout ends it with an ellipsis at the row's real edge; a scrolling one
gets the CSS marquee instead. A component whose author already turned fitting
off (`Overflow = None`, or a `MaxWidth` below 1) is passed through untouched.

### Card pages

A profile with a `Card` (the built-in WeaponSkins locker has one) draws a page
whose first row carries `hero` as an inspect card instead of a grid. CustomHUD
adds these to WeaponSkins' properties pages itself; another plugin can send them:

| Directive | Put it on | Effect |
|---|---|---|
| `hero` | the first body row | the page becomes a card; the row's text is the name, `type=` the rarity line, `image=` the picture, `icon=N` or the text colour (through `ColorIcons`) the rarity colour |
| `inline` | any later row | joins the field above it: a slider becomes that field's bar, a toggle a switch at its right, a button or submenu a `×` at its right. The locker draws no inline input or choice |
| `track=<preset>` | an inline slider | adds `track-<preset>`; the locker styles `wear` with the five exterior bands |

Every other row becomes a field, up to `Card.Slots` (8 in the locker). Value
components need nothing more: CustomHUD marks each one with its kind, on/off
state, slider position and formatted number (`kind=`, `on=`, `pct=`, `num=`),
which a custom component may also send itself. A click on a field activates
its row; the bar under a field is 51 targets, each setting `n / 50` of the
slider's range snapped to its `Step`. Other layouts ignore all of this and
list the rows as usual.

Append the raw node into the component's own line instead of wrapping it,
because CustomHUD splits the value off the line's first text node:

```csharp
public override MenuNode Render(IMenuComponentRenderContext context)
{
    var node = base.Render(context);
    var raw = new MenuRawNode("customhud.panorama", "inline;track=wear");
    return node is MenuLineNode line
        ? line with { Children = [.. line.Children, raw] }
        : MenuLineNode.Of(node, raw);
}
```

## Scope

- **Menus** go through `Core.Menu`, so any plugin that targets it renders here
  without a CustomHUD dependency.
- **Native HUD text** (center, alert, notify, chat) can be redrawn by CustomHUD
  instead of the stock HUD. It is opt-in: see
  [Capturing native HUD text](#capturing-native-hud-text). CenterHTML is not a
  TextMsg and cannot be captured.
- **Panels** (versus banner, ready tally, timeout) are drawn for other plugins
  through the `CustomHUD.Panels.v1` shared interface
  (`resources/exports/CustomHUD.Contract.dll`). Layouts are set per profile in
  config `Panels`.
- **Menu variants** (`menu_teampick` with FACEIT level badges, `menu_shop`,
  `menu_skins`, the classic `menu` list and `menu_mouse` grid) are picked per
  menu id through config `Overrides`, or for every menu through `Default` and
  `Mouse`.
- **The list** (`menu_list`, `menu_list_mouse`) draws every other menu; see
  [The list](#the-list).
- **Plugin pages** (`menu_locker`) draw ShopCore, MapChooser, Ranks, VIPCore,
  Admins and MixScrims in Mouse mode; see [Plugin pages](#plugin-pages-mouse).
- **Provider integrations** draw Ranks, ShopCore, MapChooser and WeaponSkins
  from their public APIs, with no change to their menus; see
  [Provider integrations](#provider-integrations).
- **Out of scope:** toasts, countdowns and other HUD elements.

## Building

```powershell
dotnet build
dotnet publish   # produces build/CustomHUD.zip
```

Panorama assets are client-side and ship through the Workshop addon, never the
plugin zip.

### Authoring layouts

See **[docs/PanoramaAuthoring.md](docs/PanoramaAuthoring.md)** — the F6
Panorama debugger, where Panorama's layout model genuinely differs from web
CSS, and why a browser preview can only ever be a hierarchy check.

**[docs/TestingGuide.md](docs/TestingGuide.md)** is a step-by-step pass over
every surface the plugin draws, driven by the plugins it renders for.

**[docs/DesignLanguage.md](docs/DesignLanguage.md)** describes the starter
layouts in `resources/panorama/`: tokens, anatomy, states, and the id/class
contract each one has to keep.

Layouts, styles and images compile into the `customhud` addon under
`<CS2>\content\csgo_addons\customhud` (sources) and
`<CS2>\game\csgo_addons\customhud` (compiled). Each compiled file is also copied
into `<CS2>\game\csgo\panorama` for local testing; that copy is never published.
Create the addon once in Workshop Tools, keep sources in `resources/panorama/`,
and put images in `resources/panorama/images/custom_game/customhud/`. Every file
sits under a `custom_game\customhud\` folder because CS2 only loads addon
layouts from `panorama/layout/custom_game/`.

```powershell
.\scripts\watch-panorama.ps1              # compile on save into the addon + local client
.\scripts\watch-panorama.ps1 -Once        # one-shot build, non-zero exit on failure
.\scripts\watch-panorama.ps1 -SkipClient  # addon only; -ClientRoot <path> redirects the copy
.\scripts\generate-locker-layout.ps1      # menu_locker.vxml, after changing its slot table
.\scripts\generate-list-layout.ps1        # menu_list*.vxml, after changing their slot counts
```

```
cs2.exe -addon customhud -tools -insecure +sv_lan 1 +map de_dust2
```

## License

AGPL-3.0. See [LICENSE](LICENSE).
