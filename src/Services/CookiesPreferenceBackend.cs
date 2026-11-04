using System.Runtime.CompilerServices;
using Cookies.Contract;
using SwiftlyS2.Shared;

namespace CustomHUD.Services;

internal sealed class CookiesPreferenceBackend : IPreferenceBackend
{
    private const string NavModeKey = "customhud.navmode";
    private const string ScaleKey = "customhud.scale";
    private readonly IPlayerCookiesAPIv1 _cookies;

    private CookiesPreferenceBackend(IPlayerCookiesAPIv1 cookies) => _cookies = cookies;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IPreferenceBackend? TryCreate(IInterfaceManager interfaces, string key)
        => interfaces.TryGetSharedInterface<IPlayerCookiesAPIv1>(key, out var cookies) && cookies is not null
            ? new CookiesPreferenceBackend(cookies)
            : null;

    public PlayerPreferenceEntry Read(ulong steamId)
    {
        var id = (long)steamId;
        var navMode = _cookies.Has(id, NavModeKey) ? _cookies.Get<string>(id, NavModeKey) : null;
        var scale = _cookies.Has(id, ScaleKey) ? _cookies.Get<int>(id, ScaleKey) : 0;
        return new PlayerPreferenceEntry(
            string.IsNullOrWhiteSpace(navMode) ? null : navMode,
            scale > 0 ? scale : null);
    }

    public void WriteNavMode(ulong steamId, string mode)
        => _cookies.Set((long)steamId, NavModeKey, mode);

    public void WriteScale(ulong steamId, int percent)
        => _cookies.Set((long)steamId, ScaleKey, percent);
}
