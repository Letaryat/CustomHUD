using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CustomHUD.Services;

internal sealed class PlayerPreferences(ILogger logger, Action<Action> dispatchToGameThread)
{
    private const int LoadAttempts = 3;
    private static readonly TimeSpan LoadDelay = TimeSpan.FromSeconds(1);
    private static readonly object Loaded = new();
    private readonly ConcurrentDictionary<ulong, PlayerPreferenceEntry> _entries = new();
    private readonly Dictionary<ulong, object> _loads = new();
    private readonly Lock _gate = new();
    private volatile IPreferenceBackend? _backend;

    public event Action<ulong>? NavModeChanged;
    public event Action<ulong>? ScaleChanged;

    public bool Attach(IPreferenceBackend backend)
    {
        lock (_gate)
        {
            var wasDetached = _backend is null;
            _backend = backend;
            return wasDetached;
        }
    }

    public void Detach()
    {
        lock (_gate)
        {
            _backend = null;
            _loads.Clear();
        }
    }

    public void RequestLoad(ulong steamId)
    {
        if (steamId == 0 || _backend is null) return;

        object token;
        lock (_gate)
        {
            if (_backend is null || _loads.ContainsKey(steamId)) return;
            token = new object();
            _loads[steamId] = token;
        }

        _ = Task.Run(() => LoadAsync(steamId, token));
    }

    public void Forget(ulong steamId)
    {
        if (steamId == 0) return;

        lock (_gate)
        {
            _loads.Remove(steamId);
            if (_backend is not null)
                _entries.TryRemove(steamId, out _);
        }
    }

    public string? GetNavMode(ulong steamId)
        => _entries.TryGetValue(steamId, out var entry) ? entry.NavMode : null;

    public int? GetScale(ulong steamId)
        => _entries.TryGetValue(steamId, out var entry) ? entry.ScalePercent : null;

    public void SetNavMode(ulong steamId, string mode)
    {
        if (steamId == 0) return;

        lock (_gate)
        {
            var entry = _entries.GetValueOrDefault(steamId) ?? PlayerPreferenceEntry.Empty;
            _entries[steamId] = entry with { NavMode = mode };
        }

        NavModeChanged?.Invoke(steamId);
        Persist(steamId, backend => backend.WriteNavMode(steamId, mode));
    }

    public void SetScale(ulong steamId, int percent)
    {
        if (steamId == 0) return;

        lock (_gate)
        {
            var entry = _entries.GetValueOrDefault(steamId) ?? PlayerPreferenceEntry.Empty;
            _entries[steamId] = entry with { ScalePercent = percent };
        }

        ScaleChanged?.Invoke(steamId);
        Persist(steamId, backend => backend.WriteScale(steamId, percent));
    }

    private async Task LoadAsync(ulong steamId, object token)
    {
        for (var attempt = 1; attempt <= LoadAttempts; attempt++)
        {
            await Task.Delay(LoadDelay).ConfigureAwait(false);

            var backend = _backend;
            if (backend is null || !IsCurrentLoad(steamId, token)) return;

            try
            {
                var stored = backend.Read(steamId);
                dispatchToGameThread(() => Apply(steamId, token, stored));
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[CustomHUD] Could not load preferences for {SteamId} (attempt {Attempt}).", steamId, attempt);
            }
        }

        logger.LogWarning("[CustomHUD] Could not load preferences for {SteamId} after {Attempts} attempts.", steamId, LoadAttempts);
        lock (_gate)
        {
            if (_loads.TryGetValue(steamId, out var current) && ReferenceEquals(current, token))
                _loads.Remove(steamId);
        }
    }

    private void Apply(ulong steamId, object token, PlayerPreferenceEntry stored)
    {
        bool navModeChanged;
        bool scaleChanged;

        lock (_gate)
        {
            if (!_loads.TryGetValue(steamId, out var current) || !ReferenceEquals(current, token))
                return;

            var before = _entries.GetValueOrDefault(steamId) ?? PlayerPreferenceEntry.Empty;
            var after = new PlayerPreferenceEntry(
                before.NavMode ?? stored.NavMode,
                before.ScalePercent ?? stored.ScalePercent);
            _entries[steamId] = after;
            _loads[steamId] = Loaded;
            navModeChanged = before.NavMode != after.NavMode;
            scaleChanged = before.ScalePercent != after.ScalePercent;
        }

        if (navModeChanged) NavModeChanged?.Invoke(steamId);
        if (scaleChanged) ScaleChanged?.Invoke(steamId);
    }

    private bool IsCurrentLoad(ulong steamId, object token)
    {
        lock (_gate)
            return _loads.TryGetValue(steamId, out var current) && ReferenceEquals(current, token);
    }

    private void Persist(ulong steamId, Action<IPreferenceBackend> write)
    {
        var backend = _backend;
        if (backend is null) return;

        try
        {
            write(backend);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CustomHUD] Could not persist preferences for {SteamId}.", steamId);
        }
    }
}

internal interface IPreferenceBackend
{
    PlayerPreferenceEntry Read(ulong steamId);
    void WriteNavMode(ulong steamId, string mode);
    void WriteScale(ulong steamId, int percent);
}

internal sealed record PlayerPreferenceEntry(string? NavMode, int? ScalePercent)
{
    public static readonly PlayerPreferenceEntry Empty = new(null, null);
}
