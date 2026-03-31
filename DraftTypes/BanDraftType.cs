using System.Collections.Generic;
using System.Linq;
using DraftModeTOUM.Managers;
using DraftModeTOUM;
using DraftModeTOUM.Patches;
using MiraAPI.GameOptions;
using UnityEngine;

namespace DraftModeTOUM.DraftTypes
{
    public static class BanDraftType
    {
        public static bool IsEnabled =>
            OptionGroupSingleton<DraftTypeOptions>.Instance.DraftType == DraftTypeMode.BanDraft;
        public static bool IsBanPhaseActive { get; private set; }
        public static int BanCount { get; private set; } = 3;
        public static bool ShowBannedRoles { get; private set; } = true;
        public static bool AnonymousBanUsers { get; private set; } = false;
        public static float BanPickTimeoutSeconds { get; private set; } = 10f;

        private static readonly List<byte> _banOrder = new();
        private static readonly Dictionary<byte, ushort> _bannedByPlayer = new();
        private static readonly HashSet<ushort> _bannedRoleIds = new();
        private static DraftRolePool _banPool = new();
        private static int _currentIndex = 0;
        private static bool _preserveBansForNextDraft = false;
        private static float _banTurnTimeLeft = 0f;
        private static byte _currentPickerId = 255;

        public static IReadOnlyList<byte> BanOrder => _banOrder;
        public static IReadOnlyCollection<ushort> BannedRoleIds => _bannedRoleIds;

        public static void ApplySettings()
        {
            var opts = OptionGroupSingleton<BanDraftOptions>.Instance;
            BanCount = Mathf.Clamp(Mathf.RoundToInt(opts.BanRoleCount), 1, 10);
            ShowBannedRoles = opts.ShowBannedRoles;
            AnonymousBanUsers = opts.AnonymousBanUsers;
            BanPickTimeoutSeconds = Mathf.Clamp(opts.BanPickTimeoutSeconds, 0f, 60f);
        }

        public static void StartBanPhaseHost()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            ApplySettings();
            _preserveBansForNextDraft = false;

            var players = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null && !p.Data.Disconnected).ToList();
            if (players.Count == 0)
            {
                DraftModePlugin.Logger.LogInfo("[BanDraft] No players found, skipping ban phase.");
                DraftManager.StartDraftInternal();
                return;
            }

            ResetState();

            _banPool = RolePoolBuilder.BuildPool();
            if (_banPool.RoleIds.Count == 0 || BanCount <= 0)
            {
                DraftModePlugin.Logger.LogInfo("[BanDraft] Empty role pool or BanCount=0, skipping ban phase.");
                DraftManager.StartDraftInternal();
                return;
            }

            int banSlots = Mathf.Clamp(BanCount, 1, Mathf.Min(players.Count, _banPool.RoleIds.Count));
            DraftModePlugin.Logger.LogInfo($"[BanDraft] Starting ban phase. players={players.Count} banSlots={banSlots} pool={_banPool.RoleIds.Count}");

            var shuffled = players.OrderBy(_ => UnityEngine.Random.value).ToList();
            for (int i = 0; i < banSlots; i++)
                _banOrder.Add(shuffled[i].PlayerId);

            IsBanPhaseActive = true;
            _currentIndex = 0;
            _currentPickerId = 255;
            _banTurnTimeLeft = 0f;

            var orderCopy = _banOrder.ToList();
            try
            {
                HandleBanStartLocal(orderCopy, ShowBannedRoles, AnonymousBanUsers);
                DraftNetworkHelper.BroadcastBanStart(orderCopy, ShowBannedRoles, AnonymousBanUsers);
            }
            catch (System.Exception ex)
            {
                DraftModePlugin.Logger.LogWarning($"[BanDraft] Ban UI failed: {ex.Message} — falling back to normal draft.");
                IsBanPhaseActive = false;
                DraftManager.StartDraftInternal();
                return;
            }

