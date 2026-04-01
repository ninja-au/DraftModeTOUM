using System;
using System.Collections;
using DraftModeTOUM.Managers;
using DraftModeTOUM.Patches;
using Reactor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace DraftModeTOUM
{

    public static class DraftCancelButton
    {
        // ── state ──────────────────────────────────────────────────────────────────
        private static GameObject     _root;
        private static SpriteRenderer _iconSr;
        private static SpriteRenderer _bgSr;
        private static TextMeshPro    _label;
        private static PassiveButton  _btn;

        private static Sprite _quitSprite;
        private static bool   _spriteLogged; // log sprite names once

        private const float ButtonScale   = 0.55f;
        private const float LabelYOffset  = -0.52f;
        private const float LabelFontSize = 3.5f;

        // ── public API ─────────────────────────────────────────────────────────────

        public static void Show()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (_root != null) return;

            Build();
            if (_root == null) return;

            _root.SetActive(true);
            Coroutines.Start(CoPopIn(_root.transform));
        }

        public static void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
            _root   = null;
            _btn    = null;
            _label  = null;
            _iconSr = null;
            _bgSr   = null;
        }

        // ── construction ──────────────────────────────────────────────────────────

        private static void Build()
        {
            var hud = HudManager.Instance;
            if (hud == null) return;

            var useBtn = hud.UseButton;
            if (useBtn == null)
            {
                DraftModePlugin.Logger.LogWarning("[DraftCancelButton] UseButton is null — cannot position button.");
                return;
            }

            // ── measure UseButton to find its local position and diameter ─────────
            // The ActionButton's own localPosition in HUD space IS the anchor point.
            // To get the diameter we look at the background SpriteRenderer on the
            // button's inner child and read its bounds in LOCAL space.
            Vector3 useBtnLocalPos = useBtn.transform.localPosition;

            // Diameter: walk children of UseButton for its background circle SR
            float buttonDiameter = MeasureButtonDiameter(useBtn.gameObject);
            DraftModePlugin.Logger.LogInfo(
                $"[DraftCancelButton] UseButton localPos={useBtnLocalPos}, measured diameter={buttonDiameter}");

            // Place our button one diameter to the LEFT of UseButton in HUD local space
            Vector3 ourLocalPos = new Vector3(
                useBtnLocalPos.x - buttonDiameter,
                useBtnLocalPos.y,
                useBtnLocalPos.z);

            // ── root — sibling of UseButton under HudManager ─────────────────────
            _root = new GameObject("DraftCancelButtonRoot");
            _root.transform.SetParent(useBtn.transform.parent, false); // same parent
            _root.transform.localPosition = ourLocalPos;
            _root.transform.localScale    = Vector3.zero; // CoPopIn animates this
            // ── icon ──────────────────────────────────────────────────────────────
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_root.transform, false);
            iconGo.transform.localPosition = Vector3.zero;
            iconGo.transform.localScale    = Vector3.one * 0.75f;

            _iconSr                  = iconGo.AddComponent<SpriteRenderer>();
            _iconSr.sprite           = GetQuitSprite();
            _iconSr.color            = Color.white;
            _iconSr.sortingLayerName = "UI";
            _iconSr.sortingOrder     = 51;

            // ── label ─────────────────────────────────────────────────────────────
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, LabelYOffset, 0f);

            _label = labelGo.AddComponent<TextMeshPro>();
            CopyAuFont(_label, hud);
            _label.text = "Cancel Draft";

            var lr = labelGo.GetComponent<Renderer>();
            if (lr != null) { lr.sortingLayerName = "UI"; lr.sortingOrder = 52; }

            // ── collider ──────────────────────────────────────────────────────────
            // Radius scaled to match the visual button size (diameter / 2 / ButtonScale)
            var col    = _root.AddComponent<CircleCollider2D>();
            col.radius = (buttonDiameter * 0.5f) / ButtonScale;

            // ── PassiveButton ─────────────────────────────────────────────────────
            _btn             = _root.AddComponent<PassiveButton>();
            _btn.Colliders   = new Collider2D[] { col };
            _btn.OnClick     = new UnityEngine.UI.Button.ButtonClickedEvent();
            _btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            _btn.OnMouseOut  = new UnityEngine.Events.UnityEvent();

            _btn.OnClick.AddListener((UnityAction)OnClicked);

            _btn.OnMouseOver.AddListener((UnityAction)(() =>
            {
                if (_root != null) _root.transform.localScale = Vector3.one * (ButtonScale * 1.15f);
                if (_bgSr != null) _bgSr.color = new Color32(255, 60, 60, 255);
            }));
            _btn.OnMouseOut.AddListener((UnityAction)(() =>
            {
                if (_root != null) _root.transform.localScale = Vector3.one * ButtonScale;
                if (_bgSr != null) _bgSr.color = new Color32(200, 30, 30, 235);
            }));
        }

        // ── click handler ─────────────────────────────────────────────────────────

        private static void OnClicked()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!DraftManager.IsDraftActive) return;

            DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Cancel clicked by host.");
            DraftNetworkHelper.BroadcastCancelDraft();
            DraftManager.Reset(cancelledBeforeCompletion: true);
            DraftManager.RpcSendMessageToAll("System", "Draft has been cancelled by the host.");
            DraftNetworkHelper.BroadcastDraftEnd();
            Hide();
        }

        // ── positioning helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Measures the visual diameter of an AU ActionButton by reading the largest
        /// SpriteRenderer child's bounds in local space.  Falls back to 1.3 units
        /// (empirically correct for AU's default ability button size at scale 0.55).
        /// </summary>
        private static float MeasureButtonDiameter(GameObject actionButtonGo)
        {
            float best = 0f;
            try
            {
                foreach (var sr in actionButtonGo.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.sprite == null) continue;
                    // bounds.size is in WORLD space; we want the size in the parent's
                    // local space so divide by the HUD scale on that axis.
                    float w = sr.bounds.size.x;
                    if (w > best) best = w;
                }
            }
            catch { }

            // If we got something reasonable use it; otherwise fall back.
            // A standard AU action button at scale 0.55 has world-space width ~1.05–1.15.
            // We add a small gap (10 %) so the buttons don't touch.
            if (best > 0.3f && best < 5f)
                return best * 1.1f;

            DraftModePlugin.Logger.LogWarning("[DraftCancelButton] Could not measure button; using fallback diameter 1.3.");
            return 1.3f;
        }

        // ── font helpers ───────────────────────────────────────────────────────────

        private static void CopyAuFont(TextMeshPro tmp, HudManager hud)
        {
            tmp.fontSize           = LabelFontSize;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.color              = Color.red;
            tmp.characterSpacing = -.2f;
            tmp.enableWordWrapping = false;

            // UseButton label (best match — exact AU button typeface + SDF outline)
            try
            {
                var src = hud.UseButton?.GetComponentInChildren<TextMeshPro>(true);
                if (src != null)
                {
                    tmp.font         = src.font;
                    tmp.fontMaterial = src.fontMaterial;
                    DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: UseButton label.");
                    return;
                }
            }
            catch { }

            // KillButton label (same family)
            try
            {
                var src = hud.KillButton?.GetComponentInChildren<TextMeshPro>(true);
                if (src != null)
                {
                    tmp.font         = src.font;
                    tmp.fontMaterial = src.fontMaterial;
                    DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: KillButton label.");
                    return;
                }
            }
            catch { }
            try
            {
                tmp.font         = hud.TaskPanel.taskText.font;
                tmp.fontMaterial = hud.TaskPanel.taskText.fontMaterial;
                DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: TaskPanel fallback.");
            }
            catch { }
        }




        private static Sprite GetQuitSprite()
        {
            if (_quitSprite != null) return _quitSprite;

            Sprite[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<Sprite>(); }
            catch (Exception ex)
            {
                DraftModePlugin.Logger.LogWarning($"[DraftCancelButton] FindObjectsOfTypeAll<Sprite> failed: {ex.Message}");
                return MakeXSprite();
            }

            // Log every unique sprite name once so the correct name is visible in the log
            if (!_spriteLogged)
            {
                _spriteLogged = true;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[DraftCancelButton] All loaded sprite names:");
                foreach (var sp in all)
                    if (sp != null) sb.AppendLine($"  '{sp.name}'");
                DraftModePlugin.Logger.LogInfo(sb.ToString());
            }

            // Pass 1 — exact known names (extend this list based on your log output)
            foreach (var sp in all)
            {
                if (sp == null) continue;
                switch (sp.name)
                {
                    // v2022+ lobby/end-screen names (most common):
                    case "ExitGame":
                    case "QuitButton":
                    case "LeaveGame":
                    case "Quit":
                    // older / variant names:
                    case "btn_exit":
                    case "ic_close":
                    case "Close":
                    case "ic_quit":
                    case "BtnQuit":
                    case "GameQuit":
                        _quitSprite = sp;
                        DraftModePlugin.Logger.LogInfo($"[DraftCancelButton] Quit sprite (exact): '{sp.name}'");
                        return _quitSprite;
                }
            }

            // Pass 2 — substring match
            foreach (var sp in all)
            {
                if (sp == null) continue;
                string nl = sp.name.ToLowerInvariant();
                if ((nl.Contains("quit") || nl.Contains("exit") || nl.Contains("leave"))
                    && !nl.Contains("scene") && !nl.Contains("fullscreen") && !nl.Contains("transition"))
                {
                    _quitSprite = sp;
                    DraftModePlugin.Logger.LogInfo($"[DraftCancelButton] Quit sprite (fuzzy): '{sp.name}'");
                    return _quitSprite;
                }
            }

            DraftModePlugin.Logger.LogWarning(
                "[DraftCancelButton] Quit sprite not found. Check the sprite list above in the log " +
                "and add the correct name to the switch statement in DraftCancelButton.GetQuitSprite().");
            _quitSprite = MakeXSprite();
            return _quitSprite;
        }

        // ── procedural sprite factories ───────────────────────────────────────────

