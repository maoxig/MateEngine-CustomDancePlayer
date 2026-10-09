using UnityEngine;

namespace CustomDancePlayer
{
    // Kept under the legacy type name so old prefab/script identities remain
    // compatible. 0.2 switches the real host view instead of drawing a second
    // RenderTexture preview over the desktop.
    public static class DanceCameraDemo
    {
        public static void EnsureCreated(DancePlayerUIManager ui)
        {
            var existing = ui.GetComponentInChildren<DanceCameraSync>(true);
            if (existing != null)
            {
                existing.ConfigureSwitch(ui.avatarHelper);
                return;
            }
            var root = new GameObject("DanceCameraSwitch");
            root.SetActive(false);
            root.transform.SetParent(ui.transform, false);
            var sync = root.AddComponent<DanceCameraSync>();
            sync.AvatarHelper = ui.avatarHelper;
            sync.RenderCamera = Camera.main;
            sync.enabled = false;
            root.SetActive(true);
            Debug.Log("[CustomDancePlayer] MMD camera switch ready (front view / authored camera).");
        }
    }
}
