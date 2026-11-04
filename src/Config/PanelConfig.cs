namespace CustomHUD.Config;

// A standalone HUD panel other plugins draw through the CustomHUD.Panels.v1
// shared interface (scoreboard strip, ready counter, timeout banner ...).
// Unlike a menu it has no rows, focus or input: just variables and classes.
public sealed class PanelProfile
{
    public string File { get; set; } = string.Empty;

    public string ShellPanel { get; set; } = "shell";
    public string HiddenClass { get; set; } = "hidden";

    // Panel the dialog variables are set on. Panorama variables inherit down
    // the tree, so the shell reaches every Label in the shipped layouts.
    public string VariablePanel { get; set; } = "shell";

    // Used when the caller passes no duration. <= 0 stays up until hidden.
    public float DurationSeconds { get; set; } = 0f;

    // Set to "" on spawn: Panorama renders an unset {s:var} literally.
    public List<string> BlankVariables { get; set; } = [];

    public static Dictionary<string, PanelProfile> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["versus"] = new()
        {
            File = "panel_versus.vxml_c",
            BlankVariables = ["team1", "team2", "score1", "score2", "status", "detail"],
        },
        ["ready"] = new()
        {
            File = "panel_ready.vxml_c",
            BlankVariables = ["title", "count", "state", "hint"],
        },
        ["timeout"] = new()
        {
            File = "panel_timeout.vxml_c",
            BlankVariables = ["title", "team", "timer", "detail"],
        },
    };
}
