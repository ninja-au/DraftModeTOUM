using System.Collections.Generic;
using System.Linq;
using Reactor.Utilities.Attributes;
using TMPro;
using TownOfUs.Assets;
using TownOfUs.Utilities;
using UnityEngine;
using DraftModeTOUM.Managers;

namespace DraftModeTOUM.DraftTypes
{
    [RegisterInIl2Cpp]
    public sealed class BanDraftOverlay : MonoBehaviour
    {
        private static BanDraftOverlay _instance;

        private GameObject _root;
        private GameObject _bgOverlay;
        private TextMeshPro _title;
        private readonly List<BanEntry> _entries = new();
        private readonly Dictionary<byte, BanEntry> _entryByPlayer = new();
        private byte _currentPickerId = 255;
        private bool _showBannedRoles = true;
        private bool _anonymousUsers = false;

        private static GameObject _cachedRolePrefab;

        private const float CardScale = 0.35f;
        private const float CardTiltDeg = -8f;

        public BanDraftOverlay(System.IntPtr ptr) : base(ptr) { }

        private sealed class BanEntry
        {
            public byte PlayerId;
            public TextMeshPro NameText;
            public TextMeshPro StatusText;
            public GameObject RoleCard;
            public Vector3 CardAnchor;
        }

        public static void Show(List<byte> order, bool showBannedRoles, bool anonymousUsers)
        {
            EnsureExists();
            _instance._showBannedRoles = showBannedRoles;
            _instance._anonymousUsers = anonymousUsers;
            _instance.BuildList(order);
            _instance.SetVisible(true);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
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

        private void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (_bgOverlay != null) _bgOverlay.SetActive(visible);
        }

        private void BuildUI()
        {
            if (HudManager.Instance == null) return;

            var font = HudManager.Instance.TaskPanel.taskText.font;
            var fontMat = HudManager.Instance.TaskPanel.taskText.fontMaterial;

            _bgOverlay = new GameObject("BanDraftBg");
            _bgOverlay.transform.SetParent(HudManager.Instance.transform, false);
            _bgOverlay.transform.localPosition = new Vector3(0f, 0f, 1f);

            var bgSr = _bgOverlay.AddComponent<SpriteRenderer>();
            bgSr.sprite = MakeWhiteSprite();
            bgSr.color = new Color(0f, 0f, 0f, 0.85f);
            bgSr.sortingLayerName = "UI";
            bgSr.sortingOrder = 48;

            var cam = Camera.main;
            float camH = cam != null ? cam.orthographicSize * 2f : 6f;
            float camW = camH * ((float)Screen.width / Screen.height);
            _bgOverlay.transform.localScale = new Vector3(camW, camH, 1f);
            _bgOverlay.SetActive(false);

            _root = new GameObject("BanDraftRoot");
            _root.transform.SetParent(HudManager.Instance.transform, false);
            _root.transform.localPosition = new Vector3(0f, 0.6f, -20f);

            _title = MakeText(_root, "BanDraftTitle", font, fontMat,
                "BAN PHASE", 3.4f, new Color(1f, 0.85f, 0.1f),
                new Vector3(0f, 2.4f, 0f), bold: true);

            _root.SetActive(false);
        }

