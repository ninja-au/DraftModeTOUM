using System;
using System.Text;
using DraftModeTOUM.DraftTypes;
using MiraAPI.Roles;
using UnityEngine;

namespace DraftModeTOUM.Roles;

public sealed class GunslingerRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ICustomRole
{
    public string RoleName => "Gunslinger";
    public string RoleDescription => "Eliminate the enemy team.";
    public string RoleLongDescription => "Work with your team to win the round.";
    public Color RoleColor => new Color(1f, 0.9f, 0.2f);
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;

    public CustomRoleConfiguration Configuration
    {
        get
        {
            var cfg = new CustomRoleConfiguration(this)
            {
                HideSettings = true,
                ShowInFreeplay = false,
                DefaultRoleCount = 0,
                DefaultChance = 0
            };

            cfg.UseVanillaKillButton = true;
            cfg.CanUseVent = false;
            cfg.CanUseSabotage = false;
            cfg.TasksCountForProgress = true;
            cfg.CanGetKilled = true;
            cfg.KillButtonOutlineColor = RoleColor;

            return cfg;
        }
    }

    public StringBuilder SetTabText()
    {
        var sb = new StringBuilder();
        var roleColor = ColorUtility.ToHtmlStringRGBA(RoleColor);
        sb.AppendLine($"<color=#{roleColor}>Your role is<b> {RoleName}.</b></color>");

        var alignment = "Team";
        if (TeamCaptainDraftType.TryGetTeam(PlayerControl.LocalPlayer.PlayerId, out var teamId))
        {
            var teamColor = TeamCaptainDraftType.GetTeamColor(teamId);
            var teamLabel = TeamCaptainDraftType.GetTeamLabel(teamId);
            alignment = $"<color=#{ColorUtility.ToHtmlStringRGBA(teamColor)}>{teamLabel}</color>";
        }

        sb.AppendLine($"<size=60%>Alignment: <b>{alignment}</b></size>");
        sb.Append("<size=70%>");
        sb.AppendLine(RoleLongDescription);
        return sb;
    }
}
