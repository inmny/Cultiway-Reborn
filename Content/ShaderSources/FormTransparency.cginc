// 加权透明合成：颜色累加，透射率相乘，避免相交曲面因提交顺序跳变。
struct FormTransparentOutput { float4 accumulation:SV_Target0; float4 transmission:SV_Target1; };
FormTransparentOutput formTransparent(float4 color, float depth)
{
    #if defined(UNITY_REVERSED_Z)
    depth = 1-depth;
    #endif
    float weight = clamp(pow(color.a+.01,3)*100,.01,100) * clamp(pow(1-saturate(depth)*.95,3),.01,1);
    FormTransparentOutput o;
    o.accumulation = float4(color.rgb*weight,color.a*weight);
    o.transmission = color.aaaa;
    return o;
}
