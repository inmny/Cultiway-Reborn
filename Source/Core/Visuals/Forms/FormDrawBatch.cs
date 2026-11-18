using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>两种显示共用的实际部件；几何共享，姿态、颜色和成形独立。</summary>
internal struct FormDrawItem
{
    /// <summary>保持本次实际使用的完整资源版本。</summary>
    internal FormResource Resource;
    /// <summary>本视点选定的共享或动态网格。</summary>
    internal Mesh Mesh;
    /// <summary>所属版本的共享表面。</summary>
    internal Material Material;
    /// <summary>完整世界变换与法线变换。</summary>
    internal Matrix4x4 Transform, Normal;
    /// <summary>本实例的实际骨架，空值表示刚性。</summary>
    internal Matrix4x4[] Skin;
    /// <summary>已解析的实例色与仅显示染色。</summary>
    internal Color Tint, Dye;
    /// <summary>共同成形进度和截面标记。</summary>
    internal Vector4 Build;
    /// <summary>局部表面的世界位置及半径；强度放在Build第四分量。</summary>
    internal Vector4 Contact;
    /// <summary>原版承托高度及是否共用脚下高度。</summary>
    internal Vector4 Anchor;
    /// <summary>封闭水冰共同承担一份光程。</summary>
    internal bool ClosedVolume;
    /// <summary>原版读取的真实地表；不同快照不混批。</summary>
    internal Texture Terrain;
    /// <summary>地表尺寸和范围外高度。</summary>
    internal Vector4 TerrainSize;
}

/// <summary>按网格、表面、地表及通道合批，整帧一次上传，显式提交实例。</summary>
internal sealed class FormDrawBatch
{
    /// <summary>实例参数、骨架、批次起点及共同深度的着色器入口。</summary>
    private static readonly int Instances = Shader.PropertyToID("_FormInstances"), Poses = Shader.PropertyToID("_FormPoses"),
        Start = Shader.PropertyToID("_FormBatchStart"), DepthScale = Shader.PropertyToID("_NativeDepthScale"), DepthBias = Shader.PropertyToID("_NativeDepthBias");
    /// <summary>本帧实际归组，索引指向复用组。</summary>
    private readonly Dictionary<(Mesh, Material, Texture, int), int> indices = new();
    /// <summary>只增长到实际峰值的组容器。</summary>
    private readonly List<Group> groups = new();
    /// <summary>实例连续参数与骨架矩阵行，不因批次数重复上传。</summary>
    private readonly List<Vector4> data = new(), poses = new();
    /// <summary>同一发布姿态在多个部件间共享上传。</summary>
    private readonly Dictionary<Matrix4x4[], int> poseOffsets = new();
    /// <summary>每次提交都会复制的属性块。</summary>
    private readonly MaterialPropertyBlock properties = new();
    /// <summary>本显示入口拥有的图形缓冲，退出或换世界时释放。</summary>
    private ComputeBuffer instanceBuffer, poseBuffer;
    /// <summary>本帧有效组数，峰值容器保留供下帧复用。</summary>
    private int groupCount;

    /// <summary>具备实例及结构缓冲的设备使用合批；开发验证可选择同数据单件提交作对照。</summary>
    internal bool UseInstancing = SystemInfo.supportsInstancing && SystemInfo.graphicsShaderLevel >= 45;
    /// <summary>本次实际准备的部件实例数量。</summary>
    internal int InstanceCount { get; private set; }
    /// <summary>本次实际录入命令的绘制调用数量，包含实体和透明通道。</summary>
    internal int DrawCount { get; private set; }
    /// <summary>当前真实缓冲容量，不含共享网格和固定透明工作区。</summary>
    internal long BufferBytes => ((long)(instanceBuffer?.count ?? 0) + (poseBuffer?.count ?? 0)) * 16;
    /// <summary>本帧上传的实例及去重骨架字节数。</summary>
    internal int UploadBytes { get; private set; }

    /// <summary>清除上帧的资源引用，保留实际用过的列表和缓冲容量。</summary>
    internal void Begin()
    {
        for (int i = 0; i < groupCount; i++) { groups[i].Items.Clear(); groups[i].Key = default; }
        groupCount = 0; InstanceCount = 0; DrawCount = 0; UploadBytes = 0;
        indices.Clear(); data.Clear(); poses.Clear(); poseOffsets.Clear();
    }

    /// <summary>同一几何、材质、地表及通道进入同组，实例参数不限制合批。</summary>
    /// <param name="item">已经确定的真实部件。</param><param name="pass">实体或透明绘制通道。</param>
    internal void Add(FormDrawItem item, int pass)
    {
        var key = (item.Mesh, item.Material, item.Terrain, pass);
        if (!indices.TryGetValue(key, out int index))
        {
            index = groupCount++;
            if (index == groups.Count) groups.Add(new Group());
            groups[index].Key = key; indices.Add(key, index);
        }
        groups[index].Items.Add(item); InstanceCount++;
    }

