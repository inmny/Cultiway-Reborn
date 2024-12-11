using Friflo.Engine.ECS;

namespace Cultiway.Content.CultisysComponents;

public struct XianBase : IComponent
{
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
        return (GetThreeHuaStrength() + GetFiveQiStrength()) / 2;
    }

    public float GetThreeHuaStrength()
    {
        return (jing + qi + shen) / 3;
    }

    public float GetFiveQiStrength()
    {
        return (iron + wood + water + fire + earth) / 5;
    }
}