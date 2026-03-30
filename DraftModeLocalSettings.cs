using BepInEx.Configuration;
using MiraAPI.LocalSettings;
using TownOfUs.Assets;

namespace DraftModeTOUM;

public sealed class DraftModeLocalSettings(ConfigFile config) : LocalSettingsTab(config)
{
    public override string TabName => "Draft Mode";
    protected override bool ShouldCreateLabels => true;

    public override LocalSettingTabAppearance TabAppearance => new()
    {
        TabIcon = TouRoleIcons.Traitor
    };
}
