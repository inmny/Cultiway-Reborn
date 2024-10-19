namespace Cultiway.Core.SkillLibV2;

public class TrailMeta
{
    private TrailMeta()
    {
    }

    public MetaBuilder StartBuild()
    {
        return new MetaBuilder();
    }

    public class MetaBuilder
    {
        private readonly TrailMeta _under_build;

        public MetaBuilder()
        {
            _under_build = new TrailMeta();
        }

        public TrailMeta Build()
        {
            return _under_build;
        }
    }
}