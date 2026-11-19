using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

public static partial class BodyConstructionEdits
{
    /// <summary>找出同动作链的另一侧；头、器官和命名骨段没有通用左右映射。</summary>
    internal static string OppositeSide(string part)
    {
        if (part == null) return null;
        int split = part.IndexOf('.');
        if (split < 0 || part.Substring(0, split) is not ("hand" or "elbow" or "shoulder" or "knee" or "foot")) return null;
        return part.Substring(split) switch
        {
            ".left" => part.Substring(0, split) + ".right",
            ".right" => part.Substring(0, split) + ".left",
            _ => null
        };
    }

    /// <summary>将同一种发力镜像到另一侧，保留关系编号、目标及独立制作的移动轨迹。</summary>
    internal static AbilityDefinition MirrorSide(AbilityDefinition source, CompiledBodyAction original)
    {
        string opposite = OppositeSide(original?.Effector);
        if (opposite == null) throw new InvalidDataException("当前部位没有同类的左右动作");
        if (original.WeaponForm != null) throw new InvalidDataException("原版实际器物槽只允许右手握持");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        BodyActionDefinition body = result.Body;
        if (body.Construction == null) body.Effector = opposite;
        else
        {
            foreach (BodyPartInput part in body.Construction.Parts.Where(part => part.Subject == BodyPartSubject.Source))
                part.Part = OppositeSide(part.Part) ?? part.Part;
            if (body.Construction.Support is { } support)
            {
                Reflect(support.CenterOffset);
                foreach (BodySupportPoint point in support.Points) Reflect(point.Position);
            }
        }

        const BodyOccupancy arms = BodyOccupancy.LeftArm | BodyOccupancy.RightArm;
        BodyOccupancy occupied = body.Occupancy;
        body.Occupancy = occupied & ~arms;
        if ((occupied & BodyOccupancy.LeftArm) != 0) body.Occupancy |= BodyOccupancy.RightArm;
        if ((occupied & BodyOccupancy.RightArm) != 0) body.Occupancy |= BodyOccupancy.LeftArm;
        foreach (BodyMotionKey key in body.Keys)
        {
            Reflect(key.Hand);
            Reflect(key.Foot);
            Reflect(key.Torso);
            key.Rotation[1] = -key.Rotation[1];
            key.Rotation[2] = -key.Rotation[2];
            key.Turn = -key.Turn;
            key.Heading = -key.Heading;
            if (key.Support == null) continue;
            (key.Support.LeftFoot, key.Support.RightFoot) = (key.Support.RightFoot, key.Support.LeftFoot);
            Reflect(key.Support.LeftFoot);
            Reflect(key.Support.RightFoot);
        }
        AbilityCompiler.Compile(result, cache: false);
        return result;

        void Reflect(float[] point)
        {
            if (point != null) point[1] = -point[1];
        }
    }

    /// <summary>换用器物资料中已存在的握法，同一关系继续连接所选手与真实装备。</summary>
    internal static AbilityDefinition ChangeGrip(AbilityDefinition source, string grip)
    {
        AbilityDefinition result = ExplicitRelations(source);
        if (result.Body.WeaponForm == null || !FormResources.Load(result.Body.WeaponForm).Description.Grips.Any(option => option.Id == grip))
            throw new InvalidDataException("所选握法不在实际器物资料中");
        result.Body.Construction.Relations.Single(relation => relation.Kind == BodyRelationKind.Grip).ToAnchor = grip;
        result.Body.Grip = grip;
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }

    /// <summary>修改既有普通接触；不自动添加、移除或替换其他关系。</summary>
    internal static AbilityDefinition ChangeTouch(AbilityDefinition source, string id, Action<BodyRelationDefinition> edit)
    {
        AbilityDefinition result = ExplicitRelations(source);
        BodyRelationDefinition touch = result.Body.Construction.Relations.Single(relation => relation.Kind == BodyRelationKind.Touch && relation.Id == id);
        edit(touch);
        AbilityCompiler.Compile(result, cache: false);
        return result;
    }
}
