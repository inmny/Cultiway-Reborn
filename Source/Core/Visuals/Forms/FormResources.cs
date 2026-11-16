using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>按版本加载形体，重载不会替换已经施放的资源。</summary>
public static class FormResources
{
    /// <summary>当前已发布资源组。</summary>
    private static FormResourceGeneration current;
    /// <summary>已经停用但可能仍被施放或预览引用的旧组。</summary>
    private static readonly List<FormResourceGeneration> retired = new();
    /// <summary>每个原生对象实际所属的组数，避免共享对象被某一组提前销毁。</summary>
    private static readonly Dictionary<int, (UnityEngine.Object Asset, int Count)> ownership = new();
    /// <summary>本次运行内唯一递增版本，失败版本也不复用。</summary>
    private static int sequence;
    /// <summary>自动回收只定期检查旧组，不在每帧扫描资源。</summary>
    private static float nextCollection;
    /// <summary>当前组按需建立，退出后的重新初始化同样取得新版本。</summary>
    private static FormResourceGeneration Current => current ??= CreateResources();
    /// <summary>当前运行资源版本，不写入世界存档。</summary>
    public static int Version => Current.Version;

    /// <summary>当前版本的纯显示蒸散曲面。</summary>
    internal static FormResource Steam => Current.Steam;

    /// <summary>当前资源版本的真实范围反馈球体。</summary>
    internal static FormResource ContactVolume => Current.ContactVolume;

    /// <summary>新资源检查通过并已发布，目录、预览和来源缓存可更新。</summary>
    public static event Action<int> Reloaded;
    /// <summary>提交前用新资源检查已经登记的正式构造，失败时整组回退。</summary>
    internal static event Action ValidatingReload;
    /// <summary>某一版本已经释放，相关编译缓存应移除。</summary>
    internal static event Action<int> VersionReleased;
    /// <summary>列出已经发布的资源集合，失败重载不会暴露磁盘上的半成品。</summary>
    public static IReadOnlyList<string> Ids => Current.Ids;

    /// <summary>首次加载也固定完整资源组，失败重载后不再从已修改的磁盘补读旧版本。</summary>
    private static FormResourceGeneration CreateResources()
    {
        var generation = new FormResourceGeneration(++sequence);
        try { LoadAll(generation); return generation; }
        catch { generation.Dispose(); throw; }
    }

    /// <summary>在发布边界读取完整形体和默认表面，固定本版本的可用资源集合。</summary>
    /// <param name="generation">尚未发布的资源组。</param>
    private static void LoadAll(FormResourceGeneration generation)
    {
        generation.CreateReactionForms();
        foreach (string id in Directory
                     .GetFiles(Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Forms"), "*.json")
                     .Select(Path.GetFileNameWithoutExtension).OrderBy(id => id, StringComparer.Ordinal))
        {
            FormResource resource = generation.Load(id);
            foreach (FormPartDescription part in resource.Description.Parts)
            { generation.Surface(part.Surface, true); generation.Surface(part.Surface, false); }
        }
        generation.Complete();
    }

    /// <summary>取得当前版本的共用资源。</summary>
    /// <param name="id">实际形体编号。</param>
    public static FormResource Load(string id) => Current.Load(id);

    /// <summary>在编译边界验证当前版本的表面，显示从具体资源取得同组材质。</summary>
    /// <param name="surface">实际表面编号。</param>
    /// <param name="native">是否原版投影。</param>
    public static Material Surface(string surface, bool native) => Current.Surface(surface, native);

    /// <summary>先验证全部资源及正式构造，再发布新组；失败保留当前组和正在施放的旧组。</summary>
    public static void Reload()
    {
        FormResourceGeneration previous = Current;
        previous.CloseBundle();
        var candidate = new FormResourceGeneration(++sequence);
        current = candidate;
        try
        {
            LoadAll(candidate);
            ValidatingReload?.Invoke();
        }
        catch
        {
            current = previous; candidate.Dispose(); throw;
        }
        previous.Retire(); retired.Add(previous);
        Reloaded?.Invoke(candidate.Version);
        CollectUnused();
    }

    /// <summary>从主线程回收已经没有形体使用者的旧版本，不强制触发托管回收。</summary>
    public static void CollectUnused()
    {
        for (int i = retired.Count - 1; i >= 0; i--)
            if (!retired[i].IsReferenced) { retired[i].Dispose(); retired.RemoveAt(i); }
    }

    /// <summary>每秒检查一次旧组，显示空闲时也可释放已经结束的施放资源。</summary>
    internal static void UpdateRetired()
    {
        if (retired.Count == 0 || Time.realtimeSinceStartup < nextCollection) return;
        nextCollection = Time.realtimeSinceStartup + 1f; CollectUnused();
    }

    /// <summary>退出或完整停用时释放所有组；之后新请求会重新建立资源。</summary>
    public static void Shutdown()
    {
        current?.Dispose(); current = null;
        foreach (FormResourceGeneration generation in retired) generation.Dispose();
        retired.Clear(); nextCollection = 0f;
    }

    /// <summary>登记一个组对实际对象的拥有，原生对象即使被引擎共享也不会提前释放。</summary>
    /// <param name="asset">已经加载的模型、材质或表面对象。</param>
    internal static void Retain(UnityEngine.Object asset)
    {
        int id = asset.GetInstanceID();
        ownership[id] = ownership.TryGetValue(id, out var entry) ? (asset, entry.Count + 1) : (asset, 1);
    }

    /// <summary>最后一个组离开后才交由Unity销毁，不能在工作线程调用。</summary>
    /// <param name="asset">这一组不再使用的原生对象。</param>
    internal static void Release(UnityEngine.Object asset)
    {
        int id = asset.GetInstanceID(); var entry = ownership[id];
        if (entry.Count > 1) ownership[id] = (entry.Asset, entry.Count - 1);
        else { ownership.Remove(id); UnityEngine.Object.Destroy(entry.Asset); }
    }

    /// <summary>释放失败或退出版本时同步移除其编译缓存。</summary>
    /// <param name="version">实际已经释放的版本。</param>
    internal static void NotifyReleased(int version) => VersionReleased?.Invoke(version);
}
