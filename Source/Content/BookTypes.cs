using Cultiway.Abstract;

namespace Cultiway.Content;

public class BookTypes : ExtendLibrary<BookTypeAsset, BookTypes>
{
    public static BookTypeAsset Cultibook { get; private set; }
    public static BookTypeAsset Skillbook { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        Cultibook.requirement_check = (_, _) => false;
        Skillbook.requirement_check = (_, _) => false;
    }
}