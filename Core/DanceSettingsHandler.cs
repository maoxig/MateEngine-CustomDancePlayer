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
        internal static DanceSettingsHandler Existing => _instance;
        public static DanceSettingsHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DanceSettingsHandler>(FindObjectsInactive.Include);
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
        private bool pendingSave;
        private float saveAfter;
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
                        rect.anchoredPosition = data.miniMode ? data.miniBasePosition : data.uiBasePosition;
                        Instance.hipsFollower.UpdateBaseAndInitial(); // Lock in new base
                    }
                    else
                    {
                        // If follow disabled, use raw position
                        rect.anchoredPosition = data.miniMode ? data.miniRawPosition : data.uiRawPosition;
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
                        if(data.miniMode)data.miniBasePosition=hipsFollower.basePosition;else data.uiBasePosition = hipsFollower.basePosition;
                    }
                    else
                    {
                        if(data.miniMode)data.miniRawPosition=rect.anchoredPosition;else data.uiRawPosition = rect.anchoredPosition;
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

                var pending = FilePath + ".tmp";
                File.WriteAllText(pending, JsonConvert.SerializeObject(data, settings));
                if (File.Exists(FilePath)) File.Replace(pending, FilePath, FilePath + ".bak");
                else File.Move(pending, FilePath);
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
                if (data == null) data = new DanceSettingsData();
                if (data.favorites == null) data.favorites = new List<string>();
                if (data.queue == null) data.queue = new List<string>();

                data.playStopKey = data.playStopKey ?? new DanceHotkeyBinding();
                data.stopKey = data.stopKey ?? new DanceHotkeyBinding();
                data.previousKey = data.previousKey ?? new DanceHotkeyBinding();
                data.nextKey = data.nextKey ?? new DanceHotkeyBinding();
                data.favorites = data.favorites.ConvertAll(id => id.Replace('\\', '/'));
                data.lastResourceId = (data.lastResourceId ?? "").Replace('\\', '/');
                data.schemaVersion = 3;
                data.isPlaying = false;
                Debug.Log("[DanceSettingsHandler] Settings loaded.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DanceSettingsHandler] Failed to load: {e}");
                File.Copy(FilePath, FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"), false);
                data = new DanceSettingsData();
            }
        }


        // Triggers save on setting changes
        public static void OnSettingChanged()
        {
            if (_instance == null) return;
            _instance.pendingSave = true; _instance.saveAfter = Time.unscaledTime + 0.4f;
        }
        void LateUpdate() { if (pendingSave && Time.unscaledTime >= saveAfter) { pendingSave = false; SaveToDisk(); } }
        void OnDestroy() { if (_instance == this) { if (pendingSave) SaveToDisk(); _instance = null; } }

        [Serializable]
        public class DanceHotkeyBinding
        {
            public KeyCode key = KeyCode.None;
            public bool enabled = true;
            public bool control, alt, shift;
        }
        [Serializable]
        public class DanceSettingsData
        {
            public int schemaVersion = 3;
            public string language = "auto";
            public bool miniMode;
            public Vector2 miniBasePosition = Vector2.zero;
            public Vector2 miniRawPosition = Vector2.zero;
            public string lastResourceId = "";
            public List<string> favorites = new List<string>();
            public List<string> queue = new List<string>();
            public string version = "1.0";
            public DancePlayerCore.PlayMode currentPlayMode = DancePlayerCore.PlayMode.Sequence;
            public int currentPlayIndex = -1;
            public float animationStartDelay = 0.3f;
            public float danceVolume = 0.25f;
            public bool enableDanceUIFollow = true;
            public bool showAvatarShadow = true;
            public bool enableShadowFollow = true;
            public bool enableWindowFollow = true;
            public bool enableCameraDistanceKeep = true;
            public bool enableGlobalHotkey = false;
            // Preserve the old public field identity, but never deserialize or
            // persist the retired backend selector. Playback always uses PMX.
            [JsonIgnore] public bool useNativeVmd = true;
            public bool enableVmdFootIk = true;
            public bool keepVmdRootUpright = true;
            public bool lockVmdFacingForward = false;
            public bool enableMMDCamera = false;
            // Remembers the user's window-follow preference while the MMD camera
            // temporarily owns the main view. This survives a restart made while
            // the MMD camera is still enabled.
            public bool restoreWindowFollowAfterMmdCamera = false;
            public bool autoMmdCameraScale = true;
            public float mmdCameraScale = 1.0f;
            public bool autoPlayOnStart = false;
            public bool hidePanelOnStart = false;
            public bool isPlaying = false;
            public float audioStartTime;
            public KeyCode toggleKey = KeyCode.H;
            public bool panelHotkeyEnabled = true;
            public bool toggleControl, toggleAlt, toggleShift;
            public KeyCode globalPlaybackKey = KeyCode.None;
            public int hotkeySchema = 1;
            public DanceHotkeyBinding playStopKey = new DanceHotkeyBinding();
            public DanceHotkeyBinding stopKey = new DanceHotkeyBinding();
            public DanceHotkeyBinding previousKey = new DanceHotkeyBinding();
            public DanceHotkeyBinding nextKey = new DanceHotkeyBinding();
            public bool globalControl = true;
            public bool globalAlt = true;
            public bool globalShift;
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
