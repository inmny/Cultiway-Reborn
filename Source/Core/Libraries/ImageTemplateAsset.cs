using UnityEngine;

namespace Cultiway.Core.Libraries;

public class ImageTemplateAsset : Asset
{
    public Color32[][] Pixels { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool FullApply;
    public void Load(string path)
    {
        var sprite = SpriteTextureLoader.getSprite(path);
        var tex = sprite.texture;
        Width = Mathf.RoundToInt(sprite.rect.width);
        Height = Mathf.RoundToInt(sprite.rect.height);
        var start_x = Mathf.RoundToInt(sprite.rect.x);
        var start_y = Mathf.RoundToInt(sprite.rect.y);
        Pixels = new Color32[Height][];

        var raw_pixel = tex.GetPixels32();
        for (int y = 0; y < Height; y++)
        {
            Pixels[y] = new Color32[Width];
            var yy = y + start_y;
            for (int x = 0; x < Width; x++)
            {
                var xx = x + start_x;
                Pixels[y][x] = raw_pixel[xx + yy * Width];
            }
        }
    }
}