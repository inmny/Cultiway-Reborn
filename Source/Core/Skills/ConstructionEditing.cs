using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Skills;

/// <summary>目录及人物法术页使用的简要资料，不为每行复制完整构造。</summary>
public readonly struct ConstructionEntry
{
    /// <summary>稳定编号和实际显示名称。</summary>
    public readonly string Id, Name;
    /// <summary>当前修订及展开形体数。</summary>
    public readonly int Revision, Forms;
    /// <summary>当前世界的实际创造者，零表示内容提供的构造。</summary>
    public readonly long Creator;

    /// <summary>保留当前目录事实。</summary>
    /// <param name="id">构造编号。</param>
    /// <param name="name">作者命名。</param>
    /// <param name="revision">修订。</param>
    /// <param name="forms">展开形体数。</param>
    /// <param name="creator">实际创造者。</param>
    public ConstructionEntry(string id, string name, int revision, int forms, long creator)
    { Id = id; Name = name; Revision = revision; Forms = forms; Creator = creator; }
}

/// <summary>编辑使用的读写入口；内容层负责正式学习、保存和冷却，公共界面不引用具体玩法。</summary>
public interface IConstructionEditingAccess
{
    /// <summary>世界正在切换，界面必须释放人物和草稿。</summary>
    event Action WorldClearing;
    /// <summary>读取世界目录，或某个人真正掌握的构造。</summary>
    /// <param name="actor">人物；为空时读取世界目录。</param>
    IReadOnlyList<ConstructionEntry> List(ActorExtend actor = null);
    /// <summary>复制一份完整的独立草稿。</summary>
    /// <param name="id">构造编号。</param>
    AbilityDefinition Read(string id);
    /// <summary>将已读出的分享构造登记到本世界；相同资料复用原编号，不授予人物。</summary>
    /// <param name="definition">确认导入的独立构造。</param>
    bool Import(AbilityDefinition definition);
    /// <summary>使用正式来源的完整编译及资源支付规则检查草稿，不登记世界资料。</summary>
    /// <param name="definition">独立草稿。</param>
    CompiledAbility Validate(AbilityDefinition definition);
    /// <summary>查看人物当前的真实报价，空值表示这时无法支付。</summary>
    /// <param name="actor">选择的学习者。</param><param name="plan">窗口当前实际通过的计划。</param>
    IReadOnlyList<SkillCastPaymentEntry> PreviewPayment(ActorExtend actor, CompiledAbility plan);
    /// <summary>取没有选定目标时的施放距离；具体人物及法宝可改变实际距离。</summary>
    /// <param name="actor">实际学习者。</param><param name="plan">已经通过的计划。</param>
    float PreviewRange(ActorExtend actor, CompiledAbility plan);
    /// <summary>当前人物实际提供的三轴部位与命名足，只返回稳定名称，不创建替代几何。</summary>
    /// <param name="actor">当前选择的实际人物。</param>
    (string[] Effectors, string[] Feet) PartChoices(ActorExtend actor);
    /// <summary>检查并授予独立修订，可替换此人已学的一份；失败不改变学习、世界或冷却。</summary>
    /// <param name="actor">学习者。</param>
    /// <param name="edit">独立草稿及打开时的完整资料。</param>
    /// <param name="replace">是否替换此人的原掌握关系。</param>
    AbilityDefinition Apply(ActorExtend actor, ConstructionEditSession edit, bool replace);
}

/// <summary>保存一次编辑的完整来源版本；相同修订号也不能掩盖后来被替换的资料。</summary>
public sealed class ConstructionEditSession
{
    /// <summary>原构造稳定编号。</summary>
    public string SourceId { get; }
    /// <summary>打开时的修订。</summary>
    public int SourceRevision { get; }
    /// <summary>唯一可修改的独立草稿。</summary>
    public AbilityDefinition Draft;
    /// <summary>打开时的完整资料，不向界面暴露可变引用。</summary>
    private readonly string original;

