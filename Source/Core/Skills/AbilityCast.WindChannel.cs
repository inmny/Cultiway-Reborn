using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>两侧厚面沿共同入口和出口展开，槽中团流向实际的窄口移动。</summary>
    private void AppendWindChannel(int node, List<FormVisualInstance> output)
    {
        if (!TryWindChannel(node, out var channel)) return;
        float opacity = channel.Opacity;
        if (opacity < .002f) return;
        var generation = Plan.Nodes[node].Resource.Generation;
        var resource = generation.Load("wind_wall");
        var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(channel.Forward.y, channel.Forward.x) * Mathf.Rad2Deg);
        float height = channel.Height * Mathf.Lerp(.28f, 1f, opacity);
        var walls = Matrix4x4.TRS(channel.Centre, rotation, new Vector3(channel.Length, channel.Width, height));
        output.Add(new FormVisualInstance(resource, generation.WindChannelWalls, walls,
            new Color(.62f, .83f, .84f, opacity * .83f), terrain, "wind"));

        for (int crest = 0; crest < 3; crest++)
        {
            float along = Mathf.Repeat(NodeAge(node) * .57f + crest / 3f, 1f);
            float bloom = Mathf.Sin(along * Mathf.PI);
            var centre = channel.Centre + (Vector3)channel.Forward * ((along - .5f) * channel.Length);
            float width = channel.Width * ConstructionWindChannel.WidthAt(along);
            var pose = Matrix4x4.TRS(centre, rotation,
                new Vector3(channel.Length * .25f * bloom, width * .92f, height * (.82f + along * .18f)));
            output.Add(new FormVisualInstance(resource, generation.WindChannelCrest, pose,
                new Color(.79f, .91f, .85f, opacity * bloom * .67f), terrain, "wind"));
        }
    }
}
