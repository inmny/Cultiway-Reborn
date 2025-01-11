using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct AnimData : IComponent
{
    public int      frame_idx;
    public float    next_frame_time;
    [Ignore]
    public Sprite[] frames;
    [Ignore]
    public Sprite   CurrentFrame => frames[frame_idx];
}