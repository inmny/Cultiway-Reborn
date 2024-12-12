using NeoModLoader.api;

namespace Cultiway.UI;

public class WindowNewCreatureInfo : AbstractWideWindow<WindowNewCreatureInfo>
{
    public static void Show()
    {
        if (Instance == null) CreateAndInit("Cultiway.UI.WindowNewCreatureInfo");

        ScrollWindow.showWindow(WindowId);
    }

    protected override void Init()
    {
    }
}