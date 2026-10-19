using System;
using System.IO;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Friflo.Engine.ECS;
using Newtonsoft.Json;

namespace Cultiway.Content.YaoBeasts;

/// <summary>把妖兽稳定状态写入原版单位存档，读取后从完整身体重建能力与外观。</summary>
public static class YaoPersistence
{
    /// <summary>只序列化公共字段，避免把组件的资源对象和便利属性写入存档。</summary>
    private static readonly JsonSerializerSettings settings = StableSaveJson.Settings;

    /// <summary>取得当前单位的稳定状态；没有妖兽内容时返回空。</summary>
    public static string Capture(ActorExtend actor)
    {
        var record = new Record
        {
            Yao = Read<Yao>(actor), Body = Read<YaoBody>(actor), Genome = Read<YaoGenome>(actor),
            Digestion = Read<YaoDigestion>(actor), Core = Read<YaoCore>(actor),
            Tribulation = Read<YaoTribulation>(actor), Nirvana = Read<Nirvana>(actor)
        };
        if (!record.Yao.HasValue) record.Potential = Read<YaoAwakeningPotential>(actor);
        if (!record.Yao.HasValue && !record.Genome.HasValue &&
            (!record.Potential.HasValue || record.Potential.Value.TotalScore == 0f &&
                record.Potential.Value.NextSurvivalEligibleAt == 0f)) return null;
        return JsonConvert.SerializeObject(record, settings);
    }

    /// <summary>先检查存档中的身体，再恢复组件和境界，最后一次性表达当前身体。</summary>
    public static void Restore(ActorExtend actor, string json)
    {
        Record record = JsonConvert.DeserializeObject<Record>(json, settings);
        if (record == null || record.Yao.HasValue != record.Body.HasValue ||
            !record.Yao.HasValue && !record.Genome.HasValue && !record.Potential.HasValue)
            throw new InvalidDataException("妖兽存档内容不完整");
        if (record.Body.HasValue)
        {
            YaoBody body = record.Body.Value;
            if (body.Forms == null || body.Forms.Length > 3)
                throw new InvalidDataException("妖兽存档中的身体形态数量不符合当前规则");

            // 启灵雨的通用身体按物种临时登记，重启后必须先重建定义，再整理永久身体。
            foreach (YaoFormRecord form in body.Forms)
            {
                string prefix = YaoContent.YaoSpeciesTemplates.AdaptedMorphPrefix;
                if (form.MorphId != null && form.MorphId.StartsWith(prefix, StringComparison.Ordinal) &&
                    YaoContent.YaoSpeciesTemplates.EnsureAdaptedMorph(form.MorphId.Substring(prefix.Length)) == null)
                    throw new InvalidDataException("启灵雨身体对应的物种不存在");
            }
            if (!body.TryGetActiveForm(out YaoFormRecord active) ||
                !CreaturePhenotypeCompiler.TryGetOrCompile(YaoFormPlanService.CreatePlan(active), out _) ||
                record.Yao.Value.level < 0 || record.Yao.Value.level >= Cultisyses.Yao.PowerLevels.Count)
                throw new InvalidDataException("妖兽存档中的身体或境界不符合当前规则");
        }

        if (record.Yao.HasValue)
        {
            if (!actor.HasCultisys<Yao>()) actor.NewCultisys(Cultisyses.Yao);
            Write(actor, record.Yao);
            actor.UpgradePowerLevel(Cultisyses.Yao.PowerLevels[record.Yao.Value.level]);
            if (actor.E.HasComponent<YaoAwakeningPotential>()) actor.E.RemoveComponent<YaoAwakeningPotential>();
        }
        else Write(actor, record.Potential);
        Write(actor, record.Genome);
        Write(actor, record.Digestion);
        Write(actor, record.Core);
        Write(actor, record.Tribulation);
        Write(actor, record.Nirvana);
        if (record.Body.HasValue && !YaoFormPlanService.RestoreAndExpress(actor, record.Body.Value))
            throw new InvalidDataException("妖兽身体恢复失败");
        actor.MarkCultiwayStatsDirty();
    }

    /// <summary>读取可选组件；没有内容的组件不制造空记录。</summary>
    private static T? Read<T>(ActorExtend actor) where T : struct, IComponent
        => actor.E.TryGetComponent(out T component) ? component : null;

    /// <summary>把确实存在的保存内容写回单位自己的组件。</summary>
    private static void Write<T>(ActorExtend actor, T? component) where T : struct, IComponent
    {
        if (!component.HasValue) return;
        if (actor.E.HasComponent<T>()) actor.E.GetComponent<T>() = component.Value;
        else actor.E.AddComponent(component.Value);
    }

    /// <summary>只含稳定数据的当前版本记录；图片与运行时身体整理结果均由读取后重建。</summary>
    private sealed class Record
    {
        /// <summary>尚未启灵的真实积累。</summary>
        public YaoAwakeningPotential? Potential;
        /// <summary>身份、境界与妖力。</summary>
        public Yao? Yao;
        /// <summary>全部永久形态和带来源的器官。</summary>
        public YaoBody? Body;
        /// <summary>主隐血脉与遗传位点。</summary>
        public YaoGenome? Genome;
        /// <summary>已领取精华与消化进度。</summary>
        public YaoDigestion? Digestion;
        /// <summary>妖丹及裂痕。</summary>
        public YaoCore? Core;
        /// <summary>尚未结束的天劫。</summary>
        public YaoTribulation? Tribulation;
        /// <summary>尚未结束的涅槃。</summary>
        public Nirvana? Nirvana;
    }

}
