using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

/// <summary>
/// 动画贴身残影参数。残影由 <see cref="AnimRenderer"/> 的子 SpriteRenderer 绘制，跟随主体 Transform 同步移动。
/// </summary>
public struct AnimAfterimage : IComponent
{
    /// <summary>残影层数。</summary>
    public int Count;

    /// <summary>每层残影间距占当前 Sprite 最大尺寸的比例。</summary>
    public float SpacingRatio;

    /// <summary>每层残影的最小本地间距，避免小图残影挤在主体下方不可见。</summary>
    public float MinSpacing;

    /// <summary>最近一层残影透明度。</summary>
    public float NewestAlpha;

    /// <summary>最远一层残影透明度。</summary>
    public float OldestAlpha;

    /// <summary>本地偏移方向。默认应使用 (-1, 0)，即沿主体朝向的反方向拖尾。</summary>
    public Vector2 LocalDirection;

    /// <summary>残影额外染色，最终会与主体当前颜色相乘。</summary>
    public Color Tint;

    public static AnimAfterimage HorizontalTrajectory()
    {
        return new AnimAfterimage
        {
            Count = 8,
            SpacingRatio = 0.1f,
            MinSpacing = 0.6f,
            NewestAlpha = 0.32f,
            OldestAlpha = 0.05f,
            LocalDirection = Vector2.left,
            Tint = new Color(1f, 1f, 1f, 1f)
        };
    }
}
