using System.Collections.Generic;
using Cultiway.Content;
using UnityEngine;
namespace Cultiway.Content.Utils;

internal static class RenderEasternHumanWallUtils
{
    
    private const float NodeAnimationSpeed = 4f;
    private const float VanillaWallSpriteHeight = 28f;

    private static bool _loaded;
    private static Sprite[] _horizontalSprites;
    private static Sprite[] _verticalSprites;
    private static Sprite[] _nodeWithDownSprites;
    private static Sprite[] _nodeWithoutDownSprites;

    public static void drawWalls(QuantumSpriteAsset pAsset)
    {
        if (pAsset?.group_system == null || TopTileTypes.EasternHumanWall == null)
        {
            return;
        }

        EnsureLoaded();

        DrawWall(pAsset);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _horizontalSprites = LoadSprites("hori_0", "hori_1");
        _verticalSprites = LoadSprites("vert_0", "vert_1");
        _nodeWithDownSprites = LoadSprites("node_0", "node_1");
        _nodeWithoutDownSprites = LoadSprites("node_2", "node_3");
    }

    private static Sprite[] LoadSprites(params string[] names)
    {
        List<Sprite> sprites = new();
        foreach (string name in names)
        {
            Sprite sprite = SpriteTextureLoader.getSprite($"walls/{TopTileTypes.EasternHumanWall.id}/{name}");
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites.ToArray();
    }
    private static void DrawWall(QuantumSpriteAsset asset)
    {
        List<WorldTile> tiles = TopTileTypes.EasternHumanWall.getCurrentTiles();
        if (tiles.Count == 0)
        {
            return;
        }

        float scale = World.world.quality_changer.getTweenBuildingsValue() * 0.25f;
        Material material = LibraryMaterials.instance.mat_world_object;
        for (int i = 0; i < tiles.Count; i++)
        {
            WorldTile tile = tiles[i];
            if (tile?.zone == null || !tile.zone.visible)
            {
                continue;
            }

            WallSpriteKind kind = GetSpriteKind(tile);
            Sprite sprite = SelectSprite(tile, kind);
            if (sprite == null)
            {
                continue;
            }

            QuantumSprite next = asset.group_system.getNext();
            next.setSprite(sprite);
            next.setSharedMat(material);
            next.name = kind.ToString();
            Vector3 position = tile.posV3;
            position.y += GetBuildingLikeYOffset(sprite, scale);
            position.z = 0;
            if (kind.IsNode())
            {
                position.z += 1;
            }
            next.setPosOnly(ref position);
            next.setScale(scale, scale);
        }
    }
    private static float GetBuildingLikeYOffset(Sprite sprite, float scale)
    {
        return Mathf.Max(0f, sprite.rect.height - VanillaWallSpriteHeight) * scale * 0.5f;
    }

    private static Sprite SelectSprite(WorldTile tile, WallSpriteKind kind)
    {
        Sprite[] sprites = SelectSpriteSet(kind);
        if (sprites == null || sprites.Length == 0)
        {
            sprites = FirstNonEmpty(_nodeWithDownSprites, _nodeWithoutDownSprites, _horizontalSprites, _verticalSprites);
        }

        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        if (kind.IsNode() && (sprites == _nodeWithDownSprites || sprites == _nodeWithoutDownSprites))
        {
            return AnimationHelper.getSpriteFromList(tile.random_animation_seed, sprites, NodeAnimationSpeed);
        }

        return sprites[tile.random_animation_seed % sprites.Length];
    }

    private static Sprite[] SelectSpriteSet(WallSpriteKind kind)
    {
        return kind switch
        {
            WallSpriteKind.Horizontal => _horizontalSprites,
            WallSpriteKind.Vertical => _verticalSprites,
            WallSpriteKind.NodeWithDown => _nodeWithDownSprites,
            WallSpriteKind.NodeWithoutDown => _nodeWithoutDownSprites,
            _ => null
        };
    }

    private static WallSpriteKind GetSpriteKind(WorldTile tile)
    {
        bool up = IsEasternHumanWall(tile.tile_up);
        bool down = IsEasternHumanWall(tile.tile_down);
        bool left = IsEasternHumanWall(tile.tile_left);
        bool right = IsEasternHumanWall(tile.tile_right);

        if (left && right && !up && !down)
        {
            return WallSpriteKind.Horizontal;
        }

        if (up && down && !left && !right)
        {
            return WallSpriteKind.Vertical;
        }

        return down ? WallSpriteKind.NodeWithDown : WallSpriteKind.NodeWithoutDown;
    }

    private static Sprite[] FirstNonEmpty(params Sprite[][] spriteSets)
    {
        foreach (Sprite[] spriteSet in spriteSets)
        {
            if (spriteSet != null && spriteSet.Length > 0)
            {
                return spriteSet;
            }
        }

        return null;
    }

    private static bool IsEasternHumanWall(WorldTile tile)
    {
        return tile != null && tile.top_type == TopTileTypes.EasternHumanWall;
    }

    private enum WallSpriteKind
    {
        Horizontal,
        Vertical,
        NodeWithDown,
        NodeWithoutDown
    }

    private static bool IsNode(this WallSpriteKind kind)
    {
        return kind is WallSpriteKind.NodeWithDown or WallSpriteKind.NodeWithoutDown;
    }
}