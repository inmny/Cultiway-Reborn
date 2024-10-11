namespace Cultiway.Abstract;

public abstract class ExtendComponent<TBase>
{
    public virtual TBase Base { get; }
}