using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>各类主动法相的显示样式。</summary>
internal enum BattleSpellStyle
{
    /// <summary>剑光与金行。</summary>
    Sword,
    /// <summary>幽阴与魂流。</summary>
    Yin,
    /// <summary>纯阳与日轮。</summary>
    Yang,
    /// <summary>读取真实状态的五相轮转。</summary>
    FivePhase,
    /// <summary>附着实际身体的真身法相。</summary>
    Body,
    /// <summary>盘绕龙相。</summary>
    Dragon,
    /// <summary>灵台与辅助灵流。</summary>
    Spirit
}

/// <summary>根据持有者当前的主动状态显示法相。</summary>
internal static partial class BattleSpellVisualRuntime
{
    /// <summary>记录最近的实际近身接触，供法相收势。</summary>
    private static readonly Dictionary<long, double> contacts = new();
    /// <summary>持续法相从真实状态重建，不依赖一次施放的短命执行体。</summary>
    private static readonly List<CoreFormationResolvedEffect> statusEffects = new();
    /// <summary>只在当前绘制调用中复用的状态快照。</summary>
    private static readonly Cast statusCast = new();

    /// <summary>仅记录真实近身接触时间，供法相手臂跟随；不额外生成云团。</summary>
    /// <param name="actor">真实来源。</param><param name="target">实际受到伤害的身体。</param>
    internal static void Contact(Actor actor,Actor target)
    {
        // 原版的武器伤害也包括远程和部分攻击动作；只有身体或武器实际可达时才表现近身接触。
        float reach=SpatialActorPose.Height(actor)*(actor.hasWeapon()?2.5f:.65f)+SpatialActorPose.Height(target)*.22f;
        if(actor.hasRangeAttack() || Vector2.Distance(actor.current_position,target.current_position)>reach)return;
        float growth=BattleSpellScale.Resolve(actor.GetExtend());
        if(growth<3f)return;
        double now=World.world.getCurWorldTime();
        if(contacts.TryGetValue(actor.id,out double previous) && now-previous<.35d)return;
        contacts[actor.id]=now;
    }

    /// <summary>快照只由世界时间推进，换镜头和暂停不会重播或触发攻击。</summary>
    /// <param name="output">共同空间模型。</param><param name="details">装饰预算。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output,ref int details)
    {
        double now=World.world.getCurWorldTime();
        AppendStatusForms(output, ref details, now);
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
                var cast=statusCast;cast.Source=actor;cast.Style=style;
                cast.Growth=BattleSpellScale.Resolve(owner);cast.Radius=cast.Growth*1.65f;cast.Position=SpatialActorPose.Foot(actor);
                cast.Color=ColorOf(style);
                cast.Phase=0;cast.PhaseProgress=0f;
                if(style==BattleSpellStyle.FivePhase && CoreFormationStateService.TryGet(owner,effect,out _,out var phaseState))
                {cast.Phase=phaseState.phase;cast.PhaseProgress=Mathf.Clamp01(1f-phaseState.auxiliary_timer/2f);}
                int first=output.Count;DrawCast(cast,(float)now,output,ref details);
                for(int i=first;i<output.Count;i++)output[i]=output[i].WithBodyAnchor(actor);
            }
        }
    }

    /// <summary>主体保留饱和色，亮芯另行叠加，避免所有流派最终都发白。</summary>
    /// <param name="style">实际流派。</param>
    internal static Color ColorOf(BattleSpellStyle style)=>style switch
    {
        BattleSpellStyle.Sword=>new(.45f,.76f,1f),BattleSpellStyle.Yin=>new(.48f,.25f,.86f),
        BattleSpellStyle.Yang=>new(1f,.76f,.2f),
        BattleSpellStyle.Body=>new(1f,.65f,.26f),BattleSpellStyle.Dragon=>new(.28f,.84f,.67f),
        _=>new(.5f,.75f,1f)
    };

    /// <summary>切换世界释放快照与来源，绝不跨世界继续施放。</summary>
    internal static void Clear(){contacts.Clear();statusEffects.Clear();statusCast.Source=null;}

    /// <summary>只在当帧读取的法相快照。</summary>
    private sealed class Cast
    {
        /// <summary>真实空间载体。</summary>
        internal BaseSimObject Source;
        /// <summary>主动状态对应的流派。</summary>
        internal BattleSpellStyle Style;
        /// <summary>实际持有者脚下的位置。</summary>
        internal Vector3 Position;
        /// <summary>当前真实范围倍率与实际半径。</summary>
        internal float Growth,Radius;
        /// <summary>真实相位及其已经经过的比例。</summary>
        internal int Phase;
        internal float PhaseProgress;
        /// <summary>主色。</summary>
        internal Color Color;
    }
}
