using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Utils;

/// <summary>法器外观模型数据共用的无状态向量运算与 JSON 数值读取工具。</summary>
internal static class ArtifactAppearanceMath
{
    internal static Vector3 RotateEuler(Vector3 point, Vector3 rotation)
    {
        float x = point.x;
        float y = point.y;
        float z = point.z;
        if (rotation.x != 0f)
        {
            float angle = rotation.x * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (y, z) = (y * cosine - z * sine, y * sine + z * cosine);
        }
        if (rotation.y != 0f)
        {
            float angle = rotation.y * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (x, z) = (x * cosine + z * sine, -x * sine + z * cosine);
        }
        if (rotation.z != 0f)
        {
            float angle = rotation.z * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (x, y) = (x * cosine - y * sine, x * sine + y * cosine);
        }
        return new Vector3(x, y, z);
    }

    internal static Vector3 FaceNormal(IReadOnlyList<Vector3> points)
    {
        return points.Count < 3
            ? Vector3.forward
            : Normalize(Vector3.Cross(points[1] - points[0], points[2] - points[0]), Vector3.forward);
    }

    internal static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude <= 1e-8f ? fallback : value.normalized;
    }

    internal static Vector3 Vec3(float[] value, Vector3 fallback)
    {
        if (value == null || value.Length == 0) return fallback;
        if (value.Length == 1) return new Vector3(value[0], value[0], value[0]);
        if (value.Length == 2) return new Vector3(value[0], value[1], fallback.z);
        return new Vector3(value[0], value[1], value[2]);
    }

    internal static Vector3 ReadVec3(JToken token, Vector3 fallback)
    {
        if (token == null) return fallback;
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            float scalar = token.Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            float scalar = array[0].Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (array.Count == 2)
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), fallback.z);
        return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
    }

    internal static float ReadFloat(JObject data, string key, float fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<float>() : fallback;
    }

    internal static int ReadInt(JObject data, string key, int fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<int>() : fallback;
    }

    internal static bool ReadBool(JObject data, string key, bool fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<bool>() : fallback;
    }

    internal static List<Vector2> ReadPoints2(JToken token)
    {
        List<Vector2> result = new();
        if (token is not JArray array) return result;
        foreach (JToken item in array)
        {
            if (item is JArray point && point.Count >= 2)
                result.Add(new Vector2(point[0].Value<float>(), point[1].Value<float>()));
        }
        return result;
    }

    internal static List<Vector3> ReadPoints3(JToken token)
    {
        List<Vector3> result = new();
        if (token is not JArray array) return result;
        foreach (JToken item in array) result.Add(ReadVec3(item, Vector3.zero));
        return result;
    }

    internal static Vector2 RadiusPair(JToken token, Vector2 fallback)
    {
        if (token == null) return fallback;
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            float scalar = token.Value<float>();
            return new Vector2(scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            float scalar = array[0].Value<float>();
            return new Vector2(scalar, scalar);
        }
        return new Vector2(array[0].Value<float>(), array[1].Value<float>());
    }
}
