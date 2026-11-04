using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CustomHUD.Config;

// Core's MenuLayout.MaxVisibleLines. Core keeps it internal, so it is read from
// the same file with the same parsing: a page cut at any other number would
// disagree with the one core enforces.
internal sealed class CoreMenuLimits : IDisposable
{
    public const int DefaultMaxVisibleLines = 5;
    public const string RelativePath = "menus/menus.jsonc";

    private readonly ILogger _logger;
    private readonly PhysicalFileProvider? _files;
    private readonly ConfigurationRoot? _config;
    private readonly IDisposable? _reload;
    private volatile int _maxVisibleLines = DefaultMaxVisibleLines;

    // configRoot is SwiftlyS2's configs directory, the base core resolves the file against.
    public CoreMenuLimits(string? configRoot, ILogger logger)
    {
        _logger = logger;
        if (configRoot is null || !Directory.Exists(configRoot))
        {
            logger.LogWarning("[CustomHUD] Configs directory '{Root}' not found; menu pages use core's default of {Lines} lines.",
                configRoot, DefaultMaxVisibleLines);
            return;
        }

        try
        {
            _files = new PhysicalFileProvider(configRoot);
            var builder = new ConfigurationBuilder();
            builder.Add(new JsonConfigurationSource
            {
                FileProvider = _files,
                Path = RelativePath,
                Optional = true,
                ReloadOnChange = true,
                // A file caught mid-save must not throw on the watcher thread.
                OnLoadException = context => context.Ignore = true,
            });
            _config = (ConfigurationRoot)builder.Build();
            Reload();
            _reload = ChangeToken.OnChange(_config.GetReloadToken, Reload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CustomHUD] Could not read {Path}; menu pages use core's default of {Lines} lines.",
                RelativePath, DefaultMaxVisibleLines);
        }
    }

    public int MaxVisibleLines => _maxVisibleLines;

    // Mirrors SwiftlyS2.Core.Menu.Config.MenuLayoutOptions.
    public static int Parse(string? value)
        => int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 1, 32) : DefaultMaxVisibleLines;

    private void Reload()
    {
        var lines = Parse(_config?["MenuLayout:MaxVisibleLines"]);
        if (Interlocked.Exchange(ref _maxVisibleLines, lines) != lines || _reload is null)
            _logger.LogInformation("[CustomHUD] Menu pages hold up to {Lines} lines (MenuLayout.MaxVisibleLines in configs/{Path}).",
                lines, RelativePath);
    }

    public void Dispose()
    {
        _reload?.Dispose();
        _config?.Dispose();
        _files?.Dispose();
    }
}
