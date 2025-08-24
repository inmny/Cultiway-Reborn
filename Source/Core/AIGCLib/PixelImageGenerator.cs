using System;
using System.Collections.Generic;

namespace Cultiway.Core.AIGCLib;

public abstract class PixelImageGenerator<T, TParam> where T : PixelImageGenerator<T, TParam> where TParam : class
{
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = (T)Activator.CreateInstance(typeof(T));
            }
            return _instance;
        }
    }

    private static T _instance;

    public abstract void Generate(int width, int height, IEnumerable<TParam> @params);
}