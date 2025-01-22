using System.Collections.Generic;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public enum TabButtonType
{
    INFO,
    OVERWORLD,
    WORLD,
    RACE,
    CREATURE,
    BUILDING,
    BOSS,
    OTHERS,
    DEBUG
}

public class Manager
{
    public static           PowersTab                            powers_tab;
    private static readonly Dictionary<TabButtonType, Transform> button_groups = new();
    private static          RectTransform                        top_container;

    public void Init()
    {
        top_container = new GameObject("TopContainer", typeof(RectTransform)).GetComponent<RectTransform>();
        top_container.pivot = new Vector2(0, 0.5f);

        powers_tab = TabManager.CreateTab("Cultiway", "Cultiway", "Cultiway Description",
            SpriteTextureLoader.getSprite("cultiway/icons/iconTab"));
        powers_tab.SetLayout(new List<string>
        {
            "Controller", "Container"
        });
        powers_tab.PutElement("Container", top_container, new Vector2(0, -16));

        ConstructTabContainer(TabButtonType.INFO,     SpriteTextureLoader.getSprite("ui/icons/iconAbout"));
        ConstructTabContainer(TabButtonType.WORLD,    SpriteTextureLoader.getSprite("ui/icons/iconWorldInfo"));
        ConstructTabContainer(TabButtonType.RACE,     SpriteTextureLoader.getSprite("ui/icons/iconHumans"));
        ConstructTabContainer(TabButtonType.CREATURE, SpriteTextureLoader.getSprite("ui/icons/iconSheep"));
        ConstructTabContainer(TabButtonType.DEBUG,    SpriteTextureLoader.getSprite("ui/icons/iconDebug"));


        AddButtonsForDebug();

        powers_tab.UpdateLayout();

        SwitchTab(TabButtonType.INFO);
    }

    private void AddButtonsForDebug()
    {
        AddButton(TabButtonType.DEBUG, PowerButtonCreator.CreateSimpleButton("Cultiway.UI.Buttons.LogPerf", () => { ModClass.I.LogPerf(true);}, null));
    }

    public static void AddButton(TabButtonType type, PowerButton button)
    {
        if (!button_groups.TryGetValue(type, out Transform group))
        {
            ConstructTabContainer(type, null);
            group = button_groups[type];
        }

        button.transform.SetParent(group);
        button.transform.localScale = Vector3.one;
    }

    private static void ConstructTabContainer(TabButtonType type, Sprite icon)
    {
        powers_tab.AddPowerButton("Controller",
            PowerButtonCreator.CreateSimpleButton(type.ToString(), () => { SwitchTab(type); },
                icon));
        Transform transform = new GameObject(type.ToString(), typeof(GridLayoutGroup), typeof(ContentSizeFitter))
            .transform;
        transform.SetParent(top_container);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        ((RectTransform)transform).pivot = new Vector2(0, 0.5f);

        var layout = transform.GetComponent<GridLayoutGroup>();
        layout.spacing = new Vector2(4, 4);
        layout.startAxis = GridLayoutGroup.Axis.Vertical;
        layout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        layout.constraintCount = 2;
        layout.cellSize = new Vector2(32, 32);

        var fitter = transform.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        button_groups[type] = transform;
    }

    private static void SwitchTab(TabButtonType type)
    {
        foreach (var pair in button_groups) pair.Value.gameObject.SetActive(pair.Key == type);
    }
}