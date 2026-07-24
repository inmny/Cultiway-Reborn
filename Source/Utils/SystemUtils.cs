using System;
using System.Text;

namespace Cultiway.Utils;

public static class SystemUtils
{
    private const string DeveloperUserName = "Inmny";

    public static bool IsUnderDeveloper()
    {
        return string.Equals(
            Environment.UserName,
            DeveloperUserName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFullExceptionMessage(Exception e)
    {
        StringBuilder sb = new();
        var idx = 0;
        do
        {
            sb.AppendLine($"[{idx}] {e.GetType()}: {e.Message}\n{e.StackTrace}");
            e = e.InnerException;
        } while (e != null);

        return sb.ToString();
    }
}
