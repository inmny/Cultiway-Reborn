using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>该人物已被构造移动，正式保存需要保留实际连续坐标；标记自身不写运行时对象。</summary>
public struct PreciseActorPosition : IComponent
{
}