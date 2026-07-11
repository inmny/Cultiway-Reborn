using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

[Serializable]
internal sealed class WanfaPavilionData
{
    public List<SkillBlueprint> Blueprints = new();
    public List<string> SelectedBlueprintIds = new();
}
