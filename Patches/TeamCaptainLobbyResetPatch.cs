using DraftModeTOUM.DraftTypes;
using HarmonyLib;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    public static class TeamCaptainLobbyResetPatch
    {
        public static void Postfix()
        {
            TeamCaptainDraftType.ResetState();
        }
    }
}
