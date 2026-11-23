using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>把几何检查交给工作池，按发现顺序交回主线程确认人物、地块与来源。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>每块最多保留的几何输入数，避免密集范围积存整段候选。</summary>
    private const int ContactSweepBlock = 128;

    /// <summary>工作池只读取这里的世界形状和时段。</summary>
    private readonly List<ContactSweep> contactSweeps = new();

    /// <summary>与形状一一对应，只由主线程读取的实际游戏对象和交锋规则。</summary>
    private readonly List<ContactSweepTarget> contactSweepTargets = new();

    /// <summary>每个输入独占一个结果位置。没有接触时为空。</summary>
    private readonly ContactSweepHit?[] contactSweepHits = new ContactSweepHit?[ContactSweepBlock];

    /// <summary>计算已准备的块，再按原顺序生成候选。小块不派发工作线程。</summary>
    /// <param name="step">本段预测的共同世界秒数。</param>
    /// <param name="include">主线程统一接收候选的入口。</param>
    private void CollectContactSweeps(float step, Action<ContactChoice> include)
    {
        int count = contactSweeps.Count;
        if (count == ContactSweepBlock) AbilityBatch.RunIndependent(count, count * 4, Evaluate);
        else for (int i = 0; i < count; i++) Evaluate(i);
        for (int i = 0; i < count; i++)
            if (contactSweepHits[i] is { } hit) include(contactSweepTargets[i].Resolve(hit, step));
        contactSweeps.Clear();
        contactSweepTargets.Clear();

        void Evaluate(int index) => contactSweepHits[index] = contactSweeps[index].Find();
    }

    /// <summary>滑动形状与接触时间窗口。所有内容在工作前取得。</summary>
    /// <param name="leftStart">左侧从可接触时刻开始的形状。</param>
    /// <param name="leftEnd">本步末端左侧形状。</param>
    /// <param name="rightStart">右侧从同一时刻开始的形状。</param>
    /// <param name="rightEnd">本步末端右侧形状。</param>
    /// <param name="from">可搜索的起点。</param>
    /// <param name="until">允许接触的末端。</param>
    /// <param name="exclusiveUntil">下一规则开始的时点，原规则不含该时点。</param>
    private readonly struct ContactSweep(AbilityCapsule leftStart, AbilityCapsule leftEnd,
        AbilityCapsule rightStart, AbilityCapsule rightEnd, float from, float until = 1f,
        float exclusiveUntil = float.PositiveInfinity)
    {
        /// <summary>只用已取得的几何数据查找一个接触。</summary>
        internal ContactSweepHit? Find()
        {
            if (!AbilityPairSweep.Find(leftStart, leftEnd, rightStart, rightEnd, out float at,
                    out Vector3 point, out Vector3 normal)) return null;
            float fraction = Mathf.Lerp(from, 1f, at);
            return fraction <= until && fraction < exclusiveUntil ? new ContactSweepHit(fraction, point, normal) : null;
        }
    }

    /// <summary>可以由工作池写入的接触点，不携带任何游戏对象。</summary>
    /// <param name="time">接触时点占本步的比例。</param>
    /// <param name="point">世界接触位置。</param>
    /// <param name="normal">接触面的朝向。</param>
    private readonly struct ContactSweepHit(float time, Vector3 point, Vector3 normal)
    {
        /// <summary>接触时点占本步的比例。</summary>
        internal readonly float Time = time;
        /// <summary>世界接触位置。</summary>
        internal readonly Vector3 Point = point;
        /// <summary>接触面的朝向。</summary>
        internal readonly Vector3 Normal = normal;
    }

    /// <summary>把纯几何结果与主线程持有的接触对象合并。</summary>
    /// <param name="left">本次作用的形状。</param>
    /// <param name="right">参与交锋的对方形状。</param>
    /// <param name="actor">本次接触的人物。</param>
    /// <param name="actorStart">人物在本步原起点的形状，用来确定射线的真实距离。</param>
    /// <param name="actorEnd">人物在本步末端的形状。</param>
    /// <param name="tile">世界操作对应的地块。</param>
    /// <param name="worldEffect">地块作用序号。</param>
    /// <param name="quenching">是否使用真实灭火规则。</param>
    /// <param name="reaction">接触产生的材料反应。</param>
    /// <param name="worldRecord">参与反应的持久世界材料记录。</param>
    /// <param name="worldShape">参与反应的世界材料形状。</param>
    /// <param name="worldSource">是否由已有世界材料发起反应。</param>
    private readonly struct ContactSweepTarget(ShapeMotion left, ShapeMotion? right = null,
        ActorMotion actor = default, AbilityCapsule actorStart = default, AbilityCapsule actorEnd = default,
        WorldTile tile = null, int worldEffect = 0, bool quenching = false, ThermalReaction reaction = ThermalReaction.None,
        long worldRecord = 0, AbilityCapsule worldShape = default, bool worldSource = false)
    {
        /// <summary>组装原有候选，包括仍在主线程计算的射线接触距离。</summary>
        /// <param name="hit">已确认的几何位置。</param><param name="step">本次共同世界秒数。</param>
        internal ContactChoice Resolve(in ContactSweepHit hit, float step) => new(left, actor, right, hit.Time,
            hit.Point, hit.Normal, step, quenching, worldRecord, worldShape, reaction, worldSource, tile, worldEffect,
            actorShape: actor.Id != 0 ? Slice(actorStart, actorEnd, hit.Time) : default);
    }
}
