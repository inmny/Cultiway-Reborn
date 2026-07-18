using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Debug;

/// <summary>
/// 在真实模组程序集内组合并栅格化法器外观，输出游戏运行时使用的画布、裁剪精灵和汇总预览。
/// 固定组合键保证每次启动产生相同样本，便于直接比较模板、模型、配色和栅格化改动。
/// </summary>
public sealed class ArtifactAppearanceRuntimePreviewExporter : MonoBehaviour
{
    private const string LogPrefix = "[ArtifactRuntimePreview]";
    private const int SamplesPerTemplate = 3;
    private const int SheetCellSize = 128;

    private static readonly ArtifactAppearanceRenderKind[] RuntimeViews =
    [
        ArtifactAppearanceRenderKind.Icon,
        ArtifactAppearanceRenderKind.WorldIdle,
        ArtifactAppearanceRenderKind.WorldActive,
    ];

    private string _modFolder;
    private bool _requested;
    private Coroutine _routine;
    private int _pngCount;

    /// <summary>安装导出器；重复调用会在当前导出结束后再生成一次最新结果。</summary>
    public static void Install(GameObject host, string modFolder)
    {
        ArtifactAppearanceRuntimePreviewExporter exporter =
            host.GetComponent<ArtifactAppearanceRuntimePreviewExporter>() ??
            host.AddComponent<ArtifactAppearanceRuntimePreviewExporter>();
        exporter._modFolder = modFolder;
        exporter._requested = true;
        exporter.TryStart();
    }

    private void OnEnable()
    {
        TryStart();
    }

    private void TryStart()
    {
        if (_requested && _routine == null && isActiveAndEnabled) _routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (_requested)
        {
            _requested = false;
            yield return null;

            IEnumerator export = ExportOnce();
            while (true)
            {
                bool hasNext;
                object current = null;
                try
                {
                    hasNext = export.MoveNext();
                    if (hasNext) current = export.Current;
                }
                catch (Exception exception)
                {
                    WriteFailure(exception);
                    ModClass.LogError($"{LogPrefix} 导出失败\n{exception}");
                    break;
                }

                if (!hasNext) break;
                yield return current;
            }
        }

        _routine = null;
    }

    private IEnumerator ExportOnce()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string outputRoot = PrepareOutputDirectory();
        _pngCount = 0;
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        ArtifactAppearanceTemplateDef[] templates = catalog.Templates.Values
            .OrderBy(template => template.Shape, StringComparer.Ordinal)
            .ThenBy(template => template.Key, StringComparer.Ordinal)
            .ToArray();
        ArtifactAtomSelection[] atoms = Array.Empty<ArtifactAtomSelection>();
        List<PreviewCell> cells = new();
        JArray instanceManifest = new();
        int frameCount = 0;

        WriteJson(Path.Combine(outputRoot, "exporting.json"), new JObject
        {
            ["status"] = "exporting",
            ["started_utc"] = DateTime.UtcNow.ToString("O"),
            ["templates"] = templates.Length,
        });
        ModClass.LogInfo($"{LogPrefix} 开始导出 templates={templates.Length} output={outputRoot}");