    /// <summary>先排完整帧的实例和骨架，再各上传一次，避免早先命令读到最后一组参数。</summary>
    internal void Upload()
    {
        if (!UseInstancing || InstanceCount == 0) return;
        for (int group = 0; group < groupCount; group++)
        {
            Group current = groups[group]; current.Start = data.Count / 12;
            foreach (FormDrawItem item in current.Items)
            {
                int pose = -1;
                if (item.Skin != null && !poseOffsets.TryGetValue(item.Skin, out pose))
                {
                    pose = poses.Count; poseOffsets.Add(item.Skin, pose);
                    foreach (Matrix4x4 bone in item.Skin) MatrixRows(poses, bone);
                }
                MatrixRows(data, item.Transform); MatrixRows(data, item.Normal);
                Vector4 build = item.Build;
                if (build.w > 0f)
                {
                    // 沿用已有数值缓冲和12行实例；旧材质忽略第四分量，骨架仍按原明确偏移读取。
                    int contact = poses.Count;
                    poses.Add(item.Contact); poses.Add(new Vector4(build.w, 0, 0, 0));
                    build.w = contact + 1;
                }
                data.Add(item.Tint); data.Add(item.Dye); data.Add(build);
                // 第三分量低位保留封闭体标记，高位声明原版的地下遮挡。
                data.Add(new Vector4(item.Anchor.x, item.Anchor.y, (item.ClosedVolume ? 1f : 0f) + (item.Resource?.Description.HideBelowGround == true ? 2f : 0f), pose));
            }
        }
        if (poses.Count == 0) poses.Add(Vector4.zero);
        Resize(ref instanceBuffer, data.Count); Resize(ref poseBuffer, poses.Count);
        instanceBuffer.SetData(data); poseBuffer.SetData(poses);
        UploadBytes = (data.Count + poses.Count) * 16;
    }

    /// <summary>只提交本通道，各组独立骨架、染色、透明体及成形参数来自同一帧缓冲。</summary>
    /// <param name="commands">实际相机的命令。</param><param name="pass">当前绘制通道。</param>
    /// <param name="scale">原版共同深度倍率，三维忽略。</param><param name="bias">原版共同深度偏移，三维忽略。</param>
    internal void Draw(CommandBuffer commands, int pass, float scale = 1f, float bias = 0f)
    {
        for (int i = 0; i < groupCount; i++)
        {
            Group group = groups[i];
            // 原版相机可能比场景更新晚一帧；到期的短暂形体不再提交。
            if (group.Key.Pass != pass || group.Key.Mesh == null) continue;
            properties.Clear();
            FormDrawItem first = group.Items[0];
            properties.SetFloat(DepthScale, scale); properties.SetFloat(DepthBias, bias);
            properties.SetFloat("_TerrainEnabled", first.Terrain == null ? 0f : 1f);
            if (first.Terrain != null) { properties.SetTexture("_TerrainHeights", first.Terrain); properties.SetVector("_TerrainSize", first.TerrainSize); }
            if (UseInstancing)
            {
                properties.SetBuffer(Instances, instanceBuffer); properties.SetBuffer(Poses, poseBuffer); properties.SetInt(Start, group.Start);
                commands.DrawMeshInstancedProcedural(group.Key.Mesh, 0, group.Key.Material, pass, group.Items.Count, properties); DrawCount++;
            }
            else foreach (FormDrawItem item in group.Items)
                {
                    properties.SetMatrix("_FormMatrix", item.Transform); properties.SetMatrix("_FormNormalMatrix", item.Normal);
                    properties.SetColor("_Color", item.Tint); properties.SetColor("_Dye", item.Dye); properties.SetVector("_FormBuild", item.Build);
                    properties.SetVector("_FormContact", item.Contact);
                    properties.SetVector("_TerrainAnchor", item.Anchor); properties.SetFloat("_FormClosedVolume", item.ClosedVolume ? 1f : 0f);
                    properties.SetFloat("_FormHideBelowGround", item.Resource?.Description.HideBelowGround == true ? 1f : 0f);
                    properties.SetFloat("_UseSkinning", item.Skin == null ? 0f : 1f);
                    if (item.Skin != null) properties.SetMatrixArray("_FormBones", item.Skin);
                    commands.DrawMesh(item.Mesh, item.Transform, item.Material, 0, pass, properties); DrawCount++;
                }
        }
    }

    /// <summary>矩阵按显式行写入，避免不同图形接口对结构矩阵存放顺序的差异。</summary>
    /// <param name="target">本帧连续参数。</param><param name="matrix">实际完整矩阵。</param>
    private static void MatrixRows(List<Vector4> target, Matrix4x4 matrix)
    { for (int row = 0; row < 4; row++) target.Add(matrix.GetRow(row)); }

    /// <summary>仅容量不足时增长，平常帧不重新分配原生缓冲。</summary>
    /// <param name="buffer">本入口拥有的缓冲。</param><param name="count">当前需要的向量数。</param>
    private static void Resize(ref ComputeBuffer buffer, int count)
    {
        if (buffer != null && buffer.count >= count) return;
        buffer?.Release(); buffer = new ComputeBuffer(Mathf.NextPowerOfTwo(count), 16, ComputeBufferType.Structured);
    }

    /// <summary>世界切换、停用或退出时释放本入口的全部原生缓冲与引用。</summary>
    internal void Clear()
    {
        Begin(); instanceBuffer?.Release(); poseBuffer?.Release(); instanceBuffer = null; poseBuffer = null;
    }

    /// <summary>同一共享几何与环境的一次实际批量提交。</summary>
    private sealed class Group
    {
        /// <summary>该组实际共享的资源和通道。</summary>
        internal (Mesh Mesh, Material Material, Texture Terrain, int Pass) Key;
        /// <summary>实际独立实例，清帧后立即解除旧资源引用。</summary>
        internal readonly List<FormDrawItem> Items = new();
        /// <summary>本组在整帧连续实例参数中的起点。</summary>
        internal int Start;
    }
}
