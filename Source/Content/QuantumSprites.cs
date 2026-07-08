using Cultiway.Abstract;
using UnityEngine;
using Cultiway.Content.Utils;
using Cultiway.Content.Visuals;

namespace Cultiway.Content;
public class QuantumSprites : ExtendLibrary<QuantumSpriteAsset, QuantumSprites>
{
    [CloneSource("draw_walls")]
    public static QuantumSpriteAsset EasternHumanWall { get; private set; }

    [CloneSource("ate_item")]
    public static QuantumSpriteAsset SpecialItemIcon { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
		EasternHumanWall.draw_call = new QuantumSpriteUpdater(RenderEasternHumanWallUtils.drawWalls);
		EasternHumanWall.create_object = delegate(QuantumSpriteAsset _, QuantumSprite pQSprite)
		{
			pQSprite.sprite_renderer.sortingLayerID = SortingLayer.NameToID("Objects");
			pQSprite.setSharedMat(LibraryMaterials.instance.mat_world_object);
		};

        SpecialItemIcon.draw_call = new QuantumSpriteUpdater(SpecialItemIconVfx.Draw);
        SpecialItemIcon.base_scale = 4f;
        SpecialItemIcon.add_camera_zoom_multiplier = false;
        SpecialItemIcon.render_gameplay = true;
    }

    protected override void PostInit(QuantumSpriteAsset asset)
    {
        base.PostInit(asset);
        if (!QuantumSpriteManager._initiated) return;
        
        QuantumSpriteGroupSystem group = new GameObject().AddComponent<QuantumSpriteGroupSystem>();
        group.create(asset);
        asset.group_system = group;
        asset.group_system.turn_off_renderer = asset.turn_off_renderer;
        if (Config.preload_quantum_sprites && asset.default_amount != 0)
        {
            for (int i = 0; i < asset.default_amount; i++)
            {
                group.getNext();
            }
            group.clearFull();
        }
    }
}
