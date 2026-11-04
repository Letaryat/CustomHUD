using System.Collections.Concurrent;
using SwiftlyS2.Shared;

namespace CustomHUD.Services;

// Slot -> SteamID, maintained on the game thread.
//
// IPlayer.SteamID is a native call guarded by ThrowIfDisposed, so reading it
// from IMenuRenderer's background thread races player disposal and can fault in
// native code, where a managed catch cannot help. The renderer needs the
// SteamID on every compose to resolve nav mode, so it reads this cache instead.
// IPlayer.PlayerID is a plain managed field and stays safe to use directly.
internal sealed class SteamIdCache(ISwiftlyCore core)
{
    private readonly ConcurrentDictionary<int, ulong> _bySlot = new();

    // Game thread.
    public void Refresh(int slot)
    {
        try
        {
            var player = core.PlayerManager.GetPlayer(slot);
            if (player is null || player.IsFakeClient)
            {
                _bySlot.TryRemove(slot, out _);
                return;
            }
            _bySlot[slot] = player.SteamID;
        }
        catch
        {
            // Mid-disconnect lookups legitimately throw; the slot simply has no
            // cached id until the next event.
            _bySlot.TryRemove(slot, out _);
        }
    }

    // Game thread. Seeds every already-connected player, which is what a hot
    // reload needs: the events below only fire for clients joining AFTER load,
    // so without this an existing player's menu composes with SteamID 0 and
    // silently uses default preferences.
    public void SeedConnected()
    {
        foreach (var player in core.PlayerManager.GetAllPlayers())
        {
            if (player.IsFakeClient) continue;
            Refresh(player.PlayerID);
        }
    }

    // Game thread.
    public void Forget(int slot) => _bySlot.TryRemove(slot, out _);

    // Safe from any thread. Zero means "not known yet" -- callers fall back to
    // the configured default rather than treating it as a real id.
    public ulong Get(int slot) => _bySlot.TryGetValue(slot, out var id) ? id : 0UL;

    public int? FindSlot(ulong steamId)
    {
        foreach (var (slot, id) in _bySlot)
            if (id == steamId) return slot;
        return null;
    }
}
