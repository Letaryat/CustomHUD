using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CustomHUD.Rendering;

// One player's live render state.
//
// THREAD OWNERSHIP IS THE WHOLE POINT OF THIS CLASS:
//   Entity, Shadow, ClassNames,
//   ScrollKey, RootHandled, TabHandled -- game thread only.
//   LastComposed        -- render thread only.
//   Pending, Rows       -- written by the render thread, read by the game
//                          thread. Reference assignments of immutable values,
//                          published through the Interlocked gate below.
//   Queued              -- Interlocked from both.
//   OwnerSlot           -- effectively immutable once set.
//
// Nothing outside those rules may be added without re-reasoning the coalescer.
internal sealed class PlayerRenderState
{
    public CHandle<CCSCustomHudLayout> Entity;

    // What the entity is currently showing.
    public FrameSnapshot Shadow = FrameSnapshot.Empty;

    // Distinct class names written to Entity since it spawned; see ClassNameBudget.
    public readonly HashSet<string> ClassNames = new(StringComparer.Ordinal);

    // Menu and first drawn row of the list on screen; a change rewinds the
    // layout's scroll position.
    public (IMenu Menu, int FirstRow)? ScrollKey;

    // Session of a skipped sidebar root that has already been opened past, or
    // that the input bridge is navigating from, so it is never auto-opened twice.
    public IMenuSession? RootHandled;

    // The same for a skipped tab host (MenuPageProfile.TabAutoOpen).
    public IMenuSession? TabHandled;

    // Last frame the render thread composed. Used only for its own early-out,
    // never for deciding what to draw.
    public FrameSnapshot? LastComposed;

    // Latest work awaiting apply, latest wins: a FrameSnapshot, or a
    // DeferredCompose the game thread has to compose itself.
    public volatile object? Pending;

    // Row focusability of the frame most recently composed, for the input
    // bridge. Kept beside Pending rather than inside it so a click can be
    // resolved even when no apply is outstanding.
    public volatile RowMap? Rows;

    // 1 while an apply is already queued for this player. Gates task creation
    // so a fast invalidation loop cannot stack several applies on one tick.
    public int Queued;

    // Set by the game thread when an apply or spawn failed, so the next render
    // republishes even though its content matches LastComposed. Without it a
    // failed frame is suppressed forever: the render thread would see no
    // change and never retry, leaving the entity showing the previous frame.
    // Interlocked rather than written through LastComposed, which the render
    // thread owns.
    public int ForceRepublish;

    public int OwnerSlot = -1;
}

// The sidebar slot and tab a player last opened through CustomHUD: the
// active-entry fallback for plugins whose entries carry neither the page's id
// nor its title. Outlives PlayerRenderState, because a plugin that reopens a
// page in place (ShopCore after a purchase) drops that with the old session.
// Game thread only.
internal sealed class NavMemory
{
    public (IMenu Root, int Index)? Entry;
    public (IMenu Host, int Index)? Tab;
}
