namespace Cultiway.Core;

public delegate void BookInstanceReadAction(Actor actor, Book book, BookTypeAsset book_type_asset);
public class BookTypeAssetExtend
{
    /// <summary>
    /// 自定义书封面图像名
    /// </summary>
    public string custom_cover_name;
    /// <summary>
    /// 阅读某个书实例的效果
    /// </summary>
    public BookInstanceReadAction instance_read_action;
}