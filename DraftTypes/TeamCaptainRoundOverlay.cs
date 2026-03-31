using System.Collections.Generic;
using System.Linq;
using Reactor.Utilities.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace DraftModeTOUM.DraftTypes 
{
    [RegisterInIl2Cpp]
    public sealed class TeamCaptainRoundOverlay : MonoBehaviour
    {
        private static TeamCaptainRoundOverlay _instance;
        private Canvas _canvas;
        private RectTransform _root;
        private Text _roundText;
        private Text _timerText;
        private Text _scoresText;
        private Text _statusText;

        private readonly List<byte> _teams = new();

        public TeamCaptainRoundOverlay(System.IntPtr ptr) : base(ptr) { }

        public static void Show()
        {
            EnsureExists();
            _instance.SetVisible(true);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
        }

        public static void SetRoundInfo(int round, int totalRounds)
        {
            if (_instance == null) return;
            _instance._roundText.text = $"ROUND {round}/{totalRounds}";
        }

        public static void SetTimerLabel(string text)
        {
            if (_instance == null) return;
            _instance._timerText.text = text;
        }

        public static void SetStatus(string text)
        {
            if (_instance == null) return;
            _instance._statusText.text = text;
        }

        public static void SetScores(Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            if (_instance == null) return;
            _instance.RenderScores(totalScores, roundScores);
        }

        private static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("TeamCaptainRoundOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TeamCaptainRoundOverlay>();
            _instance.BuildUI();
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("TeamCaptainRoundCanvas");
            DontDestroyOnLoad(canvasGo);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 205;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var rootGo = new GameObject("Root");
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.AddComponent<RectTransform>();
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(520f, 260f);
            _root.anchoredPosition = new Vector2(20f, -20f);

            _roundText = MakeText(_root, "RoundText", "ROUND 1/1", 26, Color.white, new Vector2(0f, -10f), true, TextAnchor.UpperLeft);
            _timerText = MakeText(_root, "TimerText", "TIME: 00:00", 22, new Color(0.9f, 0.9f, 0.9f), new Vector2(0f, -45f), false, TextAnchor.UpperLeft);
            _statusText = MakeText(_root, "StatusText", "", 20, new Color(1f, 0.85f, 0.2f), new Vector2(0f, -75f), false, TextAnchor.UpperLeft);
            _scoresText = MakeText(_root, "ScoresText", "", 20, Color.white, new Vector2(0f, -110f), false, TextAnchor.UpperLeft);
        }

        private void RenderScores(Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            var ordered = totalScores.Keys.OrderBy(k => k).ToList();
            _teams.Clear();
            _teams.AddRange(ordered);

            var lines = new List<string>();
            foreach (var teamId in _teams)
            {
                totalScores.TryGetValue(teamId, out var total);
                roundScores.TryGetValue(teamId, out var round);
                string line = $"Team {teamId + 1}: {total} (Round {round})";
                lines.Add(line);
            }
            _scoresText.text = string.Join("\n", lines);
        }

        private static Text MakeText(Transform parent, string name, string text, int fontSize, Color color, Vector2 anchoredPos, bool bold, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(520f, 30f);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
