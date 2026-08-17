using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>WindowEmpty 可见边框以内的内容安全区；方向与 RectOffset 一致。</summary>
internal readonly struct UiFrameInsets
{
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public UiFrameInsets(float left, float right, float top, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public RectOffset ToRectOffset()
    {
        return new RectOffset(Mathf.CeilToInt(Left), Mathf.CeilToInt(Right),
            Mathf.CeilToInt(Top), Mathf.CeilToInt(Bottom));
    }
}

/// <summary>原版公共 UI 资源的唯一声明和加载入口。</summary>
internal static class UiResources
{
    public const string Button = "ui/special/button";
    public const string WindowInner = "ui/special/windowInnerSliced";
    public const string WindowEmpty = "ui/special/windowEmptyFrame";
    public const string DestructiveButton = "ui/special/special_buttonRed_insides";
    public const string ToggleBox = "ui/button";
    public const string SettingButtonPrefab = "ui/SettingButton";

    public static Sprite GetSprite(string path)
    {
        return SpriteTextureLoader.getSprite(path);
    }

    public static void ApplySurface(Image image, UiSurface surface, Color? tint = null)
    {
        string path = surface switch
        {
            UiSurface.Button => Button,
            UiSurface.WindowInner => WindowInner,
            UiSurface.DestructiveButton => DestructiveButton,
            UiSurface.ToggleBox => ToggleBox,
            _ => null,
        };
        image.sprite = path == null ? null : GetSprite(path);
        image.type = Image.Type.Sliced;
        image.color = tint ?? UiTheme.Current.Palette.Normal;
    }

    /// <summary>
    /// 应用带可见边框的 WindowEmpty，并返回已经包含九宫格边界和额外间距的内容安全区。
    /// WindowEmpty 不能通过 ApplySurface 使用；子元素必须放入按该安全区内缩的 Content。
    /// </summary>
    public static UiFrameInsets ApplyWindowFrame(Image image, float minimumContentInset = 0f,
        Color? tint = null)
    {
        Sprite sprite = GetSprite(WindowEmpty);
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint ?? UiTheme.Current.Palette.Normal;

        Vector4 border = sprite != null ? sprite.border : Vector4.zero;
        float pixelsPerUnit = Mathf.Max(0.001f, image.pixelsPerUnit);
        float gap = UiTheme.Current.Metrics.WindowFrameContentGap;
        float minimum = Mathf.Max(minimumContentInset, UiTheme.Current.Metrics.WindowFrameMinimumInset);
        float left = Mathf.Ceil(Mathf.Max(minimum, border.x / pixelsPerUnit + gap));
        float bottom = Mathf.Ceil(Mathf.Max(minimum, border.y / pixelsPerUnit + gap));
        float right = Mathf.Ceil(Mathf.Max(minimum, border.z / pixelsPerUnit + gap));
        float top = Mathf.Ceil(Mathf.Max(minimum, border.w / pixelsPerUnit + gap));
        return new UiFrameInsets(left, right, top, bottom);
    }

    public static void SetImage(Image image, string path, bool preserveAspect = true)
    {
        image.sprite = GetSprite(path);
        image.overrideSprite = image.sprite;
        image.preserveAspect = preserveAspect;
    }
}

/// <summary>跨功能复用的动作图标；领域图标继续由所属功能声明。</summary>
internal static class UiIcons
{
    public const string Search = "ui/icons/iconBrowse";
    public const string Favorite = "ui/icons/iconFavoriteStar";
    public const string Equipped = "ui/icons/iconFavoriteWeapon";
    public const string Sort = "ui/icons/iconArrows";
    public const string MoveUp = "ui/icons/iconArrowMetaRight";
    public const string MoveDown = "ui/icons/iconArrowMetaLeft";
    public const string Previous = "ui/icons/iconArrowMetaLeft";
    public const string Next = "ui/icons/iconArrowMetaRight";
    public const string Edit = "ui/icons/iconEditTrait";
    public const string Copy = "ui/icons/actor_traits/iconClone";
    public const string Delete = "ui/icons/iconDemolish";
    public const string Gift = "ui/icons/iconGift";
    public const string Import = "ui/icons/iconArrowDOWN";
    public const string Save = "ui/icons/iconSaveLocal";
    public const string Undo = "ui/icons/iconGoBack";
    public const string Reset = "ui/icons/cube_knobs/icon_knob_reset";
    public const string Play = "ui/icons/iconPlay";
    public const string Add = "ui/icons/iconShovelPlus";
    public const string Remove = "ui/icons/iconShovelMinus";
    public const string Select = "ui/icons/iconArrowAttackTarget";
    public const string TargetFilter = Select;
    public const string Confirm = "ui/icons/iconSaveLocal";
    public const string Cancel = "ui/icons/iconClose";
    public const string Options = "ui/icons/iconOptions";
    public const string Info = "ui/icons/iconMainInfo";
    public const string Color = "ui/icons/iconColorCustomization";
    public const string World = "ui/icons/iconWorldInfo";
}