    /// <summary>从正式副本建立草稿。</summary>
    /// <param name="source">当前正式构造的副本。</param>
    /// <param name="sourceChanges">在玩家开始编辑前应用的来源变化。</param><param name="defaults">仅填缺项的默认资料。</param>
    public ConstructionEditSession(AbilityDefinition source, JObject sourceChanges = null, JObject defaults = null)
    {
        SourceId = source.Id; SourceRevision = source.Revision;
        original = ConstructionDefinitionLayers.Snapshot(source).ToString(Formatting.None);
        Draft = ConstructionDefinitionLayers.Resolve(JObject.Parse(original), sourceChanges, defaults: defaults);
    }

    /// <summary>当前资料是否仍等于打开时的完整版本。</summary>
    /// <param name="current">当前正式资料或草稿。</param>
    public bool Matches(AbilityDefinition current) => original == ConstructionDefinitionLayers.Snapshot(current).ToString(Formatting.None);

    /// <summary>玩家已经确认的完整草稿作为最终选择，不能在保存时重新套用来源变化。</summary>
    /// <param name="defaults">仅可填真正缺项的默认资料。</param>
    public AbilityDefinition Resolve(JObject defaults = null) => ConstructionDefinitionLayers.Resolve(ConstructionDefinitionLayers.Snapshot(Draft), defaults: defaults);
}

/// <summary>共用构造编辑入口与资料复制，具体世界事实由内容层登记。</summary>
public static class ConstructionEditing
{
    /// <summary>包装根结构时，同时改写命名步骤的实际部位和绝对事件来源。</summary>
    /// <param name="definition">仍以原主体为根的独立草稿。</param><param name="rootPath">原主体在新结构中的完整路径。</param>
    /// <param name="stepPrefix">复制时的独立步骤名称前缀，普通包装为空。</param>
    internal static void RebaseReferences(AbilityDefinition definition, string rootPath, string stepPrefix = "")
    {
        RebaseEventSources(definition.Root, rootPath);
        foreach (ConstructionStep step in definition.Steps)
        {
            step.Id = stepPrefix + step.Id;
            foreach (ConstructionOutputReference input in step.Inputs.Values)
            {
                input.Node = RebasePath(input.Node, rootPath);
                if (input.Step != null) input.Step = stepPrefix + input.Step;
            }
            if (step.Operation.When != null)
                step.Operation.When.Source = RebasePath(step.Operation.When.Source, rootPath);
        }
    }

    /// <summary>只改写原根的绝对路径，身体和相对引用仍由真实目标解析。</summary>
    /// <param name="path">原引用。</param><param name="rootPath">新根位置。</param>
    private static string RebasePath(string path, string rootPath) => path == "root" || path?.StartsWith("root/", StringComparison.Ordinal) == true ? rootPath + path.Substring(4) : path;

    /// <summary>包装原根部时，明确来源仍指向原主体，不能意外改指空组织支点。</summary>
    /// <param name="node">被包装的完整子结构。</param>
    /// <param name="rootPath">原主体在新结构内的完整路径。</param>
    internal static void RebaseEventSources(ConstructionNode node, string rootPath)
    {
        if (node.Activation != null) node.Activation.Source = RebasePath(node.Activation.Source, rootPath);
        foreach (ConstructionOperation operation in node.Operations)
            if (operation.When != null) operation.When.Source = RebasePath(operation.When.Source, rootPath);
        foreach (ConstructionNode child in node.Children) RebaseEventSources(child, rootPath);
    }

    /// <summary>当前游戏提供的正式读写入口。</summary>
    public static IConstructionEditingAccess Access { get; internal set; }

    /// <summary>复制可保存资料；不携带共享执行计划或正在施放的状态。</summary>
    /// <param name="definition">草稿或正式资料。</param>
    public static AbilityDefinition Copy(AbilityDefinition definition)
        => JsonConvert.DeserializeObject<AbilityDefinition>(JsonConvert.SerializeObject(definition));
}
