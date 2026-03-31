using DraftModeTOUM.DraftTypes;
using HarmonyLib;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
    public static class TeamCaptainCheckEndCriteriaPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            if (!TeamCaptainDraftType.HandleExternalEndGameRequest())
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    public static class TeamCaptainIsGameOverDueToDeathPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (TeamCaptainDraftType.HandleExternalEndGameRequest())
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
    public static class TeamCaptainRpcEndGamePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            if (!TeamCaptainDraftType.HandleExternalEndGameRequest()) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
    public static class TeamCaptainEndGameManagerStartPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!TeamCaptainDraftType.HandleExternalEndGameRequest()) return true;
            return false;
        }
    }
}
