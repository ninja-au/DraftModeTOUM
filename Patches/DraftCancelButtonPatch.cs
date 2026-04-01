using DraftModeTOUM.Managers;
using HarmonyLib;

namespace DraftModeTOUM.Patches
{
    /// <summary>
    /// Wires DraftCancelButton.Show() and Hide() into the draft lifecycle
    /// by patching DraftNetworkHelper's broadcast methods.
    ///
    /// Show  — triggered when the host calls BroadcastDraftStart.
    /// Hide  — triggered by BroadcastRecap (normal finish),
    ///         BroadcastCancelDraft (host /draftend command),
    ///         and BroadcastDraftEnd (redundant safety net).
    ///
    /// All game-exit / disconnect paths call DraftCancelButton.Hide()
    /// directly inside DraftModePlugin.cs (OnDisconnectPatch,
    /// BeginGameCleanupPatch, IntroCutsceneHidePatch, ShipStatusStartPatch).
    /// </summary>

    // ── Show on draft start ────────────────────────────────────────────────────

    [HarmonyPatch(typeof(DraftNetworkHelper), nameof(DraftNetworkHelper.BroadcastDraftStart))]
    public static class ShowCancelButtonOnDraftStart
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // BroadcastDraftStart is only ever called by the host, so no AmHost
            // guard is needed here.
            DraftCancelButton.Show();
            DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Shown after BroadcastDraftStart.");
        }
    }

    // ── Hide on all finish / cancel paths ─────────────────────────────────────

    /// <summary>Normal draft completion — recap is broadcast then game auto-starts.</summary>
    [HarmonyPatch(typeof(DraftNetworkHelper), nameof(DraftNetworkHelper.BroadcastRecap))]
    public static class HideCancelButtonOnRecap
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DraftCancelButton.Hide();
        }
    }

    /// <summary>Host used /draftend or clicked the cancel button itself.</summary>
    [HarmonyPatch(typeof(DraftNetworkHelper), nameof(DraftNetworkHelper.BroadcastCancelDraft))]
    public static class HideCancelButtonOnCancelDraft
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DraftCancelButton.Hide();
        }
    }

    /// <summary>BroadcastDraftEnd is called alongside BroadcastCancelDraft; belt-and-braces.</summary>
    [HarmonyPatch(typeof(DraftNetworkHelper), nameof(DraftNetworkHelper.BroadcastDraftEnd))]
    public static class HideCancelButtonOnDraftEnd
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DraftCancelButton.Hide();
        }
    }
}
