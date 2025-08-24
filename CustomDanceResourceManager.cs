using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Resource Manager: Load/Unload dance resources (.unity3d, Animator Controller, Audio)
/// </summary>
public class DanceResourceManager : MonoBehaviour
{
    private const string DANCE_FOLDER_NAME = "CustomDances";

    private AssetBundle _currentAssetBundle;
    public RuntimeAnimatorController CurrentAnimatorCtrl { get; private set; }
    public AudioClip CurrentAudioClip { get; private set; }
    public List<string> DanceFileList { get; private set; } = new List<string>();

    public AvatarHelper avatarHelper;

    void Start()
    {
        // Initialize: Load dance file list
        RefreshDanceFileList();
    }

    /// <summary>
    /// Refresh dance file list (read from CustomDances folder)
    /// </summary>
    public void RefreshDanceFileList()
    {
        DanceFileList.Clear();
        string danceFolderPath = GetDanceFolderPath();

        // Check if the folder exists
        if (!Directory.Exists(danceFolderPath))
        {
            Directory.CreateDirectory(danceFolderPath);
#if UNITY_EDITOR
            Debug.Log($"Created dance folder: {danceFolderPath}");
#endif
            return;
        }

        // Read all .unity3d files
        string[] unity3dFiles = Directory.GetFiles(danceFolderPath, "*.unity3d");
        foreach (string filePath in unity3dFiles)
        {
            DanceFileList.Add(Path.GetFileName(filePath));
        }
#if UNITY_EDITOR
        Debug.Log($"Dance list refreshed: {DanceFileList.Count} files found");
#endif
    }

    /// <summary>
    /// Load dance resource
    /// </summary>
    /// <param name="fileName">.unity3d</param>
    /// <returns>True if loaded successfully</returns>
    public bool LoadDanceResource(string fileName)
    {
        if (!avatarHelper.IsAvatarAvailable())
        {
#if UNITY_EDITOR
            Debug.LogError("Avatar is not available, cannot load resource.");
#endif
            return false;
        }
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".unity3d"))
        {
#if UNITY_EDITOR
            Debug.LogError("Invalid file name: " + fileName);
#endif
            return false;
        }

        // 2. Unload previous resources (to avoid memory leaks)
        UnloadCurrentResource();

        // 3. Compose file path
        string fullPath = Path.Combine(GetDanceFolderPath(), fileName);
        if (!File.Exists(fullPath))
        {
#if UNITY_EDITOR
            Debug.LogError("File does not exist: " + fullPath);
#endif
            return false;
        }

        // 4. Load AssetBundle
        _currentAssetBundle = AssetBundle.LoadFromFile(fullPath);
        if (_currentAssetBundle == null)
        {
#if UNITY_EDITOR
            Debug.LogError("Failed to load .unity3d (file may be corrupted or version incompatible): " + fullPath);
#endif
            return false;
        }

        // 5. Extract resources (by convention: 'file name = resource name')
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        bool loadAnimatorSuccess = LoadAnimatorController(baseName);
        bool loadAudioSuccess = LoadAudioClip(baseName);

        if (!loadAnimatorSuccess)
        {
            UnloadCurrentResource();
            return false;
        }

#if UNITY_EDITOR
        if (CurrentAudioClip != null)
        {

            Debug.Log($"Loaded successfully: {fileName} (animation + audio)");
        }
        else
        {
            Debug.LogWarning($"Loaded successfully: {fileName} (animation only, audio not found)");
        }
#endif
        return true;
    }

    /// <summary>
    /// Load animator controller
    /// </summary>
    private bool LoadAnimatorController(string baseName)
    {
        string ctrlPath = $"{baseName}.controller";
        CurrentAnimatorCtrl = _currentAssetBundle.LoadAsset<RuntimeAnimatorController>(ctrlPath);
        if (CurrentAnimatorCtrl == null)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Load audio clip
    /// </summary>
    private bool LoadAudioClip(string baseName)
    {
        string[] audioExts = { ".wav", ".mp3", ".ogg" };
        AudioSource avatarAudioSource = avatarHelper.CurrentAudioSource;

        foreach (string ext in audioExts)
        {
            string audioPath = $"{baseName}{ext}";
            CurrentAudioClip = _currentAssetBundle.LoadAsset<AudioClip>(audioPath);
            if (CurrentAudioClip != null)
            {
                avatarAudioSource.clip = CurrentAudioClip;
                avatarAudioSource.loop = false;
                return true;
            }
        }


        CurrentAudioClip = null;
        avatarAudioSource.clip = null;
        return false;
    }

    /// <summary>
    /// Unload current dance resource
    /// </summary>
    public void UnloadCurrentResource()
    {
        // 1. Stop audio playback
        if (avatarHelper.IsAvatarAvailable() && avatarHelper.CurrentAudioSource != null)
        {
            avatarHelper.CurrentAudioSource.Stop();
            avatarHelper.CurrentAudioSource.clip = null;
        }

        // 2. Unload AssetBundle
        if (_currentAssetBundle != null)
        {
            _currentAssetBundle.Unload(true); // true：unload all assets loaded from this bundle
            _currentAssetBundle = null;
#if UNITY_EDITOR
            Debug.Log("Unloaded old resources");
#endif
        }

        // 3. Clear resource references
        CurrentAnimatorCtrl = null;
        CurrentAudioClip = null;
    }

    /// <summary>
    /// Get dance folder path (encapsulation to avoid duplicate code)
    /// </summary>
    private string GetDanceFolderPath()
    {
        return Path.Combine(Application.streamingAssetsPath, DANCE_FOLDER_NAME);
    }

    /// <summary>
    /// Check if resources are loaded (provide external judgment)
    /// </summary>
    public bool IsResourceLoaded()
    {
        return CurrentAnimatorCtrl != null && CurrentAudioClip != null;
    }
}