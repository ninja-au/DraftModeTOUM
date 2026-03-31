using DraftModeTOUM.DraftTypes;
using HarmonyLib;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class TeamCaptainKillPatch
    {
        public static bool Prefix(PlayerControl __instance, PlayerControl target)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return true;
            if (TeamCaptainDraftType.FriendlyFire) return true;
            if (__instance == null || target == null) return true;

            if (TeamCaptainDraftType.TryGetTeam(__instance.PlayerId, out var t1) &&
                TeamCaptainDraftType.TryGetTeam(target.PlayerId, out var t2) &&
                t1 == t2)
            {
                return false;
            }

            return true;
        }

        public static void Postfix(PlayerControl __instance, PlayerControl target)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (!AmongUsClient.Instance.AmHost) return;
            if (__instance == null || target == null) return;
            if (target.Data == null) return;
            if (!target.Data.IsDead) return;

            TeamCaptainDraftType.RegisterKillHost(__instance.PlayerId, target.PlayerId);
        }
    }

    [HarmonyPatch]
    public static class TeamCaptainKillButtonPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PlayerControl), "CanUseKillButton") ??
                   AccessTools.Method(typeof(PlayerControl), "CanKill") ??
                   AccessTools.Method(typeof(PlayerControl), "CanUseKill");
        }

        public static void Postfix(PlayerControl __instance, ref bool __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            __result = true;
        }
    }
}
