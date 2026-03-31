using System.Linq;
using System.Text;
using DraftModeTOUM.DraftTypes;
using HarmonyLib;
using TownOfUs.Utilities;
using UnityEngine;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(TouRoleUtils), nameof(TouRoleUtils.SetTabText))]
    public static class TeamCaptainRoleTabPatch
    {
        public static void Postfix(ref StringBuilder __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (PlayerControl.LocalPlayer == null) return;
            if (!TeamCaptainDraftType.TryGetTeam(PlayerControl.LocalPlayer.PlayerId, out var teamId)) return;

            var text = __result?.ToString() ?? string.Empty;
            var lines = text.Split('\n');
            if (lines.Length < 2) return;

            var color = TeamCaptainDraftType.GetTeamColor(teamId);
            var label = TeamCaptainDraftType.GetTeamLabel(teamId);
            var colored = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{label}</color>";
            var alignmentLine = "<size=60%>Alignment: <b>" + colored + "</b></size>";

            var rest = lines.Length > 2 ? string.Join("\n", lines.Skip(2)) : string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine(lines[0]);
            sb.AppendLine(alignmentLine);
            if (!string.IsNullOrEmpty(rest))
            {
                sb.Append(rest);
            }
            __result = sb;
        }
    }

    [HarmonyPatch(typeof(TouRoleUtils), nameof(TouRoleUtils.SetDeadTabText))]
    public static class TeamCaptainRoleDeadTabPatch
    {
        public static void Postfix(ref StringBuilder __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (PlayerControl.LocalPlayer == null) return;
            if (!TeamCaptainDraftType.TryGetTeam(PlayerControl.LocalPlayer.PlayerId, out var teamId)) return;

            var text = __result?.ToString() ?? string.Empty;
            var lines = text.Split('\n');
            if (lines.Length < 2) return;

            var color = TeamCaptainDraftType.GetTeamColor(teamId);
            var label = TeamCaptainDraftType.GetTeamLabel(teamId);
            var colored = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{label}</color>";
            var alignmentLine = "<size=60%>Alignment: <b>" + colored + "</b></size>";

            var rest = lines.Length > 2 ? string.Join("\n", lines.Skip(2)) : string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine(lines[0]);
            sb.AppendLine(alignmentLine);
            if (!string.IsNullOrEmpty(rest))
            {
                sb.Append(rest);
            }
            __result = sb;
        }
    }
}
