using System;
using System.IO;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Persistence;

/// <summary>采集与恢复人物基础和修炼成果，不触发随机成长或突破奖励。</summary>
internal static class CharacterSaveService
{
    /// <summary>采集基础数值，避免把灵根的资源对象和属性表写入资料。</summary>
    internal static CharacterSaveData CaptureCharacter(ActorExtend actor)
    {
        float[] rootValues = null;
        if (actor.E.TryGetComponent(out ElementRoot root))
        {
            rootValues = new float[8];
            for (int i = 0; i < rootValues.Length; i++) rootValues[i] = root[i];
        }
        return new CharacterSaveData
        {
            ElementRoot = rootValues,
            DivineSense = actor.E.TryGetComponent(out ValuableTalent talent) ? talent.DivineSense : null,
            Qiyun = Read<Qiyun>(actor), PowerLevel = Read<PowerLevel>(actor),
            PermanentStats = actor.E.TryGetComponent(out PermanentStats stats)
                ? stats.Stats.getList().ToDictionary(item => item.id, item => item.value) : null
        };
    }

    /// <summary>先完成数据检查，再使用正常构造方法重建可计算属性。</summary>
    internal static void RestoreCharacter(ActorExtend actor, CharacterSaveData data)
    {
        Put<ElementRoot>(actor, data.ElementRoot == null ? null : new ElementRoot(data.ElementRoot));
        Put<ValuableTalent>(actor, data.DivineSense.HasValue ? new ValuableTalent(data.DivineSense.Value) : null);
        Put(actor, data.Qiyun);
        Put(actor, data.PowerLevel);
        if (data.PermanentStats != null)
        {
            var stats = new BaseStats();
            foreach (var item in data.PermanentStats) stats[item.Key] = item.Value;
            Put<PermanentStats>(actor, new PermanentStats { Stats = stats });
        }
        else Put<PermanentStats>(actor, null);
        actor.MarkCultiwayStatsDirty();
    }

    /// <summary>基础资料只在存档边界检查一次。</summary>
    internal static void ValidateCharacter(CharacterSaveData data)
    {
        if (data.ElementRoot != null && (data.ElementRoot.Length != 8 || data.ElementRoot.Any(value => !Finite(value) || value < 0)))
            throw new InvalidDataException("灵根必须包含八个有效的非负数值");
        if (data.DivineSense.HasValue && (!Finite(data.DivineSense.Value) || data.DivineSense.Value < 0))
            throw new InvalidDataException("先天资质数值无效");
        if (data.PowerLevel.HasValue && !Finite(data.PowerLevel.Value.value) ||
            data.Qiyun.HasValue && (!Finite(data.Qiyun.Value.Value) || !Finite(data.Qiyun.Value.MaxValue)))
            throw new InvalidDataException("力量层次或气运数值无效");
        if (data.PermanentStats != null)
            foreach (var item in data.PermanentStats)
                if (AssetManager.base_stats_library.get(item.Key) == null || !Finite(item.Value))
                    throw new InvalidDataException($"永久属性不存在或数值无效：{item.Key}");
    }

    /// <summary>采集已经存在的修炼状态；普通动物不产生空的修炼部分。</summary>
    internal static CultivationSaveData CaptureCultivation(ActorExtend actor)
    {
        var data = new CultivationSaveData
        {
            Xian = Read<Xian>(actor), Knight = Read<Knight>(actor), Magic = Read<Magic>(actor),
            Qi = Read<QiRefinementState>(actor), Foundation = Read<XianBase>(actor), Core = Read<Jindan>(actor),
            NascentSoul = Read<Yuanying>(actor), PrimordialSoul = Read<Yuanshen>(actor),
            Practice = Read<CultivationPracticeState>(actor), Resources = Read<CultivationResourceState>(actor),
            KnightStyles = Read<KnightStyleMastery>(actor)
        };
        return data.Xian.HasValue || data.Knight.HasValue || data.Magic.HasValue || data.Qi.HasValue ||
            data.Foundation.HasValue || data.Core.HasValue || data.NascentSoul.HasValue || data.PrimordialSoul.HasValue ||
            data.Practice.HasValue || data.Resources.HasValue || data.KnightStyles.HasValue ? data : null;
    }

