using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.AIGCLib;

public class SkillNameGenerator : EntityNameGenerator<SkillNameGenerator>
{
    protected override string NameDictPath { get; } =
    Path.Combine(Application.persistentDataPath, "Cultiway_SkillNameDict.json");
    protected override string GetSystemPrompt()
    {
        return "为用户提供的法术生成名称，要求符合玄幻风格，必须简洁（2-6个字），优先表达主机制，不要完整堆叠所有词条。仅给出一个中文名称，不要有任何符号、解释或换行。\nInput example:\n本体=雷丸；元素=lightning；形态=ball；词条=爆炸、冰冻\nOutput example:\n霜爆雷";
    }

    protected override string GetDefaultName(string[] param)
    {
        return SkillRuleNameComposer.ComposeFallback(param);
    }

    protected override string GetPrompt(string[] param)
    {
        var sb = new StringBuilder();
        sb.Append("为产生“");
        sb.Append(param[0]);
        sb.Append("”的法术，请生成一个名称。这个法术有以下词条：");
        foreach (var modifier in param)
        {
            sb.Append(modifier);
            sb.Append(';');
        }

        sb.Remove(sb.Length - 1);
        return sb.ToString();
    }

    protected override bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = PostProcess(name);
        if (name.Length is < 2 or > 6) return false;
        if (name.Contains("名字", StringComparison.Ordinal) ||
            name.Contains("名称", StringComparison.Ordinal) ||
            name.Contains("法术名", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch)) return false;
            if (!IsCjk(ch)) return false;
        }

        return true;
    }

    protected override string PostProcess(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var text = name.Trim();
        var lineBreakIndex = text.IndexOfAny(new[] { '\r', '\n' });
        if (lineBreakIndex >= 0)
        {
            text = text.Substring(0, lineBreakIndex).Trim();
        }

        var colonIndex = text.IndexOfAny(new[] { ':', '：' });
        if (colonIndex >= 0 && colonIndex < text.Length - 1)
        {
            text = text.Substring(colonIndex + 1).Trim();
        }

        text = text.Trim('\'', '"', '“', '”', '《', '》', '。', '，', ',', '、', '`', ' ');

        var start = -1;
        var end = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsCjk(text[i])) continue;
            if (start < 0) start = i;
            end = i;
        }

        return start < 0 ? string.Empty : text.Substring(start, end - start + 1);
    }

    public void GenerateFor(Entity skill_container_entity)
    {
        if (skill_container_entity.IsNull || !skill_container_entity.HasComponent<SkillContainer>())
        {
            return;
        }

        var context = SkillRuleNameComposer.CreateContext(skill_container_entity);
        if (context == null)
        {
            return;
        }

        ApplyName(skill_container_entity, SkillRuleNameComposer.Compose(context));

        _ = GenerateAiNameAsync(context, skill_container_entity);
    }

    public string GenerateRuleFor(Entity skillContainerEntity)
    {
        var context = SkillRuleNameComposer.CreateContext(skillContainerEntity);
        if (context == null) return string.Empty;

        var name = SkillRuleNameComposer.Compose(context);
        ApplyName(skillContainerEntity, name);
        return name;
    }

    public void RequestAiNameFor(Entity skillContainerEntity)
    {
        var context = SkillRuleNameComposer.CreateContext(skillContainerEntity);
        if (context == null) return;
        _ = GenerateAiNameAsync(context, skillContainerEntity);
    }

    public void RequestAiNameFor(Entity skillContainerEntity, Action<string> completed)
    {
        var context = SkillRuleNameComposer.CreateContext(skillContainerEntity);
        if (context == null)
        {
            completed(null);
            return;
        }
        _ = GenerateAiNameAsync(context, skillContainerEntity, completed);
    }

    private async Task GenerateAiNameAsync(SkillNamingContext context, Entity target, Action<string> completed = null)
    {
        if (target.IsNull)
        {
            completed?.Invoke(null);
            return;
        }

        var key = context.StoreKey;
        string name = null;

        if (RequestNewName(key))
        {
            try
            {
                var res = await Manager.RequestResponseContent(SkillRuleNameComposer.BuildPrompt(context),
                    GetSystemPrompt(), temperature: 0.7f);
                res = PostProcess(res);
                if (!string.IsNullOrEmpty(res) && IsValid(res))
                {
                    lock (NameDict)
                    {
                        if (NameDict.TryGetValue(key, out var names))
                        {
                            names.Add(res);
                        }
                        else
                        {
                            NameDict[key] = new List<string> { res };
                        }

                        Save();
                    }

                    name = res;
                }
            }
            catch (Exception e)
            {
                ModClass.LogErrorConcurrent(e.ToString());
            }
        }
        else
        {
            name = GetCachedName(key);
        }

        if (string.IsNullOrEmpty(name) || target.IsNull || SkillContainerSignature.Build(target) != context.Signature)
        {
            completed?.Invoke(null);
            return;
        }

        EventSystemHub.Publish(new EntityNameGeneratedEvent
        {
            Target = target,
            Name = name
        });
        completed?.Invoke(name);
    }

    private string GetCachedName(string key)
    {
        lock (NameDict)
        {
            if (!NameDict.TryGetValue(key, out var names)) return null;
            for (var i = 0; i < names.Count; i++)
            {
                var name = PostProcess(names[i]);
                if (IsValid(name)) return name;
            }
        }

        return null;
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff';
    }

    private static void ApplyName(Entity target, string name)
    {
        if (target.HasName)
        {
            target.GetComponent<EntityName>().value = name;
        }
        else
        {
            target.AddComponent(new EntityName(name));
        }
    }
}
