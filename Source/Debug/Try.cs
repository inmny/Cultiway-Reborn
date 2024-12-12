using System;

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
                ModClass.LogError($"{e.GetType()}: {e.Message}\n{e.StackTrace}");
                e = e.InnerException;
            } while (e != null);
        }
    }
}