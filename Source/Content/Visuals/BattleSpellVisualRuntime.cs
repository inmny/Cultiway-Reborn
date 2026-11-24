using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>高阶法术的明确视觉流派；同一元素仍按原来的弹丸、光束和领域方式作用。</summary>
internal enum BattleSpellStyle
{
    /// <summary>剑光与金行。</summary>
    Sword,
    /// <summary>分叉雷路。</summary>
    Lightning,
    /// <summary>层叠火焰。</summary>
    Fire,
    /// <summary>宽面水流。</summary>
    Water,
    /// <summary>冰晶与冷雾。</summary>
    Ice,
    /// <summary>树根与叶冠。</summary>
    Wood,
    /// <summary>低伏毒域。</summary>
    Poison,
    /// <summary>山峰与岩脊。</summary>
    Earth,
    /// <summary>多股风流。</summary>
    Wind,
    /// <summary>幽阴与魂流。</summary>
    Yin,
    /// <summary>纯阳与日轮。</summary>
    Yang,
    /// <summary>不规则的空间破裂与凝聚。</summary>
    Chaos,
    /// <summary>读取真实状态的五相轮转。</summary>
    FivePhase,
    /// <summary>附着实际身体的真身法相。</summary>
    Body,
    /// <summary>盘绕龙相。</summary>
    Dragon,
    /// <summary>灵台与辅助灵流。</summary>
    Spirit
}

/// <summary>只记录真实施放和落点，让主攻结束后仍有可读余波；不产生伤害或复制技能。</summary>
internal static partial class BattleSpellVisualRuntime
{
    /// <summary>当前实际高阶施放及短暂收尾。</summary>
    private static readonly List<Cast> casts = new();
    /// <summary>从执行体定位它唯一的显示记录。</summary>
    private static readonly Dictionary<Entity, Cast> active = new();
    /// <summary>近战高频结果只在每位来源的有限间隔产生大余波。</summary>
    private static readonly Dictionary<long, double> contacts = new();
    /// <summary>供实机检查读取的当前显示数量。</summary>
    internal static int Count => casts.Count;
    /// <summary>持续法相从真实状态重建，不依赖一次施放的短命执行体。</summary>
    private static readonly List<CoreFormationResolvedEffect> statusEffects = new();
    /// <summary>只在当前绘制调用中复用的状态快照。</summary>
    private static readonly Cast statusCast = new();

    /// <summary>普通法术采用同样的精细模型，高阶法术额外保留来源与余波。</summary>
    /// <param name="entity">真实执行体。</param><param name="growth">已应用到真实范围的境界倍率。</param>
    internal static void Register(Entity entity, float growth)
    {
        MaterialSpellVisuals.Register(entity);
        if(growth<1.8f || casts.Count>=96)return;
        var skill=entity.GetComponent<SkillEntity>();
        // 通用配方部分由自己的实际阶段显示，避免再叠加旧的领域和爆发。
        if (skill.Asset.id == CoreFormationSkills.SkillAssetIdPrefix + "FivePhase") return;
        var context=entity.GetComponent<SkillContext>();
        BattleSpellStyle style=Style(skill);
        SkillSpatialVisualCatalog.Forms.TryGetValue(skill.Asset.id,out SkillSpatialForm form);
        if (form == SkillSpatialForm.PoisonBloom) return;
        var cast=new Cast
        {
            Entity=entity,Source=context.ResolveSpatialSource(),Style=style,Form=form,
            Origin=entity.GetComponent<Position>().value,Position=entity.GetComponent<Position>().value,
            Direction=entity.GetComponent<Rotation>().value,Target=context.TargetPos,
            Growth=growth,Color=style==BattleSpellStyle.FivePhase?skill.VfxElement.AccentColor:ColorOf(style),Started=World.world.getCurWorldTime(),
            Radius=SkillEffectRadius.Effect(entity),
            AssetId=skill.Asset.id,ImpactAt=-1d,Ended=-1d
        };
        casts.Add(cast);active[entity]=cast;
    }

    /// <summary>真实范围结算发生后保留同一落点和危险边界，空地作用也有余波。</summary>
    /// <param name="entity">已经实际作用的执行体。</param><param name="position">实际落点。</param><param name="radius">实际作用半径。</param>
    internal static void OnAreaResolved(Entity entity,Vector3 position,float radius)
    {
        if(!active.TryGetValue(entity,out Cast cast))return;
        double now=World.world.getCurWorldTime();
        if(cast.ImpactAt>=0d && now-cast.ImpactAt<.7d)return;
        cast.ImpactAt=now;cast.Impact=position;cast.Radius=radius;
    }

