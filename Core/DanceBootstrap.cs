using System.IO;
using System.Diagnostics;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomDancePlayer
{
    public static class DanceBootstrap
    {
        public static GameObject Root { get; private set; }
        public static string LastCreateTiming { get; private set; }
        private static AssetBundle bundle;
        private static GameObject prefab;
        public static IEnumerator Prepare(string directory)
        {
            if (bundle == null)
            {
                var create = AssetBundle.LoadFromFileAsync(Path.Combine(directory, "Assets", "customdanceplayer.bundle"));
                yield return create;
                bundle = create.assetBundle;
            }
            if (bundle == null) throw new InvalidDataException("CustomDancePlayer UI resources are missing.");
            if (prefab == null)
            {
                var assets = bundle.LoadAllAssetsAsync<GameObject>();
                yield return assets;
                if (assets.allAssets.Length > 0) prefab = assets.allAssets[0] as GameObject;
            }
            yield return DanceUi.PrepareTheme(directory);
        }
        public static GameObject Create(string directory)
        {
            if (Root != null) return Root;
            var total = Stopwatch.StartNew();
            long previous = 0;
            if (bundle == null) bundle = AssetBundle.LoadFromFile(Path.Combine(directory, "Assets", "customdanceplayer.bundle"));
            long bundleMilliseconds = total.ElapsedMilliseconds - previous; previous = total.ElapsedMilliseconds;
            if (bundle == null) throw new InvalidDataException("CustomDancePlayer UI resources are missing.");
            if (prefab == null) prefab = bundle.LoadAllAssets<GameObject>()[0];
            long prefabMilliseconds = total.ElapsedMilliseconds - previous; previous = total.ElapsedMilliseconds;
            prefab.SetActive(false);
            Root = Object.Instantiate(prefab);
            long instantiateMilliseconds = total.ElapsedMilliseconds - previous; previous = total.ElapsedMilliseconds;
            Root.name = "CustomDancePlayer-0.2";
            Object.DontDestroyOnLoad(Root);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("CustomDancePlayerEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(Root.transform, false);
            }
            var ui = Root.GetComponentInChildren<DancePlayerUIManager>(true);
            ui.Initialize();
            long initializeMilliseconds = total.ElapsedMilliseconds - previous; previous = total.ElapsedMilliseconds;
            foreach (var hotkey in Root.GetComponentsInChildren<GlobalHotkeyListener>(true)) hotkey.enabled = DanceHotkeys.HasGlobalBinding;
            Root.SetActive(true);
            ui.BeginLibraryInitialization();
            total.Stop();
            LastCreateTiming = "bundle=" + bundleMilliseconds + "ms; prefab=" + prefabMilliseconds +
                "ms; instantiate=" + instantiateMilliseconds + "ms; initialize=" + initializeMilliseconds +
                "ms; activate=" + (total.ElapsedMilliseconds - previous) + "ms; total=" + total.ElapsedMilliseconds + "ms";
            UnityEngine.Debug.Log("[CustomDancePlayer] Bootstrap timing: " + LastCreateTiming);
            return Root;
        }
    }
}
