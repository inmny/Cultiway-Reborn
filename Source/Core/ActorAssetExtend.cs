using System;
using System.Collections.Generic;
using Cultiway.Core.Libraries;

namespace Cultiway.Core;

public class ActorAssetExtend
{
    /// <summary>
    ///     是否必然有灵根
    /// </summary>
    public bool must_have_element_root;
    /// <summary>
    ///     是否站着睡觉
    /// </summary>
    public bool sleep_standing_up;
    /// <summary>
    ///     隐藏手上物品
    /// </summary>
    public bool hide_hand_item;
    /// <summary>
    ///     国王皮肤（只有非null时才生效）
    /// </summary>
    public string[] skin_king;
    /// <summary>
    ///     领袖皮肤（只有非null时才生效）
    /// </summary>
    public string[] skin_leader;
    /// <summary>
    ///     手动指定该生物会掉落的特殊物品形态。最终候选会继续叠加 ItemShape 自己的掉落特征判断。
    /// </summary>
    public ActorDropItemShapeSet drop_item_shapes = new();
}

public class ActorDropItemShapeSet
{
    private readonly List<ItemShapeAsset> manual_shapes = new();
    private readonly List<string> manual_shape_ids = new();
    private readonly HashSet<string> manual_shape_id_set = new(StringComparer.Ordinal);
    private ItemShapeAsset[] cached_shapes = [];
    private int cached_library_fingerprint;
    private string cached_owner_id;
    private bool dirty = true;

    public void Add(params ItemShapeAsset[] shapes)
    {
        if (shapes == null) return;
        for (var i = 0; i < shapes.Length; i++)
        {
            var shape = shapes[i];
            if (shape != null && !string.IsNullOrEmpty(shape.id) && manual_shape_id_set.Add(shape.id))
            {
                manual_shapes.Add(shape);
                dirty = true;
            }
        }
    }

    public void AddIds(params string[] shape_ids)
    {
        if (shape_ids == null) return;
        for (var i = 0; i < shape_ids.Length; i++)
        {
            if (!string.IsNullOrEmpty(shape_ids[i]) && manual_shape_id_set.Add(shape_ids[i]))
            {
                manual_shape_ids.Add(shape_ids[i]);
                dirty = true;
            }
        }
    }

    public bool IsDirty(ActorAsset owner, ItemShapeLibrary shape_library)
    {
        return dirty ||
               cached_owner_id != owner?.id ||
               cached_library_fingerprint != GetLibraryFingerprint(shape_library);
    }

    public IReadOnlyList<ItemShapeAsset> GetShapes(ActorAsset owner, ItemShapeLibrary shape_library)
    {
        if (IsDirty(owner, shape_library))
        {
            Rebuild(owner, shape_library);
        }
        return cached_shapes;
    }

    private void Rebuild(ActorAsset owner, ItemShapeLibrary shape_library)
    {
        List<ItemShapeAsset> result = new();
        HashSet<string> added = new(StringComparer.Ordinal);

        void Add(ItemShapeAsset shape)
        {
            if (shape == null || string.IsNullOrEmpty(shape.id) || !added.Add(shape.id)) return;
            result.Add(shape);
        }

        foreach (var shape in manual_shapes)
        {
            Add(shape);
        }

        if (shape_library != null)
        {
            foreach (var shape_id in manual_shape_ids)
            {
                Add(shape_library.get(shape_id));
            }

            foreach (var shape in shape_library.list)
            {
                if (shape.CanDropFrom(owner))
                {
                    Add(shape);
                }
            }
        }

        cached_shapes = result.ToArray();
        cached_owner_id = owner?.id;
        cached_library_fingerprint = GetLibraryFingerprint(shape_library);
        dirty = false;
    }

    private static int GetLibraryFingerprint(ItemShapeLibrary shape_library)
    {
        unchecked
        {
            var hash = 17;
            var list = shape_library?.list;
            if (list == null) return hash;
            hash = hash * 31 + list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                hash = hash * 31 + (list[i]?.id?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
