namespace CustomHUD.Config;

// Capture of native CS2 HUD text so CustomHUD draws it instead of the game's
// own HUD.
//
// WHICH MESSAGES THIS REACHES, and why it is not obvious:
//
// SwiftlyS2's IPlayer.SendCenter / SendAlert / SendChat / SendMessage all end
// up in CPlayer::SendMsg, which emits a CUserMessageTextMsg whose `dest` is the
// MessageType value (Notify 1, Console 2, Chat 3, Center 4, ChatEOT 5,
// Alert 6). So one hook covers every one of them.
//
// But SendMsg sets `bypassPostEventAbstractHook` around its send, and
// PostEventAbstractHook -- the hook behind [ServerNetMessageHandler] -- checks
// that flag. So the ordinary server hook sees NONE of SwiftlyS2's own HUD text.
// The capture therefore uses [ServerNetMessageInternalHandler], which hooks
// CServerSideClient::SendNetMessage further down the stack where no bypass
// exists, and which hands us the target player as well.
//
// SendCenterHTML (MessageType 100) is NOT a TextMsg at all: it is buffered and
// re-sent every tick as the `show_survival_respawn_status` game event inside
// CMsgSource1LegacyGameEvent. It is deliberately out of scope here -- see
// docs/PublicPort.md.
public sealed class NativeTextConfig
{
    // Master switch. Off by default: silently swallowing another plugin's
    // messages is never a safe default, and a mis-set layout would make them
    // disappear with nothing on screen to replace them.
    public bool Enabled { get; set; } = false;

    // Layout used for every captured kind that does not name its own.
    public NativeTextProfile Default { get; set; } = new();

    // dest = 4. The one most servers actually want.
    public NativeTextProfile? Center { get; set; }
    // dest = 6.
    public NativeTextProfile? Alert { get; set; }
    // dest = 1.
    public NativeTextProfile? Notify { get; set; }
    // dest = 3 and 5. Capturing these replaces CHAT, which is almost never
    // wanted -- a chat line the player can scroll back to is not a HUD element.
    public NativeTextProfile? Chat { get; set; }
    public NativeTextProfile? ChatEndOfRound { get; set; }
    // dest = 2. Console output; capturing it is almost certainly a mistake,
    // but the knob exists rather than being silently impossible.
    public NativeTextProfile? Console { get; set; }

    public NativeTextProfile Resolve(NativeTextKind kind)
        => (kind switch
        {
            NativeTextKind.Center => Center,
            NativeTextKind.Alert => Alert,
            NativeTextKind.Notify => Notify,
            NativeTextKind.Chat => Chat,
            NativeTextKind.ChatEndOfRound => ChatEndOfRound,
            NativeTextKind.Console => Console,
            _ => null,
        }) ?? Default;
}

public sealed class NativeTextProfile
{
    // When false the message passes through to the native HUD untouched. Every
    // kind defaults to false so enabling the feature does not silently take
    // over every message class at once.
    public bool Capture { get; set; } = false;

    // Compiled layout, resolved against CustomHudConfig.LayoutRoot exactly like
    // a menu profile's File.
    public string File { get; set; } = "hud_text.vxml_c";

    public string ShellPanel { get; set; } = "shell";
    public string HiddenClass { get; set; } = "hidden";
    public string TextPanel { get; set; } = "text";
    public string TextVariable { get; set; } = "text";

    // Class marking which kind produced the text, so one layout can style a
    // center message and an alert differently. `{0}` is the lowercase kind.
    public string KindClass { get; set; } = "kind-{0}";

    // How long the text stays up. The native center message is ~5s, so that is
    // the default; a value <= 0 leaves it up until replaced.
    public float DurationSeconds { get; set; } = 5f;

    // Drop the message entirely instead of forwarding it to the native HUD.
    // False renders in CustomHUD *and* lets the native one through, which is
    // mainly useful while authoring a layout.
    public bool Suppress { get; set; } = true;
}

// Mirrors SwiftlyS2's MessageType, which is what lands in
// CUserMessageTextMsg.Dest. Values are the wire values, not an invention.
public enum NativeTextKind
{
    Notify = 1,
    Console = 2,
    Chat = 3,
    Center = 4,
    ChatEndOfRound = 5,
    Alert = 6,
}
