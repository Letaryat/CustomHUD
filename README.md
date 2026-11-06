# CustomHUD

CS2-themed Panorama menus for SwiftlyS2: every `Core.Menu` menu on your server
drawn as a real HUD, with no changes to the plugins that open them.

---

## Features

- 🎨 **Drop-in** - one line in `menus.jsonc` redraws every `Core.Menu` menu as
  a Panorama HUD instead of center-screen HTML. Plugins need no code changes.
- 🖱️ **Designed pages** for WeaponSkins, ShopCore, MapChooser, Ranks, VIPCore
  and MixScrims, with prices, live vote tallies, rank ladders and item art read
  straight from those plugins.
- 🕹️ **Four navigation modes** - each player picks `buttons`, `wasd`, `numbers`
  or `mouse` for themselves.
- 📋 **Every other plugin** gets a clean list with switches, sliders and a key
  legend.
- 🌐 **English and Lithuanian**, in each player's game language.

## Supported Plugins

### WeaponSkins

A skin locker: a category sidebar, a card per finish with its in-game art and
rarity glow, and an inspect card for name tag, wear, seed and StatTrak™.

![WeaponSkins in CustomHUD](docs/media/weaponskins.webp)

### ShopCore

Category tabs, item cards with prices, and a product page with your balance and
Buy and Preview buttons.

![ShopCore in CustomHUD](docs/media/shopcore.webp)

### MapChooser

Map votes over each map's screenshot, with a live tally that moves as votes land.

![MapChooser in CustomHUD](docs/media/mapchooser.webp)

### Ranks

Your rank on its ladder, stat tiles, weapon cards and a leaderboard podium,
including the WeaponStats, CustomCombos and Prestige modules.

![Ranks in CustomHUD](docs/media/ranks.webp)

### VIPCore

Your membership over its perk cards, a gun loadout picker, and admin pages to
manage and grant VIP.

![VIPCore in CustomHUD](docs/media/vipcore.webp)

### MixScrims

> [!WARNING]
> Not fully supported yet. Full support will be uppon toasts/CenterHTML layouts release

### Every Other Plugin

Any other `Core.Menu` menu draws as the list: switches, sliders, selectors and
submenus, with a legend of the keys that work on the focused row. Shown here in
WASD and numbers modes:

![The list in WASD and numbers modes](docs/media/list.webp)

## Navigation Modes

Each player picks their own with `!setnav <mode>`, and resizes menus with
`!hudscale <percent>` (for example `!hudscale 110`).

| Mode | Controls |
|------|----------|
| `buttons` (default) | Shift / F move, E select, Tab close - the keys bound in `menus.jsonc` |
| `wasd` | W / S move, E select, R close |
| `numbers` | `1`-`6` pick, `7` / `8` page, `9` close |
| `mouse` | Click |

Numbers mode needs the number keys bound, e.g. `bind 1 "slot1; sw_1"`; existing
`css_N` and `ms_N` binds also work. With the Cookies plugin installed, each
player's choices are remembered.

## Requirements

- SwiftlyS2 **1.5.0** or newer
- The CustomHUD Workshop addon on every player's game, served through
  AddonsManager
- Plugins that build their menus with `Core.Menu`; the deprecated
  `Core.MenusAPI` cannot be drawn
- Optional: [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies), to remember
  each player's navigation mode and scale

## Installation

1. Extract the release zip into `addons/swiftlys2/plugins/`, giving
   `addons/swiftlys2/plugins/CustomHUD/`. To build it yourself, run
   `dotnet publish`, which writes `build/CustomHUD.zip`.
2. Serve the CustomHUD Workshop addon to players through AddonsManager.
3. Start the server and check the log for:

   ```text
   [CustomHUD] Renderer 'customhud.panorama' registered.
   ```

4. Select the renderer in `addons/swiftlys2/configs/menus/menus.jsonc`:

   ```jsonc
   { "Menus": { "Renderer": "customhud.panorama" } }
   ```

That's it: the default configuration is tuned for the best overall experience.
If menus don't change, see
[Troubleshooting](docs/Configuration.md#troubleshooting).

> [!WARNING]
> A menu whose renderer is not registered fails to open. If CustomHUD fails to
> load while `menus.jsonc` names it, every menu on the server breaks, so check
> step 3 before step 4.

## Advanced

None of this is needed to run CustomHUD.

| Guide | For |
|-------|-----|
| [Configuration](docs/Configuration.md) | Server owners: every setting, layouts, hints, native HUD text capture, troubleshooting |
| [Plugin Integration](docs/PluginIntegration.md) | Plugin authors: designed pages, renderer directives, provider APIs and the Panels API |
| [Panorama Authoring](docs/PanoramaAuthoring.md) | Layout authors: building and editing the Panorama layouts |
| [Design Language](docs/DesignLanguage.md) | Layout authors: tokens, colours and the anatomy of each layout |
| [Testing Guide](docs/TestingGuide.md) | A step-by-step pass over every surface |

## Support

| To... | Open |
|-------|------|
| Report something broken | [Bug report](https://github.com/shmitzas/CustomHUD/issues/new?template=bug-report.yml) |
| Suggest a feature or improvement | [Feature request](https://github.com/shmitzas/CustomHUD/issues/new?template=feature-request.yml) |
| Get a plugin's menus drawn as designed pages | [Plugin menu support](https://github.com/shmitzas/CustomHUD/issues/new?template=plugin-menu-support.yml) |

## License

AGPL-3.0. See [LICENSE](LICENSE).
