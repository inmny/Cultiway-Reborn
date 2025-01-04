using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct AnimData : IComponent
{
    public int      frame_idx;
    public float    next_frame_time;
    public Sprite[] frames;
    public Sprite   CurrentFrame => frames[frame_idx];
}