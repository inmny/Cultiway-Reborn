namespace Cultiway.Core.Libraries;

public enum RandomEventType
{
    Neutral,
    Positive,
    Negative
}

public class RandomEventAsset : Asset
{
    public RandomEventType type = RandomEventType.Neutral;
}