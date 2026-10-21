using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>已构建形体的部件、挂点及初始作用形状；不保存单次施放进度。</summary>
public sealed class FormDescription
{
    /// <summary>形体资料版本。</summary>
    public int Version;
    /// <summary>稳定资源编号。</summary>
    public string Id;
    /// <summary>可独立控制的部件。</summary>
    public FormPartDescription[] Parts;
    /// <summary>供附着和动作使用的命名挂点。</summary>
    public FormAnchorDescription[] Anchors;
    /// <summary>随部位控制结构变化的初始作用形状。</summary>
    public FormContactDescription[] Contacts;
}

/// <summary>一个部件的资源和控制枢轴。</summary>
public sealed class FormPartDescription
{
    /// <summary>部件编号。</summary>
    public string Id;
    /// <summary>当前资源的默认显示表面，不承担实际材料规则。</summary>
    public string Surface;
    /// <summary>局部控制枢轴。</summary>
    public float[] Pivot;
    /// <summary>资源包中的网格路径。</summary>
    public string MeshAsset;
}

/// <summary>与部件绑定的命名挂点。</summary>
public sealed class FormAnchorDescription
{
    /// <summary>挂点编号。</summary>
    public string Id;
    /// <summary>所属部件索引。</summary>
    public int Part;
    /// <summary>局部位置。</summary>
    public float[] Position;
    /// <summary>局部方向。</summary>
    public float[] Direction;
}

/// <summary>绑定部位的初始胶囊作用形状，是否生效由运行过程决定。</summary>
public sealed class FormContactDescription
{
    /// <summary>形状编号。</summary>
    public string Id;
    /// <summary>所属部件索引。</summary>
    public int Part;
    /// <summary>轴线起点。</summary>
    public float[] A;
    /// <summary>轴线终点。</summary>
    public float[] B;
    /// <summary>局部半径。</summary>
    public float Radius;
}

/// <summary>一次加载、多个构造共用的形体资源；Unity 对象只在主线程使用。</summary>
public sealed class FormResource
{
    /// <summary>固定的部位说明。</summary>
    public readonly FormDescription Description;
    /// <summary>完整保留表面坐标和法向的共享网格。</summary>
    public readonly Mesh[] Meshes;

    /// <summary>绑定已读取的描述和几何。</summary>
    /// <param name="description">已检查的形体说明。</param>
    /// <param name="meshes">与部件一一对应的网格。</param>
    internal FormResource(FormDescription description, Mesh[] meshes)
    {
        Description = description;
        Meshes = meshes;
    }
}

/// <summary>读取共享形体及表面资源，不在世界存档保存资源对象。</summary>
public static class FormResources
{
    /// <summary>共用形体缓存。</summary>
    private static readonly Dictionary<string, FormResource> forms = new(StringComparer.Ordinal);
    /// <summary>原版与 Worldfall 使用各自的材质实例。</summary>
    private static readonly Dictionary<(string, bool), Material> materials = new();
    /// <summary>按运行平台加载一次的形体资源包。</summary>
    private static AssetBundle bundle;

    /// <summary>读取一个已构建形体，资源边界出错时给出具体路径。</summary>
    /// <param name="id">形体资源文件名，不带路径和扩展名。</param>
    public static FormResource Load(string id)
    {
        if (forms.TryGetValue(id, out FormResource found)) return found;
        if (id != Path.GetFileName(id)) throw new ArgumentException("形体编号不能包含路径", nameof(id));
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Forms", id + ".json");
        FormDescription data = JsonConvert.DeserializeObject<FormDescription>(File.ReadAllText(path));
        if (data?.Version != 1 || data.Parts == null || data.Anchors == null || data.Contacts == null)
            throw new InvalidDataException("不支持或不完整的形体说明：" + path);
        var meshes = new Mesh[data.Parts.Length];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < meshes.Length; i++)
        {
            FormPartDescription part = data.Parts[i];
            if (!ids.Add(part.Id) || part.Pivot?.Length != 3)
                throw new InvalidDataException("重复部位或缺少三维枢轴：" + path + "/" + part.Id);
            meshes[i] = Bundle.LoadAsset<Mesh>(part.MeshAsset);
            if (meshes[i] == null || meshes[i].uv.Length != meshes[i].vertexCount)
                throw new InvalidDataException("网格缺失或表面坐标不完整：" + part.MeshAsset);
        }
        foreach (FormAnchorDescription anchor in data.Anchors)
            if (anchor.Part < 0 || anchor.Part >= meshes.Length || anchor.Position?.Length != 3 || anchor.Direction?.Length != 3)
                throw new InvalidDataException("挂点部位或位置无效：" + path + "/" + anchor.Id);
        foreach (FormContactDescription contact in data.Contacts)
            if (contact.Part < 0 || contact.Part >= meshes.Length || contact.A?.Length != 3 || contact.B?.Length != 3 || contact.Radius <= 0f)
                throw new InvalidDataException("作用部位或形状无效：" + path + "/" + contact.Id);
        found = new FormResource(data, meshes);
        forms.Add(id, found);
        return found;
    }

    /// <summary>读取指定表面在对应视角下的共享材质；颜色覆盖留给每次显示。</summary>
    /// <param name="surface">实际已经提供的显示表面。</param>
    /// <param name="native">是否用于原版投影。</param>
    public static Material Surface(string surface, bool native)
    {
        if (materials.TryGetValue((surface, native), out Material found)) return found;
        (float kind, Color color, float roughness) = surface switch
        {
            "metal" => (0f, new Color(.53f, .64f, .70f), .23f),
            "wood" => (1f, new Color(.36f, .21f, .10f), .8f),
            "leather" => (2f, new Color(.16f, .08f, .05f), .85f),
            "water" => (3f, new Color(.10f, .40f, .62f, .56f), .18f),
            "ice" => (4f, new Color(.48f, .79f, .88f, .73f), .16f),
            _ => throw new InvalidDataException("形体未提供显示表面：" + surface)
        };
        found = new Material(Bundle.LoadAsset<Shader>("Assets/Cultiway/Shaders/FormSurface.shader"))
        { name = "Cultiway_Form_" + surface + (native ? "_Native" : "_Worldfall"), hideFlags = HideFlags.DontSave };
        found.SetTexture("_MainTex", Bundle.LoadAsset<Texture2D>("Assets/Cultiway/Forms/surface_detail.asset"));
        found.SetColor("_Color", color); found.SetFloat("_Kind", kind); found.SetFloat("_Roughness", roughness);
        found.SetFloat("_ZWrite", kind < 3f ? 1f : 0f);
        found.SetShaderPassEnabled(native ? "Worldfall" : "Native", false);
        materials.Add((surface, native), found);
        return found;
    }

    /// <summary>加载当前平台的资源包，与离线构建的名字保持一致。</summary>
    private static AssetBundle Bundle
    {
        get
        {
            if (bundle != null) return bundle;
            string platform = Application.platform switch
            {
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => "linux",
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "windows",
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => "osx",
                _ => throw new PlatformNotSupportedException("当前平台没有形体资源包")
            };
            string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/AssetBundles/cultiway_forms_" + platform);
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidDataException("形体资源包无法加载：" + path);
            return bundle;
        }
    }
}
