using System;
using System.Reflection;
using System.Text;

namespace Cultiway.Debug;

public static class Try
{
    public static void Start(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            do
            {
                StringBuilder sb = new();
                sb.AppendLine($"{e.GetType()}: {e.Message}");
                sb.AppendLine(e.StackTrace);
                if (e is ReflectionTypeLoadException rtle)
                    foreach (Exception le in rtle.LoaderExceptions)
                    {
                        sb.AppendLine($"{le.GetType()}: {le.Message}");
                        sb.AppendLine(le.StackTrace);
                    }

                ModClass.LogError(sb.ToString());
                e = e.InnerException;
            } while (e != null);
        }
    }
}