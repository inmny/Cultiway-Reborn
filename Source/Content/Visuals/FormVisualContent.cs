using Cultiway.Abstract;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Patch;

namespace Cultiway.Content.Visuals;

/// <summary>接入共同形体显示，具体能力按运行状态发布资料。</summary>
public sealed class FormVisualContent : ICanInit
{
    /// <summary>登记原版显示和世界清理，不额外建立动作时钟。</summary>
    public void Init()
    {
        var renderer = new FormNativeRenderSystem();
        ModClass.I.GeneralRenderSystems.Add(renderer);
        PatchMapBox.RegisterActionOnClearWorld(renderer.ClearWorldState);
    }
}
