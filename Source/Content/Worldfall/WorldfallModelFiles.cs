#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

/// <summary>后台读取紧凑网格，再按帧交给游戏；不在加载画面中解析整份模型文本。</summary>
internal static class WorldfallModelFiles
{
    /// <summary>每帧接入模型最多使用的毫秒数。</summary>
    private const double InstallBudgetMilliseconds = 4;

    /// <summary>同一原始物体包含的所有距离档和建筑状态。</summary>
    internal sealed class ModelAsset
    {
        /// <summary>原始资产编号。</summary>
        internal string Id;
        /// <summary>建筑、单位或植被分类。</summary>
        internal string Category;
        /// <summary>已经完成二进制读取的独立网格。</summary>
        internal MeshModel[] Models;
    }

    /// <summary>最近一次后台读取耗时，便于对照实际加载日志。</summary>
    internal static double ReadMilliseconds { get; private set; }
    /// <summary>最近一次模型接入实际分成的批次数。</summary>
    internal static int InstallBatches { get; private set; }
    /// <summary>最近一次接入模型时最长的一批耗时。</summary>
    internal static double MaxInstallMilliseconds { get; private set; }

    /// <summary>在后台读取全部正式模型；路径在游戏线程取出，工作线程只接触文件和普通网格数据。</summary>
    /// <param name="directory">正式模型目录。</param>
    /// <returns>包含全部独立模型的读取任务。</returns>
    internal static Task<ModelAsset[]> ReadAsync(string directory) => Task.Run(() => ReadAll(directory));

    /// <summary>读取转换脚本生成的模型文件，保留所有顶点、材质、动作部件和附加标记。</summary>
    /// <param name="directory">正式模型目录。</param>
    /// <returns>按资产分组的网格。</returns>
    internal static ModelAsset[] ReadAll(string directory)
    {
        Stopwatch watch = Stopwatch.StartNew();
        string[] paths = Directory.GetFiles(directory, "models.bin", SearchOption.AllDirectories);
        if (paths.Length == 0) throw new InvalidDataException("缺少三维模型包，请先运行 Scripts/Worldfall/compile_model_files.py。");
        ModelAsset[] assets = new ModelAsset[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            using FileStream file = File.OpenRead(paths[i]);
            using BinaryReader header = new(file, Encoding.UTF8, true);
            if (header.ReadUInt32() != 0x4D465743 || header.ReadInt32() != 1)
                throw new InvalidDataException("三维模型包格式不符：" + paths[i]);
            ReadBytes(header, 32); // 原编辑文件的摘要只供发布检查使用，游戏直接读取已生成的数据。
            using DeflateStream compressed = new(file, CompressionMode.Decompress);
            using BinaryReader reader = new(compressed, Encoding.UTF8);
            ModelAsset asset = new() { Id = reader.ReadString(), Category = reader.ReadString() };
            asset.Models = new MeshModel[reader.ReadInt32()];
            for (int model = 0; model < asset.Models.Length; model++) asset.Models[model] = ReadModel(reader);
            assets[i] = asset;
        }
        ReadMilliseconds = watch.Elapsed.TotalMilliseconds;
        return assets;
    }

