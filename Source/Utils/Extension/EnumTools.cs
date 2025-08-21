using System.Runtime.CompilerServices;
using Cultiway.Const;

namespace Cultiway.Utils.Extension;

public static class EnumTools
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MetaType Back(this MetaTypeExtend type)
    {
        return (MetaType)type;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MetaTypeExtend Extend(this MetaType type)
    {
        return (MetaTypeExtend)type;
    }
}