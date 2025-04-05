using UnityEngine;

namespace Cultiway.Content;

public class WakanMap
{
    private static WakanMap _instance;
    private        int      height;
    internal       float[,] map;
    private        int      width;

    private WakanMap()
    {
        Resize(MapBox.width, MapBox.height);
    }

    public static WakanMap I
    {
        get
        {
            if (_instance == null)
            {
                _instance = new();
            }

            return _instance;
        }
    }

    public float Sum()
    {
        var sum = 0f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                sum += map[x, y];
            }
        }

        return sum;
    }

    public float Avg()
    {
        return Sum() / (width * height);
    }

    public void SetAll(float v)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = v;
            }
        }
    }

    public float Max()
    {
        var max = 0f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                max = Mathf.Max(max, map[x, y]);
            }
        }

        return max;
    }

    public float Min()
    {
        var min = float.MaxValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                min = Mathf.Min(min, map[x, y]);
            }
        }

        return min;
    }

    public void Resize(int width, int height)
    {
        this.width = width;
        this.height = height;
        map = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = 100;
            }
        }
    }
}