using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 法器炼成后保留的语义原子及其贡献强度。
/// </summary>
public struct ArtifactAtomData : IComponent
{
    public ArtifactAtomEntry[] entries = [];

    public ArtifactAtomData()
    {
    }

    public float GetStrength(string atomId)
    {
        ArtifactAtomEntry[] values = entries ?? [];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].atom_id == atomId) return values[i].strength;
        }
        return 0f;
    }

    public int GetCount()
    {
        return entries?.Length ?? 0;
    }
}

/// <summary>
/// 一个参与法器构成的 atom。强度由全部材料的匹配程度确定，不表示离散槽位。
/// </summary>
public struct ArtifactAtomEntry
{
    public string atom_id;
    public float strength;
}
