using Cultiway.Core.Semantics;

namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>定义一种器官的身体要求、冲突关系和可用等级。</summary>
public sealed class CreatureOrganAsset : Asset
{
    public CreatureOrganCategoryMask Category;
    public string[] AllowedBodyPlanTags = [];
    public string[] AllowedMorphTags = [];
    public CreatureSlotRequirement[] SlotRequirements = [];
    public string[] PrerequisiteOrganIds = [];
    public string[] ConflictOrganIds = [];
    public string[] RankIds = [];
    public SemanticDescriptor Semantics = new();
    public string[] EffectFamilyIds = [];
}
