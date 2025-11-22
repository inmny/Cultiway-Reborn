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
}