using Cultiway.Const;
using Cultiway.Content.SpiritVeins;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.SpiritVeins;

/// <summary>灵脉列表左侧的图标标识。</summary>
internal sealed class SpiritVeinBanner : BannerGeneric<SpiritVein, SpiritVeinData>
{
    public override MetaType meta_type => MetaTypeExtend.SpiritVein.Back();

    public override void setupBanner()
    {
        base.setupBanner();
        part_background.gameObject.SetActive(false);
        part_icon.gameObject.SetActive(true);
        part_icon.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconSpiritVein");
        part_icon.color = Color.white;
        part_icon.preserveAspect = true;
    }

    public override void clickAction()
    {
        if (meta_object == null || meta_object.isRekt()) return;

        WorldboxGame.MetaTypes.SpiritVein.set_selected(meta_object);
        SelectedObjects.setNanoObject(meta_object);
        int tileId = meta_object.SourceCenterTileId;
        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null || (uint)tileId >= (uint)tiles.Length) return;
        MapBox.instance.locatePosition(tiles[tileId].posV);
    }

    public override void tooltipAction()
    {
        if (meta_object == null) return;
        Tooltip.show(this, Tooltips.SpiritVein.id, new TooltipData
        {
            tip_name = "vein:" + meta_object.Id,
            tooltip_scale = 0.78f,
            sound_allowed = false
        });
    }
}
