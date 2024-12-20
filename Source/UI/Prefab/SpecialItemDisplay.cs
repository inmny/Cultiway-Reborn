using Cultiway.Abstract;
using Cultiway.Core.Components;
using DG.Tweening;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class SpecialItemDisplay : APrefabPreview<SpecialItemDisplay>
{
    private SpecialItem _item;
    public  Button      Button { get; private set; }
    public  Image       Icon   { get; private set; }

    protected override void OnInit()
    {
        Icon = GetComponent<Image>();
        Button = GetComponent<Button>();
        GetComponent<TipButton>().hoverAction = HoverAction;
    }

    [Hotfixable]
    public void Setup(SpecialItem item)
    {
        Init();
        _item = item;
        Icon.sprite = item.GetSprite();
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(SpecialItemDisplay), typeof(Image), typeof(Button),
            typeof(TipButton));

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