    /// <summary>仅记录真实近身接触时间，供法相手臂跟随；不额外生成云团。</summary>
    /// <param name="actor">真实来源。</param><param name="target">实际受到伤害的身体。</param>
    internal static void Contact(Actor actor,Actor target)
    {
        // 原版的武器伤害也包括远程和部分攻击动作；只有身体或武器实际可达时才表现近身接触。
        float reach=SpatialActorPose.Height(actor)*(actor.hasWeapon()?2.5f:.65f)+SpatialActorPose.Height(target)*.22f;
        if(actor.hasRangeAttack() || Vector2.Distance(actor.current_position,target.current_position)>reach)return;
        Vector3 point=target.GetSimPos();
        float growth=BattleSpellScale.Resolve(actor.GetExtend());
        if(growth<3f || casts.Count>=96)return;
        double now=World.world.getCurWorldTime();
        if(contacts.TryGetValue(actor.id,out double previous) && now-previous<.35d)return;
        contacts[actor.id]=now;

    }

    /// <summary>快照只由世界时间推进，换镜头和暂停不会重播或触发攻击。</summary>
    /// <param name="output">共同空间模型。</param><param name="details">装饰预算。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output,ref int details)
    {
        double now=World.world.getCurWorldTime();
        MaterialSpellVisuals.AppendGroundPaths(output, ref details);
        AppendStatusForms(output, ref details, now);
        for(int i=casts.Count-1;i>=0;i--)
        {
            Cast cast=casts[i];bool live=cast.Entity.IsAvailable() && !cast.Entity.Tags.Has<TagRecycle>();
            if(live && cast.Entity.Tags.Has<TagInactive>())continue;
            if(live)cast.Position=cast.Entity.GetComponent<Position>().value;
            else if(cast.Ended<0d)cast.Ended=now;
            float age=(float)(now-cast.Started),ending=cast.Ended<0d?0f:(float)(now-cast.Ended);
            if(ending>1.65f && (cast.ImpactAt<0d || now-cast.ImpactAt>1.65f))
            {active.Remove(cast.Entity);casts.RemoveAt(i);continue;}
            float extent=Mathf.Max(cast.Radius,cast.Growth*3f);
            if(!SpatialVisualEffects.IsRelevant(cast.Position,extent) && !SpatialVisualEffects.IsRelevant(cast.Origin,extent))continue;
            if (live) DrawCast(cast, age, output, ref details);
            if(cast.ImpactAt>=0d)DrawImpact(cast,(float)(now-cast.ImpactAt),output,ref details);
        }
    }

    /// <summary>实际主动状态存续时才显示法相和五相结构，读档后也从同一状态重建。</summary>
    /// <param name="output">共同模型。</param><param name="details">装饰预算。</param><param name="now">当前世界时间。</param>
    private static void AppendStatusForms(List<SpatialVisualMeshInstance> output,ref int details,double now)
    {
        foreach(var ownerEntity in ModClass.I.W.Query<ActorBinder,CoreFormationGrantRuntime>().Entities)
        {
            Actor actor=ownerEntity.GetComponent<ActorBinder>().Actor;
            if(actor.isRekt() || !actor.isAlive() || !SpatialVisualEffects.IsRelevant(actor.GetSimPos(),25f))continue;
            var owner=actor.GetExtend();CoreFormationEffectResolver.Resolve(owner,statusEffects);
            foreach(var effect in statusEffects)
            {
                if(effect.Definition.active==null)continue;
                float remaining=CoreFormationStateService.GetRemaining(owner,effect);
                if(remaining<=0f || remaining>600f)continue;
                BattleSpellStyle style=effect.Definition.family_id switch
                {
                    CoreFormationEffectFamilies.Body=>BattleSpellStyle.Body,CoreFormationEffectFamilies.Dragon=>BattleSpellStyle.Dragon,
                    CoreFormationEffectFamilies.FivePhase=>BattleSpellStyle.FivePhase,CoreFormationEffectFamilies.Sword=>BattleSpellStyle.Sword,
                    CoreFormationEffectFamilies.Spiritual=>BattleSpellStyle.Spirit,CoreFormationEffectFamilies.Yang=>BattleSpellStyle.Yang,
                    CoreFormationEffectFamilies.Yin=>BattleSpellStyle.Yin,_=>BattleSpellStyle.Spirit
                };
                var cast=statusCast;cast.Source=actor;cast.Style=style;cast.Form=SkillSpatialForm.Shield;cast.Entity=default;
                cast.Growth=BattleSpellScale.Resolve(owner);cast.Radius=cast.Growth*1.65f;cast.Position=SpatialActorPose.Foot(actor);
                cast.Origin=cast.Position;cast.Color=ColorOf(style);cast.AssetId="active."+effect.Definition.family_id;
                cast.Phase=0;cast.PhaseProgress=0f;
                if(style==BattleSpellStyle.FivePhase && CoreFormationStateService.TryGet(owner,effect,out _,out var phaseState))
                {cast.Phase=phaseState.phase;cast.PhaseProgress=Mathf.Clamp01(1f-phaseState.auxiliary_timer/2f);}
                int first=output.Count;DrawCast(cast,(float)now,output,ref details);
                for(int i=first;i<output.Count;i++)output[i]=output[i].WithBodyAnchor(actor);
            }
        }
    }

