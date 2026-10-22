using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>将共用编辑接入实际人物掌握关系，原有施放及其他持有者保留原定义。</summary>
public sealed partial class ConstructedAbilityService : IConstructionEditingAccess
{
    /// <summary>切世界前解除编辑界面对人物的绑定。</summary>
    public event Action WorldClearing;

    /// <summary>只返回显示需要的简要资料，读取不会改变正式定义。</summary>
    /// <param name="actor">指定学习者，空值读取世界目录。</param>
    IReadOnlyList<ConstructionEntry> IConstructionEditingAccess.List(ActorExtend actor)
    {
        IEnumerable<string> ids = actor == null ? definitions.Keys :
            actor.E.TryGetComponent(out ConstructedKnowledge known) ? known.Ids : Array.Empty<string>();
        return ids.Where(definitions.ContainsKey).Select(id => new ConstructionEntry(id, definitions[id].Name,
            definitions[id].Revision, PlanFor(id).FormCount, definitions[id].Creator)).ToArray();
    }

    /// <summary>外部只能获得草稿副本，不能修改正式字典内的可变数组。</summary>
    /// <param name="id">已经存在的构造。</param>
    AbilityDefinition IConstructionEditingAccess.Read(string id) => ConstructionEditing.Copy(definitions[id]);

    /// <summary>界面预览与正式登记共同检查资料、材料、身体资源和实际支付。</summary>
    /// <param name="definition">当前独立草稿。</param>
    CompiledAbility IConstructionEditingAccess.Validate(AbilityDefinition definition) => ValidateDefinition(definition);

    /// <summary>先检查来源和人物、生成并编译独立修订，再一次交接学习关系与冷却。</summary>
    /// <param name="actor">真正学习者。</param>
    /// <param name="edit">独立草稿和打开时的完整版本。</param>
    /// <param name="replace">是否只替换此人的原掌握关系。</param>
    AbilityDefinition IConstructionEditingAccess.Apply(ActorExtend actor, ConstructionEditSession edit, bool replace)
    {
        if (actor?.Base?.data == null || !actor.Base.isAlive() || !actor.Base.hasHealth())
            throw new InvalidOperationException("学习者已经失效，请重新选择人物");
        if (edit == null || !definitions.TryGetValue(edit.SourceId, out AbilityDefinition original) || !edit.Matches(original))
            throw new InvalidOperationException("原法术已经改变，请重新打开当前修订");
        string sourceId = edit.SourceId; AbilityDefinition draft = edit.Draft;
        if (draft == null || string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Length > 128)
            throw new InvalidDataException("法术名称需要1至128个字符");
        string[] old = actor.E.TryGetComponent(out ConstructedKnowledge known) ? known.Ids : Array.Empty<string>();
        int at = Array.IndexOf(old, sourceId);
        if (replace && at < 0) throw new InvalidOperationException("此人已经不再掌握原法术，请重新打开人物法术页");
        if (!replace && old.Length >= 128) throw new InvalidOperationException("此人的构造掌握已达128项，仍可修改已学法术");
        AbilityDefinition next = ConstructionEditing.Copy(draft);
        next.Id = "Cultiway.Edited." + Guid.NewGuid().ToString("N");
        next.Creator = actor.Base.id; next.Revision = replace ? original.Revision + 1 : 1;
        string[] ids = replace ? (string[])old.Clone() : old.Concat(new[] { next.Id }).ToArray();
        if (replace) ids[at] = next.Id;
        Register(next);
        actor.E.AddComponent(new ConstructedKnowledge { Ids = ids });
        // 复制和替换均不绕过已经存在的恢复时间；正在运行的旧施放独立持有旧计划。
        if (at >= 0 && cooldowns.TryGetValue((actor.Base.id, sourceId), out double until))
        {
            cooldowns[(actor.Base.id, next.Id)] = until;
            if (replace) cooldowns.Remove((actor.Base.id, sourceId));
        }
        return ConstructionEditing.Copy(next);
    }
}
