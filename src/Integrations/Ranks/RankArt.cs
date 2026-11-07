using Ranks.Contract;

namespace CustomHUD.Integrations.RankPages;

// A rank icon in the page's terms: the directive that draws it and the colour token tinting it.
// Fed by IRanksApi.GetRankIcon, so the menu shows what the scoreboard does: the points as a CS
// Rating, the skill-group emblem, or in Custom the slot a replacement icon set fills.
internal readonly record struct RankArt(string Directive, string Tone)
{
    private const int MaxStockIcon = 18;

    // The replacement sets the Workshop addon has a class for; generate-locker-art.ps1 holds the same
    // table. Any other number keeps its tone and draws no icon, rather than an empty frame.
    private static readonly (int First, int Last)[] CustomIconSets = [(26283401, 26283500)];

    public string Payload => Directive.Length == 0 ? "tone=" + Tone : Directive + ";tone=" + Tone;

    // csr0-csr6 follow CS2's own rating bands; r1-r7 run grey to gold across the skill groups or,
    // where the icon says nothing about colour, across the ladder.
    public static RankArt For(RanksRankIcon icon, int tierIndex, int tierCount) => icon.Mode switch
    {
        RanksDisplayModes.Premier => new($"rating={Math.Max(0, icon.Value)}", $"csr{Math.Clamp(icon.Value / 5000, 0, 6)}"),
        RanksDisplayModes.Competitive => new($"image=rank/skillgroup{icon.Value}", SkillGroupTone(icon.Value)),
        RanksDisplayModes.Wingman => new($"image=rank/wingman{icon.Value}", SkillGroupTone(icon.Value)),
        RanksDisplayModes.Custom => new(HasArt(icon.Value) ? $"image=rank/skillgroup{icon.Value}" : "", LadderTone(tierCount, tierIndex)),
        _ => new("", LadderTone(tierCount, tierIndex)),
    };

    private static bool HasArt(int icon)
        => icon <= MaxStockIcon || Array.Exists(CustomIconSets, set => icon >= set.First && icon <= set.Last);

    private static string SkillGroupTone(int icon) => icon switch
    {
        <= 4 => "r1",
        <= 7 => "r2",
        <= 10 => "r3",
        <= 12 => "r4",
        <= 14 => "r5",
        <= 17 => "r6",
        _ => "r7",
    };

    private static string LadderTone(int count, int index) =>
        "r" + (1 + (int)Math.Round(6.0 * index / Math.Max(1, count - 1)));
}
