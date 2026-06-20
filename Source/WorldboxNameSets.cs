using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    [Dependency(typeof(NameGenerators))]
    public class NameSets : ExtendLibrary<NameSetAsset, NameSets>
    {
        private const string EasternHumanPrefix = "Cultiway.EasternHuman";

        private static readonly (string Id, string City, string Clan, string Culture, string Family, string Kingdom,
            string Language, string Unit, string Religion)[] EasternHumanNameSetSources =
        [
            ("human_default_set", "human_city", "human_clan", "human_culture", "human_clan", "human_kingdom",
                "human_language", "human_unit", "human_religion"),
            ("human_slavic_set", "slavic_city", "slavic_clan", "human_culture", "slavic_clan", "slavic_kingdom",
                "human_language", "slavic_unit", "human_religion"),
            ("human_germanic_set", "germanic_city", "germanic_clan", "human_culture", "germanic_clan",
                "germanic_kingdom", "human_language", "germanic_unit", "human_religion"),
            ("human_rus_set", "rus_city", "rus_clan", "human_culture", "rus_clan", "rus_kingdom",
                "human_language", "rus_unit", "human_religion"),
            ("human_posh_set", "posh_city", "posh_clan", "human_culture", "posh_clan", "posh_kingdom",
                "human_language", "posh_unit", "human_religion"),
            ("human_folk_set", "folk_city", "folk_clan", "human_culture", "folk_clan", "folk_kingdom",
                "human_language", "folk_unit", "human_religion"),
            ("human_pomeranian_set", "pomeranian_city", "pomeranian_clan", "human_culture", "pomeranian_clan",
                "pomeranian_kingdom", "human_language", "pomeranian_unit", "human_religion"),
            ("human_frankish_set", "frankish_city", "frankish_clan", "human_culture", "frankish_clan",
                "frankish_kingdom", "human_language", "frankish_unit", "human_religion"),
            ("human_rome_set", "rome_city", "rome_clan", "human_culture", "rome_clan", "rome_kingdom",
                "human_language", "rome_unit", "human_religion"),
            ("human_iberian_set", "iberian_city", "iberian_clan", "human_culture", "iberian_clan",
                "iberian_kingdom", "human_language", "iberian_unit", "human_religion"),
            ("human_monolux_set", "monolux_city", "monolux_clan", "human_culture", "monolux_clan",
                "monolux_kingdom", "human_language", "monolux_unit", "human_religion")
        ];

        public static readonly string[] EasternHumanTemplateSets = EasternHumanNameSetSources
            .Select(x => EasternHumanNameSet(x.Id))
            .ToArray();

        private static readonly string[] EasternHumanNameGeneratorSources = EasternHumanNameSetSources
            .SelectMany(x => new[] { x.City, x.Clan, x.Culture, x.Family, x.Kingdom, x.Language, x.Unit, x.Religion })
            .Distinct()
            .ToArray();

        protected override bool AutoRegisterAssets() => false;

        protected override void OnInit()
        {
            CloneEasternHumanNameGenerators();
            LogEasternHumanNameGeneratorIds();

            foreach (var source in EasternHumanNameSetSources)
            {
                AddEasternHumanNameSet(source, EasternHumanNameSet, EasternHumanNameGenerator);
                AddEasternHumanNameSet(source, LegacyEasternHumanNameSet, LegacyEasternHumanNameGenerator);
            }
        }

        private static string EasternHumanNameSet(string id)
        {
            return EasternHumanNameId(id);
        }

        private static string EasternHumanNameGenerator(string id)
        {
            return EasternHumanNameId(id);
        }

        private static string LegacyEasternHumanNameSet(string id)
        {
            return LegacyEasternHumanNameId(id);
        }

        private static string LegacyEasternHumanNameGenerator(string id)
        {
            return LegacyEasternHumanNameId(id);
        }

        private static string EasternHumanNameId(string sourceId)
        {
            if (sourceId.StartsWith("human_"))
            {
                return $"{EasternHumanPrefix}_{sourceId["human_".Length..]}";
            }

            return $"{EasternHumanPrefix}_{sourceId}";
        }

        private static string LegacyEasternHumanNameId(string sourceId)
        {
            return $"{EasternHumanPrefix}.{sourceId}";
        }

        private static void CloneEasternHumanNameGenerators()
        {
            foreach (var source in EasternHumanNameGeneratorSources)
            {
                CloneEasternHumanNameGenerator(EasternHumanNameGenerator(source), source);
                CloneEasternHumanNameGenerator(LegacyEasternHumanNameGenerator(source), source);
            }
        }

        private static void CloneEasternHumanNameGenerator(string cloneId, string source)
        {
            if (!AssetManager.name_generator.has(source))
            {
                ModClass.LogError($"({nameof(NameSets)}) Missing source name generator: {source}");
                return;
            }

            var asset = AssetManager.name_generator.has(cloneId)
                ? AssetManager.name_generator.get(cloneId)
                : AssetManager.name_generator.clone(cloneId, source);
            CopyNameGeneratorData(asset, AssetManager.name_generator.get(source));
        }

        private static void CopyNameGeneratorData(NameGeneratorAsset target, NameGeneratorAsset source)
        {
            target.special1 = source.special1?.ToArray();
            target.special2 = source.special2?.ToArray();
            target.vowels = source.vowels?.ToArray();
            target.consonants = source.consonants?.ToArray();
            target.parts = source.parts?.ToArray();
            target.addition_start = source.addition_start?.ToArray();
            target.addition_ending = source.addition_ending?.ToArray();
            target.onomastics_templates = source.onomastics_templates == null
                ? []
                : new List<string>(source.onomastics_templates);
            target.part_groups = source.part_groups == null ? null : new List<string>(source.part_groups);
            target.part_groups2 = source.part_groups2 == null ? null : new List<string>(source.part_groups2);
            target.part_groups3 = source.part_groups3 == null ? null : new List<string>(source.part_groups3);
            target.dict_parts = source.dict_parts == null
                ? null
                : new Dictionary<string, string>(source.dict_parts);
            target.use_dictionary = source.use_dictionary;
            target.templates = source.templates == null
                ? null
                : new List<string[]>(source.templates.Select(template => template.ToArray()));
            target.max_vowels_in_row = source.max_vowels_in_row;
            target.max_consonants_in_row = source.max_consonants_in_row;
            target.add_addition_chance = source.add_addition_chance;
            target.check = source.check;
            target.replacer = source.replacer;
            target.replacer_kingdom = source.replacer_kingdom;
            target.finalizer = source.finalizer;
        }

        private static void LogEasternHumanNameGeneratorIds()
        {
            foreach (var source in EasternHumanNameGeneratorSources)
            {
                ModClass.LogInfo($"({nameof(NameSets)}) EasternHuman name generator: {source} -> {EasternHumanNameGenerator(source)}");
                ModClass.LogInfo($"({nameof(NameSets)}) EasternHuman legacy name generator: {source} -> {LegacyEasternHumanNameGenerator(source)}");
            }
        }

        private void AddEasternHumanNameSet(
            (string Id, string City, string Clan, string Culture, string Family, string Kingdom, string Language,
                string Unit, string Religion) source,
            Func<string, string> nameSetId,
            Func<string, string> nameGeneratorId)
        {
            Add(new NameSetAsset
            {
                id = nameSetId(source.Id),
                city = nameGeneratorId(source.City),
                clan = nameGeneratorId(source.Clan),
                culture = nameGeneratorId(source.Culture),
                family = nameGeneratorId(source.Family),
                kingdom = nameGeneratorId(source.Kingdom),
                language = nameGeneratorId(source.Language),
                unit = nameGeneratorId(source.Unit),
                religion = nameGeneratorId(source.Religion)
            });
        }
    }
}
