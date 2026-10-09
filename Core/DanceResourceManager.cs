using System.Collections.Generic;
using System.IO;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Maoxig.RuntimeVmd;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomDancePlayer
{ // Manages loading and unloading of dance resources

    public class DanceResourceManager : MonoBehaviour
    {
        private const string DANCE_FOLDER_NAME = "CustomDances"; private AssetBundle _currentAssetBundle;
        private bool _ownsCurrentAudioClip;
        public string LastError { get; private set; }
        public readonly Dictionary<string, DanceDescriptor> Descriptors = new Dictionary<string, DanceDescriptor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string LibraryFolder => GetDanceFolderPath();
        private readonly Dictionary<string, VmdDancePackageDescriptor> _packagesByListPath =
            new Dictionary<string, VmdDancePackageDescriptor>(StringComparer.OrdinalIgnoreCase);
        private int refreshGeneration;
        public bool IsRefreshing { get; private set; }
        public bool LastRefreshRanAsync { get; private set; }
        public long LastRefreshMilliseconds { get; private set; }
        public long LastApplyMilliseconds { get; private set; }
        public AudioClip CurrentAudioClip { get; private set; }
        public AnimationClip CurrentAnimationClip { get; private set; }
        public string CurrentVmdPath { get; private set; }
        public string[] CurrentVmdOverlayPaths { get; private set; } = new string[0];
        public string CurrentVmdAudioPath { get; private set; }
        public string CurrentVmdReferencePmxPath { get; private set; }
        public float CurrentVmdAudioOffsetSeconds { get; private set; }
        internal void SetPreviewAudioOffset(float seconds) { CurrentVmdAudioOffsetSeconds = seconds; }
        internal void SetPreviewCameraReference(float? eye,float? body,float scale)
        {
            CurrentVmdCameraReferenceEyeHeight=eye;CurrentVmdCameraReferenceBodyHeight=body;CurrentVmdCameraAuthoringScale=scale;
        }
        public float? CurrentVmdCameraReferenceEyeHeight { get; private set; }
        public float? CurrentVmdCameraReferenceBodyHeight { get; private set; }
        public float? CurrentVmdCameraAuthoringScale { get; private set; }
        public float? CurrentVmdPositionScale { get; private set; }
        public bool? CurrentVmdLoop { get; private set; }
        public bool? CurrentVmdFootIk { get; private set; }
        public bool IsVmdResource => !string.IsNullOrEmpty(CurrentVmdPath);

        //public RuntimeAnimatorController CurrentAnimatorCtrl { get; private set; }
        public List<string> DanceFileList { get; private set; } = new List<string>();

        public DanceAvatarHelper avatarHelper;


        // Refreshes dance file list from folder
        public void RefreshDanceFileList()
        {
            ++refreshGeneration;
            IsRefreshing = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            LibrarySnapshot snapshot = ScanLibrary(GetDanceFolderPath(),
                Path.Combine(Application.streamingAssetsPath, "Mods"),
                Path.Combine(Application.temporaryCachePath, "RuntimeVmdPackages"));
            watch.Stop();
            ApplySnapshot(snapshot);
            LastRefreshRanAsync = false;
            LastRefreshMilliseconds = watch.ElapsedMilliseconds;
            IsRefreshing = false;
            LogWarnings(snapshot);
        }

        public IEnumerator RefreshDanceFileListAsync()
        {
            int ticket = ++refreshGeneration;
            IsRefreshing = true;
            string danceRoot = GetDanceFolderPath();
            string officialRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
            string cacheRoot = Path.Combine(Application.temporaryCachePath, "RuntimeVmdPackages");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Task<LibrarySnapshot> task = Task.Run(() => ScanLibrary(danceRoot, officialRoot, cacheRoot));
            while (!task.IsCompleted) yield return null;
            watch.Stop();
            if (ticket != refreshGeneration) yield break;
            try
            {
                if (task.IsCanceled) yield break;
                if (task.IsFaulted) throw task.Exception.GetBaseException();
                var applyWatch = System.Diagnostics.Stopwatch.StartNew();
                ApplySnapshot(task.Result);
                applyWatch.Stop();
                LastRefreshRanAsync = true;
                LastRefreshMilliseconds = watch.ElapsedMilliseconds;
                LastApplyMilliseconds = applyWatch.ElapsedMilliseconds;
                LogWarnings(task.Result);
                Debug.Log("[CustomDancePlayer] Library scan completed off the Unity main thread in " +
                    LastRefreshMilliseconds + " ms; main-thread apply " + LastApplyMilliseconds +
                    " ms; dances=" + DanceFileList.Count + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CustomDancePlayer] Library scan: " + exception);
            }
            finally
            {
                if (ticket == refreshGeneration) IsRefreshing = false;
            }
        }

        private sealed class LibrarySnapshot
        {
            public readonly Dictionary<string, DanceDescriptor> Descriptors = new Dictionary<string, DanceDescriptor>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, VmdDancePackageDescriptor> Packages = new Dictionary<string, VmdDancePackageDescriptor>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Warnings = new List<string>();
        }

        private static LibrarySnapshot ScanLibrary(string danceRoot, string officialRoot, string cacheRoot)
        {
            var snapshot = new LibrarySnapshot();
            foreach (var pair in new[] {
                new KeyValuePair<string,string>(danceRoot, ""),
                new KeyValuePair<string,string>(officialRoot, "@official/") })
            {
                try
                {
                    Directory.CreateDirectory(pair.Key);
                    ScanRoot(snapshot, pair.Key, pair.Value, cacheRoot);
                }
                catch (Exception exception) { snapshot.Warnings.Add("Scan '" + pair.Key + "': " + exception.Message); }
            }
            return snapshot;
        }

        private void ApplySnapshot(LibrarySnapshot snapshot)
        {
            DanceFileList.Clear(); _packagesByListPath.Clear(); _paths.Clear(); Descriptors.Clear();
            foreach (var pair in snapshot.Descriptors) Descriptors[pair.Key] = pair.Value;
            foreach (var pair in snapshot.Paths) _paths[pair.Key] = pair.Value;
            foreach (var pair in snapshot.Packages) _packagesByListPath[pair.Key] = pair.Value;
            DanceFileList.AddRange(Descriptors.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private static void LogWarnings(LibrarySnapshot snapshot)
        {
            foreach (string warning in snapshot.Warnings) Debug.LogWarning("[CustomDancePlayer] " + warning);
        }

        private static void Register(LibrarySnapshot snapshot, string id, string path, string format, string title = null, string author = null)
        {
            id = id.Replace('\\', '/');
            snapshot.Paths[id] = path;
            snapshot.Descriptors[id] = new DanceDescriptor { Id = id, Path = path, Format = format,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(path) : title, Author = author ?? "" };
        }

        private static void ScanRoot(LibrarySnapshot snapshot, string root, string prefix, string cacheRoot)
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            foreach (var file in files.Where(f => f.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)))
                Register(snapshot, prefix + MakeRelativePath(root, file), file, "unity3d");
            var packages = VmdDancePackage.Discover(root, cacheRoot,
                message => snapshot.Warnings.Add(message));
            var packageRoots = packages.Where(p => !p.IsArchive).Select(p => Path.GetFullPath(p.PackageRoot).TrimEnd('\\','/') + Path.DirectorySeparatorChar).ToArray();
            foreach (var package in packages)
            {
                string id = prefix + MakeRelativePath(root, package.SourcePath).TrimEnd('\\','/');
                if (!package.IsArchive) id += ".vmdance";
                id = id.Replace('\\','/');
                Register(snapshot, id, package.SourcePath, "vmdance", package.Title, package.Author);
                snapshot.Packages[id] = package;
            }
            var vmds = files.Where(p => p.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase)).ToArray();
            var overlays = new HashSet<string>(VmdLayerFiles.FindExplicitManifestOverlays(vmds, message => snapshot.Warnings.Add(message)), StringComparer.OrdinalIgnoreCase);
            foreach (var file in vmds)
                if (!IsInsideAnyRoot(file, packageRoots) && !VmdLayerFiles.IsOverlaySidecar(file) && !overlays.Contains(Path.GetFullPath(file)))
                    Register(snapshot, prefix + MakeRelativePath(root, file), file, "vmd");
            foreach (var file in files.Where(p => p.EndsWith(".me", StringComparison.OrdinalIgnoreCase)))
            {
                try {
                    var meta = OfficialDancePackage.ReadMetadata(file);
                    if (meta != null) Register(snapshot, prefix + MakeRelativePath(root, file), file, "me", (string)meta["songName"], (string)meta["songAuthor"] ?? (string)meta["mmdAuthor"]);
                } catch (Exception e) { snapshot.Warnings.Add(Path.GetFileName(file) + ": " + e.Message); }
            }
        }

        // Loads dance resource by file name
        public bool LoadDanceResource(string fileName)
        {
            VmdDancePackageDescriptor package = null;
            bool isPackage = !string.IsNullOrEmpty(fileName) && _packagesByListPath.TryGetValue(fileName, out package);
            bool isMe = fileName != null && fileName.EndsWith(".me", StringComparison.OrdinalIgnoreCase);
            bool isBundle = isMe || (fileName != null && fileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase));
            bool isVmd = fileName != null && fileName.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase);
            if (!avatarHelper.IsAvatarAvailable() || string.IsNullOrEmpty(fileName) || (!isBundle && !isVmd && !isPackage))
            {
                return false;
            }

            UnloadCurrentResource();
            if (isPackage)
            {
                if (package == null || !File.Exists(package.PrimaryVmdPath)) return false;
                CurrentVmdPath = package.PrimaryVmdPath;
                CurrentVmdOverlayPaths = package.OverlayVmdPaths ?? new string[0];
                CurrentVmdAudioPath = package.AudioPath;
                CurrentVmdReferencePmxPath = !string.IsNullOrEmpty(package.ReferencePmxPath) && File.Exists(package.ReferencePmxPath)
                    ? package.ReferencePmxPath
                    : FindSidecarPmx(CurrentVmdPath);
                CurrentVmdAudioOffsetSeconds = package.AudioOffsetSeconds;
                CurrentVmdCameraReferenceEyeHeight = package.CameraReferenceEyeHeight;
                CurrentVmdCameraReferenceBodyHeight = package.CameraReferenceBodyHeight;
                CurrentVmdCameraAuthoringScale = package.CameraAuthoringScale;
                CurrentVmdPositionScale = package.PositionScale;
                CurrentVmdLoop = package.Loop;
                CurrentVmdFootIk = package.FootIk;
                Debug.Log("[CustomDancePlayer] Selected VMD dance package: " + package.SourcePath +
                    "; motion=" + CurrentVmdPath + "; layers=" + (1 + CurrentVmdOverlayPaths.Length));
                return true;
            }

            string fullPath;
            if (!_paths.TryGetValue(fileName.Replace('\\','/'), out fullPath)) fullPath = Path.Combine(GetDanceFolderPath(), fileName);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            if (isVmd)
            {
                CurrentVmdPath = Path.GetFullPath(fullPath);
                CurrentVmdOverlayPaths = VmdLayerFiles.ResolveOverlays(CurrentVmdPath,
                    message => Debug.LogWarning("[CustomDancePlayer] " + message));
                CurrentVmdAudioPath = FindSidecarAudio(CurrentVmdPath);
                CurrentVmdReferencePmxPath = FindSidecarPmx(CurrentVmdPath);
                Debug.Log("[CustomDancePlayer] Selected runtime VMD: " + CurrentVmdPath);
                return true;
            }

            JObject meta = null;
            if (isMe)
            {
                try {
                    meta = OfficialDancePackage.ReadMetadata(fullPath);
                    if (meta == null) { LastError = "This .me is not a dance package."; return false; }
                    var extracted = OfficialDancePackage.Extract(fullPath, Path.Combine(Application.temporaryCachePath, "CustomDancePlayerME"));
                    var bundles = Directory.GetFiles(extracted, "*.bundle", SearchOption.AllDirectories);
                    if (bundles.Length != 1) { LastError = "Expected one dance bundle."; return false; }
                    fullPath = bundles[0];
                } catch (Exception e) { LastError = e.Message; return false; }
            }
            _currentAssetBundle = AssetBundle.LoadFromFile(fullPath);
            if (_currentAssetBundle == null)
            {
                return false;
            }

            LoadAnimationClipByType();
            LoadAudioClipByType();
            if (isMe)
            {
                foreach (var prefab in _currentAssetBundle.LoadAllAssets<GameObject>())
                {
                    var creator = prefab.GetComponent(typeof(DanceResourceManager).Assembly.GetType("MEDanceModCreator") ?? typeof(AvatarDanceHandler).Assembly.GetType("MEDanceModCreator"));
                    if (creator == null) continue;
                    var dance = creator.GetType().GetField("danceClip")?.GetValue(creator) as AnimationClip;
                    var song = creator.GetType().GetField("song")?.GetValue(creator) as AudioClip;
                    if (dance != null) CurrentAnimationClip = dance;
                    if (song != null) CurrentAudioClip = song;
                }
                foreach (var controller in _currentAssetBundle.LoadAllAssets<RuntimeAnimatorController>())
                {
                    var overrides = controller as AnimatorOverrideController;
                    string placeholder = (string)meta["placeholderClipName"] ?? "CUSTOM_DANCE";
                    if (overrides == null) continue;
                    try { var mapped = overrides[placeholder]; if (mapped != null) CurrentAnimationClip = mapped; } catch (ArgumentException) { }
                }
                if (avatarHelper.CurrentAudioSource != null) avatarHelper.CurrentAudioSource.clip = CurrentAudioClip;
            }
            if (CurrentAnimationClip == null) { LastError = "No dance animation was found."; UnloadCurrentResource(); return false; }
            LastError = null;
            return true;
        }

        // Loads an optional same-name .ogg/.wav/.mp3 next to a VMD file.
        public IEnumerator LoadVmdAudio()
        {
            if (!IsVmdResource) yield break;
            string audioPath = CurrentVmdAudioPath;
            AudioType audioType = AudioType.UNKNOWN;
            if (string.IsNullOrEmpty(audioPath)) yield break;
            string extension = Path.GetExtension(audioPath);
            if (extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)) audioType = AudioType.OGGVORBIS;
            else if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)) audioType = AudioType.WAV;
            else if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)) audioType = AudioType.MPEG;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(audioPath).AbsoluteUri, audioType))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[CustomDancePlayer] Could not load VMD sidecar audio '" + audioPath + "': " + request.error);
                    yield break;
                }
                request.disposeDownloadHandlerOnDispose = false;
                CurrentAudioClip = DownloadHandlerAudioClip.GetContent(request);
                _ownsCurrentAudioClip = CurrentAudioClip != null;
            }

            if (avatarHelper.CurrentAudioSource != null)
            {
                avatarHelper.CurrentAudioSource.clip = CurrentAudioClip;
                avatarHelper.CurrentAudioSource.loop = false;
            }
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
            CurrentAnimationClip = clips.FirstOrDefault(c => c.name != "CUSTOM_DANCE" && c.name != "DANCE_END") ?? clips.FirstOrDefault();
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

            if (_ownsCurrentAudioClip && CurrentAudioClip != null)
            {
                Destroy(CurrentAudioClip);
            }

            CurrentAnimationClip = null;
            CurrentAudioClip = null;
            CurrentVmdPath = null;
            CurrentVmdOverlayPaths = new string[0];
            CurrentVmdAudioPath = null;
            CurrentVmdReferencePmxPath = null;
            CurrentVmdAudioOffsetSeconds = 0f;
            CurrentVmdCameraReferenceEyeHeight = null;
            CurrentVmdCameraReferenceBodyHeight = null;
            CurrentVmdCameraAuthoringScale = null;
            CurrentVmdPositionScale = null;
            CurrentVmdLoop = null;
            CurrentVmdFootIk = null;
            _ownsCurrentAudioClip = false;
        }

        private static string FindSidecarAudio(string vmdPath)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(vmdPath) ?? string.Empty, Path.GetFileNameWithoutExtension(vmdPath));
            foreach (string extension in new[] { ".ogg", ".wav", ".mp3" })
            {
                string candidate = FindExistingPathIgnoringFileNameCase(basePath + extension);
                if (candidate != null) return candidate;
            }
            return null;
        }

        private string FindSidecarPmx(string vmdPath)
        {
            string directory = Path.GetDirectoryName(vmdPath) ?? string.Empty;
            foreach (string candidate in new[]
            {
                Path.ChangeExtension(vmdPath, ".pmx"),
                Path.Combine(directory, "_model.pmx"),
                Path.Combine(directory, "reference.pmx"),
                Path.Combine(GetDanceFolderPath(), "_model.pmx"),
                Path.Combine(GetDanceFolderPath(), "reference.pmx"),
                Path.Combine(Application.streamingAssetsPath, "_model.pmx")
                ,Path.Combine(Path.GetDirectoryName(typeof(DanceResourceManager).Assembly.Location),"Native","_model.pmx")
            })
            {
                string existing = FindExistingPathIgnoringFileNameCase(candidate);
                if (existing != null) return existing;
            }
            return null;
        }

        private static string FindExistingPathIgnoringFileNameCase(string candidate)
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (File.Exists(fullCandidate)) return fullCandidate;
            string directory = Path.GetDirectoryName(fullCandidate);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;
            string expectedName = Path.GetFileName(fullCandidate);
            string[] siblings = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < siblings.Length; index++)
                if (string.Equals(Path.GetFileName(siblings[index]), expectedName, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(siblings[index]);
            return null;
        }

        private static bool IsInsideAnyRoot(string path, string[] roots)
        {
            string fullPath = Path.GetFullPath(path);
            for (int index = 0; index < roots.Length; index++)
                if (fullPath.StartsWith(roots[index], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetSelectionKey(string relativePath)
        {
            return Path.Combine(Path.GetDirectoryName(relativePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(relativePath));
        }

        private static string MakeRelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return fullPath.Substring(fullRoot.Length);
            return fullPath;
        }

        // Gets dance folder path
        private string GetDanceFolderPath()
        {
            return Path.Combine(Application.streamingAssetsPath, DANCE_FOLDER_NAME);
        }

        // Checks if resources are loaded
        public bool IsResourceLoaded()
        {
            return IsVmdResource || CurrentAnimationClip != null;
        }
    }
}
