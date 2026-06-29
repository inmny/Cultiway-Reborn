using UnityEngine;

namespace Cultiway.Utils;

public static class UIUtils
{
    public static Font GetCurrentFont()
    {
        return LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
