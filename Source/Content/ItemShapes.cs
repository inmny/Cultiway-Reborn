using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using System.IO;

namespace Cultiway.Content;

public class ItemShapes : ExtendLibrary<ItemShapeAsset, ItemShapes>
{
    public static ItemShapeAsset Ball { get; private set; }
    public static ItemShapeAsset Talisman { get; private set; }
    public static ItemShapeAsset Blood { get; private set; }
    public static ItemShapeAsset Bone { get; private set; }
    public static ItemShapeAsset Claw { get; private set; }
    public static ItemShapeAsset Crystal { get; private set; }
    public static ItemShapeAsset Eye { get; private set; }
    public static ItemShapeAsset Feather { get; private set; }
    public static ItemShapeAsset Fur { get; private set; }
    public static ItemShapeAsset Hoof { get; private set; }
    public static ItemShapeAsset Horn { get; private set; }
    public static ItemShapeAsset Liquid { get; private set; }
    public static ItemShapeAsset Shell { get; private set; }
    public static ItemShapeAsset Silk { get; private set; }
    public static ItemShapeAsset Stone { get; private set; }
    public static ItemShapeAsset Tooth { get; private set; }
    public static ItemShapeAsset Wing { get; private set; }
    public static ItemShapeAsset Others { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ItemShape";
    protected override void OnInit()
    {
        SetFolder(Ball, "ball");
        SetFolder(Talisman, "talisman");
        SetFolder(Blood, "blood");
        SetFolder(Bone, "bone");
        SetFolder(Claw, "claw");
        SetFolder(Crystal, "crystal");
        SetFolder(Eye, "eye");
        SetFolder(Feather, "feather");
        SetFolder(Fur, "fur");
        SetFolder(Hoof, "hoof");
        SetFolder(Horn, "horn");
        SetFolder(Liquid, "liquid");
        SetFolder(Shell, "shell");
        SetFolder(Silk, "silk");
        SetFolder(Stone, "stone");
        SetFolder(Tooth, "tooth");
        SetFolder(Wing, "wing");
        AddDynamicFoldersFromDisk();
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
        }
    }
}
