using CustomHUD.Config;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHUD.Rendering;

// Every CCSCustomHudLayout touch in the plugin. GAME THREAD ONLY -- the
// renderer reaches this exclusively through Scheduler.NextTick.
internal sealed class MenuEntityHost(ISwiftlyCore core, ILogger logger)
{
    private int _counter;

    public CHandle<CCSCustomHudLayout> Spawn(ulong steamId, int ownerSlot, string layoutPath, string shellPanel, string hiddenClass)
    {
        CCSCustomHudLayout entity;
        try
        {
            entity = core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] CreateEntity<CCSCustomHudLayout> threw.");
            return default;
        }

        // The handle is taken BEFORE anything that can fail, so every later
        // failure is retryable. Forgetting an entity is the one outcome with no
        // recovery: nothing can address it again, so it stays on screen or
        // keeps transmit blocks against an index the engine will recycle.
        CHandle<CCSCustomHudLayout> handle = default;
        try
        {
            handle = core.EntitySystem.GetRefEHandle(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] GetRefEHandle failed immediately after creation.");
        }

        try
        {
            entity.StrLayout = layoutPath;
            entity.StrLayoutUpdated();

            using var kv = new CEntityKeyValues();
            kv.SetString("targetname", $"customhud_menu_{steamId}_{Interlocked.Increment(ref _counter)}");
            entity.DispatchSpawn(kv);

            // Re-read: a handle taken before DispatchSpawn is not guaranteed to
            // be the one the entity ends up with.
            handle = core.EntitySystem.GetRefEHandle(entity);

            // Transmit restriction is the ONLY bystander guard. A per-slot
            // `hidden` stamp cannot be layered on top: a dead or spectating
            // owner binds this panel to the OBSERVED player's slot, so the
            // stamp would resolve a bystander entry and hide the menu from the
            // player it belongs to -- input still routes, so it reads as
            // "clicks work, nothing drawn".
            HudTransmit.RestrictTo(entity, ownerSlot);

            // The VXML `hidden` default is not enough. A client with no entry
            // of its own resolves the GLOBAL state -- and a class the server
            // never wrote has no global entry either, so such a client renders
            // ANOTHER player's entry. Writing the hidden default explicitly
            // gives them something of their own to resolve to.
            entity.SetHasClass(shellPanel, hiddenClass, EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass);

            return handle;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Spawn failed after entity creation; cleaning up.");
            if (handle.Value is not null)
            {
                if (!TryDespawn(handle)) _retry.Add(handle);
            }
            else
            {
                // No usable handle was ever obtained, so this is the only
                // chance to remove it. No transmit restriction has been
                // installed at this point, so a failure here leaks the entity
                // but not any blocks.
                try { entity.Despawn(); }
                catch (Exception inner) { logger.LogError(inner, "[CustomHUD] Orphan entity could not be despawned."); }
            }
            return default;
        }
    }

    public void Apply(CCSCustomHudLayout entity, FrameDelta delta)
    {
        // Global writes, never the ...ForPlayer variants: the entity is already
        // restricted to one viewer, and global state still renders when that
        // viewer's client has bound to a different slot -- which is exactly
        // what happens while they spectate.
        foreach (var write in delta.Vars)
            entity.SetDialogVariableString(write.PanelId, write.Variable, write.Value);

        foreach (var write in delta.Classes)
            entity.SetHasClass(write.PanelId, write.ClassName, write.On
                ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
                : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);
    }

    // Per-player, unlike Apply: the global SetInputCaptureEnabled only writes a
    // networked field and never reaches the native call that shows the cursor.
    public void SetInputCapture(CCSCustomHudLayout entity, int slot, bool enabled)
    {
        try
        {
            entity.SetInputCaptureEnabledForPlayer(slot, enabled);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CustomHUD] Input capture {State} failed for slot {Slot}.",
                enabled ? "enable" : "disable", slot);
        }
    }

    public void Despawn(CHandle<CCSCustomHudLayout> handle)
    {
        DrainRetries();
        if (handle.Value is null) return;
        if (!TryDespawn(handle))
            _retry.Add(handle);
    }

    // A failed release leaks per-viewer transmit blocks against an index the
    // engine recycles; a failed despawn leaves a menu on a player's screen.
    // Neither is acceptable to just log, so the handle is kept and retried --
    // the entity stays addressable because the handle, not the pointer, is what
    // we hold.
    private readonly List<CHandle<CCSCustomHudLayout>> _retry = new();

    private bool TryDespawn(CHandle<CCSCustomHudLayout> handle)
    {
        // Already gone: the engine freed it, so there is nothing left to
        // release and nothing to retry.
        if (handle.Value is not { } entity) return true;

        try
        {
            HudTransmit.Release(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Transmit release failed; retrying before despawn.");
            return false;
        }

        try
        {
            entity.Despawn();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Despawn failed; entity still on screen, retrying.");
            return false;
        }
    }

    // Game thread. Called on every despawn, on shutdown, and on a timer -- a
    // retry whose only trigger was "some other despawn happens" would leave a
    // released-but-not-despawned entity (which is unrestricted, so visible to
    // everyone) on screen until one did.
    public void DrainRetries()
    {
        for (var i = _retry.Count - 1; i >= 0; i--)
            if (TryDespawn(_retry[i]))
                _retry.RemoveAt(i);
    }

    public bool HasPendingRetries => _retry.Count > 0;

    public void DenyFor(CHandle<CCSCustomHudLayout> handle, int viewerSlot)
    {
        if (handle.Value is not { } entity) return;
        try { HudTransmit.DenyFor(entity, viewerSlot); }
        catch (Exception ex) { logger.LogWarning(ex, "[CustomHUD] Per-viewer deny failed."); }
    }
}
