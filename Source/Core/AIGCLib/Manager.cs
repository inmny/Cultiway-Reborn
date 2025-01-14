namespace Cultiway.Core.AIGCLib;

public class Manager
{
    public static string BaseURL => ModClass.I.GetConfig()["AIGCSettings"]["BASE_URL"].TextVal;
    public static string APIKey => ModClass.I.GetConfig()["AIGCSettings"]["API_KEY"].TextVal;
}