        for (int templateIndex = 0; templateIndex < templates.Length; templateIndex++)
        {
            ArtifactAppearanceTemplateDef template = templates[templateIndex];
            JObject templateManifest = new()
            {
                ["template"] = template.Key,
                ["shape"] = template.Shape,
                ["camera"] = template.Camera.DeepClone(),
                ["views"] = JArray.FromObject(template.Views),
                ["samples"] = new JArray(),
            };
            JArray samples = (JArray)templateManifest["samples"];

            for (int sample = 1; sample <= SamplesPerTemplate; sample++)
            {
                string compositionKey = $"artifact-runtime-preview|{template.Key}|{sample}";
                ArtifactAppearance runtimeAppearance =
                    ArtifactComposer.ComposeAppearance(template, atoms, compositionKey);
                ArtifactAppearance unifiedAppearance = UnifyColorScheme(runtimeAppearance);
                ArtifactAppearanceMesh mesh = ArtifactAppearanceGeometry.Build(runtimeAppearance, template, catalog);
                string sampleRoot = Path.Combine(
                    outputRoot,
                    "instances",
                    template.Shape,
                    template.Key,
                    sample.ToString("00"));
                JObject sampleManifest = new()
                {
                    ["sample"] = sample,
                    ["composition_key"] = compositionKey,
                    ["runtime_schemes"] = new JArray(runtimeAppearance.parts.Select(part => part.color_scheme)),
                    ["unified_scheme"] = unifiedAppearance.parts.Length == 0
                        ? string.Empty
                        : unifiedAppearance.parts[0].color_scheme,
                    ["runtime_views"] = new JObject(),
                    ["unified_views"] = new JObject(),
                };
                WriteAppearance(Path.Combine(sampleRoot, "appearance.json"), compositionKey, runtimeAppearance,
                    unifiedAppearance);

                JObject runtimeViewManifest = (JObject)sampleManifest["runtime_views"];
                for (int viewIndex = 0; viewIndex < RuntimeViews.Length; viewIndex++)
                {
                    ArtifactAppearanceRenderKind kind = RuntimeViews[viewIndex];
                    ArtifactAppearancePixelFrame frame = ArtifactAppearanceRasterizer.Render(
                        mesh,
                        runtimeAppearance,
                        template,
                        catalog,
                        kind);
                    Color32[] display = kind == ArtifactAppearanceRenderKind.Icon
                        ? frame.Composite
                        : ComposeWorldLayers(frame);
                    string viewKey = ViewKey(kind);
                    string viewRoot = Path.Combine(sampleRoot, "runtime", viewKey);
                    string renderPath = Path.Combine(viewRoot, "render.png");
                    WritePng(renderPath, display, frame.Size, frame.Size);
                    frameCount++;

                    JObject viewManifest = FrameManifest(outputRoot, renderPath, frame);
                    if (kind != ArtifactAppearanceRenderKind.Icon)
                    {
                        string bodySpritePath = Path.Combine(viewRoot, "body_sprite.png");
                        viewManifest["body_sprite"] = WriteCroppedSprite(
                            outputRoot,
                            bodySpritePath,
                            frame.Body,
                            frame.Size);
                    }
                    if (kind == ArtifactAppearanceRenderKind.WorldActive)
                    {
                        JObject layers = new();
                        layers["body"] = WriteLayer(outputRoot, Path.Combine(viewRoot, "body.png"), frame.Body,
                            frame.Size);
                        layers["emission"] = WriteLayer(
                            outputRoot,
                            Path.Combine(viewRoot, "emission.png"),
                            frame.Emission,
                            frame.Size);
                        layers["shadow"] = WriteLayer(
                            outputRoot,
                            Path.Combine(viewRoot, "shadow.png"),
                            frame.Shadow,
                            frame.Size);
                        viewManifest["layers"] = layers;
                    }
                    runtimeViewManifest[viewKey] = viewManifest;
                    cells.Add(new PreviewCell(
                        template.Key,
                        sample,
                        "runtime",
                        viewKey,
                        display,
                        frame.Size));
                }

                ArtifactAppearancePixelFrame unifiedFrame = ArtifactAppearanceRasterizer.Render(
                    mesh,
                    unifiedAppearance,
                    template,
                    catalog,
                    ArtifactAppearanceRenderKind.WorldActive);
                Color32[] unifiedDisplay = ComposeWorldLayers(unifiedFrame);
                string unifiedPath = Path.Combine(sampleRoot, "unified", "world_active", "render.png");
                WritePng(unifiedPath, unifiedDisplay, unifiedFrame.Size, unifiedFrame.Size);
                frameCount++;
                ((JObject)sampleManifest["unified_views"])["world_active"] =
                    FrameManifest(outputRoot, unifiedPath, unifiedFrame);
                cells.Add(new PreviewCell(
                    template.Key,
                    sample,
                    "unified",
                    "world_active",
                    unifiedDisplay,
                    unifiedFrame.Size));
                samples.Add(sampleManifest);
            }

            instanceManifest.Add(templateManifest);
            yield return null;
        }

        JArray sheetManifest = WriteSheets(outputRoot, templates, cells);
        JObject manifest = new()
        {
            ["format_version"] = 1,
            ["generator"] = typeof(ArtifactAppearanceRuntimePreviewExporter).FullName,
            ["generated_utc"] = DateTime.UtcNow.ToString("O"),
            ["render_semantics"] = "world sheets alpha-compose shadow, body and emission at identity rotation with white tint",
            ["catalog"] = new JObject
            {
                ["canvas"] = catalog.Canvas,
                ["templates"] = catalog.Templates.Count,
                ["modules"] = catalog.Modules.Count,
                ["color_schemes"] = catalog.ColorSchemes.Count,
                ["surface_styles"] = catalog.SurfaceStyles.Count,
            },
            ["instances"] = instanceManifest,
            ["sheets"] = sheetManifest,
        };
        WriteJson(Path.Combine(outputRoot, "manifest.json"), manifest);

