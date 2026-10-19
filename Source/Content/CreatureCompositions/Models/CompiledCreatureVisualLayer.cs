using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>器官贡献的候选外观；显示取舍不会影响该器官的实际能力。</summary>
public sealed class CompiledCreatureVisualLayer
{
    private readonly ReadOnlyCollection<string> layerIds;

    /// <summary>来源器官等级，用于相同位置的造型选择。</summary>
    public int Rank { get; }
    /// <summary>来源身体位置。</summary>
    public string SlotId { get; }
    /// <summary>来源器官编号。</summary>
    public string OrganId { get; }
    /// <summary>这个器官贡献的候选图层。</summary>
    public IReadOnlyList<string> LayerIds => layerIds;

    /// <summary>记录完整候选，交给外观服务按实际位置决定如何显示。</summary>
    internal CompiledCreatureVisualLayer(int rank, string slotId, string organId, string[] layerIds)
    {
        Rank = rank;
        SlotId = slotId;
        OrganId = organId;
        this.layerIds = Array.AsReadOnly(layerIds ?? Array.Empty<string>());
    }
}
