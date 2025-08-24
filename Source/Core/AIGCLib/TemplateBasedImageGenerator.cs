using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.AIGCLib;

public class TemplateParam
{
    public string TemplateID;
    public Vector2 Position;
    public Color32 Color;
}
public class TemplateBasedImageGenerator : PixelImageGenerator<TemplateBasedImageGenerator, TemplateParam>
{
    public override void Generate(int width, int height, IEnumerable<TemplateParam> @params)
    {
        Texture2D texture = new Texture2D(width, height);
        var pixels = texture.GetPixels32();
        foreach (var param in @params)
        {
            var asset = ModClass.L.ImageTemplateLibrary.get(param.TemplateID);
            var template_pixels = asset.Pixels;
            var start_x = Mathf.RoundToInt(param.Position.x * width);
            var start_y = Mathf.RoundToInt(param.Position.y * height);

            var replace_color = param.Color;
            for (int y = 0; y < asset.Height; y++)
            {
                var yy = start_y + y;
                if (yy < 0 || yy >= height) continue;
                for (int x = 0; x < asset.Width; x++)
                {
                    var pixel = template_pixels[y][x];
                    if (pixel.a == 0) continue;
                    
                    var xx = start_x + x;
                    if (xx < 0 || xx >= width) continue;
                    if (pixels[yy * width + xx].a == 0 && !asset.FullApply) continue;
                    pixels[yy * width + xx] = CheckColor(pixel, replace_color);
                }
            }
        }
    }

    private static Color32 CheckColor(Color32 check_color, Color32 replace_color)
    {
        return check_color;
    }
}