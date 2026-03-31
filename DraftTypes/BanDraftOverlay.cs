using System.Collections.Generic;
using System.Linq;
using Reactor.Utilities.Attributes;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.UI;
using DraftModeTOUM.Managers;

namespace DraftModeTOUM.DraftTypes
{
    [RegisterInIl2Cpp]
    public sealed class BanDraftOverlay : MonoBehaviour
    {
        private static BanDraftOverlay _instance;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _bgOverlay;
        private Text _title;
        private readonly List<BanEntry> _entries = new();
        private readonly Dictionary<byte, BanEntry> _entryByPlayer = new();
        private byte _currentPickerId = 255;
        private bool _showBannedRoles = true;
        private bool _anonymousUsers = false;

        private bool _pendingShow = false;
        private List<byte> _pendingOrder = new();


        public BanDraftOverlay(System.IntPtr ptr) : base(ptr) { }

        private sealed class BanEntry
        {
            public byte PlayerId;
            public Text NameText;
            public Text StatusText;
            public GameObject RoleCard;
            public Vector2 CardAnchor;
        }

        public static void Show(List<byte> order, bool showBannedRoles, bool anonymousUsers)
        {
            EnsureExists();
            _instance._showBannedRoles = showBannedRoles;
            _instance._anonymousUsers = anonymousUsers;
            _instance._pendingOrder = order != null ? new List<byte>(order) : new List<byte>();
            _instance._pendingShow = true;
            _instance.TryBuildAndShow();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
        }

        public static void SetVisibleForLocal(bool visible)
        {
            if (_instance == null) return;
            _instance.SetVisible(visible);
        }

        public static void SetCurrentPicker(byte pickerId)
        {
            if (_instance == null) return;
            _instance._currentPickerId = pickerId;
            _instance.UpdateHighlight();
        }

        public static void SetBannedRole(byte pickerId, ushort roleId)
        {
            if (_instance == null) return;
            _instance.SetEntryRoleForPlayer(pickerId, roleId, showHidden: false);
        }

        public static void SetBannedRoleHidden(byte pickerId)
        {
            if (_instance == null) return;
            _instance.SetEntryRoleForPlayer(pickerId, 0, showHidden: true);
        }

        private static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("BanDraftOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BanDraftOverlay>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_pendingShow)
            {
                TryBuildAndShow();
            }
        }

        private void TryBuildAndShow()
        {
            if (_canvas == null || _root == null) BuildUI();
            if (_canvas == null || _root == null) return;
            _pendingShow = false;
            BuildList(_pendingOrder);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        private void BuildUI()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("BanDraftCanvas");
            DontDestroyOnLoad(canvasGo);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var bgGo = new GameObject("BanDraftBg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            _bgOverlay = bgGo.AddComponent<Image>();
            _bgOverlay.color = new Color(0f, 0f, 0f, 0.85f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var rootGo = new GameObject("BanDraftRoot");
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.AddComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(0f, 60f);
            _root.sizeDelta = new Vector2(900f, 700f);

            _title = MakeText(_root, "BanDraftTitle", "BAN PHASE", 46, new Color(1f, 0.85f, 0.1f), new Vector2(0f, 300f), true);
        }

        private void BuildList(List<byte> order)
        {
            if (_root == null) return;

            foreach (var e in _entries)
            {
                if (e.NameText != null) Destroy(e.NameText.gameObject);
                if (e.StatusText != null) Destroy(e.StatusText.gameObject);
                if (e.RoleCard != null) Destroy(e.RoleCard);
            }
            _entries.Clear();
            _entryByPlayer.Clear();

            float startY = 180f;
            float stepY = 140f;

            for (int i = 0; i < order.Count; i++)
            {
                byte pid = order[i];
                float y = startY - i * stepY;

                string name = GetDisplayName(pid, i);
                var nameText = MakeText(_root, $"BanName_{i}", name, 32, Color.white, new Vector2(0f, y), true);
                var statusText = MakeText(_root, $"BanStatus_{i}", "Choosing...", 24, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, y - 40f), false);

                var entry = new BanEntry
                {
                    PlayerId = pid,
                    NameText = nameText,
                    StatusText = statusText,
                    RoleCard = null,
                    CardAnchor = new Vector2(0f, y - 95f)
                };

                _entries.Add(entry);
                _entryByPlayer[pid] = entry;
            }

            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            foreach (var entry in _entries)
            {
                if (entry.NameText == null || entry.StatusText == null) continue;
                bool isCurrent = entry.PlayerId == _currentPickerId;
                entry.NameText.color = isCurrent ? new Color(1f, 0.85f, 0.1f) : Color.white;
                entry.StatusText.color = isCurrent ? new Color(1f, 0.85f, 0.1f) : new Color(0.85f, 0.85f, 0.85f);
            }
        }

        private void SetEntryRoleForPlayer(byte pickerId, ushort roleId, bool showHidden)
        {
            if (!_entryByPlayer.TryGetValue(pickerId, out var entry)) return;
            if (entry.StatusText != null)
                entry.StatusText.text = showHidden ? "Banned" : string.Empty;

            if (entry.RoleCard != null)
            {
                Destroy(entry.RoleCard);
                entry.RoleCard = null;
            }

            if (!_showBannedRoles || showHidden) { UpdateHighlight(); return; }

            var role = DraftUiManager.ResolveRole(roleId);
            string roleName = role?.NiceName ?? $"Role {roleId}";
            string alignment = DraftUiManager.GetTeamLabel(role);
            if (entry.StatusText != null)
                entry.StatusText.text = $"Banned: {roleName} ({alignment})";
            entry.RoleCard = null;
            UpdateHighlight();
        }

        private string GetDisplayName(byte pid, int index)
        {
            if (_anonymousUsers) return $"Anonymous {index + 1}";
            var player = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p != null && p.PlayerId == pid);
            return player != null ? player.Data.PlayerName : "Unknown";
        }

        private static Text MakeText(Transform parent, string name, string text, int fontSize, Color color, Vector2 anchoredPos, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800f, 60f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }

        private static Color GetTeamColor(string teamName)
        {
            if (string.IsNullOrEmpty(teamName)) return Color.white;
            string lower = teamName.ToLowerInvariant();
            if (lower.Contains("crewmate")) return new Color32(0, 255, 255, 255);
            if (lower.Contains("impostor") || lower.Contains("imposter")) return new Color32(255, 0, 0, 255);
            if (lower.Contains("neutral")) return new Color32(180, 180, 180, 255);
            return Color.white;
        }

        
    }
}