    /// <summary>使用明确的技能形式和元素登记，避免按一个通用光球换色。</summary>
    /// <param name="skill">真实技能。</param>
    internal static BattleSpellStyle Style(SkillEntity skill)
    {
        string id=skill.Asset.id;
        bool explicitNature=skill.SkillContainer.TryGetComponent(out Cultiway.Core.SkillLibV3.Settings.SkillSettings settings) &&
            settings.NatureId!=null;
        if(explicitNature)id=string.Empty;
        if(id.Contains("Dragon"))return BattleSpellStyle.Dragon;
        if(id.Contains("Body"))return BattleSpellStyle.Body;
        if(id == CoreFormationSkills.SkillAssetIdPrefix + "FivePhase")return BattleSpellStyle.FivePhase;
        if(id.Contains("FivePhaseStrike"))id=string.Empty;
        if(id.Contains("Chaos") || id.Contains("Rift"))return BattleSpellStyle.Chaos;
        if(id.Contains("Sword") || skill.VfxElement==SkillVfxElements.Metal)return BattleSpellStyle.Sword;
        // 爆炸、引力等效果可以取得最高配色优先级，但不改变炎陨、冰落等招式的元素结构。
        var element=skill.VfxElement;
        if(element==SkillVfxElements.Explosion || element==SkillVfxElements.Gravity || element==SkillVfxElements.Burnout)
            element=ModClass.I.SkillV3.VfxElementLib.ResolvePrimaryElement(skill.Asset);
        if(element==SkillVfxElements.Lightning)return BattleSpellStyle.Lightning;
        if(element==SkillVfxElements.Ice)return BattleSpellStyle.Ice;
        if(element==SkillVfxElements.Wind)return BattleSpellStyle.Wind;
        if(element==SkillVfxElements.Poison)return BattleSpellStyle.Poison;
        if(element==SkillVfxElements.Fire)return BattleSpellStyle.Fire;
        if(element==SkillVfxElements.Water)return BattleSpellStyle.Water;
        if(element==SkillVfxElements.Wood)return BattleSpellStyle.Wood;
        if(element==SkillVfxElements.Earth)return BattleSpellStyle.Earth;
        if(element==SkillVfxElements.Metal)return BattleSpellStyle.Sword;
        if(element==SkillVfxElements.Neg || element==SkillVfxElements.Curse)return BattleSpellStyle.Yin;
        if(element==SkillVfxElements.Pos)return BattleSpellStyle.Yang;
        if(element==SkillVfxElements.Entropy)return BattleSpellStyle.Chaos;
        return BattleSpellStyle.Spirit;
    }

    /// <summary>主体保留饱和色，亮芯另行叠加，避免所有流派最终都发白。</summary>
    /// <param name="style">实际流派。</param>
    internal static Color ColorOf(BattleSpellStyle style)=>style switch
    {
        BattleSpellStyle.Sword=>new(.45f,.76f,1f),BattleSpellStyle.Lightning=>new(.54f,.49f,1f),
        BattleSpellStyle.Fire=>new(1f,.23f,.035f),BattleSpellStyle.Water=>new(.035f,.48f,1f),
        BattleSpellStyle.Ice=>new(.3f,.86f,1f),BattleSpellStyle.Wood=>new(.18f,.84f,.35f),
        BattleSpellStyle.Poison=>new(.57f,.9f,.12f),BattleSpellStyle.Earth=>new(.82f,.52f,.17f),
        BattleSpellStyle.Wind=>new(.3f,.88f,.78f),BattleSpellStyle.Yin=>new(.48f,.25f,.86f),
        BattleSpellStyle.Yang=>new(1f,.76f,.2f),BattleSpellStyle.Chaos=>new(.8f,.4f,.92f),
        BattleSpellStyle.Body=>new(1f,.65f,.26f),BattleSpellStyle.Dragon=>new(.28f,.84f,.67f),
        _=>new(.5f,.75f,1f)
    };

    /// <summary>切换世界释放快照与来源，绝不跨世界继续施放。</summary>
    internal static void Clear(){casts.Clear();active.Clear();contacts.Clear();statusEffects.Clear();statusCast.Source=null;statusCast.Entity=default;MaterialSpellVisuals.Clear();}

    /// <summary>一项真实施放的短暂显示资料，不进入正式存档。</summary>
    private sealed class Cast
    {
        /// <summary>真实技能与实际载体。</summary>
        internal Entity Entity;
        /// <summary>真实空间载体。</summary>
        internal BaseSimObject Source;
        /// <summary>明确的流派和主体形式。</summary>
        internal BattleSpellStyle Style;
        /// <summary>原有形式。</summary>
        internal SkillSpatialForm Form;
        /// <summary>实际源点、当前位置、目标、方向和落点。</summary>
        internal Vector3 Origin,Position,Target,Direction,Impact;
        /// <summary>当前真实范围倍率与实际半径。</summary>
        internal float Growth,Radius;
        /// <summary>真实相位及其已经经过的比例。</summary>
        internal int Phase;
        internal float PhaseProgress;
        /// <summary>主色。</summary>
        internal Color Color;
        /// <summary>世界时钟上的开始、真实落点和结束。</summary>
        internal double Started,ImpactAt,Ended;
        /// <summary>现有技能的稳定编号。</summary>
        internal string AssetId;
    }
}
