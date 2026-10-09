using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public sealed class VmdNativePreparedData
    {
        internal byte[] PmxBytes;
        internal byte[] NativeVmdBytes;
        internal NativePmxMetadata Metadata;
        internal VmdMotionSampler Sampler;
        internal VmdMotionMergeReport MergeReport;
        internal string PmxPath;
        internal string MotionPath;
        internal string[] OverlayPaths;

        public string PreparationTiming { get; internal set; }
        public int LayerCount { get { return MergeReport == null ? 0 : MergeReport.LayerCount; } }
    }

    public sealed class VmdNativePreparedSession : IDisposable
    {
        private readonly object sync = new object();
        private NativeMmdSession session;

        internal VmdNativePreparedSession(NativeMmdSession value) { session = value; }

        internal NativeMmdSession Take()
        {
            lock (sync)
            {
                if (session == null) throw new InvalidOperationException("The prepared native session has already been consumed.");
                NativeMmdSession result = session;
                session = null;
                return result;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (session == null) return;
                session.Dispose();
                session = null;
            }
        }
    }

    /// <summary>
    /// Evaluates VMD against an actual PMX skeleton in mmd_runtime_ffi (append,
    /// fixed-axis, local-axis and arbitrary IK included), then transfers the final
    /// solved pose to the host Avatar. The default retargeter transfers one
    /// bind-relative bone pose in an anatomical world basis; the older
    /// HumanPose bridge remains available only as a diagnostic fallback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class VmdNativePmxPlayer : MonoBehaviour
    {
        private const float FramesPerSecond = 30f;
        private const int PreparedCacheCapacity = 2;
        private static readonly object PreparedCacheLock = new object();
        private static readonly Dictionary<string, PreparedCacheEntry> PreparedCache = new Dictionary<string, PreparedCacheEntry>(StringComparer.Ordinal);
        private static long preparedCacheClock;

        private sealed class PreparedCacheEntry
        {
            public Task<VmdNativePreparedData> Task;
            public long LastUse;
        }

        [SerializeField] private Animator targetAnimator;
        [SerializeField] private string referencePmxPath;
        [SerializeField] private string vmdPath;
        [SerializeField] private string[] additionalVmdPaths = new string[0];
        [SerializeField] private string nativeLibraryPath;
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool restorePoseOnStop = true;
        [SerializeField] private bool applyRootMotion = true;
        [SerializeField] private bool applyMorphs = true;
        [SerializeField] private bool applyIk = true;
        [SerializeField] private bool keepBodyUpright = true;
        [SerializeField] private bool lockBodyYaw = false;
        [SerializeField] private bool directBoneRetargeting = true;
        [SerializeField] private bool targetCalibratedRetargeting = true;
        [SerializeField] private bool respectAnimatorSpeed;
        [SerializeField] private VmdNativePhysicsMode nativePhysicsMode = VmdNativePhysicsMode.Off;
        [SerializeField] private float referenceImportScale = 0.08f;
        [SerializeField] private float cameraDistanceScale = 1f;
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform cameraOrigin;

        private readonly List<PoseBoneBinding> poseBindings = new List<PoseBoneBinding>();

        private VmdExpressionBindings expressionBindings;
        private NativeMmdSession session;
        private NativePmxMetadata metadata;
        private VmdMotionSampler auxiliarySampler;
        private float playbackEvaluationFrame;
        private Vector3 directFacingPositionOffset;
        private string directLeftLegIkName;
        private string directRightLegIkName;
        private Matrix4x4 directInitialWorldToLocal;
        private Quaternion directInitialRootRotation,directInitialBasis;
        private readonly Dictionary<PoseBoneBinding,Vector3> directInitialPositions=new Dictionary<PoseBoneBinding,Vector3>();
        private readonly Dictionary<PoseBoneBinding,Quaternion> directInitialRotations=new Dictionary<PoseBoneBinding,Quaternion>();
        private readonly Dictionary<PoseBoneBinding,Quaternion> directInitialAligned=new Dictionary<PoseBoneBinding,Quaternion>();
        public float ExternalCameraGroundHeight { get; set; }
        public bool UseExternalCameraScale { get; set; }
        private GameObject nativeRoot;
        private Transform[] nativeBones;
        private Transform retargetRootBone;
        private Quaternion retargetRootBindWorldRotation = Quaternion.identity;
        private Vector3 retargetRootBindWorldPosition;
        private Quaternion retargetRootCorrection = Quaternion.identity;
        private Vector3 retargetRootPivot;
        private bool hasInitialRetargetRootYaw;
        private float initialRetargetRootYaw;
        private GameObject proxyRoot;
        private Avatar sourceAvatar;
        private HumanPoseHandler sourcePoseHandler;
        private HumanPoseHandler targetPoseHandler;
        private HumanPoseHandler targetCalibrationPoseHandler;
        private GameObject targetCalibrationRoot;
        private readonly List<TargetCalibrationBinding> targetCalibrationBindings = new List<TargetCalibrationBinding>();
        private HumanPose sourceRestPose;
        private HumanPose targetRestPose;
        private HumanPose targetCalibrationRestPose;
        private HumanPose targetCalibratedPose;
        private HumanPose sampledPose;
        private bool hasInitialSourceBodyYaw;
        private float initialSourceBodyYaw;
        private float sourceHumanScale = 1f;
        private float targetHumanScale = 1f;
        private Quaternion directRetargetBasis = Quaternion.identity;
        private PoseBoneBinding directHipsBinding;
        private PoseBoneBinding directSpineBinding;
        private PoseBoneBinding directHeadBinding;
        private Transform directBodyCommonBone;
        private GameObject directRestRoot;
        private Dictionary<Transform, Transform> directRestBones;
        private readonly List<DirectRestNode> directRestNodes = new List<DirectRestNode>();
        private bool hasDirectFacingBaseline;
        private float directFacingBaseline;
        private float playbackTime;
        private float duration;
        private bool isPrepared;
        private bool isPlaying;
        private int sourceMotionIkToggleCount;
        private int sourceMotionIkDisableCount;
        private VmdMotionMergeReport motionMergeReport;
        private bool hasPreviousCameraPose;
        private VmdUnityCameraPose previousCameraPose;
        private float previousCameraFrame;
        private VmdCameraRenderDriver cameraRenderDriver;
        private bool hasLastCameraWorldPose;
        private Vector3 lastCameraWorldPosition;
        private Quaternion lastCameraWorldRotation;
        private float lastCameraFieldOfView;
        private bool lastCameraOrthographic;
        private Camera savedCamera;
        private Vector3 savedCameraWorldPosition;
        private Quaternion savedCameraWorldRotation;
        private float savedCameraFieldOfView;
        private bool savedCameraOrthographic;
        private bool savedCameraEnabled;
        private bool hasSavedCameraState;
        private Vector3 cameraRetargetOffset;
        private bool nativePhysicsResetPending;
        private bool hasNativePhysicsEvaluation;
        private float lastNativePhysicsTime;

        public event Action PlaybackCompleted;

        public Animator TargetAnimator
        {
            get { return targetAnimator; }
            set
            {
                if (targetAnimator == value) return;
                DisposePlayback(true);
                targetAnimator = value;
            }
        }

        public string NativeLibraryPath { get { return nativeLibraryPath; } set { nativeLibraryPath = value; } }
        public string ReferencePmxPath { get { return referencePmxPath; } }
        public string[] AdditionalVmdPaths
        {
            get { return additionalVmdPaths == null ? new string[0] : (string[])additionalVmdPaths.Clone(); }
            set { additionalVmdPaths = value == null ? new string[0] : (string[])value.Clone(); }
        }
        public Camera TargetCamera
        {
            get { return targetCamera; }
            set
            {
                if (targetCamera == value) return;
                RestoreTargetCameraState();
                ReleaseCameraRenderDriver();
                targetCamera = value;
                ResetCameraContinuityState();
                EnsureCameraRenderDriver();
            }
        }
        public Transform CameraOrigin { get { return cameraOrigin; } set { cameraOrigin = value; } }
        public bool Loop { get { return loop; } set { loop = value; } }
        public bool ApplyRootMotion { get { return applyRootMotion; } set { applyRootMotion = value; } }
        public bool ApplyMorphs { get { return applyMorphs; } set { applyMorphs = value; } }
        public bool ApplyIk { get { return applyIk; } set { applyIk = value; } }
        public bool KeepBodyUpright { get { return keepBodyUpright; } set { keepBodyUpright = value; } }
        public bool LockBodyYaw { get { return lockBodyYaw; } set { lockBodyYaw = value; } }
        public bool DirectBoneRetargeting { get { return directBoneRetargeting; } set { directBoneRetargeting = value; } }
        public bool TargetCalibratedRetargeting { get { return targetCalibratedRetargeting; } set { targetCalibratedRetargeting = value; } }
        public bool RespectAnimatorSpeed { get { return respectAnimatorSpeed; } set { respectAnimatorSpeed = value; } }
        public VmdNativePhysicsMode NativePhysicsMode
        {
            get { return nativePhysicsMode; }
            set
            {
                if (value < VmdNativePhysicsMode.Off || value > VmdNativePhysicsMode.Live)
                    throw new ArgumentOutOfRangeException("value");
                if (isPrepared && value != nativePhysicsMode)
                    throw new InvalidOperationException("Set NativePhysicsMode before loading a PMX/VMD pair.");
                nativePhysicsMode = value;
            }
        }
        public float ReferenceImportScale { get { return referenceImportScale; } set { referenceImportScale = NormalizeScale(value); } }
        public float CameraDistanceScale { get { return cameraDistanceScale; } set { cameraDistanceScale = Mathf.Clamp(value, 0.01f, 100f); } }
        public float PlaybackSpeed { get { return playbackSpeed; } set { playbackSpeed = value; } }
        public float PlaybackTime { get { return playbackTime; } }
        public float Duration { get { return duration; } }
        public bool IsLoaded { get { return session != null && isPrepared; } }
        public bool IsActive { get { return session != null && isPrepared; } }
        public bool IsPlaying { get { return isPlaying; } }
        public VmdMotion Motion { get { return auxiliarySampler == null ? null : auxiliarySampler.Motion; } }
        public int BoundHumanBoneCount { get { return poseBindings.Count; } }
        public int BoundMorphCount { get { return expressionBindings==null?0:expressionBindings.Count; } }
        public int SourceBoneCount { get { return metadata == null ? 0 : metadata.Bones.Count; } }
        public int SourceIkCount { get { return session == null ? 0 : session.IkCount; } }
        public int SourceMotionIkToggleCount { get { return sourceMotionIkToggleCount; } }
        public int SourceMotionIkDisableCount { get { return sourceMotionIkDisableCount; } }
        public float SourceHumanScale { get { return sourceHumanScale; } }
        public float TargetHumanScale { get { return targetHumanScale; } }
        public int LastNativeIkEnabledCount { get; private set; }
        public int LastNativeIkDisabledCount { get; private set; }
        public int LastTargetLegIkSolvedCount { get; private set; }
        public int LastTargetFootOrientationAppliedCount { get; private set; }
        public int LastAppliedHumanBoneCount { get; private set; }
        public float LastSourceBodyYawFromRest { get; private set; }
        public float InitialRootYawOffset { get { return initialRetargetRootYaw; } }
        public float InitialBodyYawOffset { get { return initialSourceBodyYaw; } }
        public float LastCalibratedBodyYawFromRest { get; private set; }
        public float LastAppliedBodyYawFromRest { get; private set; }
        public float LastBodyYawRetargetError { get; private set; }
        public Vector3 LastSourceBodyDeltaNormalized { get; private set; }
        public Vector3 LastAppliedBodyDeltaNormalized { get; private set; }
        public float LastBodyPositionRetargetError { get; private set; }
        public float LastAppliedBodyTiltDegrees { get; private set; }
        public float LastSourceRootTiltDegrees { get; private set; }
        public bool LastIkSolveEnabled { get; private set; }
        public bool HasAppliedCameraPose { get; private set; }
        public bool LastAppliedCameraPerspective { get; private set; }
        public float LastAppliedCameraFrame { get; private set; }
        public int CameraHistoryResetCount { get; private set; }
        public int AuthoredCameraCutCount { get; private set; }
        public int CameraRenderReapplyCount { get; private set; }
        public float CameraRetargetScale { get; private set; }
        public float LastAppliedCameraDistanceWorld { get; private set; }
        private float cameraWorldUnit;
        private float savedNearClip,savedFarClip,savedOrthoSize;
        private Matrix4x4 savedProjection;
        private bool savedProjectionCustom;
        public Vector3 CameraRetargetOffset { get { return cameraRetargetOffset; } }
        public int LastCameraHistoryResetComponentCount { get; private set; }
        public string LastCameraDiscontinuityReason { get; private set; }
        public int MotionLayerCount { get { return motionMergeReport == null ? 0 : motionMergeReport.LayerCount; } }
        public VmdMotionMergeReport MotionMergeReport { get { return motionMergeReport; } }
        public bool IsUsingDirectBoneRetargeting { get { return directBoneRetargeting && directHipsBinding != null; } }
        public bool IsUsingTargetCalibration { get { return !IsUsingDirectBoneRetargeting && targetCalibratedRetargeting && targetCalibrationPoseHandler != null && targetCalibrationBindings.Count > 0; } }
        public uint NativeAbiVersion { get { return session == null ? 0 : session.AbiVersion; } }
        public uint NativeFeatureFlags { get { return session == null ? 0 : session.FeatureFlags; } }
        public bool IsNativePhysicsActive { get { return session != null && session.IsPhysicsActive; } }
        public int NativePhysicsRigidbodyCount { get { return session == null ? 0 : session.PhysicsRigidbodyCount; } }
        public int NativePhysicsDrivenBoneCount { get { return session == null ? 0 : session.PhysicsDrivenBoneCount; } }
        public int LastNativePhysicsSubstepCount { get { return session == null ? 0 : session.LastPhysicsSubstepCount; } }
        public int LastNativePhysicsBonesWritten { get { return session == null ? 0 : session.LastPhysicsBonesWritten; } }
        public string LastDiagnostics { get; private set; }
        public string LastLoadTiming { get; private set; }

        public static Task<VmdNativePreparedData> PrepareCachedAsync(string pmxPath, string motionPath, IEnumerable<string> overlayPaths)
        {
            string fullPmxPath = ValidateFile(pmxPath, "A reference PMX path is required.", "The reference PMX was not found.");
            string fullMotionPath = ValidateFile(motionPath, "A VMD path is required.", "The VMD was not found.");
            string[] fullOverlayPaths = overlayPaths == null
                ? new string[0]
                : overlayPaths.Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => ValidateFile(path, "An additional VMD path is required.", "An additional VMD was not found."))
                    .ToArray();
            string cacheKey = BuildPreparedCacheKey(fullPmxPath, fullMotionPath, fullOverlayPaths);
            lock (PreparedCacheLock)
            {
                PreparedCacheEntry existing;
                if (PreparedCache.TryGetValue(cacheKey, out existing))
                {
                    existing.LastUse = ++preparedCacheClock;
                    return existing.Task;
                }

                Task<VmdNativePreparedData> task = Task.Run(() => Prepare(fullPmxPath, fullMotionPath, fullOverlayPaths));
                PreparedCache[cacheKey] = new PreparedCacheEntry { Task = task, LastUse = ++preparedCacheClock };
                TrimPreparedCache(cacheKey);
                task.ContinueWith(completed =>
                {
                    if (!completed.IsFaulted && !completed.IsCanceled) return;
                    lock (PreparedCacheLock)
                    {
                        PreparedCacheEntry current;
                        if (PreparedCache.TryGetValue(cacheKey, out current) && ReferenceEquals(current.Task, completed))
                            PreparedCache.Remove(cacheKey);
                    }
                }, TaskScheduler.Default);
                return task;
            }
        }

        public static Task<VmdNativePreparedSession> CreateSessionAsync(VmdNativePreparedData prepared, string libraryPath, VmdNativePhysicsMode physicsMode)
        {
            if (prepared == null) throw new ArgumentNullException("prepared");
            if (prepared.PmxBytes == null || prepared.NativeVmdBytes == null)
                throw new ArgumentException("Prepared VMD data is incomplete.", "prepared");
            return Task.Run(() => new VmdNativePreparedSession(
                NativeMmdSession.Create(prepared.PmxBytes, prepared.NativeVmdBytes, libraryPath, physicsMode)));
        }

        private static VmdNativePreparedData Prepare(string fullPmxPath, string fullMotionPath, string[] fullOverlayPaths)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            byte[] pmxBytes = File.ReadAllBytes(fullPmxPath);
            byte[] primaryBytes = File.ReadAllBytes(fullMotionPath);
            byte[][] overlayBytes = new byte[fullOverlayPaths.Length][];
            for (int index = 0; index < fullOverlayPaths.Length; index++) overlayBytes[index] = File.ReadAllBytes(fullOverlayPaths[index]);
            long filesMilliseconds = timer.ElapsedMilliseconds;

            NativePmxMetadata preparedMetadata = NativePmxMetadata.Read(pmxBytes);
            List<VmdMotion> layers = new List<VmdMotion> { VmdReader.Read(primaryBytes) };
            for (int index = 0; index < overlayBytes.Length; index++) layers.Add(VmdReader.Read(overlayBytes[index]));
            VmdMotionMergeReport preparedReport;
            VmdMotion mergedMotion = VmdMotionLayers.Merge(layers, out preparedReport);
            byte[] nativeBytes = layers.Count == 1 ? primaryBytes : VmdWriter.Write(mergedMotion);
            VmdMotionSampler preparedSampler = new VmdMotionSampler(mergedMotion);
            long totalMilliseconds = timer.ElapsedMilliseconds;
            return new VmdNativePreparedData
            {
                PmxBytes = pmxBytes,
                NativeVmdBytes = nativeBytes,
                Metadata = preparedMetadata,
                Sampler = preparedSampler,
                MergeReport = preparedReport,
                PmxPath = fullPmxPath,
                MotionPath = fullMotionPath,
                OverlayPaths = (string[])fullOverlayPaths.Clone(),
                PreparationTiming = "prepareFiles=" + filesMilliseconds + "ms; prepareManaged=" + (totalMilliseconds - filesMilliseconds) + "ms; prepareTotal=" + totalMilliseconds + "ms"
            };
        }

        private static string ValidateFile(string path, string emptyMessage, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(emptyMessage, "path");
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException(missingMessage, fullPath);
            return fullPath;
        }

        private static string BuildPreparedCacheKey(string pmxPath, string motionPath, string[] overlayPaths)
        {
            IEnumerable<string> paths = new[] { pmxPath, motionPath }.Concat(overlayPaths);
            return string.Join("|", paths.Select(path =>
            {
                FileInfo info = new FileInfo(path);
                return path.ToUpperInvariant() + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
            }));
        }

        private static void TrimPreparedCache(string protectedKey)
        {
            while (PreparedCache.Count > PreparedCacheCapacity)
            {
                string victim = PreparedCache.Where(pair => pair.Key != protectedKey)
                    .OrderBy(pair => pair.Value.LastUse).Select(pair => pair.Key).FirstOrDefault();
                if (victim == null) return;
                PreparedCache.Remove(victim);
            }
        }

        private void Reset()
        {
            targetAnimator = GetComponent<Animator>();
        }

        private void Start()
        {
            if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
            if (!string.IsNullOrWhiteSpace(referencePmxPath) && !string.IsNullOrWhiteSpace(vmdPath) && !IsLoaded)
            {
                try
                {
                    Load(referencePmxPath, vmdPath, additionalVmdPaths);
                    if (playOnStart) Play();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[RuntimeVmd.Native] Startup failed: " + exception, this);
                }
            }
        }

        private void Update()
        {
            if (!isPlaying || !isPrepared) return;
            if (respectAnimatorSpeed && targetAnimator != null && targetAnimator.speed <= 0f) return;
            playbackTime += Time.deltaTime * playbackSpeed;
            if (duration <= 0f)
            {
                playbackTime = 0f;
                isPlaying = false;
                RaiseCompleted();
            }
            else if (loop)
            {
                playbackTime = Mathf.Repeat(playbackTime, duration);
            }
            else if (playbackTime >= duration)
            {
                playbackTime = duration;
                isPlaying = false;
                RaiseCompleted();
            }
            else if (playbackTime < 0f)
            {
                playbackTime = 0f;
                isPlaying = false;
                RaiseCompleted();
            }
        }

        private void LateUpdate()
        {
            bool allowLivePhysicsStep = isPlaying &&
                (!respectAnimatorSpeed || targetAnimator == null || targetAnimator.speed > 0f);
            if (isPrepared) { ApplyAtTimeInternal(playbackTime, false, allowLivePhysicsStep); PoseApplied?.Invoke(); }
        }
        public event Action PoseApplied;

        private void OnDisable()
        {
            isPlaying = false;
            if (restorePoseOnStop) RestoreTargetPose();
            RestoreTargetCameraState();
        }

        private void OnDestroy()
        {
            RestoreTargetCameraState();
            ReleaseCameraRenderDriver();
            DisposePlayback(false);
        }

        public void Load(string pmxPath, string motionPath)
        {
            Load(pmxPath, motionPath, null);
        }

        public void Load(string pmxPath, string motionPath, IEnumerable<string> overlayPaths)
        {
            System.Diagnostics.Stopwatch fileTimer = System.Diagnostics.Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(pmxPath)) throw new ArgumentException("A reference PMX path is required.", "pmxPath");
            if (string.IsNullOrWhiteSpace(motionPath)) throw new ArgumentException("A VMD path is required.", "motionPath");
            string fullPmxPath = Path.GetFullPath(pmxPath);
            string fullVmdPath = Path.GetFullPath(motionPath);
            if (!File.Exists(fullPmxPath)) throw new FileNotFoundException("The reference PMX was not found.", fullPmxPath);
            if (!File.Exists(fullVmdPath)) throw new FileNotFoundException("The VMD was not found.", fullVmdPath);
            byte[] pmxBytes = File.ReadAllBytes(fullPmxPath);
            byte[] vmdBytes = File.ReadAllBytes(fullVmdPath);
            List<string> fullOverlayPaths = new List<string>();
            List<byte[]> overlayBytes = new List<byte[]>();
            if (overlayPaths != null)
            {
                foreach (string overlayPath in overlayPaths)
                {
                    if (string.IsNullOrWhiteSpace(overlayPath)) continue;
                    string fullOverlayPath = Path.GetFullPath(overlayPath);
                    if (!File.Exists(fullOverlayPath)) throw new FileNotFoundException("An additional VMD was not found.", fullOverlayPath);
                    fullOverlayPaths.Add(fullOverlayPath);
                    overlayBytes.Add(File.ReadAllBytes(fullOverlayPath));
                }
            }
            long fileMilliseconds = fileTimer.ElapsedMilliseconds;
            Load(pmxBytes, vmdBytes, overlayBytes);
            LastLoadTiming = "files=" + fileMilliseconds + "ms; " + LastLoadTiming;
            referencePmxPath = fullPmxPath;
            vmdPath = fullVmdPath;
            additionalVmdPaths = fullOverlayPaths.ToArray();
        }

        public bool TryLoad(string pmxPath, string motionPath, out string error)
        {
            return TryLoad(pmxPath, motionPath, null, out error);
        }

        public bool TryLoad(string pmxPath, string motionPath, IEnumerable<string> overlayPaths, out string error)
        {
            try
            {
                Load(pmxPath, motionPath, overlayPaths);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        public void Load(byte[] pmxBytes, byte[] vmdBytes)
        {
            Load(pmxBytes, vmdBytes, null);
        }

        public void Load(byte[] pmxBytes, byte[] vmdBytes, IEnumerable<byte[]> overlayVmdBytes)
        {
            ValidateTargetAnimator();
            DisposePlayback(true);
            System.Diagnostics.Stopwatch loadTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                NativePmxMetadata preparedMetadata = NativePmxMetadata.Read(pmxBytes);
                List<VmdMotion> layers = new List<VmdMotion> { VmdReader.Read(vmdBytes) };
                if (overlayVmdBytes != null)
                {
                    foreach (byte[] overlayBytes in overlayVmdBytes)
                    {
                        if (overlayBytes == null || overlayBytes.Length == 0)
                            throw new ArgumentException("Additional VMD data must not be null or empty.", "overlayVmdBytes");
                        layers.Add(VmdReader.Read(overlayBytes));
                    }
                }
                VmdMotionMergeReport preparedReport;
                VmdMotion mergedMotion = VmdMotionLayers.Merge(layers, out preparedReport);
                byte[] nativeVmdBytes = layers.Count == 1 ? vmdBytes : VmdWriter.Write(mergedMotion);
                VmdNativePreparedData prepared = new VmdNativePreparedData
                {
                    PmxBytes = pmxBytes,
                    NativeVmdBytes = nativeVmdBytes,
                    Metadata = preparedMetadata,
                    Sampler = new VmdMotionSampler(mergedMotion),
                    MergeReport = preparedReport,
                    OverlayPaths = new string[0],
                    PreparationTiming = "managed=" + loadTimer.ElapsedMilliseconds + "ms"
                };
                CompletePreparedLoad(prepared, null, loadTimer, prepared.PreparationTiming);
            }
            catch
            {
                DisposePlayback(true);
                throw;
            }
        }

        public bool TryLoadPrepared(VmdNativePreparedData prepared, out string error)
        {
            try
            {
                LoadPrepared(prepared);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        public bool TryLoadPrepared(VmdNativePreparedData prepared, VmdNativePreparedSession preparedSession, out string error)
        {
            try
            {
                LoadPrepared(prepared, preparedSession);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        public void LoadPrepared(VmdNativePreparedData prepared)
        {
            LoadPrepared(prepared, null);
        }

        public void LoadPrepared(VmdNativePreparedData prepared, VmdNativePreparedSession preparedSession)
        {
            if (prepared == null) throw new ArgumentNullException("prepared");
            if (prepared.PmxBytes == null || prepared.NativeVmdBytes == null || prepared.Metadata == null || prepared.Sampler == null)
                throw new ArgumentException("Prepared VMD data is incomplete.", "prepared");
            ValidateTargetAnimator();
            DisposePlayback(true);
            System.Diagnostics.Stopwatch loadTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                CompletePreparedLoad(prepared, preparedSession, loadTimer, prepared.PreparationTiming);
                referencePmxPath = prepared.PmxPath;
                vmdPath = prepared.MotionPath;
                additionalVmdPaths = prepared.OverlayPaths == null ? new string[0] : (string[])prepared.OverlayPaths.Clone();
            }
            catch
            {
                DisposePlayback(true);
                throw;
            }
        }

        private void CompletePreparedLoad(VmdNativePreparedData prepared, VmdNativePreparedSession preparedSession, System.Diagnostics.Stopwatch loadTimer, string preparationTiming)
        {
            metadata = prepared.Metadata;
            auxiliarySampler = prepared.Sampler;
            motionMergeReport = prepared.MergeReport;
            CountSourceIkToggles(auxiliarySampler.Motion);
            long preparationBoundary = loadTimer.ElapsedMilliseconds;
            if (preparedSession != null) session = preparedSession.Take();
            else
            {
                string library = string.IsNullOrWhiteSpace(nativeLibraryPath) ? NativeMmdSession.FindDefaultLibraryPath() : nativeLibraryPath;
                session = NativeMmdSession.Create(prepared.PmxBytes, prepared.NativeVmdBytes, library, nativePhysicsMode);
            }
            long nativeMilliseconds = loadTimer.ElapsedMilliseconds - preparationBoundary;
            if (session.BoneCount != metadata.Bones.Count)
                throw new InvalidOperationException("The native and managed PMX bone counts differ.");

            BuildNativeRig();
            long rigMilliseconds = loadTimer.ElapsedMilliseconds - preparationBoundary - nativeMilliseconds;
            BuildHumanoidBridge();
            long bridgeMilliseconds = loadTimer.ElapsedMilliseconds - preparationBoundary - nativeMilliseconds - rigMilliseconds;
            BindMorphs();
            long morphMilliseconds = loadTimer.ElapsedMilliseconds - preparationBoundary - nativeMilliseconds - rigMilliseconds - bridgeMilliseconds;
            duration = auxiliarySampler.Motion.Duration;
            playbackTime = 0f;
            isPrepared = true;
            isPlaying = false;
            nativePhysicsResetPending = true;
            hasNativePhysicsEvaluation = false;
            ApplyAtTime(0f);
            long firstPoseMilliseconds = loadTimer.ElapsedMilliseconds - preparationBoundary - nativeMilliseconds - rigMilliseconds - bridgeMilliseconds - morphMilliseconds;
            LastLoadTiming = preparationTiming + "; mainNative=" + nativeMilliseconds
                + "ms; rig=" + rigMilliseconds + "ms; bridge=" + bridgeMilliseconds
                + "ms; morph=" + morphMilliseconds + "ms; firstPose=" + firstPoseMilliseconds
                + "ms; mainTotal=" + loadTimer.ElapsedMilliseconds + "ms";
            LastDiagnostics = "backend=native-pmx; abi=" + session.AbiVersion
                + "; features=0x" + session.FeatureFlags.ToString("X8")
                + "; pmx='" + metadata.NameJapanese + "'"
                + "; sourceBones=" + SourceBoneCount
                + "; humanBones=" + BoundHumanBoneCount
                + "; retarget=" + (IsUsingDirectBoneRetargeting
                    ? "rest-rig"
                    : IsUsingTargetCalibration ? "target-calibrated" : "canonical")
                + "; ik=" + SourceIkCount
                + "; ikToggles=" + SourceMotionIkToggleCount
                + "; ikOff=" + SourceMotionIkDisableCount
                + "; nativeMorphs=" + session.MorphCount
                + "; boundMorphs=" + BoundMorphCount
                + "; physics=" + session.PhysicsMode.ToString().ToLowerInvariant()
                + "; rigidbodies=" + session.PhysicsRigidbodyCount
                + "; physicsBones=" + session.PhysicsDrivenBoneCount
                + "; load={" + LastLoadTiming + "}"
                + "; " + motionMergeReport;
        }

        public void Play()
        {
            if (!isPrepared) throw new InvalidOperationException("Load a PMX/VMD pair before calling Play.");
            if (!loop && playbackTime >= duration) playbackTime = 0f;
            isPlaying = true;
        }

        public void Pause()
        {
            isPlaying = false;
        }

        public void Stop()
        {
            isPlaying = false;
            playbackTime = 0f;
            nativePhysicsResetPending = true;
            hasNativePhysicsEvaluation = false;
            if (restorePoseOnStop) RestoreTargetPose();
            RestoreTargetCameraState();
        }

        public void Unload()
        {
            DisposePlayback(true);
        }

        public void Seek(float timeSeconds, bool applyImmediately)
        {
            playbackTime = loop && duration > 0f
                ? Mathf.Repeat(timeSeconds, duration)
                : Mathf.Clamp(timeSeconds, 0f, duration);
            nativePhysicsResetPending = true;
            if (applyImmediately && isPrepared) ApplyAtTime(playbackTime);
        }

        public void ApplyAtTime(float timeSeconds)
        {
            nativePhysicsResetPending = true;
            ApplyAtTimeInternal(timeSeconds, true, false);
        }

        private void ApplyAtTimeInternal(float timeSeconds, bool forcePhysicsReset, bool allowLivePhysicsStep)
        {
            if (!isPrepared || session == null || sourcePoseHandler == null || targetPoseHandler == null) return;
            float evaluatedTime = loop && duration > 0f ? Mathf.Repeat(timeSeconds, duration) : Mathf.Clamp(timeSeconds, 0f, duration);
            float frame = Mathf.Max(0f, evaluatedTime * FramesPerSecond);
            playbackEvaluationFrame = frame;
            if (!applyIk)
            {
                session.EvaluateWithoutIk(frame);
                nativePhysicsResetPending = true;
                hasNativePhysicsEvaluation = false;
            }
            else if (session.IsPhysicsActive)
            {
                float timelineDelta = hasNativePhysicsEvaluation ? evaluatedTime - lastNativePhysicsTime : 0f;
                bool backwards = timelineDelta < -0.000001f;
                bool discontinuity = Mathf.Abs(timelineDelta) > 0.25f;
                bool resetPhysics = forcePhysicsReset || nativePhysicsResetPending || !hasNativePhysicsEvaluation || backwards || discontinuity;
                bool timelineChanged = !hasNativePhysicsEvaluation || Mathf.Abs(timelineDelta) > 0.000001f;
                bool liveStep = nativePhysicsMode == VmdNativePhysicsMode.Live && allowLivePhysicsStep && Time.deltaTime > 0f;
                if (resetPhysics || timelineChanged || liveStep)
                {
                    float physicsDelta = 0f;
                    if (!resetPhysics)
                        physicsDelta = nativePhysicsMode == VmdNativePhysicsMode.Live
                            ? Mathf.Max(0f, Time.deltaTime)
                            : Mathf.Max(0f, timelineDelta);
                    session.EvaluateWithPhysics(frame, physicsDelta, resetPhysics);
                    nativePhysicsResetPending = false;
                    hasNativePhysicsEvaluation = true;
                    lastNativePhysicsTime = evaluatedTime;
                }
            }
            else
            {
                session.Evaluate(frame);
            }
            LastIkSolveEnabled = applyIk;
            UpdateNativeIkDiagnostics();
            ApplyNativeWorldMatrices();
            UpdateRetargetRootCorrection();
            if (IsUsingDirectBoneRetargeting && keepBodyUpright && directBodyCommonBone != null)
            {
                if (!hasDirectFacingBaseline)
                {
                    directFacingBaseline = SignedPlanarYaw(Quaternion.identity,
                        CorrectSourceRotation(directBodyCommonBone.rotation));
                    hasDirectFacingBaseline = true;
                }
                float currentFacing = SignedPlanarYaw(Quaternion.identity,
                    CorrectSourceRotation(directBodyCommonBone.rotation));
                Quaternion baselineCorrection = Quaternion.AngleAxis(-directFacingBaseline, Vector3.up);
                retargetRootCorrection = baselineCorrection * retargetRootCorrection;
                if (lockBodyYaw)
                {
                    Quaternion turnCorrection = Quaternion.AngleAxis(
                        -Mathf.DeltaAngle(directFacingBaseline, currentFacing), Vector3.up);
                    // Lock orientation about the moving Center/Waist pivot,
                    // not AllParent's distant origin. Otherwise an authored
                    // turn sweeps the translated dancer around the scene.
                    Vector3 center = retargetRootCorrection *
                        (directBodyCommonBone.position - retargetRootPivot);
                    directFacingPositionOffset = center - turnCorrection * center;
                    retargetRootCorrection = turnCorrection * retargetRootCorrection;
                }
            }
            if(IsUsingDirectBoneRetargeting)
            {
                // Direct retargeting already has solved PMX joints. Avoid an
                // unused Humanoid conversion through the proxy every frame.
                float yaw=SignedPlanarYaw(directHipsBinding.SourceBindWorldRotation,CorrectSourceRotation(directHipsBinding.Source.rotation));
                if(!hasInitialSourceBodyYaw){initialSourceBodyYaw=yaw;hasInitialSourceBodyYaw=true;}
                LastSourceBodyYawFromRest=keepBodyUpright?Mathf.DeltaAngle(initialSourceBodyYaw,yaw):yaw;
                LastSourceBodyDeltaNormalized=(CorrectSourcePosition(directHipsBinding.Source.position)-directHipsBinding.SourceBindWorldPosition)/Mathf.Max(sourceHumanScale,0.00001f);
                LastCalibratedBodyYawFromRest=LastSourceBodyYawFromRest;
                ApplyDirectBonePose();LastAppliedHumanBoneCount=poseBindings.Count;
                if(applyMorphs)ApplyMorphsAtFrame(frame);
                ApplyCamera(frame);return;
            }
            CopyNativePoseToProxy();

            EnsureMuscleBuffer(ref sampledPose);
            sourcePoseHandler.GetHumanPose(ref sampledPose);
            float rawSourceBodyYaw = SignedPlanarYaw(sourceRestPose.bodyRotation, sampledPose.bodyRotation);
            if (!hasInitialSourceBodyYaw)
            {
                initialSourceBodyYaw = rawSourceBodyYaw;
                hasInitialSourceBodyYaw = true;
            }
            LastSourceBodyYawFromRest = keepBodyUpright
                ? Mathf.DeltaAngle(initialSourceBodyYaw, rawSourceBodyYaw)
                : rawSourceBodyYaw;
            // HumanPose.bodyPosition is already divided by Avatar humanScale.
            // Keep it in that canonical space when measuring and transferring
            // root motion between differently proportioned Humanoids.
            LastSourceBodyDeltaNormalized =
                sampledPose.bodyPosition - sourceRestPose.bodyPosition;
            HumanPose outputPose = sampledPose;
            if (IsUsingTargetCalibration)
            {
                ApplyNativePoseToTargetCalibration();
                EnsureMuscleBuffer(ref targetCalibratedPose);
                targetCalibrationPoseHandler.GetHumanPose(ref targetCalibratedPose);
                LastCalibratedBodyYawFromRest = SignedPlanarYaw(
                    targetCalibrationRestPose.bodyRotation,
                    targetCalibratedPose.bodyRotation);
                CopyCanonicalUpperLimbMuscles(sampledPose, targetCalibratedPose);
                outputPose = targetCalibratedPose;
            }
            else LastCalibratedBodyYawFromRest = LastSourceBodyYawFromRest;
            if (applyRootMotion)
            {
                // Target calibration is valuable for muscles because different
                // Humanoid skeletons do not produce identical muscle values from
                // the same world-space bone rotations. HumanPose root translation
                // and rotation are already canonical, however. Taking those from
                // the calibration clone makes large travel arcs depend on target
                // proportions (the short-motion regression exposed >3 cm error).
                // Keep calibrated muscles but transfer the source HumanPose root
                // delta directly into the target rest frame.
                outputPose.bodyPosition = VmdHumanPoseRetargeting.RetargetBodyPosition(
                    sourceRestPose.bodyPosition,
                    sampledPose.bodyPosition,
                    targetRestPose.bodyPosition);
                outputPose.bodyRotation = VmdHumanPoseRetargeting.RetargetBodyRotation(
                    sourceRestPose.bodyRotation,
                    sampledPose.bodyRotation,
                    targetRestPose.bodyRotation);
                if (keepBodyUpright)
                {
                    float yaw = lockBodyYaw ? 0f : LastSourceBodyYawFromRest;
                    outputPose.bodyRotation = targetRestPose.bodyRotation * Quaternion.AngleAxis(yaw, Vector3.up);
                }
            }
            else
            {
                outputPose.bodyPosition = targetRestPose.bodyPosition;
                outputPose.bodyRotation = targetRestPose.bodyRotation;
            }
            LastAppliedBodyYawFromRest = SignedPlanarYaw(targetRestPose.bodyRotation, outputPose.bodyRotation);
            LastAppliedBodyTiltDegrees = AngleBetween(targetRestPose.bodyRotation * Vector3.up, outputPose.bodyRotation * Vector3.up);
            LastBodyYawRetargetError = Mathf.Abs(Mathf.DeltaAngle(
                LastSourceBodyYawFromRest,
                LastAppliedBodyYawFromRest));
            LastAppliedBodyDeltaNormalized =
                outputPose.bodyPosition - targetRestPose.bodyPosition;
            LastBodyPositionRetargetError =
                (LastAppliedBodyDeltaNormalized - LastSourceBodyDeltaNormalized).magnitude;
            targetPoseHandler.SetHumanPose(ref outputPose);
            LastTargetLegIkSolvedCount = applyIk ? ApplyTargetLegRetargeting() : 0;
            // HumanPose already supplies the authored ankle/toe rotations.  The
            // target leg solve preserves the ankle's world rotation, so applying
            // the PMX world delta again here twists rigs whose foot axes differ.
            LastTargetFootOrientationAppliedCount = 0;
            LastAppliedHumanBoneCount = poseBindings.Count;
            if (applyMorphs) ApplyMorphsAtFrame(frame);
            ApplyCamera(frame);
        }

        private void CountSourceIkToggles(VmdMotion motion)
        {
            sourceMotionIkToggleCount = 0;
            sourceMotionIkDisableCount = 0;
            if (motion == null) return;
            for (int frameIndex = 0; frameIndex < motion.IkFrames.Count; frameIndex++)
            {
                VmdIkKeyframe frame = motion.IkFrames[frameIndex];
                sourceMotionIkToggleCount += frame.Toggles.Count;
                for (int toggleIndex = 0; toggleIndex < frame.Toggles.Count; toggleIndex++)
                    if (!frame.Toggles[toggleIndex].Enabled) sourceMotionIkDisableCount++;
            }
        }

        private void UpdateNativeIkDiagnostics()
        {
            LastNativeIkEnabledCount = 0;
            LastNativeIkDisabledCount = 0;
            byte[] states = session == null ? null : session.IkEnabled;
            if (states == null) return;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index] != 0) LastNativeIkEnabledCount++;
                else LastNativeIkDisabledCount++;
            }
        }

        private void ValidateTargetAnimator()
        {
            if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null) throw new InvalidOperationException("A target Animator is required.");
            if (!targetAnimator.isHuman || targetAnimator.avatar == null || !targetAnimator.avatar.isValid)
                throw new InvalidOperationException("The target Animator must use a valid Humanoid Avatar.");
        }

        private void BuildNativeRig()
        {
            nativeRoot = new GameObject("RuntimeVmd.NativePmx.ReferenceRig");
            nativeRoot.hideFlags = HideFlags.HideAndDontSave;
            nativeRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            nativeBones = new Transform[metadata.Bones.Count];
            float scale = NormalizeScale(referenceImportScale);
            for (int i = 0; i < metadata.Bones.Count; i++)
            {
                NativePmxMetadata.Bone bone = metadata.Bones[i];
                GameObject boneObject = new GameObject(string.IsNullOrEmpty(bone.NameJapanese) ? "PmxBone" + i : bone.NameJapanese);
                boneObject.hideFlags = HideFlags.HideAndDontSave;
                nativeBones[i] = boneObject.transform;
            }
            for (int i = 0; i < metadata.Bones.Count; i++)
            {
                NativePmxMetadata.Bone bone = metadata.Bones[i];
                Transform parent = bone.ParentIndex >= 0 ? nativeBones[bone.ParentIndex] : nativeRoot.transform;
                Transform boneTransform = nativeBones[i];
                boneTransform.SetParent(parent, false);
                Vector3 local = bone.ParentIndex >= 0
                    ? bone.Position - metadata.Bones[bone.ParentIndex].Position
                    : bone.Position;
                boneTransform.localPosition = MmdToUnityPosition(local) * scale;
                boneTransform.localRotation = Quaternion.identity;
                boneTransform.localScale = Vector3.one;
            }
            // The control root is the MMD “All Parent” bone.  Groove is a
            // semistandard translation layer and many motions rotate it as part
            // of the authored pose.  Treating Groove as the facing/upright root
            // rotates every mapped limb by the inverse of that authored motion;
            // on large clips this can put the whole Humanoid on its side.
            int rootIndex = FindMetadataBone("全ての親", "all parent", "AllParent", "センター", "center", "Center", "グルーブ", "groove", "Groove");
            if (rootIndex >= 0)
            {
                retargetRootBone = nativeBones[rootIndex];
                retargetRootBindWorldRotation = retargetRootBone.rotation;
                retargetRootBindWorldPosition = retargetRootBone.position;
                retargetRootPivot = retargetRootBindWorldPosition;
            }
        }

        private void BuildHumanoidBridge()
        {
            Dictionary<HumanBodyBones, int> sourceMap = BuildSourceBoneMap();
            HumanBodyBones[] required =
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
            };
            for (int i = 0; i < required.Length; i++)
                if (!sourceMap.ContainsKey(required[i])) throw new InvalidOperationException("Reference PMX is missing required Humanoid bone " + required[i] + ".");

            proxyRoot = new GameObject("RuntimeVmd.NativePmx.HumanoidProxy");
            proxyRoot.hideFlags = HideFlags.HideAndDontSave;
            proxyRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Dictionary<HumanBodyBones, Transform> proxyMap = new Dictionary<HumanBodyBones, Transform>();
            float scale = NormalizeScale(referenceImportScale);
            foreach (VmdHumanoidMapEntry entry in VmdHumanoidMap.Entries)
            {
                int sourceIndex;
                if (!sourceMap.TryGetValue(entry.Bone, out sourceIndex) || proxyMap.ContainsKey(entry.Bone)) continue;
                Transform parent = FindProxyParent(entry.Bone, proxyMap) ?? proxyRoot.transform;
                GameObject boneObject = new GameObject(entry.Bone.ToString());
                boneObject.hideFlags = HideFlags.HideAndDontSave;
                boneObject.transform.SetParent(parent, false);
                boneObject.transform.position = MmdToUnityPosition(metadata.Bones[sourceIndex].Position) * scale;
                boneObject.transform.rotation = Quaternion.identity;
                boneObject.transform.localScale = Vector3.one;
                proxyMap.Add(entry.Bone, boneObject.transform);
            }

            ApplyArmTPose(proxyMap, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Vector3.left);
            ApplyArmTPose(proxyMap, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Vector3.right);
            HumanDescription description = BuildHumanDescription(proxyMap);
            sourceAvatar = AvatarBuilder.BuildHumanAvatar(proxyRoot, description);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidOperationException("Unity could not build a valid Humanoid Avatar from the reference PMX.");
            sourceAvatar.hideFlags = HideFlags.HideAndDontSave;
            Animator sourceAnimator = proxyRoot.AddComponent<Animator>();
            sourceAnimator.avatar = sourceAvatar;
            sourceAnimator.enabled = false;
            sourceHumanScale = CalculateAnimatorScale(sourceAnimator);

            if (directBoneRetargeting)
            {
                Dictionary<string, SkeletonBone> restDescription = BuildSkeletonLookup(targetAnimator.avatar);
                foreach (HumanBodyBones role in sourceMap.Keys)
                {
                    Transform targetBone = targetAnimator.GetBoneTransform(role);
                    if (targetBone != null && !restDescription.ContainsKey(targetBone.name))
                        throw new InvalidOperationException("Avatar rest pose is missing " + targetBone.name +
                            "; refusing to calibrate from an animated pose.");
                }
                directRestBones = CloneTargetHierarchy(targetAnimator, out directRestRoot);
                directRestRoot.transform.SetPositionAndRotation(targetAnimator.transform.position, targetAnimator.transform.rotation);
                directRestRoot.transform.localScale = targetAnimator.transform.lossyScale;
                directRestNodes.Clear();
                foreach (KeyValuePair<Transform, Transform> node in directRestBones)
                    if (node.Key != targetAnimator.transform)
                        directRestNodes.Add(new DirectRestNode(node.Key, node.Value));
            }
            poseBindings.Clear();
            foreach (KeyValuePair<HumanBodyBones, int> pair in sourceMap)
            {
                Transform proxy;
                if (!proxyMap.TryGetValue(pair.Key, out proxy)) continue;
                Transform target = targetAnimator.GetBoneTransform(pair.Key);
                if (target == null) continue;
                Transform source = nativeBones[pair.Value];
                Transform targetRest = target;
                if (directRestBones != null) directRestBones.TryGetValue(target, out targetRest);
                if (targetRest == null) throw new InvalidOperationException("Missing rest skeleton bone: " + target.name);
                Vector3 targetBindRootPosition = Quaternion.Inverse(targetAnimator.transform.rotation) *
                    (targetRest.position - targetAnimator.transform.position);
                poseBindings.Add(new PoseBoneBinding(pair.Key, source, proxy,
                    source.rotation, source.position, proxy.rotation, proxy.position,
                    targetBindRootPosition, target, targetRest.rotation, targetRest.position,
                    target.localRotation, target.localPosition,
                    GetTransformDepth(target, targetAnimator.transform)));
            }
            if (poseBindings.Count == 0) throw new InvalidOperationException("Reference PMX and target Avatar have no common Humanoid bones.");

            BuildDirectRetargetBindings();
            if(directBoneRetargeting)
            {
                directInitialWorldToLocal=targetAnimator.transform.worldToLocalMatrix;
                directInitialRootRotation=targetAnimator.transform.rotation;directInitialBasis=directRetargetBasis;
                directInitialPositions.Clear();directInitialRotations.Clear();directInitialAligned.Clear();
                foreach(var binding in poseBindings){directInitialPositions[binding]=binding.TargetBindWorldPosition;directInitialRotations[binding]=binding.TargetBindWorldRotation;directInitialAligned[binding]=binding.AlignedTargetBindWorldRotation;}
            }

            sourcePoseHandler = new HumanPoseHandler(sourceAvatar, proxyRoot.transform);
            targetPoseHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
            EnsureMuscleBuffer(ref sourceRestPose);
            EnsureMuscleBuffer(ref targetRestPose);
            CopyNativePoseToProxy();
            sourcePoseHandler.GetHumanPose(ref sourceRestPose);
            targetPoseHandler.GetHumanPose(ref targetRestPose);
            // Cache the target scale while the Avatar is in its restored bind
            // pose. Recomputing head-to-feet distance during playback makes root
            // translation depend on crouching or raised-leg poses.
            targetHumanScale = directBoneRetargeting ? CalculateDirectRestScale() : CalculateAnimatorScale(targetAnimator);
            EnsureMuscleBuffer(ref sampledPose);
            if (!directBoneRetargeting && targetCalibratedRetargeting) TryBuildTargetCalibrationBridge();
        }

        private void BuildDirectRetargetBindings()
        {
            directLeftLegIkName = FindLegIkName(true);
            directRightLegIkName = FindLegIkName(false);
            hasDirectFacingBaseline = false;
            directFacingBaseline = 0f;
            directHipsBinding = null;
            directSpineBinding = null;
            directHeadBinding = null;
            directBodyCommonBone = null;
            directRetargetBasis = Quaternion.identity;
            PoseBoneBinding sourceLeftArm;
            PoseBoneBinding sourceRightArm;
            PoseBoneBinding sourceHips;
            PoseBoneBinding sourceHead;
            if (TryGetPoseBinding(HumanBodyBones.LeftUpperArm, out sourceLeftArm) &&
                TryGetPoseBinding(HumanBodyBones.RightUpperArm, out sourceRightArm) &&
                TryGetPoseBinding(HumanBodyBones.Hips, out sourceHips) &&
                TryGetPoseBinding(HumanBodyBones.Head, out sourceHead))
            {
                Quaternion sourceBasis;
                Quaternion targetBasis;
                if (TryBuildBodyBasis(
                        sourceLeftArm.SourceBindWorldPosition,
                        sourceRightArm.SourceBindWorldPosition,
                        sourceHips.SourceBindWorldPosition,
                        sourceHead.SourceBindWorldPosition,
                        out sourceBasis) &&
                    TryBuildBodyBasis(
                        sourceLeftArm.TargetBindWorldPosition,
                        sourceRightArm.TargetBindWorldPosition,
                        sourceHips.TargetBindWorldPosition,
                        sourceHead.TargetBindWorldPosition,
                        out targetBasis))
                    directRetargetBasis = BuildYawOnlyBasis(sourceBasis, targetBasis);
                directHipsBinding = sourceHips;
            }

            TryGetPoseBinding(HumanBodyBones.Spine, out directSpineBinding);
            TryGetPoseBinding(HumanBodyBones.Head, out directHeadBinding);
            if (directHipsBinding != null && directSpineBinding != null)
            {
                directBodyCommonBone = FindCommonAncestor(directHipsBinding.Source, directSpineBinding.Source);
            }

            Dictionary<HumanBodyBones, PoseBoneBinding> byBone = new Dictionary<HumanBodyBones, PoseBoneBinding>();
            for (int index = 0; index < poseBindings.Count; index++)
                byBone[poseBindings[index].HumanBone] = poseBindings[index];

            poseBindings.Sort((left, right) => left.TargetDepth.CompareTo(right.TargetDepth));
            for (int index = 0; index < poseBindings.Count; index++)
            {
                PoseBoneBinding binding = poseBindings[index];
                binding.AlignedTargetBindWorldRotation = binding.TargetBindWorldRotation;
                if (directRestBones == null) continue;
                Transform rest = directRestBones[binding.Target];
                // Hips has several branches. Its tiny offset to MMD UpperBody
                // is not an anatomical axis and must never define pelvis roll.
                if (binding.HumanBone == HumanBodyBones.Hips) continue;
                if (IsFootBone(binding.HumanBone))
                {
                    rest.rotation = binding.TargetBindWorldRotation;
                    continue;
                }
                HumanBodyBones childBone = GetRetargetChild(binding.HumanBone);
                PoseBoneBinding child;
                if (binding.HumanBone == HumanBodyBones.LeftHand) childBone = HumanBodyBones.LeftMiddleProximal;
                if (binding.HumanBone == HumanBodyBones.RightHand) childBone = HumanBodyBones.RightMiddleProximal;
                if (binding.HumanBone == HumanBodyBones.Spine || binding.HumanBone == HumanBodyBones.Chest || binding.HumanBone == HumanBodyBones.UpperChest)
                {
                    while (childBone != HumanBodyBones.LastBone && !byBone.ContainsKey(childBone))
                        childBone = GetRetargetChild(childBone);
                }
                if (childBone == HumanBodyBones.LastBone || !byBone.TryGetValue(childBone, out child))
                    continue;
                binding.AimChild = child;
                Vector3 targetDirection = directRestBones[child.Target].position - rest.position;
                Vector3 sourceDirection = directRetargetBasis * (child.SourceBindWorldPosition - binding.SourceBindWorldPosition);
                if (targetDirection.sqrMagnitude <= 0.00000001f || sourceDirection.sqrMagnitude <= 0.00000001f)
                    continue;
                Vector3 sourceSecondary, targetSecondary;
                Quaternion aligned;
                if (TryGetRestSecondary(binding.HumanBone, byBone, out sourceSecondary, out targetSecondary) &&
                    VmdBoneFrameRetargeting.TryAlignRest(targetDirection, targetSecondary,
                        sourceDirection, directRetargetBasis * sourceSecondary, rest.rotation, out aligned))
                    rest.rotation = aligned;
                else
                    rest.rotation = Quaternion.FromToRotation(targetDirection, sourceDirection) * rest.rotation;
            }
            if (directRestBones != null)
            {
                foreach (PoseBoneBinding binding in poseBindings)
                    binding.AlignedTargetBindWorldRotation = directRestBones[binding.Target].rotation;
                foreach (DirectRestNode node in directRestNodes) node.CaptureNeutral();
            }
        }

        private bool TryGetRestSecondary(HumanBodyBones role,
            Dictionary<HumanBodyBones, PoseBoneBinding> bindings,
            out Vector3 sourceSecondary, out Vector3 targetSecondary)
        {
            bool left = role == HumanBodyBones.LeftUpperArm || role == HumanBodyBones.LeftLowerArm ||
                role == HumanBodyBones.LeftHand ||
                (role >= HumanBodyBones.LeftThumbProximal && role <= HumanBodyBones.LeftLittleDistal);
            bool right = role == HumanBodyBones.RightUpperArm || role == HumanBodyBones.RightLowerArm ||
                role == HumanBodyBones.RightHand ||
                (role >= HumanBodyBones.RightThumbProximal && role <= HumanBodyBones.RightLittleDistal);
            if (left || right)
            {
                PoseBoneBinding wrist, index, little;
                if (bindings.TryGetValue(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, out wrist) &&
                    bindings.TryGetValue(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal, out index) &&
                    bindings.TryGetValue(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal, out little))
                {
                    sourceSecondary = Vector3.Cross(index.SourceBindWorldPosition - wrist.SourceBindWorldPosition,
                        little.SourceBindWorldPosition - wrist.SourceBindWorldPosition);
                    targetSecondary = Vector3.Cross(directRestBones[index.Target].position - directRestBones[wrist.Target].position,
                        directRestBones[little.Target].position - directRestBones[wrist.Target].position);
                    if (sourceSecondary.sqrMagnitude > 0.00000001f && targetSecondary.sqrMagnitude > 0.00000001f)
                        return true;
                }
            }
            PoseBoneBinding leftArm, rightArm, hips, head;
            Quaternion sourceBody, targetBody;
            if (bindings.TryGetValue(HumanBodyBones.LeftUpperArm, out leftArm) &&
                bindings.TryGetValue(HumanBodyBones.RightUpperArm, out rightArm) &&
                bindings.TryGetValue(HumanBodyBones.Hips, out hips) &&
                bindings.TryGetValue(HumanBodyBones.Head, out head) &&
                TryBuildBodyBasis(leftArm.SourceBindWorldPosition, rightArm.SourceBindWorldPosition,
                    hips.SourceBindWorldPosition, head.SourceBindWorldPosition, out sourceBody) &&
                TryBuildBodyBasis(directRestBones[leftArm.Target].position, directRestBones[rightArm.Target].position,
                    directRestBones[hips.Target].position, directRestBones[head.Target].position, out targetBody))
            {
                sourceSecondary = sourceBody * Vector3.forward;
                targetSecondary = targetBody * Vector3.forward;
                return true;
            }
            sourceSecondary = targetSecondary = Vector3.zero;
            return false;
        }

        private void ApplyDirectBonePose()
        {
            if (directHipsBinding == null) return;
            LastSupportRootCorrection=Vector3.zero;
            Matrix4x4 placement=targetAnimator.transform.localToWorldMatrix*directInitialWorldToLocal;
            Quaternion rotation=targetAnimator.transform.rotation*Quaternion.Inverse(directInitialRootRotation);
            foreach(var binding in poseBindings)
            {
                binding.TargetBindWorldPosition=placement.MultiplyPoint3x4(directInitialPositions[binding]);
                binding.TargetBindWorldRotation=rotation*directInitialRotations[binding];
                binding.AlignedTargetBindWorldRotation=rotation*directInitialAligned[binding];
            }
            directRetargetBasis=rotation*directInitialBasis;
            targetHumanScale=CalculateDirectRestScale();
            foreach (DirectRestNode node in directRestNodes) node.ApplyNeutral();
            float positionScale = sourceHumanScale > 0.00001f ? targetHumanScale / sourceHumanScale : 1f;
            if (applyRootMotion)
            {
                Vector3 sourceDelta = CorrectSourcePosition(directHipsBinding.Source.position) -
                    directHipsBinding.SourceBindWorldPosition;
                directHipsBinding.Target.position = directHipsBinding.TargetBindWorldPosition +
                    directRetargetBasis * sourceDelta * positionScale;
            }
            else directHipsBinding.Target.position = directHipsBinding.TargetBindWorldPosition;

            for (int index = 0; index < poseBindings.Count; index++)
            {
                PoseBoneBinding binding = poseBindings[index];
                Quaternion sourceDelta = CorrectSourceRotation(binding.Source.rotation) *
                    Quaternion.Inverse(binding.SourceBindWorldRotation);
                Quaternion targetDelta = directRetargetBasis * sourceDelta * Quaternion.Inverse(directRetargetBasis);
                binding.Target.rotation = targetDelta * binding.AlignedTargetBindWorldRotation;
                if (binding.AimChild != null)
                {
                    Vector3 targetSegment = binding.AimChild.Target.position - binding.Target.position;
                    Vector3 sourceSegment = directRetargetBasis *
                        (CorrectSourcePosition(binding.AimChild.Source.position) - CorrectSourcePosition(binding.Source.position));
                    if (targetSegment.sqrMagnitude > 0.00000001f && sourceSegment.sqrMagnitude > 0.00000001f)
                        binding.Target.rotation = Quaternion.FromToRotation(targetSegment, sourceSegment) * binding.Target.rotation;
                }
            }

            // Preserve MMD's UpperBody pivot without changing any Spine local
            // offset. MMD LowerBody and UpperBody are siblings; a Humanoid
            // Spine inherits its Hips attachment. Compensate the pelvis position
            // for that attachment rotation, as part of root retargeting.
            if (directSpineBinding != null)
            {
                Vector3 anchorDelta = applyRootMotion
                    ? directRetargetBasis * (CorrectSourcePosition(directSpineBinding.Source.position) -
                        directSpineBinding.SourceBindWorldPosition) * positionScale
                    : Vector3.zero;
                Vector3 upperBodyAnchor = directSpineBinding.TargetBindWorldPosition + anchorDelta;
                Vector3 attachmentCorrection = upperBodyAnchor - directSpineBinding.Target.position;
                // UpperBody's sibling pivot requires a horizontal attachment
                // correction. Vertical travel belongs to Center/Groove/Hips;
                // deriving it from the rotated Spine offset adds an artificial
                // bob whenever LowerBody bends.
                attachmentCorrection.y = 0f;
                directHipsBinding.Target.position += attachmentCorrection;
            }
            if (applyIk && applyRootMotion) ConstrainDirectSupportRoot();
            LastTargetLegIkSolvedCount = applyIk ? ApplyTargetLegRetargeting() : 0;
            LastTargetFootOrientationAppliedCount = CountMappedFeet();
            LastAppliedBodyYawFromRest = SignedPlanarYaw(
                directHipsBinding.TargetBindWorldRotation,
                directHipsBinding.Target.rotation);
            LastAppliedBodyTiltDegrees = keepBodyUpright
                ? 0f
                : AngleBetween(
                    directHipsBinding.TargetBindWorldRotation * Vector3.up,
                    directHipsBinding.Target.rotation * Vector3.up);
            LastBodyYawRetargetError = Mathf.Abs(Mathf.DeltaAngle(
                LastSourceBodyYawFromRest,
                LastAppliedBodyYawFromRest));
            LastAppliedBodyDeltaNormalized = positionScale > 0.00001f
                ? Quaternion.Inverse(directRetargetBasis) *
                    (directHipsBinding.Target.position - directHipsBinding.TargetBindWorldPosition) / positionScale
                : Vector3.zero;
            LastBodyPositionRetargetError =
                (LastAppliedBodyDeltaNormalized -
                 (CorrectSourcePosition(directHipsBinding.Source.position) - directHipsBinding.SourceBindWorldPosition)).magnitude;
        }

        private static Transform FindCommonAncestor(Transform left, Transform right)
        {
            if (left == null || right == null) return null;
            HashSet<Transform> ancestors = new HashSet<Transform>();
            for (Transform current = left; current != null; current = current.parent)
                ancestors.Add(current);
            for (Transform current = right; current != null; current = current.parent)
                if (ancestors.Contains(current)) return current;
            return null;
        }

        private int CountMappedFeet()
        {
            int count = 0;
            PoseBoneBinding ignored;
            if (TryGetPoseBinding(HumanBodyBones.LeftFoot, out ignored)) count++;
            if (TryGetPoseBinding(HumanBodyBones.RightFoot, out ignored)) count++;
            return count;
        }

        private static bool TryBuildBodyBasis(Vector3 left, Vector3 right, Vector3 hips, Vector3 head, out Quaternion basis)
        {
            Vector3 rightAxis = right - left;
            Vector3 upAxis = head - hips;
            if (rightAxis.sqrMagnitude <= 0.00000001f || upAxis.sqrMagnitude <= 0.00000001f)
            {
                basis = Quaternion.identity;
                return false;
            }
            rightAxis = rightAxis.normalized;
            upAxis -= rightAxis * Vector3.Dot(upAxis, rightAxis);
            if (upAxis.sqrMagnitude <= 0.00000001f)
            {
                basis = Quaternion.identity;
                return false;
            }
            upAxis = upAxis.normalized;
            Vector3 forwardAxis = Vector3.Cross(rightAxis, upAxis);
            if (forwardAxis.sqrMagnitude <= 0.00000001f)
            {
                basis = Quaternion.identity;
                return false;
            }
            basis = Quaternion.LookRotation(forwardAxis.normalized, upAxis);
            return true;
        }

        private static Quaternion BuildYawOnlyBasis(Quaternion sourceBasis, Quaternion targetBasis)
        {
            Vector3 sourceForward = sourceBasis * Vector3.forward;
            Vector3 targetForward = targetBasis * Vector3.forward;
            sourceForward -= Vector3.up * Vector3.Dot(sourceForward, Vector3.up);
            targetForward -= Vector3.up * Vector3.Dot(targetForward, Vector3.up);
            if (sourceForward.sqrMagnitude <= 0.00000001f || targetForward.sqrMagnitude <= 0.00000001f)
                return Quaternion.identity;
            sourceForward = sourceForward.normalized;
            targetForward = targetForward.normalized;
            float yaw = Mathf.Atan2(
                Vector3.Dot(Vector3.up, Vector3.Cross(sourceForward, targetForward)),
                Vector3.Dot(sourceForward, targetForward)) * 57.2957795f;
            return Quaternion.AngleAxis(yaw, Vector3.up);
        }

        private static bool IsFootBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftFoot || bone == HumanBodyBones.RightFoot ||
                bone == HumanBodyBones.LeftToes || bone == HumanBodyBones.RightToes;
        }

        private static HumanBodyBones GetRetargetChild(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Hips: return HumanBodyBones.Spine;
                case HumanBodyBones.Spine: return HumanBodyBones.Chest;
                case HumanBodyBones.Chest: return HumanBodyBones.UpperChest;
                case HumanBodyBones.UpperChest: return HumanBodyBones.Neck;
                case HumanBodyBones.Neck: return HumanBodyBones.Head;
                case HumanBodyBones.LeftShoulder: return HumanBodyBones.LeftUpperArm;
                case HumanBodyBones.RightShoulder: return HumanBodyBones.RightUpperArm;
                case HumanBodyBones.LeftUpperArm: return HumanBodyBones.LeftLowerArm;
                case HumanBodyBones.RightUpperArm: return HumanBodyBones.RightLowerArm;
                case HumanBodyBones.LeftLowerArm: return HumanBodyBones.LeftHand;
                case HumanBodyBones.RightLowerArm: return HumanBodyBones.RightHand;
                case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.LeftLowerLeg;
                case HumanBodyBones.RightUpperLeg: return HumanBodyBones.RightLowerLeg;
                case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.LeftFoot;
                case HumanBodyBones.RightLowerLeg: return HumanBodyBones.RightFoot;
                case HumanBodyBones.LeftThumbProximal: return HumanBodyBones.LeftThumbIntermediate;
                case HumanBodyBones.LeftThumbIntermediate: return HumanBodyBones.LeftThumbDistal;
                case HumanBodyBones.RightThumbProximal: return HumanBodyBones.RightThumbIntermediate;
                case HumanBodyBones.RightThumbIntermediate: return HumanBodyBones.RightThumbDistal;
                case HumanBodyBones.LeftIndexProximal: return HumanBodyBones.LeftIndexIntermediate;
                case HumanBodyBones.LeftIndexIntermediate: return HumanBodyBones.LeftIndexDistal;
                case HumanBodyBones.RightIndexProximal: return HumanBodyBones.RightIndexIntermediate;
                case HumanBodyBones.RightIndexIntermediate: return HumanBodyBones.RightIndexDistal;
                case HumanBodyBones.LeftMiddleProximal: return HumanBodyBones.LeftMiddleIntermediate;
                case HumanBodyBones.LeftMiddleIntermediate: return HumanBodyBones.LeftMiddleDistal;
                case HumanBodyBones.RightMiddleProximal: return HumanBodyBones.RightMiddleIntermediate;
                case HumanBodyBones.RightMiddleIntermediate: return HumanBodyBones.RightMiddleDistal;
                case HumanBodyBones.LeftRingProximal: return HumanBodyBones.LeftRingIntermediate;
                case HumanBodyBones.LeftRingIntermediate: return HumanBodyBones.LeftRingDistal;
                case HumanBodyBones.RightRingProximal: return HumanBodyBones.RightRingIntermediate;
                case HumanBodyBones.RightRingIntermediate: return HumanBodyBones.RightRingDistal;
                case HumanBodyBones.LeftLittleProximal: return HumanBodyBones.LeftLittleIntermediate;
                case HumanBodyBones.LeftLittleIntermediate: return HumanBodyBones.LeftLittleDistal;
                case HumanBodyBones.RightLittleProximal: return HumanBodyBones.RightLittleIntermediate;
                case HumanBodyBones.RightLittleIntermediate: return HumanBodyBones.RightLittleDistal;
                default: return HumanBodyBones.LastBone;
            }
        }

        private static int GetTransformDepth(Transform value, Transform root)
        {
            int depth = 0;
            for (Transform current = value; current != null && current != root; current = current.parent) depth++;
            return depth;
        }

        private void TryBuildTargetCalibrationBridge()
        {
            try
            {
                Dictionary<Transform, Transform> cloneMap = CloneTargetHierarchy(targetAnimator, out targetCalibrationRoot);
                targetCalibrationBindings.Clear();
                for (int index = 0; index < poseBindings.Count; index++)
                {
                    PoseBoneBinding sourceBinding = poseBindings[index];
                    Transform target = targetAnimator.GetBoneTransform(sourceBinding.HumanBone);
                    Transform clone;
                    if (target == null || !cloneMap.TryGetValue(target, out clone)) continue;
                    targetCalibrationBindings.Add(new TargetCalibrationBinding(
                        sourceBinding.HumanBone,
                        sourceBinding.Source,
                        sourceBinding.SourceBindWorldRotation,
                        sourceBinding.SourceBindWorldPosition,
                        clone,
                        clone.rotation,
                        clone.position));
                }
                if (targetCalibrationBindings.Count == 0)
                    throw new InvalidOperationException("The target calibration clone has no mapped Humanoid bones.");

                targetCalibrationPoseHandler = new HumanPoseHandler(targetAnimator.avatar, targetCalibrationRoot.transform);
                EnsureMuscleBuffer(ref targetCalibrationRestPose);
                EnsureMuscleBuffer(ref targetCalibratedPose);
                targetCalibrationPoseHandler.GetHumanPose(ref targetCalibrationRestPose);
            }
            catch (Exception exception)
            {
                if (targetCalibrationPoseHandler != null) targetCalibrationPoseHandler.Dispose();
                targetCalibrationPoseHandler = null;
                DestroyUnityObject(targetCalibrationRoot);
                targetCalibrationRoot = null;
                targetCalibrationBindings.Clear();
                Debug.LogWarning("[RuntimeVmd.Native] Target-calibrated retargeting is unavailable; using the canonical Humanoid bridge. " + exception.Message, this);
            }
        }

        private void ApplyNativePoseToTargetCalibration()
        {
            if (!IsUsingTargetCalibration) return;
            float positionScale = CalculateTargetCalibrationPositionScale();
            for (int index = 0; index < targetCalibrationBindings.Count; index++)
            {
                TargetCalibrationBinding binding = targetCalibrationBindings[index];
                Quaternion sourceRotation = CorrectSourceRotation(binding.Source.rotation);
                Quaternion delta = sourceRotation * Quaternion.Inverse(binding.SourceBindWorldRotation);
                binding.Target.rotation = delta * binding.TargetBindWorldRotation;
                if (binding.HumanBone == HumanBodyBones.Hips)
                    binding.Target.position = binding.TargetBindWorldPosition
                        + (CorrectSourcePosition(binding.Source.position) - binding.SourceBindWorldPosition) * positionScale;
            }
        }

        private void UpdateRetargetRootCorrection()
        {
            directFacingPositionOffset = Vector3.zero;
            retargetRootCorrection = Quaternion.identity;
            LastSourceRootTiltDegrees = 0f;
            if (retargetRootBone == null) return;
            retargetRootPivot = retargetRootBone.position;
            LastSourceRootTiltDegrees = AngleBetween(
                retargetRootBindWorldRotation * Vector3.up,
                retargetRootBone.rotation * Vector3.up);
            float rawYaw = SignedPlanarYaw(retargetRootBindWorldRotation, retargetRootBone.rotation);
            if (!hasInitialRetargetRootYaw)
            {
                initialRetargetRootYaw = rawYaw;
                hasInitialRetargetRootYaw = true;
            }
            if (!keepBodyUpright && !lockBodyYaw) return;

            // VMDs are commonly authored against models whose initial facing
            // differs from the host avatar. Treat the first evaluated pose as
            // the facing baseline, then preserve every authored turn relative
            // to it. This removes a constant 90/120/180-degree package offset
            // without freezing the dancer's yaw for the rest of the motion.
            float yaw = lockBodyYaw ? 0f : Mathf.DeltaAngle(initialRetargetRootYaw, rawYaw);
            Quaternion desired = Quaternion.AngleAxis(yaw, Vector3.up) * retargetRootBindWorldRotation;
            retargetRootCorrection = desired * Quaternion.Inverse(retargetRootBone.rotation);
        }

        private Quaternion CorrectSourceRotation(Quaternion rotation)
        {
            return retargetRootCorrection * rotation;
        }

        private Vector3 CorrectSourcePosition(Vector3 position)
        {
            return retargetRootPivot + retargetRootCorrection * (position - retargetRootPivot) + directFacingPositionOffset;
        }

        // Native PMX IK solves against the reference MMD proportions. Unity's
        // Humanoid muscle transfer does not preserve the solved ankle targets on
        // a VRM with different thigh/shin lengths. Reconstruct the source leg's
        // reach and bend plane, then solve the same pose on the actual target
        // chain. This follows the proportion-aware approach used by established
        // MMD retargeters while remaining independent of their implementations.
        private int ApplyTargetLegRetargeting()
        {
            int solved = 0;
            if (TryRetargetLeg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)) solved++;
            if (TryRetargetLeg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)) solved++;
            return solved;
        }

        private bool TryRetargetLeg(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones footBone)
        {
            PoseBoneBinding sourceUpper;
            PoseBoneBinding sourceLower;
            PoseBoneBinding sourceFoot;
            if (!TryGetPoseBinding(upperBone, out sourceUpper) ||
                !TryGetPoseBinding(lowerBone, out sourceLower) ||
                !TryGetPoseBinding(footBone, out sourceFoot)) return false;

            Transform targetUpper = targetAnimator.GetBoneTransform(upperBone);
            Transform targetLower = targetAnimator.GetBoneTransform(lowerBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (targetUpper == null || targetLower == null || targetFoot == null) return false;

            Vector3 sourceA = CorrectSourcePosition(sourceUpper.Source.position);
            Vector3 sourceB = CorrectSourcePosition(sourceLower.Source.position);
            Vector3 sourceC = CorrectSourcePosition(sourceFoot.Source.position);
            float sourceUpperLength = (sourceB - sourceA).magnitude;
            float sourceLowerLength = (sourceC - sourceB).magnitude;
            float sourceReach = (sourceC - sourceA).magnitude;
            float sourceMaximum = sourceUpperLength + sourceLowerLength;
            if (sourceUpperLength <= 0.0001f || sourceLowerLength <= 0.0001f || sourceReach <= 0.0001f || sourceMaximum <= 0.0001f)
                return false;

            float targetUpperLength = (targetLower.position - targetUpper.position).magnitude;
            float targetLowerLength = (targetFoot.position - targetLower.position).magnitude;
            if (targetUpperLength <= 0.0001f || targetLowerLength <= 0.0001f) return false;

            Quaternion sourceToTarget = IsUsingDirectBoneRetargeting
                ? directRetargetBasis
                : targetAnimator.transform.rotation * Quaternion.Inverse(nativeRoot.transform.rotation);
            Vector3 direction = sourceToTarget * ((sourceC - sourceA) / sourceReach);
            // Preserve the authored knee angle even when target thigh/shin
            // proportions differ. A normalized reach ratio changes that angle.
            float bendCosine = Mathf.Clamp(Vector3.Dot((sourceB - sourceA).normalized,
                (sourceC - sourceB).normalized), -1f, 1f);
            float targetReach = Mathf.Sqrt(Mathf.Max(0f,
                targetUpperLength * targetUpperLength + targetLowerLength * targetLowerLength +
                2f * targetUpperLength * targetLowerLength * bendCosine));
            float minimumReach = Mathf.Abs(targetUpperLength - targetLowerLength) + 0.0001f;
            float maximumReach = targetUpperLength + targetLowerLength - 0.0001f;
            targetReach = Mathf.Clamp(targetReach, minimumReach, maximumReach);
            Vector3 goal = targetUpper.position + direction.normalized * targetReach;

            // A positional IK goal lives in the reference's world motion space,
            // independently of the moving pelvis. Reconstructing it from the
            // target hip and source knee angle makes a planted foot slide with
            // every Center/LowerBody movement. Keep the angle-based transfer for
            // authored FK frames; world-foot constraints determine the knee
            // angle when positional IK is active.
            if (IsUsingDirectBoneRetargeting && applyRootMotion && IsSourceLegIkEnabled(footBone))
            {
                float motionScale = sourceHumanScale > 0.00001f ? targetHumanScale / sourceHumanScale : 1f;
                Vector3 worldGoal = sourceFoot.TargetBindWorldPosition + directRetargetBasis *
                    (sourceC - sourceFoot.SourceBindWorldPosition) * motionScale;
                // During flight, preserve the authored joint pose instead of
                // stretching a differently proportioned leg to a world anchor.
                float contact=SourceFootSupportWeight(footBone);
                goal=Vector3.Lerp(goal,worldGoal,contact);
            }

            Vector3 sourceLine = (sourceC - sourceA) / sourceReach;
            Vector3 sourcePole = sourceB - sourceA - sourceLine * Vector3.Dot(sourceB - sourceA, sourceLine);
            Vector3 pole = sourceToTarget * sourcePole;
            if(IsUsingDirectBoneRetargeting)
            {
                Vector3 restForward=Quaternion.Inverse(directInitialBasis)*(directInitialRootRotation*Vector3.forward);
                Quaternion thighDelta=CorrectSourceRotation(sourceUpper.Source.rotation)*Quaternion.Inverse(sourceUpper.SourceBindWorldRotation);
                Vector3 anatomical=sourceToTarget*(thighDelta*restForward);
                Vector3 axis=(goal-targetUpper.position).normalized;
                anatomical-=axis*Vector3.Dot(anatomical,axis);
                pole-=axis*Vector3.Dot(pole,axis);
                float confidence=Mathf.Clamp01((sourcePole.magnitude/sourceUpperLength-0.01f)/0.04f);
                confidence=confidence*confidence*(3f-2f*confidence);
                if(anatomical.sqrMagnitude>0.000001f)
                {
                    anatomical=anatomical.normalized;
                    if(pole.sqrMagnitude>0.00000001f)
                        pole=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(anatomical,pole.normalized),confidence)*anatomical;
                    else pole=anatomical;
                }
            }
            if (pole.sqrMagnitude <= 0.000001f)
            {
                Vector3 targetLine = (goal - targetUpper.position).normalized;
                pole = targetLower.position - targetUpper.position;
                pole -= targetLine * Vector3.Dot(pole, targetLine);
            }
            return SolveTwoBone(targetUpper, targetLower, targetFoot, goal, pole);
        }

        private string FindLegIkName(bool left)
        {
            string[] names = left
                ? new[] { "左足ＩＫ", "左足IK", "左足ＩＫ親", "left leg ik", "LeftLegIK" }
                : new[] { "右足ＩＫ", "右足IK", "右足ＩＫ親", "right leg ik", "RightLegIK" };
            int index = FindMetadataBone(names);
            return index >= 0 && (metadata.Bones[index].Flags & 0x0020) != 0
                ? metadata.Bones[index].NameJapanese : null;
        }

        private bool IsSourceLegIkEnabled(HumanBodyBones footBone)
        {
            string name = footBone == HumanBodyBones.LeftFoot ? directLeftLegIkName : directRightLegIkName;
            if (name == null) return false;
            bool enabled;
            return !auxiliarySampler.TrySampleIkEnabled(name,
                playbackEvaluationFrame, out enabled) || enabled;
        }

        private bool TryGetSupportConstraint(HumanBodyBones upperRole, HumanBodyBones lowerRole,
            HumanBodyBones footRole, out Vector3 center, out float radius)
        {
            center = Vector3.zero; radius = 0f;
            PoseBoneBinding upper, lower, foot;
            if (!IsSourceLegIkEnabled(footRole) || !TryGetPoseBinding(upperRole, out upper) ||
                !TryGetPoseBinding(lowerRole, out lower) || !TryGetPoseBinding(footRole, out foot)) return false;
            // A lifted/swinging foot must not pull the entire body toward its
            // IK goal. Only the source's ground-support band constrains Hips.
            float supportWeight = SourceFootSupportWeight(footRole);
            if (supportWeight <= 0f) return false;
            float scale = sourceHumanScale > 0.00001f ? targetHumanScale / sourceHumanScale : 1f;
            Vector3 goal = foot.TargetBindWorldPosition + directRetargetBasis *
                (CorrectSourcePosition(foot.Source.position) - foot.SourceBindWorldPosition) * scale;
            center = goal - (upper.Target.position - directHipsBinding.Target.position);
            radius = Vector3.Distance(upper.Target.position, lower.Target.position) +
                Vector3.Distance(lower.Target.position, foot.Target.position);
            // Leave a small geometric extension reserve for a supported foot.
            // Exact full reach makes the knee's bend plane singular.
            radius*=0.995f;
            // Enlarge the reach ball smoothly as the foot leaves the floor.
            // A hard height cutoff would make the pelvis jump at liftoff.
            float excess = Mathf.Max(0f, Vector3.Distance(directHipsBinding.Target.position, center) - radius);
            radius += excess * (1f - supportWeight);
            return radius > 0.0001f;
        }

        private bool IsSourceFootSupporting(HumanBodyBones role)
        {
            PoseBoneBinding foot;
            if (!TryGetPoseBinding(role, out foot)) return false;
            float height = CorrectSourcePosition(foot.Source.position).y - foot.SourceBindWorldPosition.y;
            return height <= sourceHumanScale * 0.015f;
        }

        private float SourceFootSupportWeight(HumanBodyBones role)
        {
            PoseBoneBinding foot;
            if (!TryGetPoseBinding(role, out foot)) return 0f;
            float height = CorrectSourcePosition(foot.Source.position).y - foot.SourceBindWorldPosition.y;
            float t = Mathf.Clamp((height / Mathf.Max(sourceHumanScale, 0.00001f) - 0.015f) / 0.045f, 0f, 1f);
            return 1f - t * t * (3f - 2f * t);
        }

        private void ConstrainDirectSupportRoot()
        {
            LastSupportRootCorrection=Vector3.zero;
            Vector3 left, right; float leftRadius, rightRadius;
            bool hasLeft = TryGetSupportConstraint(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, out left, out leftRadius);
            bool hasRight = TryGetSupportConstraint(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot, out right, out rightRadius);
            Vector3 root = directHipsBinding.Target.position;
            Vector3 verticalRoot;
            // Foot IK adjusts body height before altering authored horizontal
            // travel. Prefer a feasible vertical solution; retain the full
            // projection for poses whose leg reach requires lateral movement.
            if((hasLeft||hasRight) && VmdSupportRetargeting.TryKeepHorizontalRoot(root,
                hasLeft?left:right,hasLeft?leftRadius:rightRadius,right,rightRadius,hasLeft&&hasRight,out verticalRoot))
                directHipsBinding.Target.position=verticalRoot;
            else if (hasLeft && hasRight)
                directHipsBinding.Target.position = VmdSupportRetargeting.ClosestReachableRoot(root, left, leftRadius, right, rightRadius);
            else if (hasLeft || hasRight)
            {
                Vector3 center = hasLeft ? left : right; float radius = hasLeft ? leftRadius : rightRadius;
                Vector3 delta = root - center;
                if (delta.sqrMagnitude > radius * radius) directHipsBinding.Target.position = center + delta.normalized * radius;
            }
            LastSupportRootCorrection=directHipsBinding.Target.position-root;
        }
        public Vector3 LastSupportRootCorrection { get; private set; }

        private static bool SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 goal, Vector3 pole)
        {
            Vector3 origin = upper.position;
            Vector3 toGoal = goal - origin;
            float distance = toGoal.magnitude;
            float upperLength = (lower.position - origin).magnitude;
            float lowerLength = (end.position - lower.position).magnitude;
            if (distance <= 0.0001f || upperLength <= 0.0001f || lowerLength <= 0.0001f) return false;
            Vector3 axis = toGoal / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.0001f, upperLength + lowerLength - 0.0001f);
            Vector3 reachableGoal=origin+axis*distance;
            Vector3 poleAxis = pole - axis * Vector3.Dot(pole, axis);
            if (poleAxis.sqrMagnitude <= 0.000001f)
            {
                poleAxis = Vector3.Cross(axis, Vector3.up);
                if (poleAxis.sqrMagnitude <= 0.000001f) poleAxis = Vector3.Cross(axis, Vector3.right);
            }
            poleAxis /= (float)System.Math.Sqrt(poleAxis.sqrMagnitude);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float perpendicular = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 desiredKnee = origin + axis * along + poleAxis * perpendicular;
            Quaternion savedFootRotation = end.rotation;
            Vector3 currentUpper = lower.position - origin;
            Vector3 desiredUpper = desiredKnee - origin;
            if (currentUpper.sqrMagnitude > 0.000001f && desiredUpper.sqrMagnitude > 0.000001f)
                upper.rotation = Quaternion.FromToRotation(currentUpper, desiredUpper) * upper.rotation;
            Vector3 currentLower = end.position - lower.position;
            Vector3 desiredLower = reachableGoal - lower.position;
            if (currentLower.sqrMagnitude > 0.000001f && desiredLower.sqrMagnitude > 0.000001f)
                lower.rotation = Quaternion.FromToRotation(currentLower, desiredLower) * lower.rotation;
            end.rotation = savedFootRotation;
            return true;
        }

        private float CalculateTargetCalibrationPositionScale()
        {
            for (int index = 0; index < targetCalibrationBindings.Count; index++)
            {
                TargetCalibrationBinding binding = targetCalibrationBindings[index];
                if (binding.HumanBone != HumanBodyBones.Hips) continue;
                float sourceHeight = Mathf.Abs(binding.SourceBindWorldPosition.y - nativeRoot.transform.position.y);
                float targetHeight = Mathf.Abs(binding.TargetBindWorldPosition.y - targetCalibrationRoot.transform.position.y);
                if (sourceHeight > 0.00001f && targetHeight > 0.00001f) return targetHeight / sourceHeight;
                break;
            }
            return 1f;
        }

        private static Dictionary<Transform, Transform> CloneTargetHierarchy(Animator animator, out GameObject cloneRoot)
        {
            Transform sourceRoot = animator.transform;
            cloneRoot = new GameObject("RuntimeVmd.NativePmx.TargetCalibration");
            cloneRoot.hideFlags = HideFlags.HideAndDontSave;
            cloneRoot.transform.localPosition = Vector3.zero;
            cloneRoot.transform.localRotation = Quaternion.identity;
            cloneRoot.transform.localScale = Vector3.one;

            Dictionary<string, SkeletonBone> skeleton = BuildSkeletonLookup(animator.avatar);
            HashSet<Transform> required = new HashSet<Transform>();
            required.Add(sourceRoot);
            for (int index = 0; index < (int)HumanBodyBones.LastBone; index++)
                AddRequiredWithAncestors(animator.GetBoneTransform((HumanBodyBones)index), sourceRoot, required);
            Dictionary<Transform, Transform> result = new Dictionary<Transform, Transform>();
            result.Add(sourceRoot, cloneRoot.transform);
            CloneTargetChildren(sourceRoot, cloneRoot.transform, skeleton, required, result,VmdRigScaleSnapshot.Capture(animator));
            return result;
        }

        private static Dictionary<string, SkeletonBone> BuildSkeletonLookup(Avatar avatar)
        {
            Dictionary<string, SkeletonBone> result = new Dictionary<string, SkeletonBone>(StringComparer.Ordinal);
            HumanDescription description = avatar.humanDescription;
            SkeletonBone[] skeleton = description.skeleton;
            if (skeleton == null) return result;
            for (int index = 0; index < skeleton.Length; index++)
                if (!string.IsNullOrEmpty(skeleton[index].name) && !result.ContainsKey(skeleton[index].name))
                    result.Add(skeleton[index].name, skeleton[index]);
            return result;
        }

        // MateEngine ships an older Mono mscorlib whose HashSet implementation
        // predates the ISet<T>.Add interface slot expected by net472. Keep the
        // concrete type at this runtime boundary so target calibration remains
        // available instead of falling back after a MissingMethodException.
        private static void AddRequiredWithAncestors(Transform transform, Transform root, HashSet<Transform> required)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                required.Add(current);
                if (current == root) break;
            }
        }

        private static void CloneTargetChildren(Transform source, Transform cloneParent,
            IDictionary<string, SkeletonBone> skeleton, HashSet<Transform> required,
            IDictionary<Transform, Transform> result,VmdRigScaleSnapshot scales)
        {
            for (int index = 0; index < source.childCount; index++)
            {
                Transform child = source.GetChild(index);
                if (!required.Contains(child)) continue;
                GameObject cloneObject = new GameObject(child.name);
                cloneObject.hideFlags = HideFlags.HideAndDontSave;
                Transform clone = cloneObject.transform;
                clone.SetParent(cloneParent, false);
                SkeletonBone rest;
                if (skeleton.TryGetValue(child.name, out rest))
                {
                    clone.localPosition = rest.position;
                    clone.localRotation = rest.rotation;
                    clone.localScale = scales.ScaleFor(child,rest.scale);
                }
                else
                {
                    clone.localPosition = child.localPosition;
                    clone.localRotation = child.localRotation;
                    clone.localScale = child.localScale;
                }
                result.Add(child, clone);
                CloneTargetChildren(child, clone, skeleton, required, result,scales);
            }
        }

        private static void CopyCanonicalUpperLimbMuscles(HumanPose canonical, HumanPose calibrated)
        {
            if (canonical.muscles == null || calibrated.muscles == null) return;
            int count = Math.Min(Math.Min(canonical.muscles.Length, calibrated.muscles.Length), HumanTrait.MuscleCount);
            for (int index = 0; index < count; index++)
            {
                string name = HumanTrait.MuscleName[index];
                if (IsUpperLimbMuscle(name, "Left ") || IsUpperLimbMuscle(name, "Right "))
                    calibrated.muscles[index] = canonical.muscles[index];
            }
        }

        private static bool IsUpperLimbMuscle(string name, string side)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith(side, StringComparison.Ordinal)) return false;
            string part = name.Substring(side.Length);
            return part.StartsWith("Shoulder ", StringComparison.Ordinal)
                || part.StartsWith("Arm ", StringComparison.Ordinal)
                || part.StartsWith("Forearm ", StringComparison.Ordinal)
                || part.StartsWith("Hand ", StringComparison.Ordinal)
                || part.StartsWith("Thumb ", StringComparison.Ordinal)
                || part.StartsWith("Index ", StringComparison.Ordinal)
                || part.StartsWith("Middle ", StringComparison.Ordinal)
                || part.StartsWith("Ring ", StringComparison.Ordinal)
                || part.StartsWith("Little ", StringComparison.Ordinal);
        }

        private Dictionary<HumanBodyBones, int> BuildSourceBoneMap()
        {
            Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < metadata.Bones.Count; i++)
            {
                AddBoneName(names, metadata.Bones[i].NameJapanese, i);
                AddBoneName(names, metadata.Bones[i].NameEnglish, i);
            }
            bool leftZeroThumb = ContainsBoneName(names, "左親指0") || ContainsBoneName(names, "左親指０")
                || ContainsBoneName(names, "left thumb0") || ContainsBoneName(names, "LeftThumb0");
            bool rightZeroThumb = ContainsBoneName(names, "右親指0") || ContainsBoneName(names, "右親指０")
                || ContainsBoneName(names, "right thumb0") || ContainsBoneName(names, "RightThumb0");
            Dictionary<HumanBodyBones, int> result = new Dictionary<HumanBodyBones, int>();
            HashSet<int> used = new HashSet<int>();
            foreach (VmdHumanoidMapEntry entry in VmdHumanoidMap.Entries)
            {
                string[] candidates = GetMappingCandidates(entry, leftZeroThumb, rightZeroThumb);
                int index = FindBone(names, candidates);
                if (index < 0 || used.Contains(index) || result.ContainsKey(entry.Bone)) continue;
                result.Add(entry.Bone, index);
                used.Add(index);
            }
            return result;
        }

        private static string[] GetMappingCandidates(VmdHumanoidMapEntry entry, bool leftZeroThumb, bool rightZeroThumb)
        {
            if (!leftZeroThumb)
            {
                if (entry.Bone == HumanBodyBones.LeftThumbProximal) return new[] { "左親指1", "左親指１", "left thumb1", "LeftThumb1" };
                if (entry.Bone == HumanBodyBones.LeftThumbIntermediate) return new[] { "左親指2", "左親指２", "left thumb2", "LeftThumb2" };
                if (entry.Bone == HumanBodyBones.LeftThumbDistal) return new[] { "左親指先", "左親指3", "左親指３", "left thumb tip", "LeftThumbTip" };
            }
            if (!rightZeroThumb)
            {
                if (entry.Bone == HumanBodyBones.RightThumbProximal) return new[] { "右親指1", "右親指１", "right thumb1", "RightThumb1" };
                if (entry.Bone == HumanBodyBones.RightThumbIntermediate) return new[] { "右親指2", "右親指２", "right thumb2", "RightThumb2" };
                if (entry.Bone == HumanBodyBones.RightThumbDistal) return new[] { "右親指先", "右親指3", "右親指３", "right thumb tip", "RightThumbTip" };
            }
            return entry.VmdNames;
        }

        private static void AddBoneName(Dictionary<string, int> names, string value, int index)
        {
            string normalized = NormalizeBoneName(value);
            if (normalized.Length > 0 && !names.ContainsKey(normalized)) names.Add(normalized, index);
        }

        private static bool ContainsBoneName(Dictionary<string, int> names, string value)
        {
            return names.ContainsKey(NormalizeBoneName(value));
        }

        private static int FindBone(Dictionary<string, int> names, string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                int index;
                if (names.TryGetValue(NormalizeBoneName(candidates[i]), out index)) return index;
            }
            return -1;
        }

        private int FindMetadataBone(params string[] candidates)
        {
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                string candidate = NormalizeBoneName(candidates[candidateIndex]);
                for (int boneIndex = 0; boneIndex < metadata.Bones.Count; boneIndex++)
                {
                    NativePmxMetadata.Bone bone = metadata.Bones[boneIndex];
                    if (NormalizeBoneName(bone.NameJapanese) == candidate || NormalizeBoneName(bone.NameEnglish) == candidate)
                        return boneIndex;
                }
            }
            return -1;
        }

        private static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '！' && chars[i] <= '～') chars[i] = (char)(chars[i] - 0xFEE0);
            return new string(chars).Replace(" ", string.Empty).Replace("　", string.Empty);
        }

        private static Transform FindProxyParent(HumanBodyBones bone, Dictionary<HumanBodyBones, Transform> map)
        {
            HumanBodyBones[] candidates = GetParentCandidates(bone);
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform parent;
                if (map.TryGetValue(candidates[i], out parent)) return parent;
            }
            return null;
        }

        private static HumanBodyBones[] GetParentCandidates(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Spine: return new[] { HumanBodyBones.Hips };
                case HumanBodyBones.Chest: return new[] { HumanBodyBones.Spine };
                case HumanBodyBones.UpperChest: return new[] { HumanBodyBones.Chest, HumanBodyBones.Spine };
                case HumanBodyBones.Neck: return new[] { HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine };
                case HumanBodyBones.Head: return new[] { HumanBodyBones.Neck };
                case HumanBodyBones.LeftEye:
                case HumanBodyBones.RightEye:
                case HumanBodyBones.Jaw: return new[] { HumanBodyBones.Head };
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder: return new[] { HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine };
                case HumanBodyBones.LeftUpperArm: return new[] { HumanBodyBones.LeftShoulder, HumanBodyBones.UpperChest, HumanBodyBones.Chest };
                case HumanBodyBones.RightUpperArm: return new[] { HumanBodyBones.RightShoulder, HumanBodyBones.UpperChest, HumanBodyBones.Chest };
                case HumanBodyBones.LeftLowerArm: return new[] { HumanBodyBones.LeftUpperArm };
                case HumanBodyBones.RightLowerArm: return new[] { HumanBodyBones.RightUpperArm };
                case HumanBodyBones.LeftHand: return new[] { HumanBodyBones.LeftLowerArm };
                case HumanBodyBones.RightHand: return new[] { HumanBodyBones.RightLowerArm };
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg: return new[] { HumanBodyBones.Hips };
                case HumanBodyBones.LeftLowerLeg: return new[] { HumanBodyBones.LeftUpperLeg };
                case HumanBodyBones.RightLowerLeg: return new[] { HumanBodyBones.RightUpperLeg };
                case HumanBodyBones.LeftFoot: return new[] { HumanBodyBones.LeftLowerLeg };
                case HumanBodyBones.RightFoot: return new[] { HumanBodyBones.RightLowerLeg };
                case HumanBodyBones.LeftToes: return new[] { HumanBodyBones.LeftFoot };
                case HumanBodyBones.RightToes: return new[] { HumanBodyBones.RightFoot };
                default: return FingerParentCandidates(bone);
            }
        }

        private static HumanBodyBones[] FingerParentCandidates(HumanBodyBones bone)
        {
            if (bone == HumanBodyBones.LeftThumbProximal || bone == HumanBodyBones.LeftIndexProximal || bone == HumanBodyBones.LeftMiddleProximal || bone == HumanBodyBones.LeftRingProximal || bone == HumanBodyBones.LeftLittleProximal)
                return new[] { HumanBodyBones.LeftHand };
            if (bone == HumanBodyBones.RightThumbProximal || bone == HumanBodyBones.RightIndexProximal || bone == HumanBodyBones.RightMiddleProximal || bone == HumanBodyBones.RightRingProximal || bone == HumanBodyBones.RightLittleProximal)
                return new[] { HumanBodyBones.RightHand };
            if (bone == HumanBodyBones.LeftThumbIntermediate) return new[] { HumanBodyBones.LeftThumbProximal };
            if (bone == HumanBodyBones.LeftThumbDistal) return new[] { HumanBodyBones.LeftThumbIntermediate };
            if (bone == HumanBodyBones.RightThumbIntermediate) return new[] { HumanBodyBones.RightThumbProximal };
            if (bone == HumanBodyBones.RightThumbDistal) return new[] { HumanBodyBones.RightThumbIntermediate };
            if (bone == HumanBodyBones.LeftIndexIntermediate) return new[] { HumanBodyBones.LeftIndexProximal };
            if (bone == HumanBodyBones.LeftIndexDistal) return new[] { HumanBodyBones.LeftIndexIntermediate };
            if (bone == HumanBodyBones.RightIndexIntermediate) return new[] { HumanBodyBones.RightIndexProximal };
            if (bone == HumanBodyBones.RightIndexDistal) return new[] { HumanBodyBones.RightIndexIntermediate };
            if (bone == HumanBodyBones.LeftMiddleIntermediate) return new[] { HumanBodyBones.LeftMiddleProximal };
            if (bone == HumanBodyBones.LeftMiddleDistal) return new[] { HumanBodyBones.LeftMiddleIntermediate };
            if (bone == HumanBodyBones.RightMiddleIntermediate) return new[] { HumanBodyBones.RightMiddleProximal };
            if (bone == HumanBodyBones.RightMiddleDistal) return new[] { HumanBodyBones.RightMiddleIntermediate };
            if (bone == HumanBodyBones.LeftRingIntermediate) return new[] { HumanBodyBones.LeftRingProximal };
            if (bone == HumanBodyBones.LeftRingDistal) return new[] { HumanBodyBones.LeftRingIntermediate };
            if (bone == HumanBodyBones.RightRingIntermediate) return new[] { HumanBodyBones.RightRingProximal };
            if (bone == HumanBodyBones.RightRingDistal) return new[] { HumanBodyBones.RightRingIntermediate };
            if (bone == HumanBodyBones.LeftLittleIntermediate) return new[] { HumanBodyBones.LeftLittleProximal };
            if (bone == HumanBodyBones.LeftLittleDistal) return new[] { HumanBodyBones.LeftLittleIntermediate };
            if (bone == HumanBodyBones.RightLittleIntermediate) return new[] { HumanBodyBones.RightLittleProximal };
            if (bone == HumanBodyBones.RightLittleDistal) return new[] { HumanBodyBones.RightLittleIntermediate };
            return new HumanBodyBones[0];
        }

        private static HumanDescription BuildHumanDescription(Dictionary<HumanBodyBones, Transform> proxyMap)
        {
            List<HumanBone> humans = new List<HumanBone>();
            foreach (KeyValuePair<HumanBodyBones, Transform> pair in proxyMap)
            {
                HumanBone bone = new HumanBone();
                bone.humanName = HumanTrait.BoneName[(int)pair.Key];
                bone.boneName = pair.Value.name;
                bone.limit.useDefaultValues = true;
                humans.Add(bone);
            }
            Transform root = null;
            foreach (KeyValuePair<HumanBodyBones, Transform> pair in proxyMap) { root = pair.Value.root; break; }
            List<SkeletonBone> skeleton = new List<SkeletonBone>();
            if (root != null) AddSkeleton(root, skeleton);
            HumanDescription description = new HumanDescription();
            description.human = humans.ToArray();
            description.skeleton = skeleton.ToArray();
            description.upperArmTwist = 0.5f;
            description.lowerArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;
            return description;
        }

        private static void AddSkeleton(Transform transform, List<SkeletonBone> output)
        {
            SkeletonBone bone = new SkeletonBone();
            bone.name = transform.name;
            bone.position = transform.localPosition;
            bone.rotation = transform.localRotation;
            bone.scale = transform.localScale;
            output.Add(bone);
            for (int i = 0; i < transform.childCount; i++) AddSkeleton(transform.GetChild(i), output);
        }

        private static void ApplyArmTPose(Dictionary<HumanBodyBones, Transform> map, HumanBodyBones upperBone, HumanBodyBones lowerBone, Vector3 direction)
        {
            Transform upper;
            Transform lower;
            if (!map.TryGetValue(upperBone, out upper) || !map.TryGetValue(lowerBone, out lower)) return;
            Vector3 current = lower.position - upper.position;
            if (current.sqrMagnitude < 0.00000001f) return;
            upper.rotation = Quaternion.FromToRotation(current.normalized, direction) * upper.rotation;
        }

        private void ApplyNativeWorldMatrices()
        {
            float[] matrices = session.WorldMatrices;
            float scale = NormalizeScale(referenceImportScale);
            for (int i = 0; i < nativeBones.Length; i++)
            {
                int offset = i * 16;
                Vector3 mmdPosition = new Vector3(matrices[offset + 12], matrices[offset + 13], matrices[offset + 14]);
                Vector3 forward = new Vector3(matrices[offset + 8], matrices[offset + 9], matrices[offset + 10]);
                Vector3 up = new Vector3(matrices[offset + 4], matrices[offset + 5], matrices[offset + 6]);
                Quaternion mmdRotation = forward.sqrMagnitude > 0f && up.sqrMagnitude > 0f
                    ? Quaternion.LookRotation(forward.normalized, up.normalized)
                    : Quaternion.identity;
                nativeBones[i].SetPositionAndRotation(
                    nativeRoot.transform.TransformPoint(MmdToUnityPosition(mmdPosition) * scale),
                    nativeRoot.transform.rotation * MmdToUnityRotation(mmdRotation));
                nativeBones[i].localScale = Vector3.one;
            }
        }

        private void CopyNativePoseToProxy()
        {
            for (int i = 0; i < poseBindings.Count; i++)
            {
                PoseBoneBinding binding = poseBindings[i];
                Quaternion sourceRotation = CorrectSourceRotation(binding.Source.rotation);
                Quaternion delta = sourceRotation * Quaternion.Inverse(binding.SourceBindWorldRotation);
                binding.Proxy.rotation = delta * binding.ProxyBindWorldRotation;
                if (binding.HumanBone == HumanBodyBones.Hips)
                    binding.Proxy.position = binding.ProxyBindWorldPosition + (CorrectSourcePosition(binding.Source.position) - binding.SourceBindWorldPosition);
            }
        }

        private void BindMorphs()
        {
            expressionBindings = targetAnimator == null || auxiliarySampler == null ? null :
                VmdExpressionBindings.Create(targetAnimator, auxiliarySampler.MorphNames);
            var driver=GetComponent<VmdExpressionLateDriver>()??gameObject.AddComponent<VmdExpressionLateDriver>();
            driver.Owner=this;
        }

        internal void ApplyLateExpressions()
        {
            if(isPrepared&&applyMorphs&&expressionBindings!=null)expressionBindings.Apply(auxiliarySampler,playbackTime*30f);
        }

        private void ApplyMorphsAtFrame(float frame)
        {
            if (expressionBindings != null) expressionBindings.Apply(auxiliarySampler, frame);
        }

        private void ApplyCamera(float frame)
        {
            if (!CheckCameraOwnership()) return;
            if (targetCamera == null || auxiliarySampler == null) return;
            VmdCameraPose pose;
            if (!auxiliarySampler.TrySampleCamera(frame, out pose)) return;
            VmdUnityCameraPose unityPose;
            // The PMX import scale alone only describes the reference rig.  The
            // Humanoid bridge can retarget that rig onto a differently sized host
            // avatar, so the authored MMD camera must use the same height ratio as
            // the body or close shots aim above/below the actor.
            CalculateCameraRetarget(out float cameraRetargetScale, out cameraRetargetOffset);
            CameraRetargetScale = cameraRetargetScale;
            float cameraImportScale = NormalizeScale(referenceImportScale) * CameraRetargetScale;
            if (!VmdCameraConverter.TryConvert(pose, cameraImportScale, true, out unityPose)) return;
            unityPose.Position = (unityPose.Position + cameraRetargetOffset) * Mathf.Clamp(cameraDistanceScale, 0.01f, 100f);
            bool authoredHardCut = hasPreviousCameraPose &&
                auxiliarySampler.HasCameraHardCutBetween(previousCameraFrame, frame);
            string discontinuityReason;
            bool resetHistory = VmdCameraDiscontinuity.TryGetReason(
                hasPreviousCameraPose,
                previousCameraPose,
                previousCameraFrame,
                unityPose,
                frame,
                authoredHardCut,
                out discontinuityReason);
            CaptureTargetCameraState();
            if (cameraOrigin != null)
            {
                // Camera distance is already converted to Unity metres.  Inherit
                // only the actor's position and facing, never its import scale.
                targetCamera.transform.position = UseExternalCameraScale?cameraOrigin.TransformPoint(unityPose.Position+Vector3.up*ExternalCameraGroundHeight)
                    :cameraOrigin.position + cameraOrigin.rotation * unityPose.Position;
                targetCamera.transform.rotation = cameraOrigin.rotation * unityPose.Rotation;
            }
            else
            {
                targetCamera.transform.localPosition = unityPose.Position;
                targetCamera.transform.localRotation = unityPose.Rotation;
            }
            targetCamera.fieldOfView = unityPose.FieldOfView;
            SetCameraOrthographic(targetCamera, !unityPose.Perspective);
            if (resetHistory)
            {
                LastCameraHistoryResetComponentCount = VmdCameraTemporalHistory.Reset(targetCamera);
                CameraHistoryResetCount++;
                if (authoredHardCut) AuthoredCameraCutCount++;
                LastCameraDiscontinuityReason = discontinuityReason;
            }
            previousCameraPose = unityPose;
            previousCameraFrame = frame;
            hasPreviousCameraPose = true;
            lastCameraWorldPosition = targetCamera.transform.position;
            lastCameraWorldRotation = targetCamera.transform.rotation;
            lastCameraFieldOfView = targetCamera.fieldOfView;
            lastCameraOrthographic = !unityPose.Perspective;
            cameraWorldUnit=cameraImportScale*Mathf.Clamp(cameraDistanceScale,0.01f,100f)*
                (UseExternalCameraScale&&cameraOrigin!=null?Mathf.Abs(cameraOrigin.lossyScale.y):1f);
            LastAppliedCameraDistanceWorld=Mathf.Abs(pose.Distance)*cameraWorldUnit;
            hasLastCameraWorldPose = true;
            ApplyLastCameraProjectionForRender();
            EnsureCameraRenderDriver();
            HasAppliedCameraPose = true;
            LastAppliedCameraPerspective = unityPose.Perspective;
            LastAppliedCameraFrame = frame;
        }

        internal void ApplyLastCameraPoseForRender()
        {
            if (!CheckCameraOwnership()) return;
            if (targetCamera == null || !hasLastCameraWorldPose) return;
            targetCamera.transform.SetPositionAndRotation(lastCameraWorldPosition, lastCameraWorldRotation);
            targetCamera.fieldOfView = lastCameraFieldOfView;
            SetCameraOrthographic(targetCamera, lastCameraOrthographic);
            ApplyLastCameraProjectionForRender();
            CameraRenderReapplyCount++;
        }

        public void ApplyLastCameraProjectionForRender()
        {
            if (!CheckCameraOwnership()) return;
            if(!UseExternalCameraScale||targetCamera==null||!hasLastCameraWorldPose)return;
            float near=Mathf.Max(0.0001f,cameraWorldUnit*0.03f);
            float far=Mathf.Max(near+1f,Mathf.Max(500f*cameraWorldUnit,LastAppliedCameraDistanceWorld+100f*cameraWorldUnit));
            if(targetAnimator!=null){
                float required=Vector3.Distance(targetCamera.transform.position,targetAnimator.transform.position)+Mathf.Max(1f,targetHumanScale*4f);
                if(!float.IsNaN(required)&&!float.IsInfinity(required))far=Mathf.Max(far,required);
            }
            targetCamera.nearClipPlane=near;targetCamera.farClipPlane=far;
            float aspect=Mathf.Max(0.01f,targetCamera.aspect);var matrix=new Matrix4x4();
            if(lastCameraOrthographic)
            {
                // mmd_tools uses ortho_scale = 25 * abs(distance) / 45.
                float halfHeight=Mathf.Max(0.0001f,LastAppliedCameraDistanceWorld*25f/90f);
                targetCamera.orthographicSize=halfHeight;
                matrix.m00=1f/(halfHeight*aspect);matrix.m11=1f/halfHeight;
                matrix.m22=-2f/(far-near);matrix.m23=-(far+near)/(far-near);matrix.m33=1f;
            }
            else
            {
                float cotangent=1f/Mathf.Tan(lastCameraFieldOfView*(float)System.Math.PI/360f);
                matrix.m00=cotangent/aspect;matrix.m11=cotangent;
                matrix.m22=-(far+near)/(far-near);matrix.m23=-2f*far*near/(far-near);matrix.m32=-1f;
            }
            targetCamera.projectionMatrix=matrix;
        }

        // The host owns the main camera while its world-space menus are open.
        // Check again at render time: a menu can open after the motion update.
        public Func<bool> CameraOverrideAllowed { get; set; }
        public bool CheckCameraOwnership()
        {
            if (CameraOverrideAllowed == null || CameraOverrideAllowed()) return true;
            RestoreTargetCameraState();
            HasAppliedCameraPose = false;
            ResetCameraContinuityState();
            return false;
        }

        private void EnsureCameraRenderDriver()
        {
            if (targetCamera == null) return;
            if (cameraRenderDriver == null)
            {
                cameraRenderDriver = targetCamera.GetComponent<VmdCameraRenderDriver>();
                if (cameraRenderDriver == null)
                    cameraRenderDriver = targetCamera.gameObject.AddComponent<VmdCameraRenderDriver>();
            }
            cameraRenderDriver.Bind(this);
        }

        private void ReleaseCameraRenderDriver()
        {
            if (cameraRenderDriver != null) cameraRenderDriver.Bind(null);
            cameraRenderDriver = null;
        }

        private void ResetCameraContinuityState()
        {
            hasPreviousCameraPose = false;
            previousCameraPose = default(VmdUnityCameraPose);
            previousCameraFrame = 0f;
            LastCameraHistoryResetComponentCount = 0;
            LastCameraDiscontinuityReason = null;
            hasLastCameraWorldPose = false;
        }

        private void CaptureTargetCameraState()
        {
            if (targetCamera == null) return;
            if (hasSavedCameraState && savedCamera == targetCamera) return;
            RestoreTargetCameraState();
            savedCamera = targetCamera;
            savedCameraWorldPosition = targetCamera.transform.position;
            savedCameraWorldRotation = targetCamera.transform.rotation;
            savedCameraFieldOfView = targetCamera.fieldOfView;
            savedNearClip=targetCamera.nearClipPlane;savedFarClip=targetCamera.farClipPlane;savedOrthoSize=targetCamera.orthographicSize;
            savedProjection=targetCamera.projectionMatrix;
            targetCamera.ResetProjectionMatrix();savedProjectionCustom=false;
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)
                if(Mathf.Abs(savedProjection[row,col]-targetCamera.projectionMatrix[row,col])>0.0001f)savedProjectionCustom=true;
            if(savedProjectionCustom)targetCamera.projectionMatrix=savedProjection;
            // Use the direct getter. MateEngine's old Mono crashes inside
            // RuntimePropertyInfo.GetValue because its mscorlib lacks
            // Exception.GetBaseException(), even though Camera exposes a safe
            // getter. Its reference assembly makes the setter read-only, so the
            // guarded reflection write remains a no-op only on that host.
            savedCameraOrthographic = targetCamera.orthographic;
            savedCameraEnabled = targetCamera.enabled;
            hasSavedCameraState = true;
        }

        private void RestoreTargetCameraState()
        {
            if (!hasSavedCameraState) return;
            if (savedCamera != null)
            {
                savedCamera.transform.SetPositionAndRotation(savedCameraWorldPosition, savedCameraWorldRotation);
                savedCamera.fieldOfView = savedCameraFieldOfView;
                savedCamera.nearClipPlane=savedNearClip;savedCamera.farClipPlane=savedFarClip;savedCamera.orthographicSize=savedOrthoSize;
                savedCamera.ResetProjectionMatrix();if(savedProjectionCustom)savedCamera.projectionMatrix=savedProjection;
                SetCameraOrthographic(savedCamera, savedCameraOrthographic);
                savedCamera.enabled = savedCameraEnabled;
            }
            savedCamera = null;
            hasSavedCameraState = false;
        }

        private static void SetCameraOrthographic(Camera camera, bool value)
        {
            if (camera == null) return;
#if RUNTIMEVMD_CAMERA_ORTHOGRAPHIC_READONLY
            // MateEngine's generated UnityEngine reference omits this setter.
            // Position, rotation and FOV still follow the authored VMD camera;
            // projection switching remains available in normal Unity and 7DTD.
            return;
#else
            camera.orthographic = value;
#endif
        }

        private void CalculateCameraRetarget(out float scale, out Vector3 offset)
        {
            if(UseExternalCameraScale){scale=1f;offset=Vector3.zero;return;}
            PoseBoneBinding sourceHead;
            PoseBoneBinding sourceLeftFoot;
            PoseBoneBinding sourceRightFoot;
            if (TryGetPoseBinding(HumanBodyBones.Head, out sourceHead) &&
                TryGetPoseBinding(HumanBodyBones.LeftFoot, out sourceLeftFoot) &&
                TryGetPoseBinding(HumanBodyBones.RightFoot, out sourceRightFoot) &&
                VmdCameraRetargeting.TryCalculate(
                    sourceHead.SourceBindWorldPosition,
                    sourceLeftFoot.SourceBindWorldPosition,
                    sourceRightFoot.SourceBindWorldPosition,
                    sourceHead.TargetBindRootPosition,
                    sourceLeftFoot.TargetBindRootPosition,
                    sourceRightFoot.TargetBindRootPosition,
                    out scale,
                    out offset))
            {
                return;
            }
            if (IsUsingTargetCalibration)
            {
                scale = CalculateTargetCalibrationPositionScale();
            }
            else
            {
                float sourceScale = sourceHumanScale > 0.0001f ? sourceHumanScale : 1f;
                float targetScale = targetHumanScale > 0.0001f ? targetHumanScale : sourceScale;
                scale = targetScale / sourceScale;
            }
            scale = !float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0.0001f
                ? Mathf.Clamp(scale, 0.01f, 100f)
                : 1f;
            offset = Vector3.zero;
        }

        private bool TryGetPoseBinding(HumanBodyBones bone, out PoseBoneBinding result)
        {
            for (int index = 0; index < poseBindings.Count; index++)
            {
                PoseBoneBinding binding = poseBindings[index];
                if (binding.HumanBone != bone) continue;
                result = binding;
                return true;
            }
            result = null;
            return false;
        }

        private void RestoreTargetPose()
        {
            if (IsUsingDirectBoneRetargeting)
            {
                foreach (DirectRestNode node in directRestNodes) node.Restore();
            }
            else if (targetPoseHandler != null && targetRestPose.muscles != null)
                targetPoseHandler.SetHumanPose(ref targetRestPose);
            if (expressionBindings != null) expressionBindings.Restore();
        }

        private void DisposePlayback(bool restoreTarget)
        {
            isPlaying = false;
            if (restoreTarget)
            {
                if (restorePoseOnStop) RestoreTargetPose();
                RestoreTargetCameraState();
            }
            if (sourcePoseHandler != null) sourcePoseHandler.Dispose();
            if (targetPoseHandler != null) targetPoseHandler.Dispose();
            if (targetCalibrationPoseHandler != null) targetCalibrationPoseHandler.Dispose();
            sourcePoseHandler = null;
            targetPoseHandler = null;
            targetCalibrationPoseHandler = null;
            if (session != null) session.Dispose();
            session = null;
            DestroyUnityObject(nativeRoot);
            DestroyUnityObject(proxyRoot);
            DestroyUnityObject(targetCalibrationRoot);
            DestroyUnityObject(directRestRoot);
            directRestRoot = null;
            directRestBones = null;
            directRestNodes.Clear();
            DestroyUnityObject(sourceAvatar);
            nativeRoot = null;
            proxyRoot = null;
            targetCalibrationRoot = null;
            sourceAvatar = null;
            nativeBones = null;
            retargetRootBone = null;
            retargetRootBindWorldRotation = Quaternion.identity;
            retargetRootBindWorldPosition = Vector3.zero;
            retargetRootCorrection = Quaternion.identity;
            retargetRootPivot = Vector3.zero;
            hasInitialRetargetRootYaw = false;
            initialRetargetRootYaw = 0f;
            hasInitialSourceBodyYaw = false;
            initialSourceBodyYaw = 0f;
            metadata = null;
            auxiliarySampler = null;
            poseBindings.Clear();
            targetCalibrationBindings.Clear();
            expressionBindings = null;
            isPrepared = false;
            playbackTime = 0f;
            duration = 0f;
            sourceMotionIkToggleCount = 0;
            sourceMotionIkDisableCount = 0;
            motionMergeReport = null;
            nativePhysicsResetPending = false;
            hasNativePhysicsEvaluation = false;
            lastNativePhysicsTime = 0f;
            LastNativeIkEnabledCount = 0;
            LastNativeIkDisabledCount = 0;
            LastTargetLegIkSolvedCount = 0;
            LastTargetFootOrientationAppliedCount = 0;
            LastAppliedHumanBoneCount = 0;
            LastSourceBodyYawFromRest = 0f;
            LastCalibratedBodyYawFromRest = 0f;
            LastAppliedBodyYawFromRest = 0f;
            LastBodyYawRetargetError = 0f;
            LastSourceBodyDeltaNormalized = Vector3.zero;
            LastAppliedBodyDeltaNormalized = Vector3.zero;
            LastBodyPositionRetargetError = 0f;
            LastAppliedBodyTiltDegrees = 0f;
            LastSourceRootTiltDegrees = 0f;
            sourceHumanScale = 1f;
            targetHumanScale = 1f;
            directRetargetBasis = Quaternion.identity;
            directHipsBinding = null;
            directSpineBinding = null;
            directHeadBinding = null;
            directBodyCommonBone = null;
            HasAppliedCameraPose = false;
            LastAppliedCameraFrame = 0f;
            CameraHistoryResetCount = 0;
            AuthoredCameraCutCount = 0;
            CameraRenderReapplyCount = 0;
            CameraRetargetScale = 1f;
            cameraRetargetOffset = Vector3.zero;
            ResetCameraContinuityState();
        }

        private static void DestroyUnityObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private static void EnsureMuscleBuffer(ref HumanPose pose)
        {
            if (pose.muscles == null || pose.muscles.Length != HumanTrait.MuscleCount)
                pose.muscles = new float[HumanTrait.MuscleCount];
        }

        private static float NormalizeScale(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? value : 0.08f;
        }

        // Animator.humanScale is not exposed by every Unity version used by
        // supported hosts. Head-to-feet height supplies the same ratio needed
        // for root-motion retargeting and works on all Humanoid API versions.
        private static float CalculateAnimatorScale(Animator animator)
        {
            if (animator == null || !animator.isHuman) return 1f;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head == null || (leftFoot == null && rightFoot == null)) return 1f;
            Vector3 feet = leftFoot != null && rightFoot != null
                ? (leftFoot.position + rightFoot.position) * 0.5f
                : (leftFoot != null ? leftFoot.position : rightFoot.position);
            float height = Vector3.Distance(head.position, feet);
            return height > 0.0001f && !float.IsNaN(height) && !float.IsInfinity(height) ? height : 1f;
        }

        private static Vector3 MmdToUnityPosition(Vector3 value)
        {
            return new Vector3(-value.x, value.y, -value.z);
        }

        private static Quaternion MmdToUnityRotation(Quaternion value)
        {
            return new Quaternion(-value.x, value.y, -value.z, value.w);
        }

        private static float SignedPlanarYaw(Quaternion from, Quaternion to)
        {
            Vector3 fromForward = from * Vector3.forward;
            Vector3 toForward = to * Vector3.forward;
            fromForward.y = 0f;
            toForward.y = 0f;
            if (fromForward.sqrMagnitude <= 0.000001f || toForward.sqrMagnitude <= 0.000001f) return 0f;
            fromForward /= (float)System.Math.Sqrt(fromForward.sqrMagnitude);
            toForward /= (float)System.Math.Sqrt(toForward.sqrMagnitude);
            float sine = Vector3.Cross(fromForward, toForward).y;
            float cosine = Mathf.Clamp(Vector3.Dot(fromForward, toForward), -1f, 1f);
            return Mathf.Atan2(sine, cosine) * (float)(180.0 / System.Math.PI);
        }

        private static float AngleBetween(Vector3 left, Vector3 right)
        {
            float denominator = (float)System.Math.Sqrt(left.sqrMagnitude * right.sqrMagnitude);
            if (denominator <= 0.000001f) return 0f;
            float cosine = Mathf.Clamp(Vector3.Dot(left, right) / denominator, -1f, 1f);
            return (float)(System.Math.Acos(cosine) * (180.0 / System.Math.PI));
        }

        private void RaiseCompleted()
        {
            Action handler = PlaybackCompleted;
            if (handler != null) handler();
        }

        private float CalculateDirectRestScale()
        {
            PoseBoneBinding head, left, right;
            if (!TryGetPoseBinding(HumanBodyBones.Head, out head) ||
                !TryGetPoseBinding(HumanBodyBones.LeftFoot, out left) ||
                !TryGetPoseBinding(HumanBodyBones.RightFoot, out right)) return 1f;
            return Vector3.Distance(head.TargetBindWorldPosition,
                (left.TargetBindWorldPosition + right.TargetBindWorldPosition) * 0.5f);
        }

        private sealed class DirectRestNode
        {
            public readonly Transform Target;
            private readonly Transform rest;
            private readonly Vector3 savedPosition;
            private readonly Quaternion savedRotation;
            private Vector3 neutralPosition;
            private Quaternion neutralRotation;
            public DirectRestNode(Transform target, Transform restTransform)
            {
                Target = target; rest = restTransform;
                savedPosition = target.localPosition; savedRotation = target.localRotation;
            }
            public void CaptureNeutral()
            {
                neutralPosition = rest.localPosition; neutralRotation = rest.localRotation;
            }
            public void ApplyNeutral()
            {
                if (Target == null) return;
                Target.localPosition = neutralPosition; Target.localRotation = neutralRotation;
            }
            public void Restore()
            {
                if (Target == null) return;
                Target.localPosition = savedPosition; Target.localRotation = savedRotation;
            }
        }

        private sealed class PoseBoneBinding
        {
            public readonly HumanBodyBones HumanBone;
            public readonly Transform Source;
            public readonly Transform Proxy;
            public readonly Quaternion SourceBindWorldRotation;
            public readonly Vector3 SourceBindWorldPosition;
            public readonly Quaternion ProxyBindWorldRotation;
            public readonly Vector3 ProxyBindWorldPosition;
            public readonly Vector3 TargetBindRootPosition;
            public readonly Transform Target;
            public Quaternion TargetBindWorldRotation;
            public Vector3 TargetBindWorldPosition;
            public readonly Quaternion TargetBindLocalRotation;
            public readonly Vector3 TargetBindLocalPosition;
            public readonly int TargetDepth;
            public Quaternion AlignedTargetBindWorldRotation;
            public PoseBoneBinding AimChild;

            public PoseBoneBinding(HumanBodyBones humanBone, Transform source, Transform proxy,
                Quaternion sourceBindWorldRotation, Vector3 sourceBindWorldPosition,
                Quaternion proxyBindWorldRotation, Vector3 proxyBindWorldPosition,
                Vector3 targetBindRootPosition, Transform target,
                Quaternion targetBindWorldRotation, Vector3 targetBindWorldPosition,
                Quaternion targetBindLocalRotation, Vector3 targetBindLocalPosition,
                int targetDepth)
            {
                HumanBone = humanBone;
                Source = source;
                Proxy = proxy;
                SourceBindWorldRotation = sourceBindWorldRotation;
                SourceBindWorldPosition = sourceBindWorldPosition;
                ProxyBindWorldRotation = proxyBindWorldRotation;
                ProxyBindWorldPosition = proxyBindWorldPosition;
                TargetBindRootPosition = targetBindRootPosition;
                Target = target;
                TargetBindWorldRotation = targetBindWorldRotation;
                TargetBindWorldPosition = targetBindWorldPosition;
                TargetBindLocalRotation = targetBindLocalRotation;
                TargetBindLocalPosition = targetBindLocalPosition;
                TargetDepth = targetDepth;
                AlignedTargetBindWorldRotation = targetBindWorldRotation;
            }
        }

        private sealed class TargetCalibrationBinding
        {
            public readonly HumanBodyBones HumanBone;
            public readonly Transform Source;
            public readonly Quaternion SourceBindWorldRotation;
            public readonly Vector3 SourceBindWorldPosition;
            public readonly Transform Target;
            public readonly Quaternion TargetBindWorldRotation;
            public readonly Vector3 TargetBindWorldPosition;

            public TargetCalibrationBinding(HumanBodyBones humanBone, Transform source,
                Quaternion sourceBindWorldRotation, Vector3 sourceBindWorldPosition,
                Transform target, Quaternion targetBindWorldRotation, Vector3 targetBindWorldPosition)
            {
                HumanBone = humanBone;
                Source = source;
                SourceBindWorldRotation = sourceBindWorldRotation;
                SourceBindWorldPosition = sourceBindWorldPosition;
                Target = target;
                TargetBindWorldRotation = targetBindWorldRotation;
                TargetBindWorldPosition = targetBindWorldPosition;
            }
        }
    }
}
