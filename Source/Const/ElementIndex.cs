using System.Collections.ObjectModel;

namespace Cultiway.Const;

public static class ElementIndex
{
    public const int Iron    = 0;
    public const int Wood    = 1;
    public const int Water   = 2;
    public const int Fire    = 3;
    public const int Earth   = 4;
    public const int Neg     = 5;
    public const int Pos     = 6;
    public const int Entropy = 7;

    public static readonly ReadOnlyCollection<string> ElementNames = new(new[]
    {
        $"{nameof(Cultiway)}.{nameof(Iron)}",
        $"{nameof(Cultiway)}.{nameof(Wood)}",
        $"{nameof(Cultiway)}.{nameof(Water)}",
        $"{nameof(Cultiway)}.{nameof(Fire)}",
        $"{nameof(Cultiway)}.{nameof(Earth)}",
        $"{nameof(Cultiway)}.{nameof(Neg)}",
        $"{nameof(Cultiway)}.{nameof(Pos)}",
        $"{nameof(Cultiway)}.{nameof(Entropy)}"
    });
}