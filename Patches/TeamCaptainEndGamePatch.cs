using System.Collections.Generic;
using System.Reflection;
using DraftModeTOUM.DraftTypes;
using HarmonyLib;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch]
    public static class TeamCaptainPreventEndGamePatch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            var shipCheck = AccessTools.Method(typeof(ShipStatus), "CheckEndGame");
            if (shipCheck != null) methods.Add(shipCheck);

            var clientCheck = AccessTools.Method(typeof(AmongUsClient), "CheckForGameEnd") ??
                              AccessTools.Method(typeof(AmongUsClient), "CheckForEndGame");
            if (clientCheck != null) methods.Add(clientCheck);

            return methods;
        }

        public static bool Prefix(ref bool __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return true;
            __result = false;
            return false;
        }
    }
}
