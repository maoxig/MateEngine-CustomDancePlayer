using TMPro;
using UnityEngine;

public class CustomDancePlayerFontHelper : MonoBehaviour
{
    public TMP_FontAsset CJKFontAsset; // 从 AssetBundle 或 Inspector 赋值

    public void Apply(TMP_Text text)
    {
        if (text == null || CJKFontAsset == null) return;
        text.font = CJKFontAsset;
    }

    public void ApplyToDropdown(TMP_Dropdown dd)
    {
        if (dd == null || CJKFontAsset == null) return;

        if (dd.captionText) dd.captionText.font = CJKFontAsset;
        if (dd.itemText) dd.itemText.font = CJKFontAsset;

        if (dd.template)
        {
            var texts = dd.template.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts) t.font = CJKFontAsset;
        }
    }
}
