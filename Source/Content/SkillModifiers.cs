using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content;

public class SkillModifiers : ExtendLibrary<SkillModifierAsset, SkillModifiers>
{
    protected override void OnInit()
    {
        RegisterAssets();
    }
}