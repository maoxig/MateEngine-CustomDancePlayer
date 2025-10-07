using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace CustomDancePlayer
{
    public class DanceSettingsHandler : MonoBehaviour
    {
        public static DanceSettingsHandler Instance { get; private set; }

        public DanceSettingsData data;

        private string fileName = "danceSettings.json";
        private string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        // Cached references for efficient apply/save
        private DancePlayerCore playerCore;
        private DancePlayerUIManager uiManager;
        private HipsFollower hipsFollower;
        private DanceShadowFollower shadowFollower;
        private DanceAvatarHelper avatarHelper;
        private DanceScreenCompensator screenCompensator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Cache references
            CacheComponents();

            LoadFromDisk();
            ApplyAllSettings();
        }

        private void OnApplicationQuit()
        {
            SaveToDisk(); // Auto-save on exit
        }

        private void CacheComponents()
        {
            playerCore = FindFirstObjectByType<DancePlayerCore>();
            uiManager = FindFirstObjectByType<DancePlayerUIManager>();
            hipsFollower = FindFirstObjectByType<HipsFollower>();
            shadowFollower = FindFirstObjectByType<DanceShadowFollower>();
            avatarHelper = FindFirstObjectByType<DanceAvatarHelper>();
            screenCompensator = FindFirstObjectByType<DanceScreenCompensator>();
        }

        public void SaveToDisk()
        {
            try
            {
                // Sync data from components before saving
                SyncDataFromComponents();

                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Use custom JsonSerializerSettings to handle Vector2
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Converters = new List<JsonConverter> { new Vector2Converter() },
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore // Fallback safety
                };

                string json = JsonConvert.SerializeObject(data, settings);
                File.WriteAllText(FilePath, json);
                Debug.Log("[DanceSettingsHandler] Saved settings to: " + FilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DanceSettingsHandler] Failed to save: " + e);
            }
        }

        public void LoadFromDisk()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var settings = new JsonSerializerSettings
                    {
                        Converters = new List<JsonConverter> { new Vector2Converter() }
                    };
                    data = JsonConvert.DeserializeObject<DanceSettingsData>(json, settings);
                    Debug.Log("[DanceSettingsHandler] Loaded settings from: " + FilePath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[DanceSettingsHandler] Failed to load: " + e);
                    data = new DanceSettingsData();
                }
            }
            else
            {
                data = new DanceSettingsData();
            }
        }

        public static void ApplyAllSettings()
        {
            if (Instance == null) return;
            var data = Instance.data;

            // Apply to DancePlayerCore
            if (Instance.playerCore != null)
            {
                Instance.playerCore.CurrentPlayIndex = data.currentPlayIndex;
                Instance.playerCore.CurrentPlayMode = data.currentPlayMode;
                Instance.playerCore.SetAnimationStartDelay(data.animationStartDelay);
                Instance.playerCore.autoPlayOnStart = data.autoPlayOnStart;
            }

            // Apply to DancePlayerUIManager (volume, etc.)
            if (Instance.uiManager != null)
            {

                Instance.uiManager.hidePanelOnStart = data.hidePanelOnStart;
            }
            if (Instance.avatarHelper != null)
            {
                Instance.avatarHelper.danceVolume = data.danceVolume;
            }

            // Apply to HipsFollower (follow enabled + position)
            if (Instance.hipsFollower != null)
            {
                Instance.hipsFollower.followEnabled = data.enableDanceUIFollow;
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

            // Apply to DanceShadowFollower
            if (Instance.shadowFollower != null)
            {
                Instance.shadowFollower.enabled = data.enableShadowFollow;
            }
        }

        private void SyncDataFromComponents()
        {
            // Pull current values from components to data before save
            if (playerCore != null)
            {
                data.currentPlayMode = playerCore.CurrentPlayMode;
                data.currentPlayIndex = playerCore.CurrentPlayIndex;
                data.animationStartDelay = playerCore.AnimationStartDelay;
                data.autoPlayOnStart = playerCore.autoPlayOnStart;
            }

            if (uiManager != null)
            {
                data.danceVolume = uiManager.VolumeSlider.value;
                data.hidePanelOnStart = uiManager.hidePanelOnStart;
            }

            if (hipsFollower != null)
            {
                data.enableDanceUIFollow = hipsFollower.followEnabled;
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

            if (shadowFollower != null)
            {
                data.enableShadowFollow = shadowFollower.enabled;
            }
            else
            {
                data.enableShadowFollow = false; // Default if component is missing
            }
        }

        // Public method to trigger save on setting changes (call from other scripts)
        public static void OnSettingChanged()
        {
            if (Instance != null)
            {
                Instance.SaveToDisk();
            }
        }

        [System.Serializable]
        public class DanceSettingsData
        {
            public string version = "1.0"; // For future migrations

            public DancePlayerCore.PlayMode currentPlayMode = DancePlayerCore.PlayMode.Sequence;
            public int currentPlayIndex = -1;
            public float animationStartDelay = 0.3f;
            public float danceVolume = 0.25f;
            public bool enableDanceUIFollow = true;
            public bool enableShadowFollow = true;
            public bool autoPlayOnStart = false;
            public bool hidePanelOnStart = false;
            public bool enableCameraCompensation = true;

            // UI Position Persistence
            public Vector2 uiBasePosition = Vector2.zero; // For when follow is enabled (offset)
            public Vector2 uiRawPosition = new Vector2(300f, 0f); // For when follow is disabled (absolute)
        }

        // Custom JsonConverter for Vector2 to avoid serialization issues
        private class Vector2Converter : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                // Serialize only x and y
                JObject jo = new JObject
            {
                { "x", value.x },
                { "y", value.y }
            };
                jo.WriteTo(writer);
            }

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                JObject jo = JObject.Load(reader);
                float x = jo["x"]?.Value<float>() ?? 0f;
                float y = jo["y"]?.Value<float>() ?? 0f;
                return new Vector2(x, y);
            }
        }
    }
}