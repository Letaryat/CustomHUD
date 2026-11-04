using System.Collections.Concurrent;

namespace CustomHUD.Rendering;

// Core's per-player menu translator, captured as core renders each component.
// The public API only exposes it through IMenuComponentRenderContext, and rows
// rendered outside core's window still need it ("menu.input.empty" and friends).
internal sealed class CoreTranslations
{
    private readonly ConcurrentDictionary<int, Func<string, string>> _bySlot = new();

    // True when the slot's translator is new. Core keeps one per language, so a
    // change means the player's language did.
    public bool Remember(int slot, Func<string, string> translate)
    {
        if (_bySlot.TryGetValue(slot, out var current) && ReferenceEquals(current, translate)) return false;
        _bySlot[slot] = translate;
        return true;
    }

    public Func<string, string> For(int slot)
        => _bySlot.TryGetValue(slot, out var translate) ? translate : static key => key;

    public void Forget(int slot) => _bySlot.TryRemove(slot, out _);
}
