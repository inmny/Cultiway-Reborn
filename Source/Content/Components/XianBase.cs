using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct XianBase : IComponent
{
    /// <summary>从真气谱系继承并由三花五气持续塑造的仙基成果。</summary>
    public CoreFormationSnapshot formation;

    public float jing;
    public float qi;
    public float shen;

    public float iron;
    public float wood;
    public float water;
    public float fire;
    public float earth;

    public float GetStrength()
    {
        return formation.IsValid
            ? formation.strength
            : (GetThreeHuaStrength() + GetFiveQiStrength()) / 2;
    }

    public float GetThreeHuaStrength()
    {
        return (jing + qi + shen) / 3;
    }

    public float GetFiveQiStrength()
    {
        return (iron + wood + water + fire + earth) / 5;
    }

    /// <summary>复制仙基成果内部数组，避免传承后的角色共享可变快照。</summary>
    public readonly XianBase DeepClone()
    {
        var clone = this;
        clone.formation = formation.DeepClone();
        return clone;
    }
}
