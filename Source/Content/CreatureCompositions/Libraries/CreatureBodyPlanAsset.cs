namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>说明一类身体能够使用哪些位置和固定形态。</summary>
public sealed class CreatureBodyPlanAsset : Asset
{
    public string[] SlotIds = [];
    public string[] AllowedMorphIds = [];
    public int BaseComplexityCapacity;
    public int MaximumOverlayLayers = 3;
    public string VisualRigId;
    public string[] Tags = [];
}
