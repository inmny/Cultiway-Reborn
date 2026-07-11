namespace Cultiway.Core.Persistence;

/// <summary>
/// 已加载的强类型持久化文档。
/// </summary>
public sealed class SaveDocument<TData>
{
    private readonly ModSaveManager _manager;
    internal SaveDocumentDefinition<TData> Definition { get; }

    public TData Data { get; internal set; }

    internal SaveDocument(ModSaveManager manager, SaveDocumentDefinition<TData> definition)
    {
        _manager = manager;
        Definition = definition;
    }

    public void Save()
    {
        _manager.Save(this);
    }
}
