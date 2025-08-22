namespace Cultiway.Abstract;
/// <summary>
/// 只有<see cref="DynamicAssetLibrary{T}"/>的dynamic成员才会生效
/// </summary>
public interface IDeleteWhenUnknown
{
    public int Current { get; set; }
}