using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;

namespace DraftModeTOUM;

public enum DraftTypeMode
{
    Normal = 0,
    BanDraft = 1,
    TeamCaptainBR = 2
}

public sealed class DraftTypeOptions : AbstractOptionGroup
{
    public override string GroupName => "Draft Type";
    public override uint GroupPriority => 90;

    [ModdedEnumOption("Draft Type", typeof(DraftTypeMode), new[] { "Normal Draft", "Ban + Draft", "Team Captain Battle Royale" })]
    public DraftTypeMode DraftType { get; set; } = DraftTypeMode.Normal;
}
