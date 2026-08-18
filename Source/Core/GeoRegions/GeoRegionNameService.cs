using System;
using System.Collections.Generic;
using Cultiway.Core.GeoRegions.Partitioning;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 只根据世界编号和地区计算结果生成基础名称，不读取游戏世界、资产管理器或全局随机状态。
/// 因此同一个世界中的同一地区会稳定得到相同名称。
/// </summary>
internal sealed class GeoRegionNameService
{
    // 世界编号参与名称随机种子的计算，使不同世界可以得到不同名称。
    private readonly int worldSeedId;

    /// <summary>
    /// 创建一个只服务于指定世界的地区名称生成器。
    /// </summary>
    internal GeoRegionNameService(int worldSeedId)
    {
        this.worldSeedId = worldSeedId;
    }

    /// <summary>
    /// 根据地区分类和生物群系选择修饰词与地貌称谓，生成稳定的基础名称。
    /// 未认识的生物群系会尽量使用其显示名；无法生成时使用调用方提供的备用名称。
    /// </summary>
    internal string Generate(
        GeoRegionDescriptor descriptor,
        string fallbackName,
        string biomeDisplayName)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        NamingProfile category = ResolveCategoryProfile(descriptor.CategoryCode);
        string biomeId = ResolveNamingBiomeId(descriptor);
        BiomeProfile biome = ResolveBiomeProfile(biomeId);
        var random = new StableRandom(CreateSeed(descriptor));

        string generated;
        if (!string.IsNullOrEmpty(biomeId) && biome == null)
        {
            generated = BuildUnknownBiomeName(category, biomeDisplayName, biomeId, ref random);
        }
        else
        {
            string[] semanticModifiers = biome?.Modifiers ?? category.Modifiers;
            string[] classifiers = descriptor.CategoryCode == GeoRegionCategoryCode.PrimarySpecial &&
                                   biome?.PrimaryClassifiers.Length > 0
                ? biome.PrimaryClassifiers
                : category.Classifiers;
            generated = BuildName(semanticModifiers, classifiers, ref random);
        }

