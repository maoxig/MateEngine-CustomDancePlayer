using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Resource Manager: Async load/unload dance resources (.unity3d, Animator Controller, Audio)
/// </summary>
public class DanceResourceManager : MonoBehaviour
{
    private const string DANCE_FOLDER_NAME = "CustomDances";
    private const string DANCE_FOLDER_PATH_CACHE_KEY = "CustomDanceFolderPath";

    private AssetBundle _currentAssetBundle;
    public RuntimeAnimatorController CurrentAnimatorCtrl { get; private set; }
    public AudioClip CurrentAudioClip { get; private set; }
    public List<string> DanceFileList { get; private set; } = new List<string>();

    public AvatarHelper avatarHelper;

    // Cache the dance folder path to avoid repeated calls
    private string _cachedDanceFolderPath;
    // Lock object for thread safety
    private readonly object _loadLock = new object();



    void Start()
    {
        _cachedDanceFolderPath = GetDanceFolderPath();

        _ = RefreshDanceFileList();
    }

    /// <summary>
    /// Refresh dance file list (read from CustomDances folder)
    /// </summary>
    public async Task<List<string>> RefreshDanceFileList()
    {
        List<string> newFileList = new List<string>();

        try
        {
            // 1. Asynchronously ensure the dance folder exists
            if (!Directory.Exists(_cachedDanceFolderPath))
            {
                // Auto-create the folder if it doesn't exist
                Directory.CreateDirectory(_cachedDanceFolderPath);
#if UNITY_EDITOR
                Debug.Log($"Created dance folder: {_cachedDanceFolderPath}");
#endif
                DanceFileList = newFileList;
                return newFileList;
            }

            // 2. Asynchronously enumerate .unity3d files
            await Task.Run(() =>
            {
                // Enumerate files in a thread-safe manner
                foreach (string filePath in Directory.EnumerateFiles(_cachedDanceFolderPath, "*.unity3d"))
                {
                    newFileList.Add(Path.GetFileName(filePath));
                }
            });

            // 3. Update the dance file list (main thread)
            DanceFileList = newFileList;
#if UNITY_EDITOR
            Debug.Log($"Dance list refreshed: {DanceFileList.Count} files found");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to refresh dance list: {e.Message}");
            DanceFileList = newFileList;
        }

        return DanceFileList;
    }


    /// <summary>
    /// Load dance resource
    /// </summary>
    /// <param name="fileName">.unity3d</param>
    /// <returns>True if loaded successfully</returns>
    public async Task<bool> LoadDanceResource(string fileName)
    {
        // Lock to ensure thread safety
        lock (_loadLock)
        {
            if (IsResourceLoading())
            {
                Debug.LogWarning("Another resource is loading, skip current request");
                return false;
            }
        }

        // pre-check conditions
        if (!avatarHelper.IsAvatarAvailable())
        {
            Debug.LogError("Avatar not available, cannot load resource");
            return false;
        }
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".unity3d"))
        {
            Debug.LogError($"Invalid file name: {fileName}");
            return false;
        }

        try
        {
            // 1. Unload previous resources
            UnloadCurrentResource();

            // 2. Construct full path and check file existence
            string fullPath = Path.Combine(_cachedDanceFolderPath, fileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"File not found: {fullPath}");
                return false;
            }

            // 3. Asynchronously load AssetBundle
            AssetBundleCreateRequest loadRequest = AssetBundle.LoadFromFileAsync(fullPath);
            await Task.Yield(); // Ensure we yield to allow async operation to start
            _currentAssetBundle = loadRequest.assetBundle;

            if (_currentAssetBundle == null)
            {
                Debug.LogError($"Failed to load AssetBundle: {fullPath} (corrupted or incompatible)");
                return false;
            }

            // 4.  Asynchronously load Animator Controller and AudioClip
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            bool animatorLoaded = await LoadAnimatorController(baseName);
            bool audioLoaded = await LoadAudioClip(baseName);

            if (!animatorLoaded)
            {
                UnloadCurrentResource();
                return false;
            }

#if UNITY_EDITOR
            string logMsg = audioLoaded 
                ? $"Loaded successfully: {fileName} (animation + audio)" 
                : $"Loaded successfully: {fileName} (animation only)";
            Debug.Log(logMsg);
#endif
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load resource: {e.Message}");
            UnloadCurrentResource();
            return false;
        }
    }

    /// <summary>
    /// Load animator controller
    /// </summary>
    private async Task<bool> LoadAnimatorController(string baseName)
    {
        if (_currentAssetBundle == null) return false;

        // Asynchronously load the Animator Controller
        AssetBundleRequest request = _currentAssetBundle.LoadAssetAsync($"{baseName}.controller", typeof(RuntimeAnimatorController));
        await Task.Yield(); 

        CurrentAnimatorCtrl = request.asset as RuntimeAnimatorController;
        return CurrentAnimatorCtrl != null;
    }

    /// <summary>
    /// Load audio clip
    /// </summary>
    private async Task<bool> LoadAudioClip(string baseName)
    {
        if (_currentAssetBundle == null || !avatarHelper.IsAvatarAvailable()) return false;

        string[] audioExts = { ".wav", ".mp3", ".ogg" };
        AudioSource audioSource = avatarHelper.CurrentAudioSource;

        foreach (string ext in audioExts)
        {
            string audioPath = $"{baseName}{ext}";
            // Asynchronously load the AudioClip
            AssetBundleRequest request = _currentAssetBundle.LoadAssetAsync(audioPath, typeof(AudioClip));
            await Task.Yield();

            CurrentAudioClip = request.asset as AudioClip;
            if (CurrentAudioClip != null)
            {
                audioSource.clip = CurrentAudioClip;
                audioSource.loop = false;
                return true;
            }
        }

        // Clear audio if not found
        CurrentAudioClip = null;
        audioSource.clip = null;
        return false;
    }

    public bool IsResourceLoading()
    {
        lock (_loadLock)
        {
            return _currentAssetBundle != null ||
                   (avatarHelper.IsAvatarAvailable() && avatarHelper.CurrentAudioSource.isPlaying);
        }
    }
    /// <summary>
    /// Unload current dance resource
    /// </summary>
    public void UnloadCurrentResource()
    {
        lock (_loadLock)
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
                _currentAssetBundle.Unload(true);
                _currentAssetBundle = null;
#if UNITY_EDITOR
                Debug.Log("Unloaded old resources");
#endif
            }

            // 3. Clear references
            CurrentAnimatorCtrl = null;
            CurrentAudioClip = null;
        }
    }

    /// <summary>
    /// Get dance folder path (encapsulation to avoid duplicate code)
    /// </summary>
    private string GetDanceFolderPath()
    {
        if (string.IsNullOrEmpty(_cachedDanceFolderPath))
        {
            _cachedDanceFolderPath = Path.Combine(Application.streamingAssetsPath, DANCE_FOLDER_NAME);
        }
        return _cachedDanceFolderPath;
    }

    /// <summary>
    /// Check if resources are loaded (provide external judgment)
    /// </summary>
    public bool IsResourceLoaded()
    {
        return CurrentAnimatorCtrl != null && CurrentAudioClip != null;
    }
}