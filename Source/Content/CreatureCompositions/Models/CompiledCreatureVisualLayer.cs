using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>同一外观通道最终采用的一组器官图片。</summary>
public sealed class CompiledCreatureVisualLayer
{
    private readonly ReadOnlyCollection<string> layerIds;

    public string Channel { get; }
    public string SlotId { get; }
    public string OrganId { get; }
    public IReadOnlyList<string> LayerIds => layerIds;

    internal CompiledCreatureVisualLayer(string channel, string slotId, string organId, string[] layerIds)
    {
        Channel = channel;
        SlotId = slotId;
        OrganId = organId;
        this.layerIds = Array.AsReadOnly(layerIds ?? Array.Empty<string>());
    }
}
