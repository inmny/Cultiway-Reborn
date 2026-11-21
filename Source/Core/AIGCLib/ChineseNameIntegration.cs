#if CHINESENAME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chinese_Name.Naming;
using Cultiway.Utils;

namespace Cultiway.Core.AIGCLib;

/// <summary>把修仙法术的语义和种族命名包交给中文名。</summary>
internal static class ChineseNameIntegration
{
    /// <summary>法术在中文名规则和存档中的稳定编号。</summary>
    internal const string Spell = "cultiway:spell";

    /// <summary>在词素加载之后登记法术和东方人族规则。</summary>
    internal static void Register(string folder)
    {
        var service = NamingApi.Current;
        GeoRegions.GeoRegionNamingIntegration.Register();
        var category = new NamingCategory(new NameType(Spell, "修仙法术", [
            new("element", InputKind.Text, label: "元素", example: "semantic.element.fire"),
            new("form", InputKind.Text, label: "形态", example: "semantic.form.ball"),
            new("motion", InputKind.Text, label: "运动"),
            new("trajectory", InputKind.Text, label: "轨迹"),
            new("modifier", InputKind.Text, label: "主要词条"),
            new("base", InputKind.Text, label: "原型名称", example: "火丸")
        ], minLength: 2, maxLength: 6))
        {
            Defaults = SpellDefaults(),
            CultureIcon = "ui/Icons/iconFire",
            Description = "由创法者的文化决定排列，预览示例为火球。"
        };
        AddCondition(SkillNameAtomCategory.Element, "element", "元素");
        AddCondition(SkillNameAtomCategory.Form, "form", "形态");
        category.InputGlyph("cultiway:spell-element", '\uE240', "element", "法术元素",
            "ui/Icons/iconFire", "cultiway:spell-element", "按法术的实际元素和当前语言抽取词素。");
        category.InputGlyph("cultiway:spell-form", '\uE241', "form", "法术形态",
            "ui/Icons/iconMeteorite", "cultiway:spell-form", "按法术的实际形态和当前语言抽取词素。");
        service.RegisterCategory(category);
        var eastern = NamingJson.Read<NamingPack>(File.ReadAllText(
            Path.Combine(folder, "Content", "Naming", "eastern-human.naming.json")));
        service.RegisterDefaultPack(eastern);

        // 条件从法术现有的语义登记中读取，预览和实际取名使用相同的语义 ID。
        void AddCondition(SkillNameAtomCategory atomCategory, string field, string label)
        {
            var values = ModClass.L.SkillNameAtomLibrary.All
                .Where(atom => atom.category == atomCategory && atom.semantic != null)
                .Select(atom => atom.semantic)
                .GroupBy(semantic => semantic.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().GetName(), StringComparer.Ordinal);
            category.Values(field, label, values);
        }
    }

    /// <summary>根据法术配置构建请求。有创法者时使用其文化和语言，共享法术使用世界方案。</summary>
    internal static NameResult GenerateSpell(SkillNamingContext context, Actor namingActor)
    {
        var input = new Dictionary<string, object>();
        Add("element", context.ElementSemantic?.id);
        Add("form", context.FormSemantic?.id);
        Add("motion", context.MotionSemantic?.id);
        Add("trajectory", context.TrajectoryId);
        Add("modifier", SkillRuleNameComposer.PrimaryModifier(context)?.id.ToLowerInvariant());
        Add("base", context.BaseName);
        return NamingApi.Current.Generate(new NameRequest(Spell,
            unchecked((uint)NamingRuleUtils.StableHash(context.Signature)),
            namingActor?.culture?.id, namingActor?.language?.id, input));

        void Add(string field, string value)
        {
            if (!string.IsNullOrEmpty(value) && value.Length <= 256) input[field] = value;
        }
    }

    /// <summary>复用现有命名词素，为元素、形态、运动和词条建立可编辑的词库。</summary>
    private static NamingPack SpellDefaults()
    {
        var pack = new NamingPack { Format = 1, Id = "cultiway:spell-defaults", Label = "修仙法术", Supplemental = true };
        foreach (var atom in ModClass.L.SkillNameAtomLibrary.All)
        {
            string category = atom.category.ToString().ToLowerInvariant();
            string semantic = atom.category == SkillNameAtomCategory.Modifier ? atom.id.ToLowerInvariant() : atom.semantic?.id;
            if (string.IsNullOrEmpty(semantic)) continue;
            string pool = $"cultiway:spell-{category}.{semantic}";
            if (!pack.Pools.TryGetValue(pool, out var entries))
                pack.Pools[pool] = entries = new Dictionary<string, double>();
            foreach (var text in atom.name_stems.Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                string id = $"cultiway:stem.{unchecked((uint)NamingRuleUtils.StableHash(pool + "/" + text)):x8}";
                if (entries.ContainsKey(id)) continue;
                entries.Add(id, 1);
                pack.Words.Add(new Word { Id = id, Text = text });
            }
        }
        AddGroups("element", "fire", "火系法术词素", "火", "炎", "赤");
        AddGroups("element", "ice", "冰系法术词素", "冰", "霜", "玄冰");
        AddGroups("element", "water", "水系法术词素", "水", "寒", "玄水");
        AddGroups("element", "wind", "风系法术词素", "风", "岚", "罡");
        AddGroups("element", "lightning", "雷系法术词素", "雷", "霆", "电");
        AddGroups("form", "ball", "球状法术词素", "丸", "弹", "珠");
        AddGroups("form", "slash", "斩击法术词素", "斩", "刃", "锋");

        void AddGroups(string category, string key, string label, string plain, params string[] poetic)
        {
            string pool = $"cultiway:spell-{category}.semantic.{category}.{key}";
            if (!pack.Pools.TryGetValue(pool, out var entries)) return;
            pack.PoolAliases[label] = pool;
            pack.PoolLabels[pool] = label;
            AddGroup(key + "-plain", plain);
            AddGroup(key + "-poetic", poetic);

            void AddGroup(string id, params string[] stems)
            {
                var words = pack.Words.Where(word => entries.ContainsKey(word.Id) && stems.Contains(word.Text))
                    .Select(word => word.Id).ToList();
                if (words.Count > 0) pack.WordGroups[id] = new WordGroup { Pool = pool, Words = words };
            }
        }
        pack.Rules[Spell] =
        [
            new()
            {
                Id = "semantic", Priority = 10, Parts =
                [
                    Stem("element", true), Stem("modifier", true), Stem("motion", true, .3), Stem("form", false)
                ]
            },
            new()
            {
                Id = "elemental", Priority = 5, Parts = [Stem("element", false), new() { Id = "suffix", Literal = "术" }]
            },
            new() { Id = "original", Parts = [new() { Id = "base", Input = "base" }] }
        ];
        return pack;
    }

    /// <summary>按请求中指定的语义从对应词库抽取一个词素。</summary>
    private static Part Stem(string field, bool optional, double chance = 1) => new()
    {
        Id = field, Pool = "cultiway:spell-" + field, PoolInput = field, Optional = optional, Chance = chance
    };
}
#endif
