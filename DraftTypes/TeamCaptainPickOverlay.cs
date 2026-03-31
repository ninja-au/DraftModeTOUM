using System.Collections.Generic;
using System.Linq;
using Reactor.Utilities.Attributes;
using DraftModeTOUM.Patches;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DraftModeTOUM.DraftTypes
{
    [RegisterInIl2Cpp]
    public sealed class TeamCaptainPickOverlay : MonoBehaviour
    {
        private static TeamCaptainPickOverlay _instance;
        private Canvas _canvas;
        private RectTransform _root;
        private Text _title;
        private Text _turnText;
        private RectTransform _teamsRoot;
        private RectTransform _availableRoot;

        private readonly List<GameObject> _teamColumns = new();
        private readonly List<GameObject> _availableButtons = new();
        private List<byte> _captains = new();
        private byte _currentCaptainId = 255;
        private List<byte> _lastAvailable = new();
        private bool _captainSelectMode = false;
        private int _captainsNeeded = 0;

        public TeamCaptainPickOverlay(System.IntPtr ptr) : base(ptr) { }

        public static void Show(List<byte> captains, Dictionary<byte, List<byte>> teams, List<byte> available)
        {
            EnsureExists();
            _instance._captains = captains.ToList();
            _instance._captainSelectMode = false;
            _instance.BuildUI();
            UpdateTeams(teams, available);
            _instance.SetVisible(true);
        }

        public static void ShowCaptainSelect(List<byte> selected, List<byte> available, int captainsNeeded)
        {
            EnsureExists();
            _instance._captainSelectMode = true;
            _instance._captainsNeeded = captainsNeeded;
            _instance._captains = selected.ToList();
            _instance.BuildUI();
            _instance.RenderTeams(new Dictionary<byte, List<byte>>());
            _instance._lastAvailable = available != null ? available.ToList() : new List<byte>();
            _instance.UpdateTurnLabel();
            _instance.RenderAvailable(_instance._lastAvailable);
            _instance.SetVisible(true);
        }

        public static void UpdateCaptainSelect(List<byte> selected, List<byte> available, int captainsNeeded)
        {
            if (_instance == null) return;
            _instance._captainSelectMode = true;
            _instance._captainsNeeded = captainsNeeded;
            _instance._captains = selected.ToList();
            _instance._lastAvailable = available != null ? available.ToList() : new List<byte>();
            _instance.UpdateTurnLabel();
            _instance.RenderAvailable(_instance._lastAvailable);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
        }

        public static void UpdateTeams(Dictionary<byte, List<byte>> teams, List<byte> available)
        {
            if (_instance == null) return;
            if (_instance._captainSelectMode) return;
            _instance.RenderTeams(teams);
            _instance._lastAvailable = available != null ? available.ToList() : new List<byte>();
            _instance.RenderAvailable(_instance._lastAvailable);
        }

        public static void SetCurrentCaptain(byte captainId)
        {
            if (_instance == null) return;
            _instance._currentCaptainId = captainId;
            _instance.UpdateTurnLabel();
            _instance.RenderAvailable(_instance._lastAvailable);
        }

        private static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("TeamCaptainPickOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TeamCaptainPickOverlay>();
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        private void BuildUI()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("TeamCaptainCanvas");
            DontDestroyOnLoad(canvasGo);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 210;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var rootGo = new GameObject("Root");
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.AddComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(1600f, 900f);
            _root.anchoredPosition = Vector2.zero;

            _title = MakeText(_root, "Title", "TEAM CAPTAIN BATTLE ROYALE", 44, new Color(1f, 0.85f, 0.1f), new Vector2(0f, 390f), true);
            _turnText = MakeText(_root, "TurnText", "", 28, Color.white, new Vector2(0f, 340f), true);

            var teamsGo = new GameObject("Teams");
            teamsGo.transform.SetParent(_root, false);
            _teamsRoot = teamsGo.AddComponent<RectTransform>();
            _teamsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _teamsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _teamsRoot.pivot = new Vector2(0.5f, 0.5f);
            _teamsRoot.sizeDelta = new Vector2(1500f, 500f);
            _teamsRoot.anchoredPosition = new Vector2(0f, 80f);

            var availGo = new GameObject("Available");
            availGo.transform.SetParent(_root, false);
            _availableRoot = availGo.AddComponent<RectTransform>();
            _availableRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _availableRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _availableRoot.pivot = new Vector2(0.5f, 0.5f);
            _availableRoot.sizeDelta = new Vector2(1500f, 220f);
            _availableRoot.anchoredPosition = new Vector2(0f, -300f);

            MakeText(_availableRoot, "AvailableLabel", "AVAILABLE PLAYERS", 28, Color.white, new Vector2(0f, 90f), true);
        }

        private void RenderTeams(Dictionary<byte, List<byte>> teams)
        {
            foreach (var col in _teamColumns) Destroy(col);
            _teamColumns.Clear();

            int teamCount = _captains.Count;
            if (teamCount <= 0) return;
            float spacing = 1500f / teamCount;
            float startX = -((teamCount - 1) * spacing) / 2f;

            for (int i = 0; i < teamCount; i++)
            {
                var colGo = new GameObject($"TeamCol_{i}");
                colGo.transform.SetParent(_teamsRoot, false);
                var rt = colGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(260f, 480f);
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);

                var title = MakeText(rt, "TeamTitle", $"TEAM {i + 1}", 26, TeamCaptainDraftType.GetTeamColor((byte)i), new Vector2(0f, 210f), true);
                var members = teams.TryGetValue((byte)i, out var list) ? list : new List<byte>();
                float y = 150f;
                foreach (var pid in members)
                {
                    string name = GetPlayerName(pid);
                    MakeText(rt, $"Member_{pid}", name, 22, Color.white, new Vector2(0f, y), false);
                    y -= 34f;
                }

                _teamColumns.Add(colGo);
            }
        }

        private void RenderAvailable(List<byte> available)
        {
            if (available == null) return;
            foreach (var b in _availableButtons) Destroy(b);
            _availableButtons.Clear();

            bool isCaptain = _captains.Contains(PlayerControl.LocalPlayer.PlayerId);
            bool isCurrentCaptain = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == _currentCaptainId;
            bool isHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

            int count = available.Count;
            if (count == 0) return;
            float spacing = 260f;
            int cols = 5;
            float startX = -((cols - 1) * spacing) / 2f;
            float startY = 20f;

            for (int i = 0; i < count; i++)
            {
                byte pid = available[i];
                int row = i / cols;
                int col = i % cols;
                var pos = new Vector2(startX + col * spacing, startY - row * 70f);
                var btnGo = MakeButton(_availableRoot, $"Avail_{pid}", GetPlayerName(pid), 22, pos);
                if (_captainSelectMode)
                {
                    var button = btnGo.GetComponent<Button>();
                    if (isHost)
                        button.onClick.AddListener((UnityAction)(() => TeamCaptainDraftType.HandleCaptainSelectHost(pid)));
                    else
                        button.interactable = false;
                }
                else if (isCaptain && isCurrentCaptain)
                {
                    var button = btnGo.GetComponent<Button>();
                    button.onClick.AddListener((UnityAction)(() => DraftNetworkHelper.SendTeamPickToHost(pid)));
                }
                else
                {
                    btnGo.GetComponent<Button>().interactable = false;
                }
                _availableButtons.Add(btnGo);
            }
        }

        private void UpdateTurnLabel()
        {
            if (_turnText == null) return;
            if (_captainSelectMode)
            {
                _turnText.text = $"HOST: PICK CAPTAINS ({_captains.Count}/{_captainsNeeded})";
                return;
            }

            if (_currentCaptainId == 255)
            {
                _turnText.text = "";
                return;
            }

            string capName = GetPlayerName(_currentCaptainId);
            bool isMe = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == _currentCaptainId;
            _turnText.text = isMe ? "YOUR TURN TO PICK" : $"{capName} IS PICKING";
        }

        private static string GetPlayerName(byte pid)
        {
            var p = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x != null && x.PlayerId == pid);
            return p != null ? p.Data.PlayerName : $"Player {pid}";
        }

        private static Text MakeText(Transform parent, string name, string text, int fontSize, Color color, Vector2 anchoredPos, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 40f);
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

        private static GameObject MakeButton(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 50f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
            colors.pressedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            button.colors = colors;

            var label = MakeText(rt, "Label", text, fontSize, Color.white, Vector2.zero, false);
            label.rectTransform.sizeDelta = new Vector2(200f, 40f);
            return go;
        }
    }
}
