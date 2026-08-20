namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>当前身体中的一个器官，只记录会改变实际效果的内容。</summary>
public readonly struct CreatureOrganEntry
{
    public readonly string SlotId;
    public readonly string OrganId;
    public readonly int Rank;

    public CreatureOrganEntry(string slotId, string organId, int rank)
    {
        SlotId = slotId;
        OrganId = organId;
        Rank = rank;
    }
}
