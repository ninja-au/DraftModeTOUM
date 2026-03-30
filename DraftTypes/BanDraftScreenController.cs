using System.Collections.Generic;
using DraftModeTOUM;
using DraftModeTOUM.Patches;
using UnityEngine;

namespace DraftModeTOUM.DraftTypes
{
    public sealed class BanDraftScreenController : MonoBehaviour
    {
        private static BanRoleMenu _activeMenu;

        public static void Show(List<ushort> roleIds, bool allowPick)
        {
            if (!allowPick)
            {
                Hide();
                return;
            }

            Hide();
            DraftStatusOverlay.SetState(OverlayState.BackgroundOnly);
            _activeMenu = BanRoleMenu.Create();
            _activeMenu.Begin(roleIds ?? new List<ushort>(), roleId =>
            {
                DraftNetworkHelper.SendBanPickToHost(roleId);
                Hide();
            });
        }

        public static void Hide()
        {
            if (_activeMenu == null) return;
            try { _activeMenu.Close(); } catch { }
            try { Destroy(_activeMenu.gameObject); } catch { }
            _activeMenu = null;
        }
    }
}
