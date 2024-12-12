using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Cultiway.Debug;

public static class Assert
{
    public static void Equals<T>(T a, T b)
    {
        if (!EqualityComparer<T>.Default.Equals(a, b)) throw new AssertionException("", "");
    }
}