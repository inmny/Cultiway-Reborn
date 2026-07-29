using System;
using System.Collections.Generic;
using Cultiway.Core.Persistence;
using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>
/// 无来源伤害（缺少有效攻击者）的境界等级配置。
/// 当 <see cref="Cultiway.Core.ActorExtend.GetHit"/> 的攻击者缺失时，原本按 0 级结算，
/// 此处按 <see cref="AttackType"/> 提供可配置的替代等级，使境界压制、抗性与无效命中判定仍有意义。
/// 等级通过 <see cref="ModSaveManager"/> 持久化到全局存档，参考万法阁/百宝阁的 SaveDocument 模式。
/// </summary>
public static class SourcelessDamageLevels
{
    /// <summary>等级上限，同时约束配置窗口中的滑杆与输入框。</summary>
    public const float MaxLevel = 100f;

    /// <summary>连续写入的最小间隔，避免拖动滑杆时每一步都落盘。</summary>
    private const float SaveInterval = 2f;

    private static readonly int CategoryCount = Enum.GetValues(typeof(AttackType)).Length;
    private static float[] _levels = new float[CategoryCount];
    private static SaveDocument<SourcelessDamageLevelsData> _document;
    private static float _lastSaveTime;
    private static bool _dirty;
    private static bool _initialized;

    /// <summary>配置发生变化时通知配置窗口刷新。</summary>
    public static event Action Changed;

    /// <summary>供配置窗口遍历的全部伤害类别，按枚举值升序排列。</summary>
    public static IReadOnlyList<AttackType> Categories { get; } =
        Array.AsReadOnly((AttackType[])Enum.GetValues(typeof(AttackType)));

    /// <summary>注册持久化文档并从磁盘载入已保存的等级。必须在 <see cref="ModClass"/> 构造 Persistence 之后调用。</summary>
    public static void Initialize(ModSaveManager saveManager)
    {
        if (_initialized) return;
        _document = saveManager.Register(SourcelessDamageLevelsSaveDefinition.Create());
        LoadInto(_document.Data);
        _initialized = true;
    }

    /// <summary>读取指定伤害类别的无来源等级，未配置或未初始化时为 0。</summary>
    public static float GetLevel(AttackType type)
    {
        var index = (int)type;
        return (uint)index < (uint)_levels.Length ? _levels[index] : 0f;
    }

    /// <summary>设置指定伤害类别的无来源等级，自动夹取并取整到 [0, <see cref="MaxLevel"/>]。</summary>
    public static void SetLevel(AttackType type, float value)
    {
        var index = (int)type;
        if ((uint)index >= (uint)_levels.Length) return;
        var snapped = Snap(value);
        if (_levels[index] == snapped) return;
        _levels[index] = snapped;
        _dirty = true;
        Changed?.Invoke();
        TryFlush();
    }

    /// <summary>立即把节流窗口内累积的修改写入磁盘。</summary>
    public static void Flush()
    {
        if (!_dirty || _document == null) return;
        _lastSaveTime = Time.realtimeSinceStartup;
        _document.Save();
        _dirty = false;
    }

    private static void TryFlush()
    {
        if (!_dirty || _document == null) return;
        if (Time.realtimeSinceStartup - _lastSaveTime < SaveInterval) return;
        Flush();
    }

    private static void LoadInto(SourcelessDamageLevelsData data)
    {
        var stored = data.Levels;
        if (stored == null || stored.Length != CategoryCount)
        {
            var merged = new float[CategoryCount];
            if (stored != null)
            {
                var overlap = Math.Min(stored.Length, CategoryCount);
                for (int i = 0; i < overlap; i++) merged[i] = Snap(stored[i]);
            }
            data.Levels = merged;
        }

        // 共享引用：修改 _levels 即修改文档数据，保存时直接序列化当前数组。
        _levels = data.Levels;
        for (int i = 0; i < CategoryCount; i++) _levels[i] = Snap(_levels[i]);
    }

    private static float Snap(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp(value, 0f, MaxLevel));
    }
}
