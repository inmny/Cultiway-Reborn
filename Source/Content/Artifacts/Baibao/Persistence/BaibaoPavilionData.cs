using System;
using System.Collections.Generic;

namespace Cultiway.Content.Artifacts.Baibao.Persistence;

[Serializable]
internal sealed class BaibaoPavilionData
{
    public List<ArtifactBlueprint> Blueprints = new();
    public List<string> SelectedBlueprintIds = new();
}
