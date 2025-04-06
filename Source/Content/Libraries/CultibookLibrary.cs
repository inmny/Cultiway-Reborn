using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

public class CultibookLibrary : DynamicAssetLibrary<CultibookAsset>
{
    public CultibookAsset NewCultibook(string author)
    {
        var asset = new CultibookAsset()
        {
            id = Guid.NewGuid().ToString()
        };
        asset.Contributors.Add(author);
        asset.CultibookEntity = ModClass.I.W.CreateEntity(new Cultibook()
        {
            ID = asset.id
        }, new ItemLevel());
        add_dynamic(asset);
        return asset;
    }
    public CultibookAsset NewCopy(CultibookAsset asset, string contributor)
    {
        throw new NotImplementedException();
    }
}