using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门权限资产；用于声明角色可以执行的宗门行为。
/// </summary>
public class SectPermissionAsset : Asset
{
    /// <summary>
    /// 权限名称的本地化 key。
    /// </summary>
    public string nameKey;

    /// <summary>
    /// 权限说明的本地化 key。
    /// </summary>
    public string descriptionKey;

    /// <summary>
    /// 权限在 UI 中使用的图标路径。
    /// </summary>
    public string iconPath;

    /// <summary>
    /// 权限分组标识，用于 UI 归类展示。
    /// </summary>
    public string group;

    /// <summary>
    /// 获取权限的本地化名称。
    /// </summary>
    public string GetName()
    {
        return (string.IsNullOrEmpty(nameKey) ? id : nameKey).Localize();
    }

    /// <summary>
    /// 获取权限的本地化说明。
    /// </summary>
    public string GetDescription()
    {
        return (string.IsNullOrEmpty(descriptionKey) ? $"{id}.Info" : descriptionKey).Localize();
    }
}
