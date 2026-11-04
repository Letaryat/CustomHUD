using System.Collections.Concurrent;
using CustomHUD.Config;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHUD.Rendering;

// One entity per (player, panel profile), so a versus strip and a ready
// counter can be up at once. Same diff-before-write rules as HudSurface.
//
// GAME THREAD ONLY.
internal sealed class PanelSurface(MenuEntityHost entities, ILogger logger)
{
    private readonly ConcurrentDictionary<(int Slot, string Profile), Surface> _surfaces = new();

    private sealed class Surface
    {
        public CHandle<CCSCustomHudLayout> Entity;
        public string LayoutPath = string.Empty;
        public string ShellPanel = "shell";
        public string HiddenClass = "hidden";
        public readonly Dictionary<string, string> Variables = new(StringComparer.Ordinal);
        public readonly HashSet<string> Classes = new(StringComparer.Ordinal);
        public DateTime ExpiresAt = DateTime.MaxValue;
        public bool Visible;
    }

    public void Show(
        int slot, ulong steamId, string profileName, PanelProfile profile, string layoutPath,
        IReadOnlyDictionary<string, string> variables, IReadOnlyCollection<string>? classes, float durationSeconds)
    {
        var key = (slot, profileName.ToLowerInvariant());
        try
        {
            var surface = _surfaces.GetOrAdd(key, _ => new Surface());

            if (!surface.Entity.IsValid || !string.Equals(surface.LayoutPath, layoutPath, StringComparison.Ordinal))
            {
                entities.Despawn(surface.Entity);
                surface.Entity = entities.Spawn(steamId, slot, layoutPath, profile.ShellPanel, profile.HiddenClass);
                surface.LayoutPath = layoutPath;
                surface.ShellPanel = profile.ShellPanel;
                surface.HiddenClass = profile.HiddenClass;
                surface.Variables.Clear();
                surface.Classes.Clear();
                surface.Visible = false;

                if (surface.Entity.Value is { } fresh)
                {
                    foreach (var name in profile.BlankVariables)
                    {
                        fresh.SetDialogVariableString(profile.VariablePanel, name, string.Empty);
                        surface.Variables[name] = string.Empty;
                    }
                }
            }

            if (surface.Entity.Value is not { } entity) return;

            foreach (var (name, value) in variables)
            {
                var text = value ?? string.Empty;
                if (surface.Variables.TryGetValue(name, out var current) && string.Equals(current, text, StringComparison.Ordinal))
                    continue;
                entity.SetDialogVariableString(profile.VariablePanel, name, text);
                surface.Variables[name] = text;
            }

            // Null means "leave the classes alone"; an empty collection clears them.
            if (classes is not null)
            {
                foreach (var stale in surface.Classes.Where(c => !classes.Contains(c)).ToList())
                {
                    SetClass(entity, profile.ShellPanel, stale, false);
                    surface.Classes.Remove(stale);
                }
                foreach (var cls in classes)
                {
                    if (string.IsNullOrWhiteSpace(cls) || !surface.Classes.Add(cls)) continue;
                    SetClass(entity, profile.ShellPanel, cls, true);
                }
            }

            if (!surface.Visible)
            {
                SetClass(entity, profile.ShellPanel, profile.HiddenClass, false);
                surface.Visible = true;
            }

            surface.ExpiresAt = durationSeconds > 0f
                ? DateTime.UtcNow.AddSeconds(durationSeconds)
                : DateTime.MaxValue;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Panel {Profile} failed for slot {Slot}.", profileName, slot);
        }
    }

    public void ExpireDue()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, surface) in _surfaces)
        {
            if (surface.Visible && surface.ExpiresAt <= now)
                HideSurface(key, surface);
        }
    }

    public void Hide(int slot, string profileName)
    {
        var key = (slot, profileName.ToLowerInvariant());
        if (_surfaces.TryGetValue(key, out var surface))
            HideSurface(key, surface);
    }

    public void HideAll(int slot)
    {
        foreach (var (key, surface) in _surfaces)
            if (key.Slot == slot) HideSurface(key, surface);
    }

    public void HideEverywhere(string profileName)
    {
        var name = profileName.ToLowerInvariant();
        foreach (var (key, surface) in _surfaces)
            if (key.Profile == name) HideSurface(key, surface);
    }

    // Kept spawned rather than despawned: panels like a ready counter toggle
    // every few seconds, and a respawn would re-send the whole layout.
    private void HideSurface((int Slot, string Profile) key, Surface surface)
    {
        if (!surface.Visible || surface.Entity.Value is not { } entity) return;
        try
        {
            SetClass(entity, surface.ShellPanel, surface.HiddenClass, true);
            surface.Visible = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Panel {Profile} hide failed for slot {Slot}.", key.Profile, key.Slot);
        }
    }

    public void Drop(int slot)
    {
        foreach (var key in _surfaces.Keys.Where(k => k.Slot == slot).ToList())
        {
            if (!_surfaces.TryRemove(key, out var surface)) continue;
            entities.Despawn(surface.Entity);
            surface.Entity = default;
        }
    }

    public void DenyForJoiningClient(int viewerSlot)
    {
        foreach (var (key, surface) in _surfaces)
        {
            if (key.Slot == viewerSlot) continue;
            entities.DenyFor(surface.Entity, viewerSlot);
        }
    }

    public void Shutdown()
    {
        foreach (var key in _surfaces.Keys.ToList())
        {
            if (!_surfaces.TryRemove(key, out var surface)) continue;
            entities.Despawn(surface.Entity);
        }
    }

    private static void SetClass(CCSCustomHudLayout entity, string panel, string className, bool on)
        => entity.SetHasClass(panel, className, on
            ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
            : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);
}
