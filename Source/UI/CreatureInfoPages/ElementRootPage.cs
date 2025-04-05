using System;
using System.Text;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using MathNet.Numerics.Distributions;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class ElementRootPage : MonoBehaviour
{
    public Text Text { get; private set; }

    public static void Setup(CreatureInfoPage page)
    {
        var er_page = page.gameObject.AddComponent<ElementRootPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        er_page.Text = text;

        float p = 1 / 36f;
        bool q_is_one = Mathf.Approximately(Q, 1);
        if (!q_is_one)
        {
            p = (1 - Q) / (1 - Mathf.Pow(Q, 36));
        }
        
        for (var i = 0; i < _edgeValues.Length; i++)
        {
            var s_i = q_is_one ? p * i : p * (1 - Mathf.Pow(Q, i)) / (1 - Q);
            var cdf = 0.5 + s_i / 2;
            var z = Normal.InvCDF(0, 1, cdf);
            _edgeValues[i] = (float)z;
        }
    }

    private const float Q = 0.9f;
    private static float[] _edgeValues = new float[36];
    private static string GetSingleStrengthLevel(float strength)
    {
        int level = 0;
        for (int i = 0; i < _edgeValues.Length; i++)
        {
            if (strength > _edgeValues[i]) continue;
            level = i;
            break;
        }
        return LM.Get($"Cultiway.Stage.{level / 9}") + "阶" + LM.Get($"Cultiway.Level.{level % 9}");
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        ElementRoot er = ae.GetElementRoot();
        sb.AppendLine($"灵根类别: {er.Type.GetName()}");
        sb.AppendLine($"\t{er.Type.GetDescription()}");
        sb.AppendLine("各组分强度:");
        for (var i = 0; i < ElementIndex.ElementNames.Count; i++)
            sb.AppendLine($"\t{LM.Get(ElementIndex.ElementNames[i])}: {GetSingleStrengthLevel(er[i])}");

        sb.AppendLine($"综合评价: {GetSingleStrengthLevel(Mathf.Log(ae.GetElementRoot().GetStrength()))}");

        var er_page = page.GetComponent<ElementRootPage>();
        er_page.Text.text = sb.ToString();
    }
}