using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactAppearanceCatalogManager : ICanInit, ICanReload
{
    public void Init()
    {
        ArtifactAppearanceCatalogLoader.Load();
    }

    public void OnReload()
    {
        ArtifactAppearanceCatalogLoader.Load();
    }
}
