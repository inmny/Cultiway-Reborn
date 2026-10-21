using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>世界空间程序化视觉共享的材质资源。</summary>
internal static class WorldVisualResources
{
    /// <summary>按运行平台选择 Shader Bundle 文件名，与资源工程打包脚本的输出一一对应。</summary>
    private static string ShaderBundleRelativePath
    {
        get
        {
            string bundleName = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "cultiway_shaders_windows",
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => "cultiway_shaders_linux",
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => "cultiway_shaders_osx",
                _ => throw new InvalidOperationException(
                    $"Cultiway Shader Bundle 不支持的平台: {Application.platform}"),
            };
            return $"Content/AssetBundles/{bundleName}";
        }
    }
    private const string WeaponSweepShaderAssetPath = "Assets/Cultiway/Shaders/WeaponSweep.shader";
    private const string WeaponThrustShaderAssetPath = "Assets/Cultiway/Shaders/WeaponThrust.shader";
    private static Material transparentSpriteMaterial;
    /// <summary>能量向背景叠加亮度，实体仍使用普通透明材质。</summary>
    private static Material additiveEnergyMaterial;
    /// <summary>同一表面材质按顶点资料区分火、水、岩石等质感。</summary>
    private static Material surfaceMaterial;
    private static AssetBundle shaderBundle;
    private static Shader weaponSweepShader;
    private static Shader weaponThrustShader;
    private static Material weaponSweepCoreMaterial;
    private static Material weaponSweepGlowMaterial;
    private static Material weaponThrustCoreMaterial;
    private static Material weaponThrustGlowMaterial;

    /// <summary>返回支持纹理、顶点色和透明混合的共享材质。</summary>
    internal static Material TransparentSpriteMaterial
    {
        get
        {
            if (transparentSpriteMaterial != null) return transparentSpriteMaterial;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader == null) throw new InvalidOperationException("找不到世界视觉所需的透明 Shader");
            transparentSpriteMaterial = new Material(shader)
            {
                name = "Cultiway_WorldVisualTransparentMaterial",
                hideFlags = HideFlags.DontSave,
            };
            return transparentSpriteMaterial;
        }
    }

    /// <summary>使用游戏自带粒子材质表现发光，避免能量球显示成不透明塑料。</summary>
    internal static Material AdditiveEnergyMaterial => additiveEnergyMaterial ??= new Material(Shader.Find("Legacy Shaders/Particles/Additive"))
    {
        name="Cultiway_SpatialEnergy",hideFlags=HideFlags.DontSave
    };

    /// <summary>读取随模组发布的主体材质，动画时钟由真实世界推进。</summary>
    internal static Material SurfaceMaterial => surfaceMaterial ??= new Material(LoadShader("Assets/Cultiway/Shaders/SpatialSurface.shader"))
    {
        name="Cultiway_SpatialSurface",hideFlags=HideFlags.DontSave
    };

    /// <summary>返回扫掠扇面实体层共用的程序化材质。</summary>
    internal static Material WeaponSweepCoreMaterial =>
        weaponSweepCoreMaterial ??= CreateWeaponMaterial(
            "Cultiway_WeaponSweepCore", WeaponSweepShader, 0f, 1.15f);

    /// <summary>返回扫掠扇面外围柔光层共用的程序化材质。</summary>
    internal static Material WeaponSweepGlowMaterial =>
        weaponSweepGlowMaterial ??= CreateWeaponMaterial(
            "Cultiway_WeaponSweepGlow", WeaponSweepShader, 1f, 0.72f);

    /// <summary>返回轴向突刺枪芒实体层共用的程序化材质。</summary>
    internal static Material WeaponThrustCoreMaterial =>
        weaponThrustCoreMaterial ??= CreateWeaponMaterial(
            "Cultiway_WeaponThrustCore", WeaponThrustShader, 0f, 1.55f);

    /// <summary>返回轴向突刺枪芒外围柔光层共用的程序化材质。</summary>
    internal static Material WeaponThrustGlowMaterial =>
        weaponThrustGlowMaterial ??= CreateWeaponMaterial(
            "Cultiway_WeaponThrustGlow", WeaponThrustShader, 1f, 1.15f);

    /// <summary>从专用 AssetBundle 加载扫掠 Shader。</summary>
    private static Shader WeaponSweepShader
    {
        get
        {
            if (weaponSweepShader != null) return weaponSweepShader;
            weaponSweepShader = LoadShader(WeaponSweepShaderAssetPath);
            return weaponSweepShader;
        }
    }

    /// <summary>从专用 AssetBundle 加载轴向突刺 Shader。</summary>
    private static Shader WeaponThrustShader
    {
        get
        {
            if (weaponThrustShader != null) return weaponThrustShader;
            weaponThrustShader = LoadShader(WeaponThrustShaderAssetPath);
            return weaponThrustShader;
        }
    }

    /// <summary>按固定 Mod 相对路径加载并常驻 Shader Bundle。</summary>
    private static AssetBundle ShaderBundle
    {
        get
        {
            if (shaderBundle != null) return shaderBundle;
            string fullPath = Path.Combine(
                ModClass.I.GetDeclaration().FolderPath,
                ShaderBundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("找不到 Cultiway Shader Bundle", fullPath);
            }
            shaderBundle = AssetBundle.LoadFromFile(fullPath);
            if (shaderBundle == null)
            {
                throw new InvalidDataException($"无法加载 Cultiway Shader Bundle: {fullPath}");
            }
            return shaderBundle;
        }
    }

    /// <summary>从常驻 Bundle 取得指定路径的 Shader，并在资产缺失时立即报告构建错误。</summary>
    internal static Shader LoadShader(string assetPath)
    {
        Shader shader = ShaderBundle.LoadAsset<Shader>(assetPath);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader Bundle 中缺少资产: {assetPath}");
        }
        return shader;
    }

    /// <summary>创建只保存程序化视觉层级差异的共享材质。</summary>
    private static Material CreateWeaponMaterial(
        string name,
        Shader shader,
        float layerMode,
        float flowSpeed)
    {
        var material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave,
        };
        material.SetFloat("_LayerMode", layerMode);
        material.SetFloat("_FlowSpeed", flowSpeed);
        material.SetFloat("_Opacity", 1f);
        return material;
    }
}
