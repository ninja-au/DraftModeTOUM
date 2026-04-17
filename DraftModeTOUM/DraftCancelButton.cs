using System.Collections;
using DraftModeTOUM.Managers;
using DraftModeTOUM.Patches;
using Reactor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace DraftModeTOUM;


    public static class DraftCancelButton
    {
        private static GameObject _root;
        private static SpriteRenderer _iconSr;
        private static SpriteRenderer _bgSr;
        private static TextMeshPro _label;
        private static PassiveButton _btn;

        private const float ButtonScale = 0.55f;
        private const float LabelYOffset = -0.52f;
        private const float LabelFontSize = 3.5f;

        public static void Show()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (_root != null) return;

            Build();
            if (_root == null) return;

            _root.SetActive(true);
            Coroutines.Start(CoPopIn(_root.transform));
        }

        private static IEnumerator CoPopIn(Transform t)
        {
            if (t == null) yield break;
            const float dur = 0.20f;
            for (float elapsed = 0f; elapsed < dur; elapsed += Time.deltaTime)
            {
                if (t == null) yield break;
                float s = EaseOutBack(elapsed / dur) * ButtonScale;
                t.localScale = Vector3.one * s;
                yield return null;
            }
            if (t != null)
                t.localScale = Vector3.one * ButtonScale;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static void Hide()
        {
            if (_root == null) return;
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
            _root = null;
            _btn = null;
            _label = null;
            _iconSr = null;
            _bgSr = null;
        }

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

            Vector3 useBtnLocalPos = useBtn.transform.localPosition;

            float buttonDiameter = MeasureButtonDiameter(useBtn.gameObject);
            DraftModePlugin.Logger.LogInfo(
                $"[DraftCancelButton] UseButton localPos={useBtnLocalPos}, measured diameter={buttonDiameter}");

            Vector3 ourLocalPos = new Vector3(
                useBtnLocalPos.x - buttonDiameter,
                useBtnLocalPos.y,
                useBtnLocalPos.z);

            _root = new GameObject("DraftCancelButtonRoot");
            _root.transform.SetParent(useBtn.transform.parent, false);
            _root.transform.localPosition = ourLocalPos;
            _root.transform.localScale = Vector3.zero;
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_root.transform, false);
            iconGo.transform.localPosition = Vector3.zero;
            iconGo.transform.localScale = Vector3.one * 0.75f;

            _iconSr = iconGo.AddComponent<SpriteRenderer>();
            _iconSr.sprite = DraftAssets.QuitSprite.LoadAsset();
            _iconSr.color = Color.white;
            _iconSr.sortingLayerName = "UI";
            _iconSr.sortingOrder = 51;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, LabelYOffset, 0f);

            _label = labelGo.AddComponent<TextMeshPro>();
            CopyAuFont(_label, hud);
            _label.text = "Cancel Draft";

            var lr = labelGo.GetComponent<Renderer>();
            if (lr != null)
            {
                lr.sortingLayerName = "UI";
                lr.sortingOrder = 52;
            }

            var col = _root.AddComponent<CircleCollider2D>();
            col.radius = (buttonDiameter * 0.5f) / ButtonScale;

            _btn = _root.AddComponent<PassiveButton>();
            _btn.Colliders = new Collider2D[] { col };
            _btn.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            _btn.OnMouseOver = new UnityEngine.Events.UnityEvent();
            _btn.OnMouseOut = new UnityEngine.Events.UnityEvent();

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

        private static float MeasureButtonDiameter(GameObject actionButtonGo)
        {
            float best = 0f;
            try
            {
                foreach (var sr in actionButtonGo.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.sprite == null) continue;
                    float w = sr.bounds.size.x;
                    if (w > best) best = w;
                }
            }
            catch
            {
            }

            if (best > 0.3f && best < 5f)
                return best * 1.1f;

            DraftModePlugin.Logger.LogWarning(
                "[DraftCancelButton] Could not measure button; using fallback diameter 1.3.");
            return 1.3f;
        }


        private static void CopyAuFont(TextMeshPro tmp, HudManager hud)
        {
            tmp.fontSize = LabelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.red;
            tmp.characterSpacing = -.2f;
            tmp.enableWordWrapping = false;

            try
            {
                var src = hud.UseButton?.GetComponentInChildren<TextMeshPro>(true);
                if (src != null)
                {
                    tmp.font = src.font;
                    tmp.fontMaterial = src.fontMaterial;
                    DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: UseButton label.");
                    return;
                }
            }
            catch
            {
            }

            try
            {
                var src = hud.KillButton?.GetComponentInChildren<TextMeshPro>(true);
                if (src != null)
                {
                    tmp.font = src.font;
                    tmp.fontMaterial = src.fontMaterial;
                    DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: KillButton label.");
                    return;
                }
            }
            catch
            {
            }

            try
            {
                tmp.font = hud.TaskPanel.taskText.font;
                tmp.fontMaterial = hud.TaskPanel.taskText.fontMaterial;
                DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Font: TaskPanel fallback.");
            }
            catch
            {
            }
        }
    }
