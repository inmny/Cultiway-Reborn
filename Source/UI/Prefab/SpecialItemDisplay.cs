using Cultiway.Abstract;
using Cultiway.Core.Components;
using DG.Tweening;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class SpecialItemDisplay : APrefabPreview<SpecialItemDisplay>
{
    private SpecialItem    _item;
    public  Button         Button        { get; private set; }
    public  MultiLayerIcon Icon          { get; private set; }
    public  RectTransform  RectTransform { get; private set; }

    protected override void OnInit()
    {
        Icon = GetComponent<MultiLayerIcon>();
        Button = GetComponent<Button>();
        RectTransform = GetComponent<RectTransform>();
        GetComponent<TipButton>().hoverAction = HoverAction;
    }

    [Hotfixable]
    public void Setup(SpecialItem item)
    {
        Init();
        _item = item;
        Icon.Setup(RectTransform.sizeDelta, (item.GetSprite(), new Rect(0, 0, 1, 1)));
    }

    private static void _init()
    {
        GameObject obj = MultiLayerIcon.Instantiate(ModClass.I.PrefabLibrary, pName: nameof(SpecialItemDisplay))
            .gameObject;
        obj.AddComponent<Button>();
        obj.AddComponent<TipButton>();

        Prefab = obj.AddComponent<SpecialItemDisplay>();
        Prefab.GetComponent<TipButton>().hoverAction = Prefab.HoverAction;
    }

    [Hotfixable]
    private void HoverAction()
    {
        Tooltip.show(gameObject, WorldboxGame.Tooltips.SpecialItem.id, new TooltipData
        {
            tip_name = _item.self.Id.ToString()
        });
        transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        transform.DOKill();
        transform.DOScale(1f, 0.1f).SetEase(Ease.InBack);
    }
}