using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CustomDancePlayer
{ // Manages loading and unloading of dance resources

    public class DanceResourceManager : MonoBehaviour
    {
        private const string DANCE_FOLDER_NAME = "CustomDances"; private AssetBundle _currentAssetBundle;
        public AudioClip CurrentAudioClip { get; private set; }
        public AnimationClip CurrentAnimationClip { get; private set; }

        //public RuntimeAnimatorController CurrentAnimatorCtrl { get; private set; }
        public List<string> DanceFileList { get; private set; } = new List<string>();

        public DanceAvatarHelper avatarHelper;


        // Refreshes dance file list from folder
        public void RefreshDanceFileList()
        {
            DanceFileList.Clear();
            string danceFolderPath = GetDanceFolderPath();

            if (!Directory.Exists(danceFolderPath))
            {
                Directory.CreateDirectory(danceFolderPath);
                return;
            }

            // Search recursively for all .unity3d files
            string[] files = Directory.GetFiles(danceFolderPath, "*.unity3d", SearchOption.AllDirectories);

            // Store relative paths so LoadDanceResource can locate them
            DanceFileList.AddRange(files.Select(fullPath =>
            {
                string relativePath = fullPath.Substring(danceFolderPath.Length);
                return relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }));
        }

        // Loads dance resource by file name
        public bool LoadDanceResource(string fileName)
        {
            if (!avatarHelper.IsAvatarAvailable() || string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".unity3d"))
            {
                return false;
            }

            UnloadCurrentResource();
            string fullPath = Path.Combine(GetDanceFolderPath(), fileName);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            _currentAssetBundle = AssetBundle.LoadFromFile(fullPath);
            if (_currentAssetBundle == null)
            {
                return false;
            }

            LoadAnimationClipByType();
            LoadAudioClipByType();
            return true;
        }
        //private bool LoadAnimatorController(string baseName)
        //{
        //    string ctrlPath = $"{baseName}.controller";
        //    CurrentAnimatorCtrl = _currentAssetBundle.LoadAsset<RuntimeAnimatorController>(ctrlPath);
        //    if (CurrentAnimatorCtrl == null)
        //    {
        //        return false;
        //    }
        //    return true;
        //}

        // Loads animation clip from asset bundle
        private bool LoadAnimationClipByType()
        {
            AnimationClip[] clips = _currentAssetBundle.LoadAllAssets<AnimationClip>();
            CurrentAnimationClip = clips.Length > 0 ? clips[0] : null;
            return CurrentAnimationClip != null;
        }

        // Loads audio clip from asset bundle
        private bool LoadAudioClipByType()
        {
            AudioClip[] clips = _currentAssetBundle.LoadAllAssets<AudioClip>();
            CurrentAudioClip = clips.Length > 0 ? clips[0] : null;

            if (avatarHelper.CurrentAudioSource != null)
            {
                avatarHelper.CurrentAudioSource.clip = CurrentAudioClip;
                avatarHelper.CurrentAudioSource.loop = false;
            }

            return CurrentAudioClip != null;
        }

        // Unloads current resources
        public void UnloadCurrentResource()
        {
            if (avatarHelper.IsAvatarAvailable() && avatarHelper.CurrentAudioSource != null)
            {
                avatarHelper.CurrentAudioSource.Stop();
                avatarHelper.CurrentAudioSource.clip = null;
            }

            if (_currentAssetBundle != null)
            {
                _currentAssetBundle.Unload(true);
                _currentAssetBundle = null;
            }

            CurrentAnimationClip = null;
            CurrentAudioClip = null;
        }

        // Gets dance folder path
        private string GetDanceFolderPath()
        {
            return Path.Combine(Application.streamingAssetsPath, DANCE_FOLDER_NAME);
        }

        // Checks if resources are loaded
        public bool IsResourceLoaded()
        {
            return CurrentAnimationClip != null && CurrentAudioClip != null;
        }
    }
}