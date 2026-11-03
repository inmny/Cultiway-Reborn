using Newtonsoft.Json;

namespace Cultiway.Content.Persistence;

/// <summary>模组实际移动过的人物位置；运行中的动作、速度和模型引用不进入此记录。</summary>
public sealed class ActorPositionSaveData
{
    /// <summary>实际地图横坐标。</summary>
    [JsonProperty(Required = Required.Always)]
    public float X;

    /// <summary>实际地图纵坐标。</summary>
    [JsonProperty(Required = Required.Always)]
    public float Y;

    /// <summary>稳定的实际承托建筑编号；地面和临时腾空为零。</summary>
    public long Surface;

    /// <summary>已站稳表面在建筑模型坐标中的高度；临时腾起和速度不恢复。</summary>
    public float? SurfaceHeight;
}