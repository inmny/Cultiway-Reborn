#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstPerson.Core;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Worldfall;

/// <summary>读取由 Blender 导出的网格、材质和动作部件。</summary>
internal static class WorldfallModelFiles
{
    /// <summary>从模组资源目录读取各物体的三档模型和建筑状态。</summary>
    /// <param name="models">按 Worldfall 名称共用的模型表。</param>
    internal static void Load(Dictionary<string, MeshModel> models)
    {
        string directory = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Worldfall", "Models");
        if (!Directory.Exists(directory)) return;
        int assets = 0;
        foreach (string path in Directory.GetFiles(directory, "models.json", SearchOption.AllDirectories))
        {
            JObject document = JObject.Parse(File.ReadAllText(path));
            foreach (JObject exported in (JArray)document["Models"])
            {
                MeshModel model = exported.ToObject<MeshModel>();
                string info = string.Join(";", ((JObject)exported["Info"]).Properties().Select(property => property.Name + "=" + property.Value));
                WorldfallMeshes.SetInfo(model, info);
                WorldfallModelIntegration.PrepareDaemonProportions(model);
                WorldfallNatureMaterials.Apply((string)document["Id"], model);
                if ((string)document["Category"] == "building")
                {
                    WorldfallMeshes.SetInfo(model, "cultiway_building=1");
                }
                models[model.Name] = model;
            }
            assets++;
        }
        ModClass.LogInfo($"[Worldfall] 已读取 {assets} 个 Blender 物体的网格。");
    }
}
#endif
