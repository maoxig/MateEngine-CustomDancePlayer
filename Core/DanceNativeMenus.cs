using UnityEngine;

namespace CustomDancePlayer
{
    internal static class DanceNativeMenus
    {
        private static MenuActions[] menus;
        private static float nextScan;
        public static bool IsOpen()
        {
            if (menus == null || Time.unscaledTime >= nextScan)
            {
                menus = Object.FindObjectsByType<MenuActions>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                nextScan = Time.unscaledTime + 0.5f;
            }
            foreach (var owner in menus)
            {
                if (owner == null || !owner.isActiveAndEnabled) continue;
                var radial = owner.radialMenuObject;
                if (radial != null && radial.activeInHierarchy && radial.transform.localScale.x > 0.01f) return true;
                foreach (var entry in owner.menuEntries)
                {
                    var panel = entry.menu;
                    if (panel == null || !panel.activeInHierarchy) continue;
                    // The H window registers with MenuActions for input blocking.
                    // It must not suspend its own camera controls.
                    if (panel.GetComponent<DanceWindow>() != null ||
                        DanceBootstrap.Root != null && panel.transform.IsChildOf(DanceBootstrap.Root.transform)) continue;
                    return true;
                }
            }
            return false;
        }
    }
}