    /// <summary>建立必要体系后覆盖永久状态，不重放突破或奖励。</summary>
    internal static void RestoreCultivation(ActorExtend actor, CultivationSaveData data)
    {
        if (data.Xian.HasValue && !actor.HasCultisys<Xian>()) actor.NewCultisys(Cultisyses.Xian);
        if (data.Knight.HasValue && !actor.HasCultisys<Knight>()) actor.NewCultisys(Cultisyses.Knight);
        if (data.Magic.HasValue && !actor.HasCultisys<Magic>()) actor.NewCultisys(Cultisyses.Magic);
        Put(actor, data.Xian); Put(actor, data.Knight); Put(actor, data.Magic);
        Put(actor, data.Qi); Put(actor, data.Foundation); Put(actor, data.Core);
        Put(actor, data.NascentSoul); Put(actor, data.PrimordialSoul);
        Put(actor, data.Practice); Put(actor, data.Resources); Put(actor, data.KnightStyles);
        if (data.Xian.HasValue) actor.UpgradePowerLevel(Cultisyses.Xian.PowerLevels[data.Xian.Value.level]);
        if (data.Knight.HasValue) actor.UpgradePowerLevel(Cultisyses.Knight.PowerLevels[data.Knight.Value.level]);
        if (data.Magic.HasValue) actor.UpgradePowerLevel(Cultisyses.Magic.PowerLevels[data.Magic.Value.level]);
        actor.MarkCultiwayStatsDirty();
    }

    /// <summary>先验证境界与成果格式，防止写入半套修炼状态。</summary>
    internal static void ValidateCultivation(CultivationSaveData data)
    {
        if (data.Xian.HasValue) CheckRealm(data.Xian.Value.level, Cultisyses.Xian.PowerLevels.Count, data.Xian.Value.wakan);
        if (data.Knight.HasValue) CheckRealm(data.Knight.Value.level, Cultisyses.Knight.PowerLevels.Count, data.Knight.Value.vigor);
        if (data.Magic.HasValue) CheckRealm(data.Magic.Value.level, Cultisyses.Magic.PowerLevels.Count, data.Magic.Value.spirit);
        if (data.Qi.HasValue) CheckFormation(data.Qi.Value.formation);
        if (data.Foundation.HasValue) CheckFormation(data.Foundation.Value.formation);
        if (data.Core.HasValue) CheckFormation(data.Core.Value.formation);
        if (data.NascentSoul.HasValue) CheckFormation(data.NascentSoul.Value.formation);
        if (data.PrimordialSoul.HasValue) CheckFormation(data.PrimordialSoul.Value.formation);
    }

    /// <summary>检查一个体系的稳定境界与资源。</summary>
    private static void CheckRealm(int level, int count, float resource)
    {
        if (level < 0 || level >= count || !Finite(resource)) throw new InvalidDataException("境界或修炼资源无效");
    }

    /// <summary>未形成的成果可以为空；已有成果必须符合当前明确的数据格式。</summary>
    private static void CheckFormation(CoreFormationSnapshot formation)
    {
        if (formation.version != 0 && formation.version != CoreFormationSnapshot.CurrentVersion)
            throw new InvalidDataException("境界成果需要对应版本转换");
    }

    /// <summary>读取已有组件，不创建默认成长。</summary>
    internal static T? Read<T>(ActorExtend actor) where T : struct, IComponent
        => actor.E.TryGetComponent(out T value) ? value : null;

    /// <summary>恢复组件的存在性，避免读档过程中随机生成的内容冒充保存结果。</summary>
    private static void Put<T>(ActorExtend actor, T? value) where T : struct, IComponent
    {
        if (value.HasValue) actor.E.AddComponent(value.Value);
        else if (actor.E.HasComponent<T>()) actor.E.RemoveComponent<T>();
    }

    /// <summary>排除无法参与属性计算的非有限数值。</summary>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
