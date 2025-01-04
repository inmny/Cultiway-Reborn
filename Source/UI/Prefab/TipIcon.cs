using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;
[RequireComponent(typeof(Image), typeof(Button), typeof(TipButton))]
public class TipIcon : APrefabPreview<TipIcon>
{
    public Image Icon { get; private set; }
    public TipButton Tip { get; private set; }
    protected override void OnInit()
    {
        Icon = gameObject.GetComponent<Image>();
        Tip = gameObject.GetComponent<TipButton>();
        Tip.type = WorldboxGame.Tooltips.RawTip.id;
    }

    public void Setup(Sprite icon, TooltipData tip_data, Vector2 size = default)
    {
        Init();
        Icon.sprite = icon;
        Tip.textOnClick = tip_data.tip_name;
        Tip.textOnClickDescription = tip_data.tip_description;
        Tip.text_description_2 = tip_data.tip_description_2;
        if (size != default)
        {
            SetSize(size);
        }
    }
    private static void _init()
    {
        var obj = ModClass.NewPrefabPreview(nameof(TipIcon), typeof(Image), typeof(Button), typeof(TipButton));

        Prefab = obj.AddComponent<TipIcon>();
    }
}