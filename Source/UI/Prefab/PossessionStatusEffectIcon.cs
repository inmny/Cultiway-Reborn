using System.Globalization;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class PossessionStatusEffectIcon : APrefabPreview<PossessionStatusEffectIcon>
{
    private Image _icon;
    private TipButton _tip;
    public RectTransform RectTransform { get; private set; }

    protected override void OnInit()
    {
        RectTransform = GetComponent<RectTransform>();
        _icon = transform.FindRecursive("icon").GetComponent<Image>();
        _tip = GetComponent<TipButton>();
    }

    public void Setup(Status status)
    {
        Init();
        gameObject.SetActive(true);

        _icon.color = Color.white;
        _icon.sprite = status?.asset?.getSprite() ?? LoadFallbackSprite();
        _tip.type = "status_updatable";
        _tip.setHoverAction(() =>
        {
            if (status == null || status.is_finished || status.asset == null) return;

            Tooltip.show(gameObject, "status_updatable", new TooltipData
            {
                tip_name = status.asset.getLocaleID(),
                tip_description = status.asset.getDescriptionID(),
                status = status
            });
        });
    }

    public void Setup(Entity statusEntity)
    {
        Init();
        gameObject.SetActive(true);

        if (statusEntity.IsNull || !statusEntity.TryGetComponent(out StatusComponent statusComponent))
        {
            gameObject.SetActive(false);
            return;
        }

        StatusEffectAsset asset = statusComponent.Type;
        if (asset == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _icon.color = Color.white;
        _icon.sprite = asset.GetSpriteIcon();
        _tip.type = WorldboxGame.Tooltips.RawTip.id;
        _tip.setHoverAction(() => Tooltip.show(gameObject, WorldboxGame.Tooltips.RawTip.id, new TooltipData
        {
            tip_name = asset.GetName(),
            tip_description = asset.GetDescription(),
            tip_description_2 = GetTimeText(statusEntity)
        }));
    }

    public void Clear()
    {
        Init();
        gameObject.SetActive(false);
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(PossessionStatusEffectIcon), typeof(RectTransform),
            typeof(Button), typeof(TipButton), typeof(CanvasGroup));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(28f, 28f);
        obj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        GameObject icon = obj.NewChild("icon", typeof(Image), typeof(Shadow));
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(28f, 28f);

        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = LoadFallbackSprite();
        iconImage.preserveAspect = true;

        obj.GetComponent<Button>().targetGraphic = iconImage;
        TipButton tip = obj.GetComponent<TipButton>();
        tip.type = WorldboxGame.Tooltips.RawTip.id;

        Prefab = obj.AddComponent<PossessionStatusEffectIcon>();
    }

    private static string GetTimeText(Entity statusEntity)
    {
        if (statusEntity.IsNull || !statusEntity.HasComponent<AliveTimer>()) return string.Empty;

        float elapsed = Mathf.Max(0f, statusEntity.GetComponent<AliveTimer>().value);
        if (!statusEntity.HasComponent<AliveTimeLimit>())
        {
            return $"已持续 {FormatTime(elapsed)}";
        }

        float limit = Mathf.Max(0f, statusEntity.GetComponent<AliveTimeLimit>().value);
        float remain = Mathf.Max(0f, limit - elapsed);
        return $"剩余 {FormatTime(remain)} / {FormatTime(limit)}";
    }

    private static string FormatTime(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }

    private static Sprite LoadFallbackSprite()
    {
        return SpriteTextureLoader.getSprite(StatusEffectAsset.DefaultIconPath)
               ?? SpriteTextureLoader.getSprite("ui/icons/iconMana")
               ?? SpriteTextureLoader.getSprite("ui/icons/iconDamage");
    }
}
