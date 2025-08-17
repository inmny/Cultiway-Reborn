using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public abstract class ExtendComponent<TBase>
{
    public virtual TBase Base { get; }
    public virtual Entity E { get; }
    public bool HasComponent<T>() where T : struct, IComponent
    {
        return E.HasComponent<T>();
    }

    public ref T GetComponent<T>() where T : struct, IComponent
    {
        return ref E.GetComponent<T>();
    }

    public void AddComponent<T>(T component = default) where T : struct, IComponent
    {
        E.AddComponent(component);
    }
}