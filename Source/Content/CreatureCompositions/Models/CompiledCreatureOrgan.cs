using Cultiway.Content.CreatureCompositions.Libraries;

namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>已经解析到静态资源的一个器官。</summary>
public readonly struct CompiledCreatureOrgan
{
    public readonly CreatureOrganEntry Entry;
    public readonly CreatureBodySlotAsset PrimarySlot;
    public readonly CreatureOrganAsset Organ;
    public readonly CreatureOrganRankAsset Rank;

    internal CompiledCreatureOrgan(
        CreatureOrganEntry entry,
        CreatureBodySlotAsset primarySlot,
        CreatureOrganAsset organ,
        CreatureOrganRankAsset rank)
    {
        Entry = entry;
        PrimarySlot = primarySlot;
        Organ = organ;
        Rank = rank;
    }
}
