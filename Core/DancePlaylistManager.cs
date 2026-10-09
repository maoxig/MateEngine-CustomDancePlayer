using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

namespace CustomDancePlayer
{
    public class DancePlaylistManager : MonoBehaviour
    {
        public enum PlaylistType { All, Favorites, Folder, Queue }

        public PlaylistType CurrentType { get; private set; } = PlaylistType.All;
        public string CurrentFolder { get; private set; } = ""; // Empty means root or flat list logic
        public bool Initialized { get; private set; }
        public string Search { get; set; } = "";
        public string Format { get; set; } = "";

        // This is the list currently being displayed/played
        public List<string> CurrentPlaylistData { get; private set; } = new List<string>();

        [Header("Dependencies")]
        public DanceResourceManager resourceManager;
        public DanceSettingsHandler settingsHandler;

        public void Init()
        {
            if (resourceManager == null) resourceManager = FindFirstObjectByType<DanceResourceManager>();
            if (settingsHandler == null) settingsHandler = DanceSettingsHandler.Instance;
            
            RefreshPlaylist();
            Initialized = true;
        }

        // Switch to a new playlist view
        public void SetPlaylistType(PlaylistType type, string folderName = "")
        {
            CurrentType = type;
            CurrentFolder = folderName;
            RefreshPlaylist();
        }

        // Rebuilds the CurrentPlaylistData based on filters
        public void RefreshPlaylist()
        {
            CurrentPlaylistData.Clear();
            if (resourceManager == null || settingsHandler == null) return;
            List<string> allFiles = resourceManager.DanceFileList;

            switch (CurrentType)
            {
                case PlaylistType.Queue:
                    CurrentPlaylistData.AddRange(settingsHandler.data.queue.Where(allFiles.Contains));
                    break;
                case PlaylistType.All:
                    CurrentPlaylistData.AddRange(allFiles);
                    break;

                case PlaylistType.Favorites:
                    HashSet<string> favs = new HashSet<string>(settingsHandler.data.favorites);
                    // Only add favorites that actually exist
                    CurrentPlaylistData.AddRange(allFiles.Where(f => favs.Contains(f)));
                    break;

                case PlaylistType.Folder:
                    if (string.IsNullOrEmpty(CurrentFolder))
                    {
                        // List folders themselves? Or root files? 
                        // For the File List, we usually want to see files. 
                        // If we are in "Folder Mode" but no folder selected, maybe show all files? 
                        // Or distinct folders are handled by UI, and this only returns files for a SPECIFIC folder.
                        CurrentPlaylistData.AddRange(allFiles);
                    }
                    else
                    {
                        // Filter for files strictly within this folder (relative path starts with FolderName)
                        // Note: Our modified resource manager returns "Folder\File.unity3d"
                        string folderPrefix = CurrentFolder.Replace('\\','/') + "/";
                        string folderPrefixAlt = folderPrefix;

                        CurrentPlaylistData.AddRange(allFiles.Where(f => 
                            f.StartsWith(folderPrefix) || f.StartsWith(folderPrefixAlt)
                        ));
                    }
                    break;
            }
        }

        public void ApplyFilters()
        {
            RefreshPlaylist();
            CurrentPlaylistData.RemoveAll(id => !resourceManager.Descriptors.TryGetValue(id, out var d) ||
                (!string.IsNullOrEmpty(Format) && d.Format != Format) ||
                (!string.IsNullOrEmpty(Search) && d.Title.IndexOf(Search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                 id.IndexOf(Search, System.StringComparison.OrdinalIgnoreCase) < 0));
        }

        // Helper to get all available folders for UI generation
        public List<string> GetAvailableFolders()
        {
            return resourceManager.DanceFileList
                .Select(Path.GetDirectoryName)
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();
        }

        public bool IsFavorite(string fileName)
        {
            return settingsHandler.data.favorites.Contains(fileName);
        }

        public void ToggleFavorite(string fileName)
        {
            if (IsFavorite(fileName))
            {
                settingsHandler.data.favorites.Remove(fileName);
            }
            else
            {
                settingsHandler.data.favorites.Add(fileName);
            }
            DanceSettingsHandler.OnSettingChanged();
            
            // If we are looking at Favorites, refresh the list immediately
            if (CurrentType == PlaylistType.Favorites)
            {
                RefreshPlaylist();
            }
        }
        
        // Resolves index relative to CURRENT playlist to absolute file name
        public string GetFileByIndex(int index)
        {
            if (index >= 0 && index < CurrentPlaylistData.Count)
            {
                return CurrentPlaylistData[index];
            }
            return null;
        }

        // Finds index of a file in current playlist (returns -1 if not found)
        public int GetIndexByFile(string fileName)
        {
            return CurrentPlaylistData.IndexOf(fileName);
        }
    }
}
