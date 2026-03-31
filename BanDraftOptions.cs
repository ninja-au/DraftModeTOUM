using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;

namespace DraftModeTOUM;

public sealed class BanDraftOptions : AbstractOptionGroup
{
    public override string GroupName => "Ban + Draft Mode";
    public override uint GroupPriority => 91;

    public override System.Func<bool> GroupVisible => () =>
        MiraAPI.GameOptions.OptionGroupSingleton<DraftTypeOptions>.Instance.DraftType == DraftTypeMode.BanDraft;

    [ModdedNumberOption("Banned Roles Count", 1f, 10f, 1f, MiraNumberSuffixes.None, "0")]
    public float BanRoleCount { get; set; } = 3f;

    [ModdedToggleOption("Show Banned Roles")]
    public bool ShowBannedRoles { get; set; } = true;

    [ModdedToggleOption("Anonymous Ban Users")]
    public bool AnonymousBanUsers { get; set; } = false;

    [ModdedNumberOption("Ban Pick Timeout (Seconds)", 0f, 30f, 1f, MiraNumberSuffixes.None, "0")]
    public float BanPickTimeoutSeconds { get; set; } = 10f;
}