            StartNextBanTurnHost();
        }

        public static void HandleBanStartLocal(List<byte> order, bool showBannedRoles, bool anonymousUsers)
        {
            IsBanPhaseActive = true;
            var incoming = new List<byte>(order);
            _banOrder.Clear();
            _banOrder.AddRange(incoming);
            _bannedByPlayer.Clear();
            _bannedRoleIds.Clear();
            ShowBannedRoles = showBannedRoles;
            AnonymousBanUsers = anonymousUsers;
            DraftStatusOverlay.SetState(OverlayState.BackgroundOnly);

            try
            {
                BanDraftOverlay.Show(_banOrder, ShowBannedRoles, AnonymousBanUsers);
            }
            catch (System.Exception ex)
            {
                DraftModePlugin.Logger.LogWarning($"[BanDraft] Overlay failed: {ex.Message}");
            }
        }

        public static void HandleBanTurnLocal(byte pickerId, List<ushort> availableRoleIds, int index, int total)
        {
            if (!IsBanPhaseActive) return;
            BanDraftOverlay.SetCurrentPicker(pickerId);

            bool isPicker = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == pickerId;
            if (isPicker)
            {
                BanDraftOverlay.SetVisibleForLocal(false);
                BanDraftScreenController.Show(availableRoleIds, true);
            }
            else
            {
                BanDraftOverlay.SetVisibleForLocal(true);
                BanDraftScreenController.Hide();
            }
        }

        public static void HandleBanPickHost(byte pickerId, ushort roleId)
        {
            if (!AmongUsClient.Instance.AmHost || !IsBanPhaseActive) return;
            if (_currentIndex >= _banOrder.Count) return;
            if (_banOrder[_currentIndex] != pickerId) return;
            if (!IsRoleBanEligible(roleId)) return;

            _bannedRoleIds.Add(roleId);
            _bannedByPlayer[pickerId] = roleId;
            DraftNetworkHelper.BroadcastBanPick(pickerId, roleId);

            _currentIndex++;
            StartNextBanTurnHost();
        }

        public static void HandleBanPickLocal(byte pickerId, ushort roleId)
        {
            if (!IsBanPhaseActive) return;
            _bannedByPlayer[pickerId] = roleId;
            _bannedRoleIds.Add(roleId);
            if (ShowBannedRoles) BanDraftOverlay.SetBannedRole(pickerId, roleId);
            else BanDraftOverlay.SetBannedRoleHidden(pickerId);
        }

        public static void EndBanPhaseHost()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            IsBanPhaseActive = false;
            BanDraftOverlay.Hide();
            BanDraftScreenController.Hide();
            PreserveBansForNextDraft();
            DraftNetworkHelper.BroadcastBanEnd();
            DraftManager.StartDraftInternal();
        }

        public static void EndBanPhaseLocal()
        {
            IsBanPhaseActive = false;
            BanDraftOverlay.Hide();
            BanDraftScreenController.Hide();
            DraftStatusOverlay.SetState(OverlayState.Hidden);
        }

        public static void ApplyBansToPool(DraftRolePool pool)
        {
            if (pool == null || _bannedRoleIds.Count == 0) return;
            foreach (var id in _bannedRoleIds.ToArray())
            {
                pool.RoleIds.Remove(id);
                pool.MaxCounts.Remove(id);
                pool.Weights.Remove(id);
                pool.Factions.Remove(id);
            }
        }

        public static void Reset()
        {
            EndBanPhaseLocal();
            bool keepBans = _preserveBansForNextDraft;
            _preserveBansForNextDraft = false;
            ResetState(keepBans);
        }

        private static void ResetState(bool keepBans = false)
        {
            _banOrder.Clear();
            _bannedByPlayer.Clear();
            if (!keepBans) _bannedRoleIds.Clear();
            _currentIndex = 0;
            _currentPickerId = 255;
            _banTurnTimeLeft = 0f;
            _banPool = new DraftRolePool();
        }

        private static void PreserveBansForNextDraft()
        {
            _preserveBansForNextDraft = true;
        }

        private static void StartNextBanTurnHost()
        {
            if (!IsBanPhaseActive) return;

            if (_currentIndex >= _banOrder.Count)
            {
                DraftModePlugin.Logger.LogInfo("[BanDraft] Ban phase complete, starting draft.");
                EndBanPhaseHost();
                return;
            }

            var available = GetAvailableBanRoleIds();
            if (available.Count == 0)
            {
                DraftModePlugin.Logger.LogInfo("[BanDraft] No available roles to ban, starting draft.");
                EndBanPhaseHost();
                return;
            }

            byte pickerId = _banOrder[_currentIndex];
            DraftModePlugin.Logger.LogInfo($"[BanDraft] Ban turn {_currentIndex + 1}/{_banOrder.Count} picker={pickerId} available={available.Count}");
            _currentPickerId = pickerId;
            _banTurnTimeLeft = BanPickTimeoutSeconds;
            DraftNetworkHelper.BroadcastBanTurn(pickerId, available, _currentIndex, _banOrder.Count);
        }

        private static bool IsRoleBanEligible(ushort roleId)
        {
            if (_bannedRoleIds.Contains(roleId)) return false;
            return _banPool.RoleIds.Contains(roleId);
        }

        private static List<ushort> GetAvailableBanRoleIds()
        {
            return _banPool.RoleIds.Where(id => !_bannedRoleIds.Contains(id)).ToList();
        }

        public static void Tick(float deltaTime)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!IsBanPhaseActive) return;
            if (BanPickTimeoutSeconds <= 0f) return;
            if (_currentIndex >= _banOrder.Count) return;

            _banTurnTimeLeft -= deltaTime;
            if (_banTurnTimeLeft > 0f) return;

            var available = GetAvailableBanRoleIds();
            if (available.Count == 0)
            {
                EndBanPhaseHost();
                return;
            }

            var pickId = available[UnityEngine.Random.Range(0, available.Count)];
            DraftModePlugin.Logger.LogInfo($"[BanDraft] Auto-picking roleId={pickId} for picker={_currentPickerId}");
            _banTurnTimeLeft = 0f;
            HandleBanPickHost(_currentPickerId, pickId);
        }
    }
}
