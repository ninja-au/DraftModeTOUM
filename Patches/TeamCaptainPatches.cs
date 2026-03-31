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
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var list = new System.Collections.Generic.List<System.Reflection.MethodBase>();
            var m = AccessTools.Method(typeof(PlayerControl), "CanUseKillButton");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(PlayerControl), "CanKill");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(PlayerControl), "CanUseKill");
            if (m != null) list.Add(m);
            return list;
        }

        public static void Postfix(PlayerControl __instance, ref bool __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class TeamCaptainHudKillButtonPatch
    {
        public static void Postfix(HudManager __instance)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (__instance == null) return;
            try
            {
                var killButton = __instance.KillButton;
                if (killButton == null) return;
                killButton.gameObject.SetActive(true);
                var setEnabled = AccessTools.Method(killButton.GetType(), "SetEnabled");
                if (setEnabled != null)
                {
                    setEnabled.Invoke(killButton, new object[] { true });
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
