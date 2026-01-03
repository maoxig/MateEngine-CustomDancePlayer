using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CustomDancePlayer
{ // Centralizes all settings as the single source of truth
    public class DanceSettingsHandler : MonoBehaviour
    {
        private static DanceSettingsHandler _instance;
        public static DanceSettingsHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DanceSettingsHandler>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("DanceSettingsHandler");
                        _instance = go.AddComponent<DanceSettingsHandler>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private DanceSettingsData _data;
        public DanceSettingsData data
        {
            get
            {
                if (_data == null)
                {
                    _data = new DanceSettingsData();
                    LoadFromDisk();
                }
                return _data;
            }
            set => _data = value;
        }

        private string FilePath => Path.Combine(Application.persistentDataPath, "danceSettings.json");

        // Component references for applying settings
        public HipsFollower hipsFollower;


        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CacheComponents();
            LoadFromDisk();
            ApplyAllSettings();

        }
        private void CacheComponents()
        {
            hipsFollower = FindFirstObjectByType<HipsFollower>();
        }
        public static void ApplyAllSettings()
        {
            if (Instance == null) return;
            var data = Instance.data;


            if (Instance.hipsFollower != null)
            {
                var rect = Instance.hipsFollower.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (data.enableDanceUIFollow)
                    {
                        // If follow enabled, use basePosition (offset relative to hips)
                        rect.anchoredPosition = data.uiBasePosition;
                        Instance.hipsFollower.UpdateBaseAndInitial(); // Lock in new base
                    }
                    else
                    {
                        // If follow disabled, use raw position
                        rect.anchoredPosition = data.uiRawPosition;
                    }
                }
            }
        }
        private void SyncDataFromComponents()
        {

            if (hipsFollower != null)
            {
                var rect = hipsFollower.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (data.enableDanceUIFollow)
                    {
                        data.uiBasePosition = hipsFollower.basePosition;
                    }
                    else
                    {
                        data.uiRawPosition = rect.anchoredPosition;
                    }
                }
            }

        }


        void OnApplicationQuit()
        {
            SaveToDisk();
        }

        // Saves settings to disk
        public void SaveToDisk()
        {
            try
            {
                SyncDataFromComponents();

                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Converters = new List<JsonConverter> { new Vector2Converter() },
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(data, settings));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DanceSettingsHandler] Failed to save: {e}");
            }
        }

        // Loads settings from disk
        public void LoadFromDisk()
        {
            if (!File.Exists(FilePath))
            {
                data = new DanceSettingsData();
                return;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                data = JsonConvert.DeserializeObject<DanceSettingsData>(json, new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new Vector2Converter() }
                });
                Debug.Log("[DanceSettingsHandler] Settings loaded.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DanceSettingsHandler] Failed to load: {e}");
                data = new DanceSettingsData();
            }
        }


        // Triggers save on setting changes
        public static void OnSettingChanged()
        {
            //if (Instance != null)
            //{
            //    Instance.SaveToDisk();
            //}
        }

        [Serializable]
        public class DanceSettingsData
        {
            public string version = "1.0";
            public DancePlayerCore.PlayMode currentPlayMode = DancePlayerCore.PlayMode.Sequence;
            public int currentPlayIndex = -1;
            public float animationStartDelay = 0.3f;
            public float danceVolume = 0.25f;
            public bool enableDanceUIFollow = true;
            public bool enableShadowFollow = true;
            public bool enableWindowFollow = true;
            public bool enableCameraDistanceKeep = true;
            public bool enableGlobalHotkey = false;
            public bool enableMMDCamera = false;
            public float mmdCameraScale = 1.0f;
            public bool autoPlayOnStart = false;
            public bool hidePanelOnStart = false;
            public bool isPlaying = false;
            public float audioStartTime;
            public KeyCode toggleKey = KeyCode.H;
            public Vector2 uiBasePosition = Vector2.zero;
            public Vector2 uiRawPosition = new Vector2(300f, 0f);
        }

        private class Vector2Converter : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                JObject jo = new JObject { { "x", value.x }, { "y", value.y } };
                jo.WriteTo(writer);
            }

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                JObject jo = JObject.Load(reader);
                return new Vector2(jo["x"]?.Value<float>() ?? 0f, jo["y"]?.Value<float>() ?? 0f);
            }
        }
    }
}