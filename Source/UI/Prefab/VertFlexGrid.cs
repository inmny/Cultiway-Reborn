using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class VertFlexGrid : APrefabPreview<VertFlexGrid>
{
    public Image           Background { get; private set; }
    public GridLayoutGroup Layout     { get; private set; }

    protected override void OnInit()
    {
        Background = GetComponent<Image>();
        Layout = GetComponent<GridLayoutGroup>();
    }

    public void Setup(float width, Vector2 cell_size, Vector2 spacing)
    {
        Init();
        SetSize(new Vector2(width, 0));
        Layout.spacing = spacing;
        Layout.cellSize = cell_size;

        var padding = new RectOffset();

        var column_count = (int)((width - spacing.x) / (cell_size.x + spacing.x));
        Layout.constraintCount = column_count;

        padding.left = (int)((width - (column_count * cell_size.x + (column_count - 1) * spacing.x)) / 2);
        padding.right = padding.left;
        padding.top = (int)(spacing.y / 2);
        padding.bottom = padding.top;
        Layout.padding = padding;
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(VertFlexGrid), typeof(Image), typeof(ContentSizeFitter),
            typeof(GridLayoutGroup));
        var bg = obj.GetComponent<Image>();
        bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        bg.type = Image.Type.Sliced;

        var fitter = obj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = obj.GetComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;


        Prefab = obj.AddComponent<VertFlexGrid>();
    }
}