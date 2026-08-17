using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cultiway.Core.ControlledTasks;

/// <summary>受控任务命令资产库，集中保证命令唯一性、底层任务引用和稳定展示顺序。</summary>
public sealed class ControlledTaskCommandLibrary : AssetLibrary<ControlledTaskCommandAsset>
{
    private readonly ReadOnlyCollection<ControlledTaskCommandAsset> commands;

    public IReadOnlyList<ControlledTaskCommandAsset> Commands => commands;
    public int Revision { get; private set; }

    public ControlledTaskCommandLibrary()
    {
        commands = list.AsReadOnly();
    }

    public override ControlledTaskCommandAsset add(ControlledTaskCommandAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (has(asset.id))
            throw new InvalidOperationException($"Controlled task command asset '{asset.id}' is already registered.");
        return base.add(asset);
    }

    public override void post_init()
    {
        // Content 的 ExtendLibrary.OnInit 已完成字段填写，此处再统一建立 Library 不变量。
        for (var i = 0; i < list.Count; i++) Validate(list[i]);
        list.Sort(Compare);
        Revision++;
        base.post_init();
    }

    public bool TryGet(string id, out ControlledTaskCommandAsset asset)
    {
        asset = null;
        return !string.IsNullOrEmpty(id) && dict.TryGetValue(id, out asset);
    }

    private static void Validate(ControlledTaskCommandAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (string.IsNullOrWhiteSpace(asset.id))
            throw new InvalidOperationException("Controlled task command asset requires an id.");
        if (asset.Task == null || string.IsNullOrEmpty(asset.Task.id) ||
            !ReferenceEquals(AssetManager.tasks_actor.get(asset.Task.id), asset.Task))
            throw new InvalidOperationException(
                $"Controlled task command asset '{asset.id}' references an unregistered actor task.");
        if (string.IsNullOrWhiteSpace(asset.NameLocaleKey) ||
            string.IsNullOrWhiteSpace(asset.DescriptionLocaleKey))
            throw new InvalidOperationException(
                $"Controlled task command asset '{asset.id}' requires name and description locale keys.");
        if (string.IsNullOrEmpty(asset.IconPath)) asset.IconPath = "ui/icons/iconShowTasks";
        if (asset.TargetMode is not ControlledTaskTargetMode.None and not ControlledTaskTargetMode.WorldTile)
            throw new InvalidOperationException(
                $"Controlled task command asset '{asset.id}' has unsupported target mode '{asset.TargetMode}'.");
        if (asset.TargetMode == ControlledTaskTargetMode.WorldTile && asset.ApplyWorldTileContext == null)
            throw new InvalidOperationException(
                $"World-tile controlled task command asset '{asset.id}' requires a target context writer.");
        if (asset.TargetMode == ControlledTaskTargetMode.None && asset.ApplyWorldTileContext != null)
            throw new InvalidOperationException(
                $"No-target controlled task command asset '{asset.id}' cannot define a tile context writer.");
    }

    private static int Compare(ControlledTaskCommandAsset left, ControlledTaskCommandAsset right)
    {
        int category = left.Category.CompareTo(right.Category);
        if (category != 0) return category;
        int order = left.Order.CompareTo(right.Order);
        return order != 0 ? order : string.CompareOrdinal(left.id, right.id);
    }
}
