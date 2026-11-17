using System.Collections.Generic;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>原人物及有效映像共用接触机会，只有离开全部作用面后才能重入。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>所有映像在同一预测起点的真实表面，只在起点离开检查期间使用。</summary>
    private readonly List<BarrierContact> correspondenceStarts = new();

    /// <summary>起点映像使用独立索引，瞬时换位后仍能查询其离开前的真实表面。</summary>
    private readonly AbilityContactIndex correspondenceStartIndex = new();

    /// <summary>离开及区间查询的候选缓冲，身体查找使用另一份缓冲。</summary>
    private readonly List<int> correspondenceCandidates = new();

    /// <summary>所有起点采集完毕后建立映像索引，整轮区间计算继续复用它。</summary>
    private void IndexCorrespondenceStarts()
    {
        correspondenceStarts.Clear();
        correspondenceStartIndex.Clear();
        foreach (RunningCast image in active)
        {
            if (!image.Cast.Plan.HasCorrespondences) continue;
            foreach (AbilityCapsule shape in image.Before)
            {
                if (CorrespondenceReceiver(image, shape.Node) == null ||
                    image.Cast.Plan.Nodes[shape.Node].Correspondence.DamageFraction <= 0f) continue;
                correspondenceStarts.Add(new BarrierContact(image, shape));
                correspondenceStartIndex.Add(AbilityContactIndex.Swept(shape, shape));
            }
        }
    }

    /// <summary>原人物的任何有效映像仍与同组攻击接触时，原目标仍处于同一接触机会。</summary>
    /// <param name="running">原攻击施放。</param><param name="key">原接触组及原人物。</param>
    private bool CorrespondenceTouching(RunningCast running, (int, long, int, long) key)
    {
        foreach (AbilityCapsule shape in running.Before)
        {
            if (!running.Cast.AffectsActors(shape.Node) || running.HitKey(shape.Node, key.Item4) != key) continue;
            correspondenceStartIndex.Query(AbilityContactIndex.Swept(shape, shape), correspondenceCandidates);
            foreach (int candidate in correspondenceCandidates)
            {
                BarrierContact image = correspondenceStarts[candidate];
                if (CorrespondenceReceiver(image.Running, image.Shape.Node)?.id != key.Item4 ||
                    !AbilityPairSweep.Find(shape, shape, image.Shape, image.Shape, out _, out _, out _)) continue;
                if (shape.Node < 0 && running.BeforeJoint != null &&
                    !BodySurfacePose.Find(running.BeforeJoint.Contact, running.BeforeJoint.Contact,
                        image.Shape, image.Shape, "surface", out _, out _, out _)) continue;
                return true;
            }
        }
        return false;
    }

    /// <summary>区间覆盖包含原人物的有效映像，避免在持续贴住或重叠映像之间伪造离开。</summary>
    /// <param name="shape">同组真实攻击。</param><param name="target">原人物编号。</param>
    private void AppendCorrespondenceIntervals(in ShapeMotion shape, long target)
    {
        // 只在新位置接触的随动，旧表面维持到本边界，新表面只在终点接受接触。
        // 旧、新位置都接触时，不能把“没有中间扫掠”当作实际离开。
        correspondenceStartIndex.Query(shape.Bounds, correspondenceCandidates);
        foreach (int candidate in correspondenceCandidates)
        {
            BarrierContact image = correspondenceStarts[candidate];
            if (!image.Running.Cast.ContactAtDestination(image.Shape.Node) ||
                CorrespondenceReceiver(image.Running, image.Shape.Node)?.id != target) continue;
            AppendContactInterval(shape, image.Shape, image.Shape, true, false);
        }
        shapeIndex.Query(shape.Bounds, correspondenceCandidates);
        foreach (int candidate in correspondenceCandidates)
        {
            ShapeMotion image = shapes[candidate];
            if (CorrespondenceReceiver(image.Running, image.To.Node)?.id != target ||
                image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence.DamageFraction <= 0f) continue;
            AppendContactInterval(shape, image.From, image.To, image.Continuous, false);
        }
    }
}
