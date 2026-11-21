using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace Cultiway.Content.Compatibility.GodTools;

/// <summary>绑定 GodTools 的公开榜单接口。只在登记时查找类型，读榜时直接调用已绑定的委托。</summary>
internal sealed class GodToolsRankingApi
{
    private const string RegistrySlot = "GodTools.Rankings.Registry/v1";
    private const string ApiNamespace = "GodTools.Features.Rankings.";
    private readonly Type groupType;
    private readonly Type sourceType;
    private readonly Type fieldType;
    private readonly Type boardType;
    private readonly Type fieldKind;
    private readonly Type valueType;
    private readonly MethodInfo registerGroup;
    private readonly MethodInfo registerSource;
    private readonly MethodInfo registerField;
    private readonly MethodInfo registerBoard;
    private readonly Func<double, string, string, object> numeric;
    private readonly Func<string, object> text;
    private readonly Func<string, string, double?, object> choice;
    private readonly Func<IReadOnlyDictionary<string, string>, object> tags;
    private readonly Action<string, string> showDescription;

    /// <summary>登记准备回调，兼容两个模组先后加载以及单独安装 Cultiway。</summary>
    /// <param name="register">GodTools 准备好后调用的登记入口。</param>
    internal static void WhenReady(Action<GodToolsRankingApi> register)
    {
        var domain = AppDomain.CurrentDomain;
        var published = domain.GetData(RegistrySlot);
        if (published is Type registry)
        {
            Connect(registry);
            return;
        }
        domain.SetData(RegistrySlot, (published as Action<Type>) + Connect);

        void Connect(Type ready)
        {
            try { register(new GodToolsRankingApi(ready)); }
            catch (Exception error) { ModClass.LogError($"登记天榜扩展失败：{error}"); }
        }
    }

    private GodToolsRankingApi(Type registry)
    {
        var assembly = registry.Assembly;
        groupType = assembly.GetType(ApiNamespace + "RankingGroup", true);
        sourceType = assembly.GetType(ApiNamespace + "RankingSource`1", true);
        fieldType = assembly.GetType(ApiNamespace + "RankingField`1", true);
        boardType = assembly.GetType(ApiNamespace + "RankingBoard", true);
        fieldKind = assembly.GetType(ApiNamespace + "RankingFieldKind", true);
        valueType = assembly.GetType(ApiNamespace + "RankingValue", true);
        registerGroup = Method(registry, "RegisterGroup");
        registerSource = Method(registry, "RegisterSource");
        registerField = Method(registry, "RegisterField");
        registerBoard = Method(registry, "RegisterBoard");
        numeric = Bind<Func<double, string, string, object>>(valueType, "Numeric");
        text = Bind<Func<string, object>>(valueType, "Text");
        choice = Bind<Func<string, string, double?, object>>(valueType, "Choice");
        tags = Bind<Func<IReadOnlyDictionary<string, string>, object>>(valueType, "TagSet");
        showDescription = Bind<Action<string, string>>(assembly.GetType("GodTools.UI.WindowRankings", true),
            "ShowEntryDescription");
    }

    /// <summary>登记 Cultiway 的天榜分组。</summary>
    internal void Group(string id, string label, int order) =>
        Register(registerGroup, groupType, id, ("Name", label), ("Order", order));

    /// <summary>登记可排序的对象来源和对象交互。</summary>
    internal void Source<T>(string id, Func<IEnumerable<T>> read, Func<T, string> key, Func<T, string> name,
        Func<T, Sprite> icon, Action<string> inspect, Func<string, WorldTile> locate = null,
        Func<string, NanoObject> metaPortrait = null) =>
        Register(registerSource.MakeGenericMethod(typeof(T)), sourceType.MakeGenericType(typeof(T)), id,
            ("Read", read), ("Key", key), ("Name", name), ("Icon", icon), ("Inspect", inspect),
            ("Locate", locate), ("MetaPortrait", metaPortrait));

    /// <summary>登记指定来源的指标，值读取委托按指标类型绑定一次。</summary>
    internal void Field<T>(string source, string id, string label, string kind, Func<T, object> read)
    {
        var item = Expression.Parameter(typeof(T));
        var adapted = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(T), valueType),
            Expression.Convert(Expression.Invoke(Expression.Constant(read), item), valueType), item).Compile();
        var definition = Definition(fieldType.MakeGenericType(typeof(T)), id, ("Name", label),
            ("Kind", Enum.Parse(fieldKind, kind)), ("Read", adapted), ("Owner", ModClass.I.GetDeclaration()));
        if (!(bool)registerField.MakeGenericMethod(typeof(T)).Invoke(null, new[] { source, definition }))
            throw new InvalidOperationException($"无法登记指标 {source} / {id}");
    }

    /// <summary>登记具有默认主指标和列顺序的榜单。</summary>
    internal void Board(string id, string label, string group, int order, string icon, params string[] columns) =>
        Register(registerBoard, boardType, id, ("SourceID", id), ("Name", label), ("GroupID", group),
            ("Order", order), ("IconPath", icon), ("Columns", columns), ("DefaultSort", columns[0]));

    /// <summary>创建原始数值指标，可覆盖其显示文字。</summary>
    internal object Number(double value, string label = null) => numeric(value, label, null);

    /// <summary>创建文本指标。</summary>
    internal object Text(string value) => text(value);

    /// <summary>创建带稳定筛选键的分类指标。</summary>
    internal object Choice(string id, string label, double? order = null) => choice(id, label, order);

    /// <summary>创建由多个稳定键组成的标签指标。</summary>
    internal object Tags(IReadOnlyDictionary<string, string> values) => tags(values);

    /// <summary>在天榜内显示对象介绍。</summary>
    internal void ShowDescription(string title, string description) => showDescription(title, description);

    private static MethodInfo Method(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static) ?? throw new MissingMethodException(type.FullName, name);

    private static T Bind<T>(Type type, string name) where T : Delegate =>
        (T)Method(type, name).CreateDelegate(typeof(T));

    private static void Register(MethodInfo method, Type type, string id, params (string Name, object Value)[] values)
    {
        if (!(bool)method.Invoke(null, new[] { Definition(type, id, values) }))
            throw new InvalidOperationException($"无法登记天榜项目 {id}");
    }

    private static object Definition(Type type, string id, params (string Name, object Value)[] values)
    {
        var definition = Activator.CreateInstance(type);
        type.GetProperty("ID").SetValue(definition, id);
        type.GetProperty("Owner")?.SetValue(definition, ModClass.I.GetDeclaration());
        foreach (var (name, value) in values) type.GetProperty(name).SetValue(definition, value);
        return definition;
    }
}
