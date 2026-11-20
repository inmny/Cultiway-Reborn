using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在编译时展开真实碎片，战斗中不创建新计划或读取几何。</summary>
public static partial class AbilityCompiler
{
    private static readonly CompiledFlight interruptedFall = new(new ConstructionOperation
    {
        WorldVelocity = true,
        Acceleration = new[] { 0f, 0f, -16f },
        Ground = ConstructionGroundResponse.Stop
    }, Vector3.zero, "中断落物");

    /// <summary>预留原主体的无作用落物，保持普通部位和真实碎解产物的索引。</summary>
    private static void AppendInterruptedFalls(float lifetime, List<CompiledForm> nodes)
    {
        int count = nodes.Count;
        for (int i = 0; i < count; i++)
        {
            CompiledForm source = nodes[i];
            if (source.InterruptFallSeconds <= 0f) continue;
            string path = source.Path + "/$interrupt_fall";
            if (nodes.Count >= MaxForms) Fail(path, "中断落物也计入128个部位上限");
            source.InterruptFall = nodes.Count;
            nodes.Add(new CompiledForm
            {
                Path = path, Parent = i, FragmentSource = i, Resource = source.Resource, IsInterruptFall = true,
                Anchor = Matrix4x4.identity, Rotation = Quaternion.identity, Scale = 1f,
                Presence = new Vector2(0f, source.InterruptFallSeconds),
                Hold = new CompiledConstructionHold(null, lifetime, path),
                Surface = source.Surface, Matter = source.Matter, FollowMatter = source.FollowMatter,
                Dye = source.Dye, ContactRole = ConstructionContactRole.Decoration,
                SourceLoss = ConstructionSourceLoss.Continue, Flight = interruptedFall
            });
        }
    }

    /// <summary>为原主体预留有限分件；这些部位只在真实碎解时开始。</summary>
    /// <param name="source">仍持有完整分件的原主体。</param>
    /// <param name="sourceIndex">原主体索引。</param>
    /// <param name="lifetime">整次存在上限。</param>
    /// <param name="output">同一计划的有限部位。</param>
    private static void AppendFragments(CompiledForm source, int sourceIndex, float lifetime, List<CompiledForm> output)
    {
        CompiledFracture fracture = source.Fracture;
        if (fracture == null) return;
        if (output.Count + fracture.Active.Length > MaxForms) Fail(source.Path, "碎解分件也计入128个部位上限");
        fracture.Pieces = new int[fracture.Active.Length];
        for (int part = 0; part < fracture.Pieces.Length; part++)
        {
            fracture.Pieces[part] = output.Count;
            output.Add(new CompiledForm
            {
                Path = source.Path + "/$piece/" + source.Resource.Description.Parts[part].Id,
                Parent = sourceIndex, FragmentSource = sourceIndex, Resource = source.Resource.Part(part),
                Anchor = Matrix4x4.identity, Rotation = Quaternion.identity, Scale = 1f,
                Presence = new Vector2(0f, fracture.Seconds), Active = new Vector2(0f, fracture.Seconds),
                Hold = new CompiledConstructionHold(null, lifetime, source.Path + "/碎片维持"),
                Surface = source.Surface, Matter = source.Matter, FollowMatter = source.FollowMatter,
                Dye = source.Dye, MediumChannel = source.MediumChannel,
                ContactKey = source.ContactKey, ContactPolicy = source.ContactPolicy, Impact = source.Impact,
                ContactRole = fracture.Active[part] ? source.ContactRole : ConstructionContactRole.Decoration,
                Strength = fracture.Active[part] ? source.Strength / fracture.ActiveCount : 0f,
                OnContact = source.OnContact, Interception = source.Interception,
                ReflectionLimit = source.ReflectionLimit,
                SourceLoss = fracture.SourceLoss ?? source.SourceLoss,
                Flight = fracture.Flight
            });
        }
    }
}
