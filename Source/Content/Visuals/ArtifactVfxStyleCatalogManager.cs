using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;

namespace Cultiway.Content.Visuals;

/// <summary>能力配置引用的法器视觉语义样式。</summary>
public sealed class ArtifactVfxStyleCatalogManager : ICanInit, ICanReload
{
    public void Init()
    {
        ArtifactVfxStyleCatalog.Load();
    }

    public void OnReload()
    {
        ArtifactVfxStyleCatalog.Load();
    }
}
