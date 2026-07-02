namespace Cultiway.Const;

public static class SectConst
{
    public const string BuildingGroup = "sect";
    public const string BuildingTypeHall = "type_sect_hall";
    public const string BuildingTypeScripturePavilion = "type_sect_scripture_pavilion";
    public const int ContributionTeachCultibook = 2;
    public const int ContributionSectChore = 1;
    public const int ContributionOrganizeScripture = 2;
    public const int ContributionSectLecture = 3;
    public const int ContributionWriteScriptureBook = 5;
    public const int ContributionBuildSectBuilding = 8;
    public const int PersonnelRealmScorePerLevel = 100;
    public const int PersonnelTenureScorePerYear = 1;
    public const int PersonnelRecruitRange = 80;
    public const int PersonnelRecruitMaxLevelAboveLeader = 1;
    public const float PersonnelInnerDiscipleMasterMinRecruitWillingness = 30f;
    public const int ScriptureStudyTopCandidateCount = 5;
    public const int ScriptureCorePermissionMinStage = 1;
    public const int ScriptureHighPermissionMinStage = 3;
    public const int ScriptureBasicReadCost = 2;
    public const int ScriptureCoreReadCost = 8;
    public const int ScriptureHighReadCost = 20;
    public const float ScriptureReadPermissionDiscount = 0f;
    public const float ScriptureReadOutOfPermissionMultiplier = 3f;
    public const float ScriptureStudyKnownCultibookCap = 50f;
    public const float ScriptureStudyElixirMasteryCap = 100f;
    public const float SectStudyJobChance = 0.18f;
    public const float SectAffairJobChance = 0.25f;
    public const float SectChoreJobChance = SectAffairJobChance;
    public const int SectLectureMaxAudience = 4;
    public const float SectLectureNewCultibookGain = 12f;
    public const float SectLectureKnownCultibookGain = 8f;
    public const float SectLectureCultibookMasteryCap = 50f;
    public const float SectConstructionCheckInterval = TimeScales.SecPerMonth;
    public const int SectConstructionMaxBuilders = 3;
    public const int SectConstructionMembersPerBuilder = 6;
    public const int SectConstructionDecisionCooldown = 50;
    public const int ResidenceRandomTileAttempts = 12;
    public const int ResidenceFoundingSearchZoneRadius = 8;
    public const int ResidenceInitialZoneRadius = 1;
    public const int ResidenceMinBuildSites = 1;
    public const float ResidenceMinSiteScore = 1f;
    public const float ResidenceWakanScale = 100f;
    public const float ResidenceDirtyWakanScale = 100f;
}
