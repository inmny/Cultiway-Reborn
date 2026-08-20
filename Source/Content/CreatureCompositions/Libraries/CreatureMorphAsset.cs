namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>绑定一套固定生物模板、移动方式和身体容量变化。</summary>
public sealed class CreatureMorphAsset : Asset
{
    public string BodyPlanId;
    public string ActorAssetId;
    public CreatureLocomotionKind LocomotionKind;
    public string[] LockedSlots = [];
    public CreatureSlotCapacityChange[] AddedSlotCapacity = [];
    public int BaseComplexityModifier;
    public string VisualRigId;
    public string[] Tags = [];
}
