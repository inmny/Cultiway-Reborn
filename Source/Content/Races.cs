using Cultiway.Abstract;

namespace Cultiway.Content;

public class Races : ExtendLibrary<Race, Races>
{
    [GetOnly("human")]
    public static Race Human { get; private set; }
    public static Race ConstraintSpirit { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Race");
        ConstraintSpirit.nameLocale = "Cultiway.Actor.ConstraintSpirit";
        ConstraintSpirit.path_icon = "cultiway/icons/races/iconMings";
        ConstraintSpirit.name_template_city = Human.name_template_city;
        ConstraintSpirit.name_template_clan = Human.name_template_clan;
        ConstraintSpirit.name_template_culture = Human.name_template_culture;
        ConstraintSpirit.name_template_kingdom = Human.name_template_kingdom;
    }
}