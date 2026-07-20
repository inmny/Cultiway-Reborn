using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using System.IO;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public partial class ItemShapes : ExtendLibrary<ItemShapeAsset, ItemShapes>
{
    public static ItemShapeAsset ElementRoot { get; private set; }
    public static ItemShapeAsset Ball { get; private set; }
    public static ItemShapeAsset Talisman { get; private set; }
    public static ItemShapeAsset MagicScroll { get; private set; }
    public static ItemShapeAsset Bamboo { get; private set; }
    public static ItemShapeAsset Blood { get; private set; }
    public static ItemShapeAsset Bone { get; private set; }
    public static ItemShapeAsset Claw { get; private set; }
    public static ItemShapeAsset Crystal { get; private set; }
    public static ItemShapeAsset Eye { get; private set; }
    public static ItemShapeAsset Feather { get; private set; }
    public static ItemShapeAsset Flower { get; private set; }
    public static ItemShapeAsset Fruit { get; private set; }
    public static ItemShapeAsset Fur { get; private set; }
    public static ItemShapeAsset Herb { get; private set; }
    public static ItemShapeAsset Hoof { get; private set; }
    public static ItemShapeAsset Horn { get; private set; }
    public static ItemShapeAsset Liquid { get; private set; }
    public static ItemShapeAsset Lotus { get; private set; }
    public static ItemShapeAsset Mushroom { get; private set; }
    public static ItemShapeAsset Root { get; private set; }
    public static ItemShapeAsset Shell { get; private set; }
    public static ItemShapeAsset Silk { get; private set; }
    public static ItemShapeAsset Stone { get; private set; }
    public static ItemShapeAsset Tooth { get; private set; }
    public static ItemShapeAsset Vine { get; private set; }
    public static ItemShapeAsset Wing { get; private set; }
    public static ItemShapeAsset Wood { get; private set; }
    public static ItemShapeAsset Others { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ItemShape";

    public static ItemShapeAsset PickDropShape(Actor actor, ActorExtend actor_extend, int seed)
    {
        if (actor_extend != null &&
            actor_extend.TryGetComponent(out Jindan jindan) &&
            jindan.strength > 0.8f &&
            seed % 5 == 0)
        {
            return Ball;
        }

        var candidates = actor?.asset?
            .GetExtend<ActorAssetExtend>()
            .drop_item_shapes
            .GetShapes(actor.asset, ModClass.L.ItemShapeLibrary);
        if (candidates == null || candidates.Count == 0) return Blood ?? Ball;

        return candidates[Math.Abs(seed) % candidates.Count];
    }

    public static string PickIngredientNameCandidate(string shape_id, int seed)
    {
        var shape = string.IsNullOrEmpty(shape_id) ? null : ModClass.L.ItemShapeLibrary.get(shape_id);
        var candidate = shape?.PickIngredientNameCandidate(seed);
        return !string.IsNullOrEmpty(candidate) ? candidate : "材";
    }

    protected override void OnInit()
    {
        SetFolder(Ball, "ball");
        SetFolder(Talisman, "talisman");
        SetFolder(MagicScroll, "magic_scroll");
        SetFolder(Bamboo, "bamboo");
        SetFolder(Blood, "blood");
        SetFolder(Bone, "bone");
        SetFolder(Claw, "claw");
        SetFolder(Crystal, "crystal");
        SetFolder(Eye, "eye");
        SetFolder(Feather, "feather");
        SetFolder(Flower, "flower");
        SetFolder(Fruit, "fruit");
        SetFolder(Fur, "fur");
        SetFolder(Herb, "herb");
        SetFolder(Hoof, "hoof");
        SetFolder(Horn, "horn");
        SetFolder(Liquid, "liquid");
        SetFolder(Lotus, "lotus");
        SetFolder(Mushroom, "mushroom");
        SetFolder(Root, "root");
        SetFolder(Shell, "shell");
        SetFolder(Silk, "silk");
        SetFolder(Stone, "stone");
        SetFolder(Tooth, "tooth");
        SetFolder(Vine, "vine");
        SetFolder(Wing, "wing");
        SetFolder(Wood, "wood");
        SetupArtifactShapes();
        ElementRoot.GetIcon = (e) => 
        {
            if (!e.HasComponent<ElementRoot>()) return null;
            var element_root = e.GetComponent<ElementRoot>();
            return element_root.Type.GetSprite();
        };
        AddDynamicFoldersFromDisk();
        ConfigureIngredientDropRules();
        ConfigureIngredientSemantics();
    }

    protected override void PostInit(ItemShapeAsset asset)
    {
        asset.LoadTextures();
    }

    private static void SetFolder(ItemShapeAsset asset, string folder)
    {
        if (asset == null) return;
        asset.major_texture_folder = $"cultiway/icons/item_shapes/{folder}";
    }

    private void AddDynamicFoldersFromDisk()
    {
        var path = Path.Combine(ModClass.Instance.GetDeclaration().FolderPath, "GameResources", "cultiway", "icons", "item_shapes");
        if (!Directory.Exists(path)) return;

        foreach (var dir in Directory.GetDirectories(path))
        {
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(folderName))
            {
                continue;
            }
            if (Directory.GetFiles(dir).Length == 0) continue;
            var id = $"{Prefix()}.{folderName.FirstToUpper()}";
            if (cached_library.has(id)) continue;

            SetFolder(Add(new ItemShapeAsset { id = id }), folderName);
            ModClass.LogWarning($"Added dynamic folder: {id}");
        }
    }

    private void ConfigureIngredientDropRules()
    {
        void Set(ItemShapeAsset shape, string[] name_candidates, Func<ActorAsset, bool> check_drop_feature)
        {
            if (shape == null) return;
            shape.ingredient_name_candidates = name_candidates ?? [];
            shape.CheckDropFeature = check_drop_feature;
        }

        Set(Ball, ["珠", "核", "丸"], null);

        Set(Blood, ["血", "血珠", "血晶"], asset =>
        {
            return asset != null &&
                   asset != Actors.Plant &&
                   !ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct") &&
                   (asset.is_humanoid || asset.civ || asset.default_animal || asset.source_meat || asset.source_meat_insect ||
                    ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon"));
        });

        Set(Bone, ["骨", "灵骨", "骨片"], asset =>
        {
            return asset != null &&
                   asset != Actors.Plant &&
                   !asset.source_meat_insect &&
                   !ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct") &&
                   (asset.is_humanoid || asset.civ || asset.default_animal || asset.source_meat ||
                    ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon"));
        });

        Set(Claw, ["爪"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   asset != Actors.Plant &&
                   (asset.default_animal || asset.source_meat || ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon")) &&
                   ContainsAssetText(asset, "tiger", "lion", "wolf", "hound", "fox", "bear", "panda", "dragon", "wyvern", "raptor");
        });

        Set(Crystal, ["晶", "灵晶", "晶砂"], asset =>
        {
            return ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct", "crystal", "spirit", "elemental", "wisp");
        });

        Set(Eye, ["瞳", "眼", "目"], asset =>
        {
            return asset != null &&
                   asset != Actors.Plant &&
                   !ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct") &&
                   (asset.is_humanoid || asset.civ || asset.default_animal || asset.source_meat || asset.source_meat_insect ||
                    ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon"));
        });

        Set(Feather, ["羽"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   ContainsAssetText(asset, "bird", "rooster", "eagle", "mallard", "fenghuang", "jinwu", "zhuque");
        });

        Set(Fur, ["毛", "绒", "毫"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   asset != Actors.Plant &&
                   !asset.flying &&
                   !asset.force_ocean_creature &&
                   (asset.default_animal || asset.source_meat) &&
                   ContainsAssetText(asset, "tiger", "lion", "wolf", "hound", "fox", "bear", "panda", "pig", "deer", "horse");
        });

        Set(Hoof, ["蹄"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   ContainsAssetText(asset, "deer", "horse", "boar", "qilin", "centaur", "minotaur", "bull");
        });

        Set(Horn, ["角"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   ContainsAssetText(asset, "deer", "boar", "triceratops", "qilin", "dragon", "wyvern", "daemon", "demon", "minotaur", "bull");
        });

        Set(Liquid, ["液", "露", "浆"], asset =>
        {
            return asset != null &&
                   asset != Actors.Plant &&
                   !ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct") &&
                   (asset.is_humanoid || asset.civ || asset.default_animal || asset.source_meat || asset.source_meat_insect ||
                    ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon"));
        });

        Set(Shell, ["甲", "壳", "鳞甲"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   (asset.flag_turtle || asset.source_meat_insect || ContainsAssetText(asset, "turtle", "tortoise", "xuanwu", "crab"));
        });

        Set(Silk, ["丝"], asset =>
            asset?.source_meat_insect == true || ContainsAssetText(asset, "spider", "silk", "cocoon", "butterfly"));

        Set(Stone, ["石", "灵石", "砂"], asset =>
        {
            return ContainsAssetText(asset, "robot", "servo", "skullcannon", "skull_cannon", "construct", "stone", "golem", "giant", "titan", "sphinx");
        });

        Set(Tooth, ["牙"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   asset != Actors.Plant &&
                   !asset.force_ocean_creature &&
                   (asset.default_animal || asset.source_meat || ContainsAssetText(asset, "dragon", "wyvern", "qilin", "daemon", "demon"));
        });

        Set(Wing, ["翼"], asset =>
        {
            return asset != null &&
                   !asset.is_humanoid &&
                   !asset.civ &&
                   !asset.source_meat_insect &&
                   (asset.flying || ContainsAssetText(asset, "dragon", "wyvern", "bird", "rooster", "eagle", "mallard", "fenghuang", "jinwu", "zhuque", "griffin"));
        });

        Set(Bamboo, ["竹"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Flower, ["花"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Fruit, ["果"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Herb, ["草"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Lotus, ["莲"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Mushroom, ["芝", "菇"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Root, ["根", "参"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Vine, ["藤"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
        Set(Wood, ["木"], asset => asset == Actors.Plant || ContainsAssetText(asset, "plant", "treant", "tree"));
    }

    private static bool ContainsAssetText(ActorAsset asset, params string[] values)
    {
        if (asset == null) return false;
        var text = $"{asset.id}|{asset.name_locale}|{asset.texture_id}".ToLowerInvariant();
        return text.ContainsAny(values);
    }
}
