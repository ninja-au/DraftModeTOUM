using System;
using System.Reflection;
using DraftModeTOUM.Managers;
using DraftModeTOUM.Patches;
using MiraAPI.Utilities.Assets;
using MiraAPI.Utilities;
using MiraAPI.Hud;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Utilities;
using UnityEngine;

// Kept in the root namespace so DraftModePlugin.cs and DraftCancelButtonPatch.cs
// can reference it without any extra using directive.
namespace DraftModeTOUM;

public sealed class DraftCancelButton : TownOfUsButton
{
    // ── Static handle so patches can call Show() / Hide() ────────────────────
    private static DraftCancelButton? _instance;

    public static void Show()
    {
        if (_instance != null) _instance.Disabled = false;
    }

    public static void Hide()
    {
        if (_instance != null) _instance.Disabled = true;
    }

    public override string Name => "Cancel Draft";
    public override float InitialCooldown => 0f;
    public override float Cooldown => 0f;

    public override bool ZeroIsInfinite { get; set; } = true;
    public override ButtonLocation Location => ButtonLocation.BottomRight;


    public override Color TextOutlineColor => new Color32(198, 22, 22, 255);

    public override LoadableAsset<Sprite> Sprite => new EmbeddedSpriteAsset(GetButtonSprite());

    public override bool Disabled { get; set; } = true;

    public override bool Enabled(RoleBehaviour? role)
    {
        return AmongUsClient.Instance.AmHost && !Disabled;
    }

    public override bool CanUse()
    {
        return base.CanUse() && DraftManager.IsDraftActive && !Disabled;
    }
    public override void CreateButton(Transform parent)
    {
        base.CreateButton(parent);
        _instance = this;

        if (Button?.graphic != null)
        {
        Button.graphic.transform.localScale = Vector3.one * 1.2f;
        }
        var useBtn = HudManager.Instance?.UseButton;
if (useBtn != null && Button != null)
{
    var pos = Button.transform.localPosition;
    var usePos = useBtn.transform.localPosition;
    Button.transform.localPosition = new Vector3(
        pos.x,
        usePos.y,
        pos.z);
}
        }
    

    protected override void OnClick()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!DraftManager.IsDraftActive) return;

        DraftModePlugin.Logger.LogInfo("[DraftCancelButton] Cancel clicked by host.");
        DraftNetworkHelper.BroadcastCancelDraft();
        DraftNetworkHelper.BroadcastCreateNotif("<color=#FF0000>Draft Mode</color> has been cancelled by the <color=#FFBFCC><b>Host</b></color>!");  // ADD THIS
        DraftManager.Reset(cancelledBeforeCompletion: true);
        DraftNetworkHelper.BroadcastDraftEnd();
        Hide();
    }

    // ── Sprite loading ────────────────────────────────────────────────────────

    private static Sprite? _cachedButtonSprite;

    private static Sprite GetButtonSprite()
    {
        if (_cachedButtonSprite != null) return _cachedButtonSprite;

        try
        {
            var asm          = Assembly.GetExecutingAssembly();
            const string res = "DraftModeTOUM.button.png";

            using var stream = asm.GetManifestResourceStream(res);
            if (stream != null)
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                var tex       = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;

                if (ImageConversion.LoadImage(tex, bytes))
                {
                    _cachedButtonSprite           = UnityEngine.Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    _cachedButtonSprite.hideFlags = HideFlags.HideAndDontSave;

                    DraftModePlugin.Logger.LogInfo(
                        $"[DraftCancelButton] Loaded embedded button.png ({tex.width}x{tex.height}).");
                    return _cachedButtonSprite;
                }

                DraftModePlugin.Logger.LogWarning("[DraftCancelButton] ImageConversion.LoadImage failed.");
            }
            else
            {
                string available = string.Join(", ", asm.GetManifestResourceNames());
                DraftModePlugin.Logger.LogWarning(
                    $"[DraftCancelButton] Resource '{res}' not found. Available: [{available}]");
            }
        }
        catch (Exception ex)
        {
            DraftModePlugin.Logger.LogWarning(
                $"[DraftCancelButton] Exception loading sprite: {ex.Message}");
        }

        DraftModePlugin.Logger.LogWarning("[DraftCancelButton] Using fallback X sprite.");
        _cachedButtonSprite = MakeXSprite();
        return _cachedButtonSprite;
    }

    private static Sprite MakeXSprite()
    {
        const int S   = 80;
        const int ARM = 6;
        var tex       = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        var px        = new Color32[S * S];

        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(200, 30, 30, 255);

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
        var spr       = UnityEngine.Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S * 0.9f);
        spr.hideFlags = HideFlags.HideAndDontSave;
        return spr;
    }
}
