using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;

namespace DraftModeTOUM;

public sealed class DraftTypeOptions : AbstractOptionGroup
{
    public override string GroupName => "Draft Type";
    public override uint GroupPriority => 90;

    [ModdedToggleOption("Ban + Draft Mode")]
    public bool EnableBanDraft { get; set; } = false;

    [ModdedNumberOption("Banned Roles Count", 1f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float BanRoleCount { get; set; } = 3f;

    [ModdedToggleOption("Show Banned Roles")]
    public bool ShowBannedRoles { get; set; } = true;

    [ModdedToggleOption("Anonymous Ban Users")]
    public bool AnonymousBanUsers { get; set; } = false;
}
