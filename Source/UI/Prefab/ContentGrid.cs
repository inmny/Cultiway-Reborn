using Cultiway.Abstract;
using Cultiway.Const;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class ContentGrid : APrefabPreview<ContentGrid>
{
    public Text            Title        { get; private set; }
    public LocalizedText   Localization { get; private set; }
    public GridLayoutGroup Grid         { get; private set; }

    protected override void OnInit()
    {
        Title = transform.Find(nameof(Title)).GetComponent<Text>();
        Localization = transform.Find(nameof(Title)).GetComponent<LocalizedText>();
        Grid = transform.Find(nameof(Grid)).GetComponent<GridLayoutGroup>();
    }

    public override void SetSize(Vector2 pSize)
    {
        Init();
        var width = pSize.x;
        var cell_size = Grid.cellSize.x;
        var spacing = Grid.spacing.x;
        var count = (int)((width + spacing) / (cell_size + spacing));
        Grid.constraintCount = count;
        var offset = (width - count * cell_size - (count - 1) * spacing) / 2;
        Grid.padding.left = (int)offset;
        Grid.padding.right = (int)offset;
        GetComponent<RectTransform>().sizeDelta = pSize;
        Grid.GetComponent<RectTransform>().sizeDelta = pSize;
        Title.GetComponent<RectTransform>().sizeDelta = new Vector2(pSize.x, 30);
    }

    public void Setup(float width, string title_key, Vector2 cell_size = default, Vector2 spacing = default)
    {
        Init();
        Localization.setKeyAndUpdate(title_key);
        if (cell_size != default) Grid.cellSize = cell_size;

        if (spacing != default) Grid.spacing = spacing;

        SetSize(new Vector2(width, 0));
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(ContentGrid), typeof(Image), typeof(ContentSizeFitter),
            typeof(VerticalLayoutGroup));
        var vert_layout = obj.GetComponent<VerticalLayoutGroup>();
        vert_layout.padding = new RectOffset(0, 0, 4, 4);
        vert_layout.childAlignment = TextAnchor.UpperCenter;

        var fitter = obj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bg = obj.GetComponent<Image>();
        bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        bg.type = Image.Type.Sliced;

        var title_obj = new GameObject(nameof(Title), typeof(Text), typeof(LocalizedText), typeof(LayoutElement));
        title_obj.transform.SetParent(obj.transform);
        title_obj.transform.localPosition = Vector3.zero;
        title_obj.transform.localScale = Vector3.one;
        title_obj.GetComponent<LayoutElement>().ignoreLayout = true;

        var title_text = title_obj.GetComponent<Text>();
        title_text.font = LocalizedTextManager.currentFont;
        title_text.fontSize = 12;
        title_text.alignment = TextAnchor.MiddleCenter;
        title_text.color = UIColors.BackgroundTextColor;


        var grid_obj = new GameObject(nameof(Grid), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        grid_obj.transform.SetParent(obj.transform);
        grid_obj.transform.localPosition = Vector3.zero;
        grid_obj.transform.localScale = Vector3.one;

        fitter = grid_obj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;

        var layout = grid_obj.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(18, 18);
        layout.spacing = new Vector2(2,   2);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;


        Prefab = obj.AddComponent<ContentGrid>();
    }
}