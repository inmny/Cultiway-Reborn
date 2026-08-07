namespace Cultiway.Core;

/// <summary>自定义地图模式的非像素渲染生命周期。</summary>
public interface ICustomMapModeRenderer
{
    void SetVisible(bool visible);

    void Update(float elapsed);

    void SetAllDirty();

    void ClearWorld();
}