    /// <summary>等待后台结果时持续让出画面刷新，读完后每帧只接入一小批网格。</summary>
    /// <param name="loading">已经开始的后台读取。</param>
    /// <param name="models">仅由游戏线程修改的共用模型表。</param>
    /// <param name="whenReady">全部模型接入后安装外观入口。</param>
    internal static void Install(Task<ModelAsset[]> loading, Dictionary<string, MeshModel> models, Action whenReady)
    {
        int assetIndex = 0, modelIndex = 0, modelCount = 0;
        InstallBatches = 0;
        MaxInstallMilliseconds = 0;

        void NextBatch()
        {
            if (!loading.IsCompleted)
            {
                QueueNext(NextBatch);
                return;
            }
            ModelAsset[] assets = loading.GetAwaiter().GetResult();
            Stopwatch watch = Stopwatch.StartNew();
            while (assetIndex < assets.Length)
            {
                ModelAsset asset = assets[assetIndex];
                while (modelIndex < asset.Models.Length)
                {
                    MeshModel model = asset.Models[modelIndex++];
                    WorldfallModelIntegration.PrepareDaemonProportions(model);
                    WorldfallNatureMaterials.Apply(asset.Id, model);
                    if (asset.Category == "building") WorldfallMeshes.SetInfo(model, "cultiway_building=1");
                    models[model.Name] = model;
                    modelCount++;
                    if (watch.Elapsed.TotalMilliseconds >= InstallBudgetMilliseconds) break;
                }
                if (modelIndex == asset.Models.Length)
                {
                    assetIndex++;
                    modelIndex = 0;
                }
                if (watch.Elapsed.TotalMilliseconds >= InstallBudgetMilliseconds) break;
            }
            InstallBatches++;
            MaxInstallMilliseconds = Math.Max(MaxInstallMilliseconds, watch.Elapsed.TotalMilliseconds);
            if (assetIndex < assets.Length)
            {
                QueueNext(NextBatch);
                return;
            }
            ModClass.LogInfo($"[Worldfall] 已读取 {assets.Length} 个物体、{modelCount} 个网格；后台读取 {ReadMilliseconds:F0} 毫秒，分 {InstallBatches} 批接入，最长 {MaxInstallMilliseconds:F1} 毫秒。");
            whenReady();
        }

        QueueNext(NextBatch);
    }

    /// <summary>借用现有加载队列等待下一帧，避免阻塞等待任务或连续占用游戏线程。</summary>
    /// <param name="action">下一帧接着处理的模型批次。</param>
    private static void QueueNext(MapLoaderAction action)
    {
        SmoothLoader.add(action, "Load Cultiway models", pSkipFrame: true);
        // 每帧等待不重复写磁盘日志；最终完成时统一报告实际耗时。
        int index = SmoothLoader._started ? SmoothLoader._index + SmoothLoader._added : SmoothLoader._actions.Count - 1;
        SmoothLoader._actions[index].debug_log = false;
    }

    /// <summary>直接构造一个网格，避免文本和临时对象树的中转。</summary>
    /// <param name="reader">当前模型包。</param>
    /// <returns>尚未交给游戏的网格。</returns>
    private static MeshModel ReadModel(BinaryReader reader)
    {
        MeshModel model = new() { Name = reader.ReadString() };
        WorldfallMeshes.SetInfo(model, reader.ReadString());
        model.SlotNames = new string[reader.ReadInt32()];
        for (int i = 0; i < model.SlotNames.Length; i++) model.SlotNames[i] = reader.ReadString();
        model.SlotKind = ReadBytes(reader, reader.ReadInt32());
        model.SlotFlags = ReadBytes(reader, reader.ReadInt32());
        model.SlotColor = ReadArray<uint>(reader);
        model.Min = ReadVector(reader);
        model.Max = ReadVector(reader);
        model.Parts = new MeshPart[reader.ReadInt32()];
        for (int i = 0; i < model.Parts.Length; i++)
            model.Parts[i] = new MeshPart
            {
                Name = reader.ReadString(), Parent = reader.ReadInt32(), Pivot = ReadVector(reader),
                Pos = ReadArray<float>(reader), Nrm = ReadArray<float>(reader), Tri = ReadArray<int>(reader),
                Slot = ReadBytes(reader, reader.ReadInt32()), Axis = ReadBytes(reader, reader.ReadInt32()),
                Version = reader.ReadInt32()
            };
        return model;
    }

    /// <summary>读取同为四字节元素的位置、法线、索引或颜色数组。</summary>
    /// <typeparam name="T">单精度数、有符号整数或颜色整数。</typeparam>
    /// <param name="reader">当前模型包。</param>
    /// <returns>可直接用于原生绘制的数组。</returns>
    private static T[] ReadArray<T>(BinaryReader reader) where T : struct
    {
        int count = reader.ReadInt32();
        byte[] bytes = ReadBytes(reader, checked(count * 4));
        T[] values = new T[count];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    /// <summary>完整读取指定长度，模型包被截断时直接报告读取错误。</summary>
    /// <param name="reader">当前模型包。</param>
    /// <param name="count">需要的字节数。</param>
    /// <returns>完整数据。</returns>
    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException("三维模型包不完整。");
        return bytes;
    }

    /// <summary>读取与原生坐标一致的三维向量。</summary>
    /// <param name="reader">当前模型包。</param>
    /// <returns>横向、前后和高度。</returns>
    private static Vec3 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}
#endif
