param(
    [string]$InputDir = (Join-Path $PSScriptRoot "..\GameResources\cultiway\icons\item_shapes\talisman"),
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\bin\Preview\TalismanVfx"),
    [string]$AccentColor = "#5CCBFF"
)

Add-Type -AssemblyName System.Drawing

$generatorSource = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class TalismanVfxPreviewGenerator
{
    private const int CanvasSize = 64;
    private const int SourceActivationFrameCount = 15;
    private const int VisibleActivationFrameCount = 11;
    private const int AwakeFrameCount = SourceActivationFrameCount / 3;
    private const int BurnFrameCount = SourceActivationFrameCount - AwakeFrameCount;

    private struct Px
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Px(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    public static void Generate(string inputPath, string outputDir, string accentHex)
    {
        Directory.CreateDirectory(outputDir);
        var source = ReadSource(inputPath);
        var accent = ParseHex(accentHex);
        var baseColor = new Px(0.82f, 0.96f, 1f, 1f);
        const int awakeCount = AwakeFrameCount;
        const int burnCount = BurnFrameCount;
        var frames = new Bitmap[SourceActivationFrameCount];
        var names = new string[frames.Length];

        for (var i = 0; i < awakeCount; i++)
        {
            var t = i / Math.Max(1f, awakeCount - 1f);
            frames[i] = ToBitmap(BuildAwakeFrame(source, baseColor, accent, t));
            names[i] = i.ToString("00") + "_awake";
        }

        for (var i = 0; i < burnCount; i++)
        {
            var t = i / Math.Max(1f, burnCount - 1f);
            var index = awakeCount + i;
            frames[index] = ToBitmap(BuildBurnFrame(source, baseColor, accent, t));
            names[index] = index.ToString("00") + "_burn";
        }

        for (var i = 0; i < VisibleActivationFrameCount; i++)
        {
            var framePath = Path.Combine(outputDir, names[i] + ".png");
            frames[i].Save(framePath, ImageFormat.Png);
        }

        using (var sheet = BuildSheet(frames, names, VisibleActivationFrameCount))
        {
            sheet.Save(Path.Combine(outputDir, "_sheet.png"), ImageFormat.Png);
        }

        foreach (var frame in frames)
        {
            frame.Dispose();
        }
    }

    private static Px[] BuildAwakeFrame(Px[] source, Px color, Px accent, float t)
    {
        var eased = EaseOut(t);
        var scale = Lerp(0.68f, 1.05f, eased);
        var alpha = Lerp(0.18f, 1f, eased);
        var tintAmount = Lerp(0.08f, 0.34f, (float)Math.Sin(t * Math.PI));
        var frame = DrawScaled(source, scale, alpha, Lerp(color, accent, 0.4f), tintAmount);
        AddGlow(frame, accent, 2, Lerp(0.22f, 0.48f, eased));
        return frame;
    }

    private static Px[] BuildBurnFrame(Px[] source, Px color, Px accent, float t)
    {
        var frame = Empty();
        var fire = Lerp(new Px(1f, 0.52f, 0.08f, 1f), accent, 0.35f);
        var progress = Lerp(0.06f, 1.18f, FastBurnProgress(t));
        var center = (CanvasSize - 1) * 0.5f;

        for (var y = 0; y < CanvasSize; y++)
        {
            var yNorm = y / (CanvasSize - 1f);
            for (var x = 0; x < CanvasSize; x++)
            {
                var index = Idx(x, y);
                var pixel = source[index];
                if (pixel.A <= 0.01f) continue;

                var edge = Math.Min(Math.Min(x, y), Math.Min(CanvasSize - 1 - x, CanvasSize - 1 - y)) / center;
                edge = Clamp01(edge);
                var noise = Hash01(x, y);
                var burnOrder = yNorm * 0.74f + edge * 0.18f + noise * 0.08f;
                var front = burnOrder - progress;
                if (front < -0.06f) continue;

                if (front < 0.09f)
                {
                    var heat = Clamp01(1f - front / 0.09f);
                    var c = Lerp(accent, fire, 0.55f + noise * 0.35f);
                    c.A = pixel.A * Lerp(0.42f, 1f, heat);
                    frame[index] = c;
                    continue;
                }

                var paper = Lerp(pixel, color, 0.12f + (float)Math.Sin(t * Math.PI) * 0.12f);
                paper.A *= Lerp(1f, 0.32f, Clamp01((t - 0.48f) / 0.47f));
                frame[index] = paper;
            }
        }

        AddGlow(frame, accent, 3, Lerp(0.42f, 0.18f, t));
        return frame;
    }

    private static Px[] DrawScaled(Px[] source, float scale, float alpha, Px tint, float tintAmount)
    {
        var frame = Empty();
        var center = (CanvasSize - 1) * 0.5f;
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var sx = (int)Math.Round((x - center) / scale + center);
                var sy = (int)Math.Round((y - center) / scale + center);
                if (sx < 0 || sx >= CanvasSize || sy < 0 || sy >= CanvasSize) continue;
                var pixel = source[Idx(sx, sy)];
                if (pixel.A <= 0.01f) continue;
                var c = Lerp(pixel, tint, tintAmount);
                c.A = pixel.A * alpha;
                frame[Idx(x, y)] = c;
            }
        }

        return frame;
    }

    private static Px[] ReadSource(string inputPath)
    {
        var pixels = Empty();
        using (var bitmap = new Bitmap(inputPath))
        {
            var scale = Math.Min((CanvasSize - 8f) / bitmap.Width, (CanvasSize - 8f) / bitmap.Height);
            var drawWidth = Math.Max(1, Math.Min(CanvasSize, (int)Math.Round(bitmap.Width * scale)));
            var drawHeight = Math.Max(1, Math.Min(CanvasSize, (int)Math.Round(bitmap.Height * scale)));
            var offsetX = (CanvasSize - drawWidth) / 2;
            var offsetY = (CanvasSize - drawHeight) / 2;
            for (var y = 0; y < drawHeight; y++)
            {
                var sy = Math.Min(bitmap.Height - 1, (int)Math.Floor(y / Math.Max(1f, drawHeight - 1f) * (bitmap.Height - 1)));
                for (var x = 0; x < drawWidth; x++)
                {
                    var sx = Math.Min(bitmap.Width - 1, (int)Math.Floor(x / Math.Max(1f, drawWidth - 1f) * (bitmap.Width - 1)));
                    var c = bitmap.GetPixel(sx, sy);
                    var targetY = offsetY + drawHeight - 1 - y;
                    pixels[Idx(offsetX + x, targetY)] = new Px(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
                }
            }
        }

        return pixels;
    }

    private static void AddGlow(Px[] frame, Px glowColor, int radius, float alpha)
    {
        var source = (Px[])frame.Clone();
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var index = Idx(x, y);
                if (source[index].A > 0.62f) continue;
                var glow = 0f;
                for (var oy = -radius; oy <= radius; oy++)
                {
                    var ny = y + oy;
                    if (ny < 0 || ny >= CanvasSize) continue;
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var nx = x + ox;
                        if (nx < 0 || nx >= CanvasSize) continue;
                        var distance = (float)Math.Sqrt(ox * ox + oy * oy);
                        if (distance > radius) continue;
                        var neighborAlpha = source[Idx(nx, ny)].A;
                        if (neighborAlpha <= 0.01f) continue;
                        glow = Math.Max(glow, neighborAlpha * (1f - distance / (radius + 0.01f)));
                    }
                }

                if (glow <= 0.01f) continue;
                var c = glowColor;
                c.A = Clamp01(glow * alpha * (1f - frame[index].A * 0.35f));
                frame[index] = AlphaBlend(frame[index], c);
            }
        }
    }

    private static Bitmap ToBitmap(Px[] pixels)
    {
        var bitmap = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var c = pixels[Idx(x, y)];
                bitmap.SetPixel(x, CanvasSize - 1 - y, Color.FromArgb(ToByte(c.A), ToByte(c.R), ToByte(c.G), ToByte(c.B)));
            }
        }

        return bitmap;
    }

    private static Bitmap BuildSheet(Bitmap[] frames, string[] names, int frameCount)
    {
        const int scale = 3;
        const int margin = 8;
        const int labelHeight = 16;
        var cellW = CanvasSize * scale + margin * 2;
        var cellH = CanvasSize * scale + margin * 2 + labelHeight;
        var columns = Math.Min(6, frameCount);
        var rows = (int)Math.Ceiling(frameCount / (float)columns);
        var sheet = new Bitmap(columns * cellW, rows * cellH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        using (var brush = new SolidBrush(Color.FromArgb(235, 30, 30, 30)))
        using (var textBrush = new SolidBrush(Color.White))
        {
            g.FillRectangle(brush, 0, 0, sheet.Width, sheet.Height);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            for (var i = 0; i < frameCount; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = col * cellW + margin;
                var y = row * cellH + margin;
                DrawChecker(g, x, y, CanvasSize * scale, CanvasSize * scale);
                g.DrawImage(frames[i], x, y, CanvasSize * scale, CanvasSize * scale);
                g.DrawString(names[i], SystemFonts.DefaultFont, textBrush, x, y + CanvasSize * scale + 2);
            }
        }

        return sheet;
    }

    private static void DrawChecker(Graphics g, int x, int y, int width, int height)
    {
        using (var a = new SolidBrush(Color.FromArgb(70, 70, 70)))
        using (var b = new SolidBrush(Color.FromArgb(92, 92, 92)))
        {
            const int size = 12;
            for (var py = 0; py < height; py += size)
            {
                for (var px = 0; px < width; px += size)
                {
                    g.FillRectangle(((px / size + py / size) % 2 == 0) ? a : b, x + px, y + py, size, size);
                }
            }
        }
    }

    private static Px ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return new Px(0.36f, 0.8f, 1f, 1f);
        hex = hex.Trim().TrimStart('#');
        if (hex.Length < 6) return new Px(0.36f, 0.8f, 1f, 1f);
        var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Px(r, g, b, 1f);
    }

    private static Px[] Empty()
    {
        return new Px[CanvasSize * CanvasSize];
    }

    private static Px Lerp(Px a, Px b, float t)
    {
        t = Clamp01(t);
        return new Px(Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t), Lerp(a.A, b.A, t));
    }

    private static Px AlphaBlend(Px background, Px foreground)
    {
        var a = foreground.A + background.A * (1f - foreground.A);
        if (a <= 0.001f) return new Px();
        return new Px(
            (foreground.R * foreground.A + background.R * background.A * (1f - foreground.A)) / a,
            (foreground.G * foreground.A + background.G * background.A * (1f - foreground.A)) / a,
            (foreground.B * foreground.A + background.B * background.A * (1f - foreground.A)) / a,
            a);
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            var n = x * 73856093 ^ y * 19349663 ^ 83492791;
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private static float EaseOut(float t)
    {
        t = Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float FastBurnProgress(float t)
    {
        t = Clamp01(t);
        return 1f - (float)Math.Pow(1f - t, 1.35f);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Clamp01(t);
    }

    private static float Clamp01(float v)
    {
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }

    private static int ToByte(float v)
    {
        return Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));
    }

    private static int Idx(int x, int y)
    {
        return y * CanvasSize + x;
    }
}
"@

Add-Type -TypeDefinition $generatorSource -ReferencedAssemblies "System.Drawing"

$resolvedInput = Resolve-Path $InputDir
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$files = Get-ChildItem -LiteralPath $resolvedInput -Filter *.png | Sort-Object Name
foreach ($file in $files) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $iconRoot = Join-Path $OutputDir $name
    if (Test-Path -LiteralPath $iconRoot) {
        Remove-Item -LiteralPath $iconRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $iconRoot | Out-Null
    [TalismanVfxPreviewGenerator]::Generate($file.FullName, $iconRoot, $AccentColor)
}

Write-Host "Generated $($files.Count) talisman VFX previews under $OutputDir"
