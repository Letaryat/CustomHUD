using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHUD.Rendering;

// A client binds its HUD panel to a player slot when the panel is CREATED,
// using whatever its HUD follows at that moment -- the observed player while
// spectating -- and never re-binds. So a bystander who happens to be watching
// the owner when an entity spawns renders the owner's per-player state, and no
// amount of per-slot hiding reaches them.
//
// Keeping the entity off their wire is the only fix: no transmit, no panel, no
// binding. Pair it with GLOBAL writes on the surviving single viewer's entity --
// global state carries no slot, so it renders whichever slot that viewer bound
// to.
//
// THE TRAP -- there is no such thing as a persistent global deny. SwiftlyS2's
// `SetTransmitState(bool)` resolves to a for-loop over slots calling the
// PER-PLAYER native, and that native early-returns for any slot with no
// registered player. The block lives in the player object's bit vector, and
// ClientConnect re-creates that object from scratch. So a deny only covers the
// clients connected at the moment it was issued, and a reconnect wipes it.
//
// Consequence: every restricted entity must be re-denied for each joining
// client. See PanoramaMenuRenderer.DenyForJoiningClient.
internal static class HudTransmit
{
    public static void RestrictTo(CCSCustomHudLayout entity, int ownerSlot)
    {
        // Deny for every slot the engine currently knows about, then hand the
        // owner back. Allowing by explicit slot rather than by walking the
        // player list matters: a client is registered (and therefore caught by
        // the sweep above) before it reports IsValid, so an entity spawned in
        // that window would be hidden from its own owner.
        entity.SetTransmitState(false);
        entity.SetTransmitState(true, ownerSlot);
    }

    // Re-blocks a single joining client against an entity that already existed
    // when they connected.
    public static void DenyFor(CCSCustomHudLayout entity, int viewerSlot)
        => entity.SetTransmitState(false, viewerSlot);

    // Blocks are held per player against an entity INDEX and the engine recycles
    // indices, so a leaked block would eventually hide an unrelated entity.
    // Always call before despawning a restricted entity.
    public static void Release(CCSCustomHudLayout entity)
        => entity.SetTransmitState(true);
}