        stopwatch.Stop();
        File.Delete(Path.Combine(outputRoot, "exporting.json"));
        WriteJson(Path.Combine(outputRoot, "complete.json"), new JObject
        {
            ["status"] = "complete",
            ["generated_utc"] = DateTime.UtcNow.ToString("O"),
            ["templates"] = templates.Length,
            ["runtime_instances"] = templates.Length * SamplesPerTemplate,
            ["frames"] = frameCount,
            ["png_files"] = _pngCount,
            ["elapsed_seconds"] = Math.Round(stopwatch.Elapsed.TotalSeconds, 3),
        });
        ModClass.LogInfo(
            $"{LogPrefix} 导出完成 templates={templates.Length} frames={frameCount} png={_pngCount} elapsed={stopwatch.Elapsed.TotalSeconds:0.00}s output={outputRoot}");
    }

    private static ArtifactAppearance UnifyColorScheme(ArtifactAppearance source)
    {
        if (source.parts.Length == 0) return source;
        string scheme = source.parts[0].color_scheme;
        ArtifactAppearancePart[] parts = new ArtifactAppearancePart[source.parts.Length];
        for (int i = 0; i < source.parts.Length; i++)
        {
            ArtifactAppearancePart part = source.parts[i];
            part.color_scheme = scheme;
            parts[i] = part;
        }
        return new ArtifactAppearance
        {
            template_key = source.template_key,
            parts = parts,
        };
    }

    private static Color32[] ComposeWorldLayers(ArtifactAppearancePixelFrame frame)
    {
        Color32[] result = new Color32[frame.Body.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = AlphaOver(AlphaOver(frame.Shadow[i], frame.Body[i]), frame.Emission[i]);
        }
        return result;
    }

    private static Color32 AlphaOver(Color32 destination, Color32 source)
    {
        if (source.a == 0) return destination;
        if (destination.a == 0) return source;
        float sourceAlpha = source.a / 255f;
        float destinationAlpha = destination.a / 255f;
        float alpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
        return new Color32(
            ClampByte((source.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte((source.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte((source.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte(alpha * 255f));
    }

    private JObject WriteLayer(string outputRoot, string path, Color32[] pixels, int size)
    {
        WritePng(path, pixels, size, size);
        return new JObject
        {
            ["file"] = RelativePath(outputRoot, path),
            ["visible"] = ArtifactAppearanceRenderer.HasVisiblePixel(pixels),
        };
    }

    private JObject WriteCroppedSprite(string outputRoot, string path, Color32[] source, int size)
    {
        Rect rect = ArtifactAppearanceRenderer.FindOpaqueRect(source, size);
        int x = Mathf.RoundToInt(rect.x);
        int unityY = Mathf.RoundToInt(rect.y);
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        int top = size - unityY - height;
        Color32[] cropped = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(source, (top + y) * size + x, cropped, y * width, width);
        }
        WritePng(path, cropped, width, height);

        Vector2 canvasCenter = new(size * 0.5f, size * 0.5f);
        return new JObject
        {
            ["file"] = RelativePath(outputRoot, path),
            ["rect_unity"] = new JArray(x, unityY, width, height),
            ["rect_top_down"] = new JArray(x, top, width, height),
            ["pivot"] = new JArray(
                (canvasCenter.x - rect.x) / rect.width,
                (canvasCenter.y - rect.y) / rect.height),
        };
    }

    private static JObject FrameManifest(
        string outputRoot,
        string renderPath,
        ArtifactAppearancePixelFrame frame)
    {
        ArtifactAppearanceProjection projection = frame.Projection;
        return new JObject
        {
            ["size"] = frame.Size,
            ["render"] = RelativePath(outputRoot, renderPath),
            ["projection"] = new JObject
            {
                ["target"] = Vec3(projection.Target),
                ["rotation"] = Vec3(projection.Rotation),
                ["scale"] = projection.Scale,
                ["light"] = Vec3(projection.Light),
            },
        };
    }

    private JArray WriteSheets(
        string outputRoot,
        IReadOnlyList<ArtifactAppearanceTemplateDef> templates,
        IReadOnlyList<PreviewCell> cells)
    {
        Dictionary<string, PreviewCell> byKey = cells.ToDictionary(cell => cell.Key, StringComparer.Ordinal);
        JArray manifest = new();
        foreach (IGrouping<string, ArtifactAppearanceTemplateDef> shapeGroup in templates.GroupBy(template => template.Shape))
        {
            string[] rows = shapeGroup
                .Select(template => template.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            manifest.Add(WriteSheet(
                outputRoot,
                $"{shapeGroup.Key}.icon.runtime.png",
                rows,
                RuntimeColumns("icon"),
                byKey));
            manifest.Add(WriteSheet(
                outputRoot,
                $"{shapeGroup.Key}.idle.runtime.png",
                rows,
                RuntimeColumns("world_idle"),
                byKey));
            manifest.Add(WriteSheet(
                outputRoot,
                $"{shapeGroup.Key}.active.comparison.png",
                rows,
                ActiveComparisonColumns(),
                byKey));
        }
        return manifest;
    }

    private JObject WriteSheet(
        string outputRoot,
        string fileName,
        IReadOnlyList<string> rows,
        IReadOnlyList<SheetColumn> columns,
        IReadOnlyDictionary<string, PreviewCell> cells)
    {
        int width = columns.Count * SheetCellSize;
        int height = rows.Count * SheetCellSize;
        Color32[] pixels = new Color32[width * height];
        FillCheckerboard(pixels, width, height);
        for (int row = 0; row < rows.Count; row++)
        {
            for (int column = 0; column < columns.Count; column++)
            {
                SheetColumn spec = columns[column];
                string key = PreviewCell.BuildKey(rows[row], spec.Sample, spec.Mode, spec.View);
                if (!cells.TryGetValue(key, out PreviewCell cell)) continue;
                int left = column * SheetCellSize;
                int top = row * SheetCellSize;
                DrawBorder(
                    pixels,
                    width,
                    left,
                    top,
                    SheetCellSize,
                    spec.Mode == "unified"
                        ? new Color32(79, 184, 171, 255)
                        : new Color32(171, 151, 99, 255));
                BlitNearest(pixels, width, left, top, SheetCellSize, cell.Pixels, cell.Size);
            }
        }

        string path = Path.Combine(outputRoot, "sheets", fileName);
        WritePng(path, pixels, width, height);
        return new JObject
        {
            ["file"] = RelativePath(outputRoot, path),
            ["rows"] = new JArray(rows),
            ["columns"] = new JArray(columns.Select(column => column.Label)),
            ["cell_size"] = SheetCellSize,
        };
    }

    private static SheetColumn[] RuntimeColumns(string view)
    {
        SheetColumn[] columns = new SheetColumn[SamplesPerTemplate];
        for (int sample = 1; sample <= SamplesPerTemplate; sample++)
        {
            columns[sample - 1] = new SheetColumn($"runtime_{sample}", sample, "runtime", view);
        }
        return columns;
    }

    private static SheetColumn[] ActiveComparisonColumns()
    {
        SheetColumn[] columns = new SheetColumn[SamplesPerTemplate * 2];
        for (int sample = 1; sample <= SamplesPerTemplate; sample++)
        {
            int index = (sample - 1) * 2;
            columns[index] = new SheetColumn($"runtime_{sample}", sample, "runtime", "world_active");
            columns[index + 1] = new SheetColumn($"unified_{sample}", sample, "unified", "world_active");
        }
        return columns;
    }

    private static void FillCheckerboard(Color32[] pixels, int width, int height)
    {
        Color32 dark = new(31, 34, 38, 255);
        Color32 light = new(42, 46, 51, 255);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = ((x / 12 + y / 12) & 1) == 0 ? dark : light;
            }
        }
    }

    private static void DrawBorder(
        Color32[] pixels,
        int width,
        int left,
        int top,
        int size,
        Color32 color)
    {
        int right = left + size - 1;
        int bottom = top + size - 1;
        for (int offset = 0; offset < size; offset++)
        {
            pixels[top * width + left + offset] = color;
            pixels[bottom * width + left + offset] = color;
            pixels[(top + offset) * width + left] = color;
            pixels[(top + offset) * width + right] = color;
        }
    }

    private static void BlitNearest(
        Color32[] destination,
        int destinationWidth,
        int cellLeft,
        int cellTop,
        int cellSize,
        Color32[] source,
        int sourceSize)
    {
        int scale = Math.Max(1, (cellSize - 16) / sourceSize);
        int drawSize = sourceSize * scale;
        int left = cellLeft + (cellSize - drawSize) / 2;
        int top = cellTop + (cellSize - drawSize) / 2;
        for (int sourceY = 0; sourceY < sourceSize; sourceY++)
        {
            for (int sourceX = 0; sourceX < sourceSize; sourceX++)
            {
                Color32 color = source[sourceY * sourceSize + sourceX];
                if (color.a == 0) continue;
                int targetLeft = left + sourceX * scale;
                int targetTop = top + sourceY * scale;
                for (int y = 0; y < scale; y++)
                {
                    int row = (targetTop + y) * destinationWidth + targetLeft;
                    for (int x = 0; x < scale; x++)
                    {
                        destination[row + x] = AlphaOver(destination[row + x], color);
                    }
                }
            }
        }
    }

    private void WritePng(string path, Color32[] pixels, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        try
        {
            texture.SetPixels32(ToUnityPixels(pixels, width, height));
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            _pngCount++;
        }
        finally
        {
            Object.Destroy(texture);
        }
    }

    private static Color32[] ToUnityPixels(Color32[] source, int width, int height)
    {
        if (width == height) return ArtifactAppearanceRenderer.ToUnityPixels(source, width);
        Color32[] result = new Color32[source.Length];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(source, y * width, result, (height - 1 - y) * width, width);
        }
        return result;
    }

    private static void WriteAppearance(
        string path,
        string compositionKey,
        ArtifactAppearance runtime,
        ArtifactAppearance unified)
    {
        WriteJson(path, new JObject
        {
            ["composition_key"] = compositionKey,
            ["runtime"] = JObject.FromObject(runtime),
            ["unified"] = JObject.FromObject(unified),
        });
    }

    private string PrepareOutputDirectory()
    {
        string parent = PreviewParent();
        string outputRoot = Path.GetFullPath(Path.Combine(parent, "latest"));
        string parentPrefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                              Path.DirectorySeparatorChar;
        if (!outputRoot.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"非法的运行时预览输出路径: {outputRoot}");
        if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
        Directory.CreateDirectory(outputRoot);
        string failurePath = Path.Combine(parent, "failed.txt");
        if (File.Exists(failurePath)) File.Delete(failurePath);
        return outputRoot;
    }

    private string PreviewParent()
    {
        return Path.GetFullPath(Path.Combine(_modFolder, "artifacts", "artifact_runtime_preview"));
    }

    private void WriteFailure(Exception exception)
    {
        try
        {
            string parent = PreviewParent();
            Directory.CreateDirectory(parent);
            File.WriteAllText(
                Path.Combine(parent, "failed.txt"),
                DateTime.UtcNow.ToString("O") + Environment.NewLine + exception,
                new UTF8Encoding(false));
        }
        catch
        {
            // 原始异常会写入模组日志，这里不能让失败报告覆盖它。
        }
    }

    private static void WriteJson(string path, JToken value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(
            path,
            value.ToString(Formatting.Indented) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static string RelativePath(string root, string path)
    {
        return path.Substring(root.Length + 1).Replace('\\', '/');
    }

    private static string ViewKey(ArtifactAppearanceRenderKind kind)
    {
        return kind switch
        {
            ArtifactAppearanceRenderKind.Icon => "icon",
            ArtifactAppearanceRenderKind.WorldIdle => "world_idle",
            ArtifactAppearanceRenderKind.WorldActive => "world_active",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static JArray Vec3(Vector3 value)
    {
        return new JArray(value.x, value.y, value.z);
    }

    private static byte ClampByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
    }

    private sealed class PreviewCell
    {
        internal readonly string Key;
        internal readonly Color32[] Pixels;
        internal readonly int Size;

        internal PreviewCell(
            string template,
            int sample,
            string mode,
            string view,
            Color32[] pixels,
            int size)
        {
            Key = BuildKey(template, sample, mode, view);
            Pixels = pixels;
            Size = size;
        }

        internal static string BuildKey(string template, int sample, string mode, string view)
        {
            return $"{template}|{sample}|{mode}|{view}";
        }
    }

    private readonly struct SheetColumn
    {
        internal readonly string Label;
        internal readonly int Sample;
        internal readonly string Mode;
        internal readonly string View;

        internal SheetColumn(string label, int sample, string mode, string view)
        {
            Label = label;
            Sample = sample;
            Mode = mode;
            View = view;
        }
    }
}
