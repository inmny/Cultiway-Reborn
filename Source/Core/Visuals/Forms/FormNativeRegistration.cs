using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>池中对象或运行时模板被启用、复制时登记实际绘制器。</summary>
public sealed class FormNativeRegistration : MonoBehaviour
{
    /// <summary>只登记本对象，不每帧搜索层级。</summary>
    private void OnEnable() => FormNativeScenePass.Register(GetComponent<Renderer>());
}
