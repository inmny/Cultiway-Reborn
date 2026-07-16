using System;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;/// <summary>从法器材料语义解析能力效果所需的元素构成。</summary>
public static class ArtifactMaterialEffects
{
    public static ElementComposition ResolveMaterialComposition(ArtifactMaterialData material)
    {
        return new ElementComposition(
            iron: material.GetTrait(ArtifactMaterialTraits.Iron),
            wood: material.GetTrait(ArtifactMaterialTraits.Wood),
            water: material.GetTrait(ArtifactMaterialTraits.Water),
            fire: material.GetTrait(ArtifactMaterialTraits.Fire),
            earth: material.GetTrait(ArtifactMaterialTraits.Earth),
            neg: material.GetTrait(ArtifactMaterialTraits.Neg),
            pos: material.GetTrait(ArtifactMaterialTraits.Pos),
            entropy: material.GetTrait(ArtifactMaterialTraits.Entropy),
            normalize: true);
    }
}
