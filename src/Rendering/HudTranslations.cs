using System.Collections.Concurrent;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace CustomHUD.Rendering;

// CustomHUD's own strings in each player's language. Resolving a localizer reads
// native player state, so it happens on the game thread; the composer reads the
// cached one on the render worker, which is safe because a localizer is immutable.
internal sealed class HudTranslations(Func<IPlayer, ILocalizer> resolve, ILocalizer server)
{
    private readonly ConcurrentDictionary<int, ILocalizer> _bySlot = new();

    // Game thread. Runs inside core's frame composition, which must never see it throw.
    public ILocalizer Refresh(IPlayer player)
    {
        try
        {
            var localizer = resolve(player);
            _bySlot[player.PlayerID] = localizer;
            return localizer;
        }
        catch (Exception)
        {
            return For(player.PlayerID);
        }
    }

    // Any thread. The server's language until the game thread has resolved the player's.
    public ILocalizer For(int slot) => _bySlot.TryGetValue(slot, out var localizer) ? localizer : server;

    public void Forget(int slot) => _bySlot.TryRemove(slot, out _);

    // SwiftlyS2 answers a key no file has with "<language>.<key>", never null.
    public static string? Find(ILocalizer localizer, string key)
    {
        var value = localizer[key];
        return value.EndsWith(key, StringComparison.Ordinal) ? null : value;
    }
}
