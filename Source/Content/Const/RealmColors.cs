using UnityEngine;

namespace Cultiway.Content.Const;

/// <summary>
///     统一管理境界与五行对应的视觉颜色，供渲染系统复用。
/// </summary>
public static class RealmColors
{
    public static readonly Color QiRefining = new(1f, 1f, 1f, 0.15f);
    public static readonly Color Foundation = new(0.53f, 0.81f, 0.92f, 0.25f);
    public static readonly Color Jindan = new(1f, 0.84f, 0f, 0.35f);
    public static readonly Color Yuanying = new(0.58f, 0.44f, 0.86f, 0.45f);
    public static readonly Color Huashen = new(1f, 1f, 1f, 0.55f);

    public static readonly Color IronElement = new(1f, 0.84f, 0f, 1f);
    public static readonly Color WoodElement = new(0.13f, 0.55f, 0.13f, 1f);
    public static readonly Color WaterElement = new(0.25f, 0.41f, 0.88f, 1f);
    public static readonly Color FireElement = new(1f, 0.27f, 0f, 1f);
    public static readonly Color EarthElement = new(0.55f, 0.27f, 0.07f, 1f);
}

