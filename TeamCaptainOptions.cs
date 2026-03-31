using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;

namespace DraftModeTOUM;

public enum CaptainSelectionMode
{
    Random = 0,
    HostChooses = 1
}

public enum TeamWinCondition
{
    LastTeamStanding = 0,
    TimeLimit = 1,
    Both = 2
}

public sealed class TeamCaptainOptions : AbstractOptionGroup
{
    public override string GroupName => "Team Captain Battle Royale";
    public override uint GroupPriority => 92;

    public override System.Func<bool> GroupVisible => () =>
        MiraAPI.GameOptions.OptionGroupSingleton<DraftTypeOptions>.Instance.DraftType == DraftTypeMode.TeamCaptainBR;

    [ModdedNumberOption("Team Captains", 2f, 5f, 1f, MiraNumberSuffixes.None, "0")]
    public float TeamCaptains { get; set; } = 2f;

    [ModdedEnumOption("Captain Selection", typeof(CaptainSelectionMode), new[] { "Random", "Host Chooses" })]
    public CaptainSelectionMode SelectionMode { get; set; } = CaptainSelectionMode.Random;

    [ModdedEnumOption("Win Condition", typeof(TeamWinCondition), new[] { "Last Team Standing", "Time Limit", "Both" })]
    public TeamWinCondition WinCondition { get; set; } = TeamWinCondition.LastTeamStanding;

    [ModdedNumberOption("Round Time (Minutes)", 1f, 60f, 1f, MiraNumberSuffixes.None, "0")]
    public float RoundTimeMinutes { get; set; } = 5f;

    [ModdedNumberOption("Rounds", 2f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float Rounds { get; set; } = 3f;

    [ModdedNumberOption("Prep Time (Seconds)", 0f, 60f, 5f, MiraNumberSuffixes.None, "0")]
    public float PrepTimeSeconds { get; set; } = 30f;

    [ModdedToggleOption("Friendly Fire")]
    public bool FriendlyFire { get; set; } = false;

    [ModdedToggleOption("Tasks Count If Dead")]
    public bool TasksCountIfDead { get; set; } = true;

    [ModdedNumberOption("Round Win Points", 0f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float RoundWinPoints { get; set; } = 3f;

    [ModdedNumberOption("Kill Points", 0f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float KillPoints { get; set; } = 1f;

    [ModdedNumberOption("Task Points", 0f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float TaskPoints { get; set; } = 2f;
}