        return string.IsNullOrWhiteSpace(generated)
            ? string.IsNullOrWhiteSpace(fallbackName) ? "GeoRegion" : fallbackName.Trim()
            : generated.Trim();
    }

    /// <summary>
    /// 选择命名最能代表该地区的生物群系：基础地区用核心群系，其他层级用占比最高的群系。
    /// </summary>
    internal static string ResolveNamingBiomeId(GeoRegionDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        return descriptor.Layer == GeoRegionLayer.Primary
            ? descriptor.CoreBiomeId
            : descriptor.DominantBiomeId;
    }

    /// <summary>
    /// 各选一个修饰词和地貌称谓，并组合成名称。
    /// </summary>
    private static string BuildName(
        string[] modifiers,
        string[] classifiers,
        ref StableRandom random)
    {
        return AppendClassifier(
            Pick(modifiers, ref random),
            Pick(classifiers, ref random));
    }

    /// <summary>
    /// 遇到未内置命名词表的生物群系时，用其显示名作为修饰部分，再搭配地区分类称谓。
    /// </summary>
    private static string BuildUnknownBiomeName(
        NamingProfile category,
        string biomeDisplayName,
        string biomeId,
        ref StableRandom random)
    {
        string semantic = NormalizeBiomeDisplayName(biomeDisplayName, biomeId);
        string classifier = Pick(category.Classifiers, ref random);
        return AppendClassifier(semantic, classifier);
    }

    /// <summary>
    /// 拼接修饰词和地貌称谓，并合并首尾重复文字，例如避免名称末尾出现重复的“原”。
    /// </summary>
    private static string AppendClassifier(string modifier, string classifier)
    {
        string left = modifier ?? string.Empty;
        string right = classifier ?? string.Empty;
        if (string.IsNullOrEmpty(left)) return right;
        if (string.IsNullOrEmpty(right) || left.EndsWith(right, StringComparison.Ordinal)) return left;
        int maximumOverlap = Math.Min(left.Length, right.Length);
        for (int length = maximumOverlap; length > 0; length--)
        {
            if (string.CompareOrdinal(left, left.Length - length, right, 0, length) == 0)
            {
                return left + right.Substring(length);
            }
        }
        return left + right;
    }

    /// <summary>
    /// 清理生物群系显示名中的通用后缀；无可用译名时把编号转换成可读文字。
    /// </summary>
    private static string NormalizeBiomeDisplayName(string displayName, string biomeId)
    {
        string normalizedDisplay = displayName?.Trim();
        string result = string.IsNullOrWhiteSpace(normalizedDisplay) ||
                        string.Equals(normalizedDisplay, biomeId, StringComparison.Ordinal) ||
                        normalizedDisplay.StartsWith("biome_", StringComparison.OrdinalIgnoreCase)
            ? HumanizeBiomeId(biomeId)
            : normalizedDisplay;
        string[] suffixes = { "生物群系", "群系", "Biome", "biome" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (!result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            result = result.Substring(0, result.Length - suffix.Length).Trim();
            break;
        }
        return string.IsNullOrEmpty(result) ? "异域" : result;
    }

    /// <summary>
    /// 去掉生物群系编号的常见前缀、命名空间和分隔符，得到可用于名称的文字。
    /// </summary>
    private static string HumanizeBiomeId(string biomeId)
    {
        if (string.IsNullOrWhiteSpace(biomeId)) return "异域";
        string result = biomeId.Trim();
        if (result.StartsWith("biome_", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring("biome_".Length);
        }
        int namespaceSeparator = result.LastIndexOfAny(new[] { ':', '/', '\\' });
        if (namespaceSeparator >= 0 && namespaceSeparator + 1 < result.Length)
        {
            result = result.Substring(namespaceSeparator + 1);
        }
        string[] words = result.Split(
            new[] { '_', '-', '.', ' ' },
            StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? "异域" : string.Join(" ", words);
    }

    /// <summary>
    /// 综合世界、地区层级、分类、位置和生物群系生成稳定种子，让名称不受调用顺序影响。
    /// </summary>
    private ulong CreateSeed(GeoRegionDescriptor descriptor)
    {
        ulong hash = 14695981039346656037UL;
        AddSeedValue(ref hash, worldSeedId);
        AddSeedValue(ref hash, (int)descriptor.Layer);
        AddSeedValue(ref hash, (int)descriptor.CategoryCode);
        AddSeedValue(ref hash, GetMinimumTileId(descriptor));
        AddSeedValue(ref hash, (int)descriptor.DominantPrimaryCode);
        AddSeedValue(ref hash, (int)descriptor.DominantLandformCode);
        AddSeedValue(ref hash, ResolveNamingBiomeId(descriptor));
        return hash;
    }

    private static int GetMinimumTileId(GeoRegionDescriptor descriptor)
    {
        int minimum = int.MaxValue;
        for (int i = 0; i < descriptor.TileCount; i++)
        {
            int tileId = descriptor.GetTileId(i);
            if (tileId < minimum) minimum = tileId;
        }
        return minimum == int.MaxValue ? 0 : minimum;
    }

    private static void AddSeedValue(ref ulong hash, int value)
    {
        unchecked
        {
            uint encoded = (uint)value;
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(encoded >> shift);
                hash *= 1099511628211UL;
            }
        }
    }

    private static void AddSeedValue(ref ulong hash, string value)
    {
        string normalized = value ?? string.Empty;
        AddSeedValue(ref hash, normalized.Length);
        unchecked
        {
            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                hash ^= (byte)character;
                hash *= 1099511628211UL;
                hash ^= (byte)(character >> 8);
                hash *= 1099511628211UL;
            }
        }
    }

    /// <summary>
    /// 从词表中稳定选取一项；空词表返回空字符串。
    /// </summary>
    private static string Pick(string[] values, ref StableRandom random)
    {
        return values.Length == 0 ? string.Empty : values[random.Next(values.Length)];
    }

    private static NamingProfile ResolveCategoryProfile(GeoRegionCategoryCode code)
    {
        int index = (int)code;
        if ((uint)index >= (uint)CategoryProfiles.Length || CategoryProfiles[index] == null)
        {
            throw new InvalidOperationException($"GeoRegion 缺少分类命名 profile: category={code}");
        }
        return CategoryProfiles[index];
    }

    /// <summary>
    /// 查找指定生物群系的专用命名词表；未登记时返回空，交由通用名称流程处理。
    /// </summary>
    private static BiomeProfile ResolveBiomeProfile(string biomeId)
    {
        return !string.IsNullOrEmpty(biomeId) && BiomeProfiles.TryGetValue(biomeId, out BiomeProfile profile)
            ? profile
            : null;
    }

    /// <summary>
    /// 建立每种地区分类的修饰词和地貌称谓词表，并检查所有分类都已覆盖。
    /// </summary>
    private static NamingProfile[] BuildCategoryProfiles()
    {
        var profiles = new NamingProfile[(int)GeoRegionCategoryCode.Archipelago + 1];
        AddCategory(profiles, GeoRegionCategoryCode.PrimarySea, "沧,碧,玄,澜,潮,苍", "海,洋,海域");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryLake, "镜,澄,碧,静,月,青", "湖,泊,泽");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryRiver, "长,清,曲,银,奔,云", "河,川,水");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryLava, "炎,熔,赤,烬,炽,焰", "火原,熔谷,炎池,火境");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryGoo, "灰,腐,毒,瘴,蚀,浊", "疫地,污泽,腐原,泥沼");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryMountains, "苍,玄,云,寒,峻,霜", "山脉,群峰,岭");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryGrassland, "青,翠,风,晴,牧,芳", "原,野,甸,坪,草场");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryForest, "苍,翠,青,幽,松,枫", "林,森,林海,木原");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryJungle, "莽,翠,雨,藤,幽,绿", "雨林,密林,藤海,莽林");
        AddCategory(profiles, GeoRegionCategoryCode.PrimarySwamp, "泥,雾,苔,湿,幽,绿", "沼,泽,淖,湿地");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryDesert, "金,赤,炎,旱,燧,黄", "漠,沙海,沙原,旱地");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryBeach, "白,金,潮,晴,贝,珊", "滩,沙岸,汀,海滨");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryTundra, "霜,雪,冻,凛,寒,白", "雪原,冻土,寒野,冰原");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryHighlands, "高,云,苍,风,青,峻", "高原,高地,台地,塬");
        AddCategory(profiles, GeoRegionCategoryCode.PrimaryWasteland, "荒,枯,焦,灰,寂,断", "荒原,废土,焦野,荒地");
        AddCategory(profiles, GeoRegionCategoryCode.PrimarySpecial, "奇,幻,秘,灵,异,玄", "境,域,原,地");
        AddCategory(profiles, GeoRegionCategoryCode.LandformPlain, "苍,青,晴,风,广,静", "平原,平野,原野");
        AddCategory(profiles, GeoRegionCategoryCode.LandformMountain, "云,玄,峻,寒,苍,玉", "山地,山系,岭");
        AddCategory(profiles, GeoRegionCategoryCode.LandformCanyon, "断,赤,玄,深,回,裂", "峡,峡谷,壑");
        AddCategory(profiles, GeoRegionCategoryCode.LandformBasin, "静,深,环,苍,幽,云", "盆地,谷地,洼地");
        AddCategory(profiles, GeoRegionCategoryCode.LandmassIsland, "古,新,晴,雾,星,月,潮,玉", "岛,屿,礁,渚");
        AddCategory(profiles, GeoRegionCategoryCode.LandmassContinent, "沧,玄,雍,荆,云,苍,坤,华", "洲,陆,大地");
        AddCategory(profiles, GeoRegionCategoryCode.LandmassMainland, "大,古,玄,坤,天,苍,云,广", "大陆,大洲,广陆,天陆");
        AddCategory(profiles, GeoRegionCategoryCode.Peninsula, "断,长,云,潮,玉,苍", "半岛,岬地,陆岬");
        AddCategory(profiles, GeoRegionCategoryCode.Strait, "回,断,双,玉,玄,潮", "海峡,水道,海门");
        AddCategory(profiles, GeoRegionCategoryCode.Archipelago, "星,链,云,潮,玉,苍", "群岛,岛链,列岛");
        for (int index = 1; index < profiles.Length; index++)
        {
            if (profiles[index] == null)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 分类命名 profile 未覆盖: category={(GeoRegionCategoryCode)index}");
            }
        }
        return profiles;
    }

    /// <summary>
    /// 建立已知生物群系的专用修饰词，以及必要时使用的专用称谓。
    /// </summary>
    private static Dictionary<string, BiomeProfile> BuildBiomeProfiles()
    {
        var profiles = new Dictionary<string, BiomeProfile>(StringComparer.Ordinal);
        AddBiome(profiles, "biome_grass", "青,翠,青草,晴,芳");
        AddBiome(profiles, "biome_savanna", "金草,旱风,苍黄,长草,烈阳");
        AddBiome(profiles, "biome_jungle", "莽,雨,藤,翠叶,幽绿");
        AddBiome(profiles, "biome_desert", "金沙,赤沙,炎砂,旱风,燧黄");
        AddBiome(profiles, "biome_lemon", "柠,金柠,酸香,明黄,柠风", "原,林,园,境");
        AddBiome(profiles, "biome_permafrost", "霜,雪,冻,凛,寒白");
        AddBiome(profiles, "biome_swamp", "泥雾,苔,湿,幽沼,绿泽");
        AddBiome(profiles, "biome_crystal", "晶,琉璃,棱光,辉晶,澄晶", "原,谷,境,林");
        AddBiome(profiles, "biome_enchanted", "灵,祝福,仙木,辉叶,秘林", "森,原,境,谷");
        AddBiome(profiles, "biome_corrupted", "腐,咒,黯蚀,秽,幽腐", "境,土,原,泽");
        AddBiome(profiles, "biome_infernal", "炎狱,烬,焦,炼狱,赤焰", "狱,焦土,原,境");
        AddBiome(profiles, "biome_candy", "糖,蜜,甜晶,彩糖,蜜露", "原,林,境,谷");
        AddBiome(profiles, "biome_mushroom", "菌,孢,蕈,幽菇,菌伞", "林,原,泽,境");
        AddBiome(profiles, "biome_wasteland", "荒,枯,焦,灰,寂");
        AddBiome(profiles, "biome_birch", "白桦,银桦,霜木,白林,银叶");
        AddBiome(profiles, "biome_maple", "枫,丹枫,红叶,赤枫,秋林");
        AddBiome(profiles, "biome_rocklands", "荒石,铁岩,苍岩,砾,石风");
        AddBiome(profiles, "biome_garlic", "蒜香,银蒜,辛香,白蒜,驱邪", "原,田,园,境");
        AddBiome(profiles, "biome_flower", "百花,芳,绮花,香风,花海");
        AddBiome(profiles, "biome_celestial", "星,天穹,辉光,圣辉,辰光", "原,境,地,圣域");
        AddBiome(profiles, "biome_clover", "四叶,幸运,翠叶,青叶,福草");
        AddBiome(profiles, "biome_singularity", "奇点,引力,虚空,坍缩,无光", "泽,境,域,渊");
        AddBiome(profiles, "biome_paradox", "悖论,逆时,错序,回环,异序", "原,境,域,地");
        AddBiome(profiles, "biome_sand", "金沙,白沙,潮沙,晴沙,贝沙");
        AddBiome(profiles, "biome_hill", "青丘,风丘,苍丘,高丘,云丘");
        AddBiome(profiles, "biome_biomass", "生质,血肉,脉动,增殖,赤肉", "原,海,地,巢");
        AddBiome(profiles, "biome_cybertile", "赛博,矩阵,电路,霓光,机械", "原,域,地,境");
        AddBiome(profiles, "biome_pumpkin", "南瓜,金瓜,丰收,橙灯,瓜藤", "原,田,园,境");
        AddBiome(profiles, "biome_tumor", "肿瘤,癌殖,血瘤,畸生,肉瘤", "地,原,巢,境");
        AddBiome(profiles, "biome_bamboo", "竹,青篁,翠竹,风竹,幽篁", "海,原,林,谷");
        AddBiome(profiles, "biome_candle", "烛,明焰,灯火,蜡光,烛影", "原,林,境,野");
        AddBiome(profiles, "biome_cemetery", "墓,幽冢,寂骨,冥火,古坟", "原,地,林,境");
        AddBiome(profiles, "biome_coral", "珊瑚,彩礁,红珊,海晶,潮彩", "原,林,境,地");
        AddBiome(profiles, "biome_dark", "暗,黯夜,幽影,黑月,暮色", "林,原,境,地");
        AddBiome(profiles, "biome_fern", "蕨,古蕨,苍莽,远古,巨叶", "林,原,谷,境");
        AddBiome(profiles, "biome_fleshblood", "血肉,赤骸,脉动,肉林,猩红", "原,地,巢,境");
        AddBiome(profiles, "biome_knowledge", "知识,书页,启明,智慧,秘典", "林,原,境,地");
        AddBiome(profiles, "biome_oak", "橡,古橡,苍木,巨木,橡冠", "林,森,谷,原");
        AddBiome(profiles, "biome_rice", "稻,金穗,水稻,丰年,青禾", "田,原,泽,野");
        AddBiome(profiles, "biome_titans", "泰坦,巨灵,古神,巨人,擎天", "原,谷,境,遗土");
        return profiles;
    }

    private static void AddCategory(
        NamingProfile[] profiles,
        GeoRegionCategoryCode code,
        string modifiers,
        string classifiers)
    {
        profiles[(int)code] = new NamingProfile(SplitWords(modifiers), SplitWords(classifiers));
    }

    private static void AddBiome(
        Dictionary<string, BiomeProfile> profiles,
        string biomeId,
        string modifiers,
        string primaryClassifiers = null)
    {
        profiles.Add(
            biomeId,
            new BiomeProfile(
                SplitWords(modifiers),
                SplitWords(primaryClassifiers)));
    }

    private static string[] SplitWords(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    // 所有实例共用的只读命名词表，在类型首次使用时一次性建立。
    private static readonly NamingProfile[] CategoryProfiles = BuildCategoryProfiles();
    private static readonly Dictionary<string, BiomeProfile> BiomeProfiles = BuildBiomeProfiles();

    /// <summary>
    /// 一种地区分类可使用的修饰词和地貌称谓。
    /// </summary>
    private sealed class NamingProfile
    {
        /// <summary>保存已经拆分好的分类命名词表。</summary>
        internal NamingProfile(string[] modifiers, string[] classifiers)
        {
            Modifiers = modifiers;
            Classifiers = classifiers;
        }

        /// <summary>名称前半部分的候选词。</summary>
        internal string[] Modifiers { get; }

        /// <summary>名称末尾地貌称谓的候选词。</summary>
        internal string[] Classifiers { get; }
    }

    /// <summary>
    /// 一个已知生物群系可使用的修饰词，以及它在基础地区中可覆盖的专用称谓。
    /// </summary>
    private sealed class BiomeProfile
    {
        /// <summary>保存已经拆分好的生物群系命名词表。</summary>
        internal BiomeProfile(string[] modifiers, string[] primaryClassifiers)
        {
            Modifiers = modifiers;
            PrimaryClassifiers = primaryClassifiers;
        }

        /// <summary>体现该生物群系特征的候选词。</summary>
        internal string[] Modifiers { get; }

        /// <summary>基础地区可使用的专用地貌称谓。</summary>
        internal string[] PrimaryClassifiers { get; }
    }

    /// <summary>
    /// 名称生成专用的简单稳定随机数状态，不会读写游戏的全局随机数。
    /// </summary>
    private struct StableRandom
    {
        // 每次取数后都会推进的内部状态。
        private ulong state;

        /// <summary>用稳定种子初始化；零种子改用固定的非零值。</summary>
        internal StableRandom(ulong seed)
        {
            state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>
        /// 返回从零到指定上限之前的一个稳定整数，并推进内部状态。
        /// </summary>
        internal int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            ulong value = state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            state = value;
            return (int)((value * 2685821657736338717UL) % (ulong)exclusiveMax);
        }
    }
}
