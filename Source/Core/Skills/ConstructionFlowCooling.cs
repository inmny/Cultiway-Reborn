using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>有限熔融层随实际流动输送热量，冷却后留下可被热流冲开的固体。</summary>
public sealed class ConstructionFlowCooling
{
    /// <summary>每单位材料高于凝结所需的初始热量。凝结所需的热量单位为一。</summary>
    public float Heat = 2f;
    /// <summary>每格表面每世界秒散去的热量，薄边会比厚处先耗尽。</summary>
    public float Loss = .38f;
    /// <summary>热流需同时达到的推压门槛，厚壳会分担更多压力。</summary>
    public float BreakPressure = 1.25f;

    /// <summary>冷却部位共用已经检查的参数值，不保留作者工作表。</summary>
    /// <param name="path">实际设置的位置。</param>
    internal ConstructionFlowCooling Compile(string path)
    {
        if (!(Heat > 0 && Heat <= 32 && Loss > 0 && Loss <= 12 && BreakPressure > 0 && BreakPressure <= 100))
            throw new InvalidDataException(path + "：熔流冷却需要有限的正数初始热量、散热速度与破壳压力");
        return (ConstructionFlowCooling)MemberwiseClone();
    }
}
