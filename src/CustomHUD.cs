using CustomHUD.Capture;
using CustomHUD.Commands;
using CustomHUD.Config;
using CustomHUD.Contract;
using CustomHUD.Input;
using CustomHUD.Integrations;
using CustomHUD.Navigation;
using CustomHUD.Rendering;
using CustomHUD.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;

namespace CustomHUD;

[PluginMetadata(
    Id = "CustomHUD",
    Version = "2.0.0",
    Name = "CustomHUD",
    Author = "Shmitzas",
    Description = "CS2-themed Panorama renderer for the SwiftlyS2 menu framework. Consumer plugins need no code changes.")]
public sealed class CustomHUD(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? _services;
    private ILogger<CustomHUD>? _logger;
    private IDisposable? _configReload;
    private IDisposable? _rendererRegistration;
    private IDisposable? _componentRendererRegistration;
    private PanoramaMenuRenderer? _renderer;
    private PlayerPreferences? _prefs;
    private SteamIdCache? _steamIds;
    private NavModeResolver? _navModes;
    private MenuInputBridge? _input;
    private EventDelegates.OnClientPutInServer? _onPutInServer;
    private EventDelegates.OnClientSteamAuthorize? _onSteamAuthorize;
    private EventDelegates.OnClientDisconnected? _onDisconnected;
    private CancellationTokenSource? _retryTimer;
    private CancellationTokenSource? _nativeTextTimer;
    private HudSurface? _hudSurface;
    private NativeTextCapture? _nativeText;
    private PanelSurface? _panelSurface;
    private IntegrationHost? _integrations;
    private readonly PanelService _panels = new();
    private const string CookiesInterfaceKey = "Cookies.Player.v1";
    private readonly Lock _cookiesGate = new();
    private bool? _cookiesAttached;
    private bool _cookiesMissingLogged;

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) =>
        interfaceManager.AddSharedInterface<ICustomHudPanels, PanelService>(ICustomHudPanels.InterfaceKey, _panels);

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        TryAttachCookies(interfaceManager);
        _integrations?.Bind(interfaceManager);
    }

    public override void OnAllPluginsLoaded()
    {
        lock (_cookiesGate)
        {
            if (_cookiesAttached == false && !_cookiesMissingLogged)
            {
                _logger?.LogInformation(
                    "[CustomHUD] Cookies was not found; player preferences are in-memory only.");
                _cookiesMissingLogged = true;
            }
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<CustomHudConfig>("config.jsonc", "CustomHUD")
            .Configure(b => b.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true));

        var configProvider = new CustomHudConfigProvider();
        configProvider.Replace(ReadConfig());

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddSingleton(configProvider);
        collection.AddSingleton(sp => new PlayerPreferences(
            sp.GetRequiredService<ILogger<PlayerPreferences>>(),
            action => Core.Scheduler.NextTick(action)));
        collection.AddSingleton(new SteamIdCache(Core));
        collection.AddSingleton<NavModeResolver>();
        collection.AddSingleton(sp => new MenuEntityHost(Core, sp.GetRequiredService<ILogger<MenuEntityHost>>()));
        collection.AddSingleton<CoreTranslations>();
        // Core.Localizer is the server language, the fallback until a player's is resolved.
        collection.AddSingleton(new HudTranslations(player => Core.Translation.GetPlayerLocalizer(player), Core.Localizer));
        // BasePath is {configs}/plugins/CustomHUD; core reads menus.jsonc from {configs}.
        collection.AddSingleton(sp => new CoreMenuLimits(
            Path.GetFullPath(Path.Combine(Core.Configuration.BasePath, "..", "..")),
            sp.GetRequiredService<ILogger<CoreMenuLimits>>()));
        collection.AddSingleton(sp => new IntegrationHost(Core, sp.GetRequiredService<CustomHudConfigProvider>(),
            sp.GetRequiredService<HudTranslations>(), sp.GetRequiredService<ILogger<IntegrationHost>>()));
        collection.AddSingleton(sp => new PanoramaMenuRenderer(
            Core,
            sp.GetRequiredService<CustomHudConfigProvider>(),
            sp.GetRequiredService<NavModeResolver>(),
            sp.GetRequiredService<SteamIdCache>(),
            sp.GetRequiredService<MenuEntityHost>(),
            sp.GetRequiredService<CoreTranslations>(),
            sp.GetRequiredService<HudTranslations>(),
            sp.GetRequiredService<CoreMenuLimits>(),
            sp.GetRequiredService<IntegrationHost>(),
            sp.GetRequiredService<ILogger<PanoramaMenuRenderer>>()));
        collection.AddSingleton(sp => new MenuInputBridge(
            Core,
            sp.GetRequiredService<CustomHudConfigProvider>(),
            sp.GetRequiredService<PanoramaMenuRenderer>(),
            sp.GetRequiredService<NavModeResolver>(),
            sp.GetRequiredService<SteamIdCache>(),
            sp.GetRequiredService<CoreTranslations>(),
            sp.GetRequiredService<ILogger<MenuInputBridge>>()));
        collection.AddSingleton(sp => new HudSurface(
            sp.GetRequiredService<MenuEntityHost>(),
            sp.GetRequiredService<ILogger<HudSurface>>()));
        collection.AddSingleton(sp => new PanelSurface(
            sp.GetRequiredService<MenuEntityHost>(),
            sp.GetRequiredService<ILogger<PanelSurface>>()));
        collection.AddSingleton(_panels);
        collection.AddSingleton(sp => new NativeTextCapture(
            Core,
            sp.GetRequiredService<CustomHudConfigProvider>(),
            sp.GetRequiredService<SteamIdCache>(),
            sp.GetRequiredService<HudSurface>(),
            sp.GetRequiredService<ILogger<NativeTextCapture>>()));
        collection.AddSingleton(sp => new NumbersNavCommands(
            Core,
            sp.GetRequiredService<MenuInputBridge>(),
            sp.GetRequiredService<NavModeResolver>(),
            sp.GetRequiredService<SteamIdCache>(),
            sp.GetRequiredService<ILogger<NumbersNavCommands>>()));
        collection.AddSingleton<SettingsCommands>();

        _services = collection.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<CustomHUD>>();

        _prefs = _services.GetRequiredService<PlayerPreferences>();

        _renderer = _services.GetRequiredService<PanoramaMenuRenderer>();
        _integrations = _services.GetRequiredService<IntegrationHost>();
        _input = _services.GetRequiredService<MenuInputBridge>();
        _steamIds = _services.GetRequiredService<SteamIdCache>();
        _steamIds.SeedConnected();
        _navModes = _services.GetRequiredService<NavModeResolver>();

        // A preference change only needs the open session invalidated; the next
        // compose picks up the new layout and the apply path respawns the
        // entity, since StrLayout is fixed once an entity has spawned.
        _prefs.NavModeChanged += OnPreferenceChanged;
        _prefs.ScaleChanged += OnPreferenceChanged;

        _rendererRegistration = Core.Menu.Renderers.Register(_renderer);
        _componentRendererRegistration = Core.Menu.Renderers.RegisterComponentRenderer(
            new PanoramaComponentRenderer(configProvider, _services.GetRequiredService<CoreTranslations>(),
                _services.GetRequiredService<HudTranslations>()));

        _input.Subscribe();

        _hudSurface = _services.GetRequiredService<HudSurface>();
        _nativeText = _services.GetRequiredService<NativeTextCapture>();
        _nativeText.Subscribe();

        // Expiry is swept rather than scheduled per message: a per-message
        // timer would have to be cancelled every time a newer message replaced
        // the one it was counting down. Panels with a duration share the sweep.
        _panelSurface = _services.GetRequiredService<PanelSurface>();
        _panels.Attach(Core, configProvider, _steamIds, _panelSurface);
        _nativeTextTimer = Core.Scheduler.RepeatBySeconds(0.25f, () =>
        {
            _hudSurface?.ExpireDue(configProvider.Current);
            _panelSurface?.ExpireDue();
        });

        // A despawn that failed has no other trigger: its only retry points
        // would be the next unrelated despawn and shutdown, so a released but
        // undespawned entity -- which is unrestricted, and therefore visible to
        // every player -- could sit on screen indefinitely.
        //
        // The host is captured directly rather than resolved per tick: the
        // service provider is disposed on unload, and the token below is what
        // stops the timer before that can be observed.
        var entityHost = _services.GetRequiredService<MenuEntityHost>();
        _retryTimer = Core.Scheduler.RepeatBySeconds(5f, entityHost.DrainRetries);

        var numbers = _services.GetRequiredService<NumbersNavCommands>();
        numbers.RegisterAliases();
        Core.Registrator.Register(numbers);
        Core.Registrator.Register(_services.GetRequiredService<SettingsCommands>());

        // A transmit deny only covers clients connected when it was issued, and
        // a reconnect rebuilds the player object holding it -- so every live
        // menu entity has to be re-denied against each joining client or a
        // spectator who connects mid-menu starts receiving somebody else's menu.
        //
        // The same hook keeps the SteamID cache warm: the renderer runs off the
        // game thread and cannot read IPlayer.SteamID itself.
        _onPutInServer = e =>
        {
            _steamIds?.Refresh(e.PlayerId);
            _prefs?.RequestLoad(_steamIds?.Get(e.PlayerId) ?? 0);
            _renderer?.DenyForJoiningClient(e.PlayerId);
            _hudSurface?.DenyForJoiningClient(e.PlayerId);
            _panelSurface?.DenyForJoiningClient(e.PlayerId);
        };
        Core.Event.OnClientPutInServer += _onPutInServer;

        _onSteamAuthorize = e =>
        {
            _steamIds?.Refresh(e.PlayerId);
            _prefs?.RequestLoad(_steamIds?.Get(e.PlayerId) ?? 0);
            // A menu opened before authorization composed against SteamID 0 and
            // therefore default preferences. Nothing else would redraw it, so a
            // static menu would keep them for its whole lifetime.
            _renderer?.InvalidateSlot(e.PlayerId);
        };
        Core.Event.OnClientSteamAuthorize += _onSteamAuthorize;

        // Core does NOT call IMenuRenderer.Clear when a player disconnects
        // mid-menu -- it drops the session instead. Without this the entity
        // survives with nothing left to despawn it, and since
        // DenyForJoiningClient skips the entity's own slot, the next player to
        // take that slot would start receiving it.
        _onDisconnected = e =>
        {
            var steamId = _steamIds?.Get(e.PlayerId) ?? 0;
            _prefs?.Forget(steamId);
            _renderer?.OnPlayerDisconnected(e.PlayerId);
            _hudSurface?.Drop(e.PlayerId);
            _panelSurface?.Drop(e.PlayerId);
            _navModes?.ClearSlot(e.PlayerId);
            _steamIds?.Forget(e.PlayerId);
        };
        Core.Event.OnClientDisconnected += _onDisconnected;

        _configReload = ChangeToken.OnChange(
            () => Core.Configuration.Manager.GetReloadToken(),
            () =>
            {
                configProvider.Replace(ReadConfig());
                _logger?.LogInformation("[CustomHUD] Config reloaded.");
            });

        _logger.LogInformation(
            "[CustomHUD] Renderer '{Id}' registered. Select it with \"Renderer\": \"{Id}\" in configs/menus/menus.jsonc.",
            configProvider.Current.RendererId, configProvider.Current.RendererId);

        if (Core.Configuration.Manager.GetSection("CustomHUD:MouseOverrides").GetChildren().Any())
            _logger.LogWarning(
                "[CustomHUD] config.jsonc has a MouseOverrides section, which is no longer read. "
                + "The WeaponSkins locker rule is built in now; move any rule you added yourself to MouseLayouts and delete MouseOverrides.");

        // Builds before the list wrote the classic layouts into config.jsonc, and a pinned profile
        // wins over it: without this an upgraded server keeps the old look with nothing saying why.
        if (configProvider.Current.Default is { } pinnedKeys)
            _logger.LogInformation(
                "[CustomHUD] config.jsonc sets \"Default\" ({File}), so keyboard menus draw in it rather than the list. Set \"Default\" to null for the list.",
                pinnedKeys.File);
        if (configProvider.Current.Mouse is { } pinnedMouse)
            _logger.LogInformation(
                "[CustomHUD] config.jsonc sets \"Mouse\" ({File}), so mouse menus without a layout rule draw in it rather than the list. Set \"Mouse\" to null for the list.",
                pinnedMouse.File);
    }

    public override void Unload()
    {
        // Stop the timers before anything they touch goes away -- they outlive
        // the plugin otherwise and fire against a disposed provider.
        _retryTimer?.Cancel();
        _retryTimer?.Dispose();
        _retryTimer = null;
        _nativeTextTimer?.Cancel();
        _nativeTextTimer?.Dispose();
        _nativeTextTimer = null;

        // Unhook the capture before anything else: while it is installed a
        // message can still arrive, and a Stop returned after teardown would
        // swallow somebody's text with nothing left to draw it.
        _nativeText?.Unsubscribe();
        _hudSurface?.Shutdown();
        _panels.Detach();
        _panelSurface?.Shutdown();

        // Shut the renderer down FIRST. Consumer sessions still hold a direct
        // reference to it, so disposing the registry handle neither revokes
        // those nor stops an in-flight background render -- and a render that
        // lands after cleanup would respawn an entity nothing will ever
        // despawn. The flag inside Shutdown is what makes that impossible.
        _renderer?.Shutdown();

        // Integrations subscribe to provider events; a handler left behind outlives this assembly.
        _integrations?.Dispose();

        if (_onPutInServer is not null) { Core.Event.OnClientPutInServer -= _onPutInServer; _onPutInServer = null; }
        if (_onSteamAuthorize is not null) { Core.Event.OnClientSteamAuthorize -= _onSteamAuthorize; _onSteamAuthorize = null; }
        if (_onDisconnected is not null) { Core.Event.OnClientDisconnected -= _onDisconnected; _onDisconnected = null; }

        _input?.Unsubscribe();

        if (_prefs is not null)
        {
            _prefs.NavModeChanged -= OnPreferenceChanged;
            _prefs.ScaleChanged -= OnPreferenceChanged;
            _prefs.Detach();
        }

        _componentRendererRegistration?.Dispose();
        _rendererRegistration?.Dispose();
        _configReload?.Dispose();
        _services?.Dispose();
    }

    private void OnPreferenceChanged(ulong steamId)
    {
        _renderer?.OnPreferenceChanged(steamId);
    }

    private void TryAttachCookies(IInterfaceManager interfaceManager)
    {
        lock (_cookiesGate)
        {
            if (_prefs is null) return;

            var backend = ResolveCookies(interfaceManager);
            if (backend is null)
            {
                var wasAttached = _cookiesAttached == true;
                _prefs.Detach();
                _cookiesAttached = false;
                if (wasAttached)
                    _logger?.LogWarning("[CustomHUD] Cookies was unloaded; player preferences are now in-memory only.");
                return;
            }

            var newlyAttached = _prefs.Attach(backend);
            var wasPreviouslyAttached = _cookiesAttached == true;
            _cookiesAttached = true;
            if (!wasPreviouslyAttached)
                _logger?.LogInformation("[CustomHUD] Persisting player preferences through Cookies.");

            if (newlyAttached)
                for (var slot = 0; slot < 64; slot++)
                    _prefs.RequestLoad(_steamIds?.Get(slot) ?? 0);
        }
    }

    private IPreferenceBackend? ResolveCookies(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(CookiesInterfaceKey)) return null;

        try
        {
            return CookiesPreferenceBackend.TryCreate(interfaceManager, CookiesInterfaceKey);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException)
        {
            _logger?.LogWarning(ex, "[CustomHUD] Cookies is loaded but its contract assembly is unavailable; restart the server.");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CustomHUD] Could not connect to the Cookies preference interface.");
            return null;
        }
    }

    private CustomHudConfig ReadConfig()
        => Core.Configuration.Manager.GetSection("CustomHUD").Get<CustomHudConfig>() ?? new CustomHudConfig();
}
