using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cultiway.Core.Persistence;

/// <summary>只用于明确登记的稳定数据记录，不遍历组件的资源属性和计算属性。</summary>
public static class StableSaveJson
{
    /// <summary>原版元资料沿用属性和默认值规则，不能把空自定义容器交给原版转换器。</summary>
    public static readonly JsonSerializerSettings NativeSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.None
    };
    /// <summary>稳定记录只写公共字段，并遵守两种序列化忽略标记。</summary>
    public static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new FieldsResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.None
    };

    /// <summary>从稳定记录选择普通字段；运行时属性不进入存档。</summary>
    private sealed class FieldsResolver : DefaultContractResolver
    {
        /// <summary>保留字段原名，并排除项目已有的临时字段标记。</summary>
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsDefined(typeof(Friflo.Json.Fliox.IgnoreAttribute), true))
                .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
    }
}