private static Sprite MakeXSprite()
{
    const int S   = 80;
    const int ARM = 6;
    var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
    var px  = new Color32[S * S];

    // 1. Set the initial background to Red instead of transparent black
    // Using (200, 30, 30) for a nice "Among Us" style dark red
    for (int i = 0; i < px.Length; i++) 
        px[i] = new Color32(200, 30, 30, 255); 

    // 2. Draw the white "X" on top
    for (int i = 8; i < S - 8; i++)
    {
        for (int t = -ARM; t <= ARM; t++)
        {
            int x1 = Mathf.Clamp(i + t,           0, S - 1);
            int x2 = Mathf.Clamp((S - 1 - i) + t, 0, S - 1);
            int y  = Mathf.Clamp(i,                0, S - 1);
            px[y * S + x1] = new Color32(255, 255, 255, 255);
            px[y * S + x2] = new Color32(255, 255, 255, 255);
        }
    }

    tex.SetPixels32(px);
    tex.Apply();
    // Adjusted pixelsPerUnit (S * 0.9f) to ensure it fits the button area
    return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S * 0.9f);
}   

        private static Sprite MakeCircleSprite()
        {
            const int S = 80;
            float r = S / 2f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px  = new Color32[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx    = x - r + 0.5f;
                    float dy    = y - r + 0.5f;
                    float alpha = Mathf.Clamp01(r - 1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S / 1.2f);
        }

        // ── pop-in animation ──────────────────────────────────────────────────────

        private static IEnumerator CoPopIn(Transform t)
        {
            const float dur = 0.20f;
            for (float elapsed = 0f; elapsed < dur; elapsed += Time.deltaTime)
            {
                float s = EaseOutBack(elapsed / dur) * ButtonScale;
                t.localScale = Vector3.one * s;
                yield return null;
            }
            t.localScale = Vector3.one * ButtonScale;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
