using System.Runtime.CompilerServices;
using System.Text;

namespace Cultiway.Utils.Extension;

public static class StringTools
{
    private static StringBuilder sb = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static string LeaveDigit(this string a)
    {
        sb.Clear();
        for (int i = 0; i < a.Length; i++)
        {
            if (char.IsDigit(a, i)) sb.Append(a[i]);
        }

        return sb.ToString();
    }

    public static int ToInt(this string a)
    {
        return int.Parse(a);
    }
}