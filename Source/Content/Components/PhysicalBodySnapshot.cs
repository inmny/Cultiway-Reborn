using System;
using Cultiway.Const;

namespace Cultiway.Content.Components;

/// <summary>只保存物质肉身和灵根稳定值的有界快照。</summary>
public struct PhysicalBodySnapshot
{
    /// <summary>人物形态资产编号。</summary>
    public string actor_asset_id;

    /// <summary>亚种稳定编号。</summary>
    public long subspecies_id;

    /// <summary>肉身性别。</summary>
    public ActorSex sex;

    /// <summary>头部外观编号。</summary>
    public int head;

    /// <summary>表型编号。</summary>
    public int phenotype_index;

    /// <summary>表型色阶。</summary>
    public int phenotype_shade;

    /// <summary>快照时肉身年龄。</summary>
    public int body_age;

    /// <summary>按编号稳定排序的肉身特质。</summary>
    public string[] body_trait_ids;

    /// <summary>八种灵根的连续构成。</summary>
    public float[] element_root_values;

    /// <summary>快照是否包含严格恢复所需全部值。</summary>
    public readonly bool IsValid =>
        !string.IsNullOrEmpty(actor_asset_id) && subspecies_id > 0L &&
        body_trait_ids != null && element_root_values?.Length == ElementIndex.Count;

    /// <summary>深拷贝内部数组，避免不同人物共享本相数据。</summary>
    /// <returns>不共享内部数组的新快照。</returns>
    public readonly PhysicalBodySnapshot DeepClone()
    {
        PhysicalBodySnapshot clone = this;
        clone.body_trait_ids = body_trait_ids == null ? null : (string[])body_trait_ids.Clone();
        clone.element_root_values = element_root_values == null ? null : (float[])element_root_values.Clone();
        return clone;
    }
}
