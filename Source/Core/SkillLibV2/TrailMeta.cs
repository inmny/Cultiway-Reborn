using UnityEngine;

namespace Cultiway.Core.SkillLibV2;

public class TrailMeta
{
    private TrailMeta()
    {
    }

    public Color Color { get; private set; }

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

        public MetaBuilder SetColor(Color color)
        {
            _under_build.Color = color;
            return this;
        }

        public TrailMeta Build()
        {
            return _under_build;
        }
    }
}