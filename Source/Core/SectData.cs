using System.Collections.Generic;

namespace Cultiway.Core;

public class SectData : MetaObjectData
{
    public List<long> ScriptureBookIDs = new();
    public string FounderActorName;
    public long FounderActorID;
    public string LeaderActorName;
    public long LeaderActorID = -1;
    public string HomeCityName;
    public long HomeCityID = -1;
    public string ResidenceName;
    public int ResidenceTileID = -1;
    public float ResidenceFoundedTime;
    public float FoundedTime;
    public int Level = 1;
    public int Reputation;
    public string DoctrineCultibookId;
    public string DoctrineCultibookName;
    public int CultibookCount;
    public int ElixirRecipeCount;
    public int SkillbookCount;
    public int BannerBackgroundIndex;
    public int BannerIconIndex;
}