        private void BuildList(List<byte> order)
        {
            if (HudManager.Instance == null) return;
            if (_root == null) BuildUI();
            if (_root == null) return;

            foreach (var e in _entries)
            {
                if (e.NameText != null) Destroy(e.NameText.gameObject);
                if (e.StatusText != null) Destroy(e.StatusText.gameObject);
                if (e.RoleCard != null) Destroy(e.RoleCard);
            }
            _entries.Clear();
            _entryByPlayer.Clear();

            var font = HudManager.Instance.TaskPanel.taskText.font;
            var fontMat = HudManager.Instance.TaskPanel.taskText.fontMaterial;

            float startY = 1.6f;
            float stepY = 1.15f;

            for (int i = 0; i < order.Count; i++)
            {
                byte pid = order[i];
                float y = startY - i * stepY;

                string name = GetDisplayName(pid, i);
                var nameText = MakeText(_root, $"BanName_{i}", font, fontMat,
                    name, 2.2f, Color.white, new Vector3(0f, y, 0f), bold: true);

                var statusText = MakeText(_root, $"BanStatus_{i}", font, fontMat,
                    "Choosing...", 1.6f, new Color(0.85f, 0.85f, 0.85f),
                    new Vector3(0f, y - 0.45f, 0f), bold: false);

                var entry = new BanEntry
                {
                    PlayerId = pid,
                    NameText = nameText,
                    StatusText = statusText,
                    RoleCard = null,
                    CardAnchor = new Vector3(0f, y - 0.9f, 0f)
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
                if (entry.RoleCard == null)
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

            if (!_showBannedRoles || showHidden) return;

            if (!EnsureRolePrefab() || HudManager.Instance == null) return;

            var role = DraftUiManager.ResolveRole(roleId);
            string roleName = role?.NiceName ?? $"Role {roleId}";
            string teamName = DraftUiManager.GetTeamLabel(role);
            Sprite icon = DraftUiManager.GetRoleIcon(role);
            Color color = DraftUiManager.GetRoleColor(role);

            var cardObj = Instantiate(_cachedRolePrefab, _root.transform);
            cardObj.name = "BanDraftRoleCard";

            if (cardObj.transform.childCount == 0) { Destroy(cardObj); return; }
            var actualCard = cardObj.transform.GetChild(0);
            if (actualCard.childCount < 3) { Destroy(cardObj); return; }

            var roleText = actualCard.GetChild(0).GetComponent<TextMeshPro>();
            var roleImage = actualCard.GetChild(1).GetComponent<SpriteRenderer>();
            var teamText = actualCard.GetChild(2).GetComponent<TextMeshPro>();
            var rollover = actualCard.GetComponent<ButtonRolloverHandler>();

            cardObj.transform.localPosition = entry.CardAnchor;
            cardObj.transform.localScale = Vector3.one * CardScale;
            cardObj.transform.localRotation = Quaternion.Euler(0f, 0f, CardTiltDeg);

            if (roleText != null) roleText.text = roleName;
            if (teamText != null)
            {
                teamText.text = teamName;
                teamText.enableAutoSizing = true;
                teamText.fontSizeMax = 3.2f;
                teamText.color = GetTeamColor(teamName);
            }
            if (roleImage != null)
            {
                roleImage.sprite = icon;
                roleImage.SetSizeLimit(2.0f);
                roleImage.color = Color.white;
            }

            var cardBg = actualCard.GetComponent<SpriteRenderer>();
            if (cardBg != null) cardBg.color = color;
            if (rollover != null)
            {
                rollover.OutColor = color;
                rollover.OverColor = Color.white;
            }
            if (roleText != null) roleText.color = color;

            foreach (var tmp in cardObj.GetComponentsInChildren<TMP_Text>())
            {
                var r = tmp.GetComponent<Renderer>();
                if (r != null) { r.sortingLayerName = "UI"; r.sortingOrder = 1; }
            }
            foreach (var sr in cardObj.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingLayerName = "UI";
                sr.sortingOrder = 1;
            }

            entry.RoleCard = cardObj;
            UpdateHighlight();
        }

        private string GetDisplayName(byte pid, int index)
        {
            if (_anonymousUsers) return $"User {index + 1}";
            var player = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p != null && p.PlayerId == pid);
            return player != null ? player.Data.PlayerName : "Unknown";
        }

        private static bool EnsureRolePrefab()
        {
            if (_cachedRolePrefab != null) return true;
            try
            {
                var bundle = TouAssets.MainBundle;
                if (bundle == null) return false;
                var prefab = bundle.LoadAsset("SelectRoleGame")?.TryCast<GameObject>();
                if (prefab == null) return false;
                var holderGo = prefab.transform.Find("RoleCardHolder");
                if (holderGo == null) return false;
                _cachedRolePrefab = holderGo.gameObject;
                return true;
            }
            catch (System.Exception ex)
            {
                DraftModePlugin.Logger.LogWarning($"[BanDraftOverlay] Prefab load failed: {ex.Message}");
                return false;
            }
        }

        private static TextMeshPro MakeText(
            GameObject parent, string name,
            TMP_FontAsset font, Material fontMat,
            string text, float fontSize, Color color,
            Vector3 offset, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = offset;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontMaterial = fontMat;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.enableWordWrapping = false;
            tmp.text = text;

            var r = go.GetComponent<Renderer>();
            if (r != null) { r.sortingLayerName = "UI"; r.sortingOrder = 50; }
            return tmp;
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

        private static Sprite _white;
        private static Sprite MakeWhiteSprite()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }
    }
}
