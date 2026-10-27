#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Globalization;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

/// <summary>让附身视角使用当前身体的真实眼位，并避开自己的模型。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>每份身体只计算一次眼位，变身与距离档各有自己的记录。</summary>
    private static readonly Dictionary<MeshModel, BeastEye.Shape> creatureEyes = new();

    /// <summary>用当前形态的眼部代替原生仅按旧颜色槽估算的视角。</summary>
    /// <param name="m">当前实际模型。</param>
    /// <param name="__result">供原生相机使用的眼位。</param>
    private static void CalibrateCreatureEye(MeshModel m, ref BeastEye.Shape __result)
    {
        if (!m.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal)) return;
        if (!creatureEyes.TryGetValue(m, out BeastEye.Shape eye))
        {
            // 明确写出的眼位优先；否则取本身体的眼部、骑手头部或真实头部。
            int head = m.PartIndex(m.Info("cultiway_eye_part") ?? "head");
            if (head >= 0 && m.Parts[head].VertexCount == 0) head = -1;
            Vec3 at;
            string authored = m.Info("cultiway_eye");
            if (authored != null)
            {
                string[] values = authored.Split(',');
                at = new Vec3(float.Parse(values[0], CultureInfo.InvariantCulture),
                    float.Parse(values[1], CultureInfo.InvariantCulture),
                    float.Parse(values[2], CultureInfo.InvariantCulture));
            }
            else
            {
                int rider = m.PartIndex("rider_head");
                if (rider < 0) rider = m.PartIndex("rider");
                int eyes = m.PartIndex("eyes");
                if (rider >= 0 && m.Parts[rider].VertexCount > 0)
                {
                    CreaturePartBounds(m.Parts[rider], out Vec3 low, out Vec3 high);
                    at = new Vec3((low.X + high.X) * 0.5f, low.Y + (high.Y - low.Y) * 0.3f,
                        low.Z + (high.Z - low.Z) * 0.9f);
                    head = rider;
                }
                else if (eyes >= 0 && m.Parts[eyes].VertexCount > 0)
                {
                    CreaturePartBounds(m.Parts[eyes], out Vec3 low, out Vec3 high);
                    at = (low + high) * 0.5f;
                }
                else if (head >= 0)
                {
                    CreaturePartBounds(m.Parts[head], out Vec3 low, out Vec3 high);
                    at = new Vec3((low.X + high.X) * 0.5f, low.Y + (high.Y - low.Y) * 0.3f,
                        low.Z + (high.Z - low.Z) * 0.72f);
                }
                else
                    at = new Vec3((m.Min.X + m.Max.X) * 0.5f, m.Min.Y + (m.Max.Y - m.Min.Y) * 0.2f,
                        m.Min.Z + (m.Max.Z - m.Min.Z) * 0.8f);
            }
            eye = new BeastEye.Shape
            {
                Head = head, Eye = at, View = at,
                Pivot = head >= 0 ? m.Parts[head].Pivot : at,
                // 原生相机会隐藏整个自身体并保留影子，羽翼、树冠和口鼻不会挡住视线。
                InHead = false
            };
            creatureEyes.Add(m, eye);
        }
        __result = eye;
    }

    /// <summary>完整隐藏自己的身体时，也不再把口鼻单独画回镜头前。</summary>
    /// <param name="m">当前身体。</param>
    /// <param name="__result">原生口鼻补画网格。</param>
    private static void ClearPossessedMuzzle(MeshModel m, ref MeshModel __result)
    {
        if (m.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal)) __result = null;
    }

    /// <summary>从水面起飞后，相机不再扣除游泳时的半身高度。</summary>
    /// <param name="host">当前附身单位。</param>
    /// <param name="swimming">游戏原本是否按水中身体处理。</param>
    /// <param name="up">相机离地高度。</param>
    /// <param name="__result">原生是否成功取得兽形视角。</param>
    private static void CorrectAirborneEye(Actor host, bool swimming, ref float up, bool __result)
    {
        if (__result && swimming && creatureMovements.TryGetValue(host, out CreatureMovement state) && state.flying)
            up += state.tall * 0.5f;
    }

    /// <summary>读取一个有几何部件的真实范围，不用空部件的旧转轴估高度。</summary>
    /// <param name="part">实际头部、眼部或骑手。</param>
    /// <param name="low">最小坐标。</param>
    /// <param name="high">最大坐标。</param>
    private static void CreaturePartBounds(MeshPart part, out Vec3 low, out Vec3 high)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue;
        float x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
        for (int i = 0; i < part.VertexCount; i++)
        {
            x0 = Math.Min(x0, part.Pos[i * 3]); x1 = Math.Max(x1, part.Pos[i * 3]);
            y0 = Math.Min(y0, part.Pos[i * 3 + 1]); y1 = Math.Max(y1, part.Pos[i * 3 + 1]);
            z0 = Math.Min(z0, part.Pos[i * 3 + 2]); z1 = Math.Max(z1, part.Pos[i * 3 + 2]);
        }
        low = new Vec3(x0, y0, z0);
        high = new Vec3(x1, y1, z1);
    }
}
#endif
