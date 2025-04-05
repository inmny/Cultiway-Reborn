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
            StringBuilder sb = new();
            sb.AppendLine($"Meet error when trying to run action {action}. Exceptions show below:");
            do
            {
                sb.AppendLine($"{e.GetType()}: {e.Message}");
                sb.AppendLine(e.StackTrace);
                if (e is ReflectionTypeLoadException rtle)
                    foreach (Exception le in rtle.LoaderExceptions)
                    {
                        sb.AppendLine($"{le.GetType()}: {le.Message}");
                        sb.AppendLine(le.StackTrace);
                    }

                e = e.InnerException;
            } while (e != null);
            ModClass.LogError(sb.ToString());
        }
    }
}