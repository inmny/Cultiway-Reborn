using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class TextButton : APrefabPreview<TextButton>
{
    public Button        Button       { get; private set; }
    public Text          Text         { get; private set; }
    public LocalizedText Localization { get; private set; }

    protected override void OnInit()
    {
        Button = GetComponent<Button>();
        Text = GetComponent<Text>();
        Localization = GetComponent<LocalizedText>();
    }

    public void Setup(string localization_key, UnityAction action)
    {
        Init();
        Localization.autoField = true;
        Localization.setKeyAndUpdate(localization_key);
        Button.onClick.RemoveAllListeners();
        Button.onClick.AddListener(action);
    }

    public void SetupLocalized(string localized_text, UnityAction action)
    {
        Init();
        Text.text = localized_text;
        Localization.autoField = false;
        Button.onClick.RemoveAllListeners();
        Button.onClick.AddListener(action);
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(TextButton), typeof(Button), typeof(Text),
            typeof(LocalizedText),
            typeof(ContentSizeFitter));


        var text = obj.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        var fitter = obj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;


        Prefab = obj.AddComponent<TextButton>();
    }
}