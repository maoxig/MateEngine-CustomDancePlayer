using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Maoxig.RuntimeVmd;
using UnityEngine;
namespace CustomDancePlayer
{
    public class DancePlayerCore : MonoBehaviour
    {
        public const float CameraReferenceHeight = 1.65f;
        public const float CameraReferenceEyeHeight = 1.65f;
        public DanceAvatarHelper avatarHelper;
        public DanceResourceManager resourceManager;
        public DancePlayerUIManager uiManager;
        public DancePlaylistManager playlistManager;
        public enum PlayMode { Sequence, Loop, Random }
        private DanceSettingsHandler settings => DanceSettingsHandler.Instance;
        private Coroutine loading;
        private Task<VmdNativePreparedSession> pendingNativeSession;
        private VmdNativePmxPlayer native;
        private VmdHumanoidPlayer managed;
        private Animator leasedAnimator;
        private RuntimeAnimatorController originalController;
        private UniversalBlendshapes blendshapes;
        private bool originalBlendshapesEnabled;
        private DanceExpressionState originalExpression;
        private DanceSceneState originalScene;
        private Behaviour official;
        private bool officialWasEnabled;
        private int generation;
        private bool endQueued;
        private string authoringPreviewId;
        private float timeAtPause;
        private AvatarAnimatorController hostAnimator;
        private bool hostAnimatorWasEnabled;
        private float originalAnimatorSpeed;
        private bool audioScheduled;
        private float pausedAudioTime;
        private static readonly System.Reflection.FieldInfo AnimatorFullPathHashField =
            typeof(AnimatorStateInfo).GetField("m_FullPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        public bool Paused { get; private set; }
        public string LastError { get; private set; }
        public string CurrentResourceId { get; private set; }
        public VmdNativePmxPlayer CurrentVmdPlayer => native;
        public bool CanSeek => IsPlaying && loading == null && Duration > 0;
        public bool HasRuntimeCameraPose => native != null ? native.HasAppliedCameraPose : managed != null && managed.HasAppliedCameraPose;
        public bool RuntimeCameraPerspective => native != null ? native.LastAppliedCameraPerspective : managed == null || managed.LastAppliedCameraPerspective;
        public void SetRuntimeCamera(Camera camera) { if (native != null) native.TargetCamera = camera; if (managed != null) managed.TargetCamera = camera; }
        public void SetCameraScale(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 10f);
            settings.data.mmdCameraScale = value;
            float effective = GetEffectiveCameraScale();
            if (native != null) native.CameraDistanceScale = effective;
            if (managed != null) managed.CameraDistanceScale = effective;
        }
        public bool CapturePreviewCameraReference(string resourceId,float? eye,float? body,float totalScale)
        {
            if(!IsPlaying || CurrentResourceId!=resourceId || !resourceManager.IsVmdResource)return false;
            resourceManager.SetPreviewCameraReference(eye,body,totalScale);
            SetCameraScale(1f);DanceSettingsHandler.OnSettingChanged();return true;
        }
        public bool SetAuthoringPreviewCameraReference(string resourceId,float eye,float packageScale)
        {
            if(!IsPlaying || CurrentResourceId!=resourceId || !resourceManager.IsVmdResource || !IsFinitePositive(eye) || !IsFinitePositive(packageScale))return false;
            authoringPreviewId=resourceId;
            resourceManager.SetPreviewCameraReference(eye,null,packageScale);
            float effective=GetEffectiveCameraScale();
            if(native!=null)native.CameraDistanceScale=effective;
            if(managed!=null)managed.CameraDistanceScale=effective;
            return true;
        }
        private static bool IsFinitePositive(float value)=>value>0 && !float.IsNaN(value) && !float.IsInfinity(value);
        public float GetCameraReferenceEyeHeight() => resourceManager?.CurrentVmdCameraReferenceEyeHeight ?? (resourceManager?.CurrentVmdCameraReferenceBodyHeight.HasValue==true ? resourceManager.CurrentVmdCameraReferenceBodyHeight.Value*1.6f/1.65f : CameraReferenceEyeHeight);
        public float GetCameraReferenceBodyHeight() => resourceManager?.CurrentVmdCameraReferenceBodyHeight ?? (resourceManager?.CurrentVmdCameraReferenceEyeHeight.HasValue==true ? resourceManager.CurrentVmdCameraReferenceEyeHeight.Value*1.65f/1.6f : CameraReferenceHeight);
        public float GetCameraAuthoringScale() => resourceManager?.CurrentVmdCameraAuthoringScale ?? 1f;
        public float GetAvatarCameraScale()
        {
            float eye=avatarHelper==null?0f:avatarHelper.MeasureAvatarEyeHeight();
            float body=avatarHelper==null?0f:avatarHelper.MeasureAvatarHeight();
            return eye>0f?eye/GetCameraReferenceEyeHeight():body>0f?body/GetCameraReferenceBodyHeight():1f;
        }
        public float GetEffectiveCameraScale()
        {
            bool authoring=authoringPreviewId!=null && authoringPreviewId==CurrentResourceId;
            float adjustment=authoring?1f:Mathf.Clamp(settings.data.mmdCameraScale,.1f,10f);
            return Mathf.Clamp(GetCameraAuthoringScale()*((authoring||settings.data.autoMmdCameraScale)?GetAvatarCameraScale():1f)*adjustment,.01f,100f);
        }
        public void SetVmdFootIk(bool value)
        {
            settings.data.enableVmdFootIk = value;
            bool effective = resourceManager != null && resourceManager.CurrentVmdFootIk.HasValue ? resourceManager.CurrentVmdFootIk.Value : value;
            if (native != null) native.ApplyIk = effective;
            if (managed != null) { managed.ApplyLegIk = effective; managed.ApplyToeIk = effective; }
        }
        public void SetVmdRootOptions(bool upright, bool lockFacing)
        {
            settings.data.keepVmdRootUpright = upright;
            settings.data.lockVmdFacingForward = lockFacing;
            if (native != null) { native.KeepBodyUpright = upright; native.LockBodyYaw = lockFacing; }
        }
        public bool IsPlaying { get => DanceSettingsHandler.Existing != null && DanceSettingsHandler.Existing.data.isPlaying; set { if (DanceSettingsHandler.Existing != null) DanceSettingsHandler.Existing.data.isPlaying=value; } }
        public float Duration => native != null ? native.Duration : managed != null ? managed.Duration : resourceManager.CurrentAnimationClip != null ? resourceManager.CurrentAnimationClip.length : 0;
        public float PlaybackTime => native != null ? native.PlaybackTime : managed != null ? managed.PlaybackTime : !IsPlaying ? 0 : Paused ? timeAtPause : Mathf.Max(0, Time.time - settings.data.audioStartTime);
        public bool IsLoading => loading != null;
        void Start() { InitPlayer(); }
        public void InitPlayer()
        {
            if (playlistManager == null) playlistManager = GetComponent<DancePlaylistManager>();
            if (playlistManager == null) playlistManager = gameObject.AddComponent<DancePlaylistManager>();
            playlistManager.resourceManager = resourceManager; playlistManager.settingsHandler = settings;
            if (!playlistManager.Initialized) playlistManager.Init();
        }
        public bool PlayDanceByIndex(int index)
        {
            InitPlayer(); string id = playlistManager.GetFileByIndex(index);
            if (id == null || !avatarHelper.IsAvatarAvailable()) { LastError = DanceLocale.T("error.avatar"); return false; }
            StopPlay();
            if (!resourceManager.LoadDanceResource(id)) { LastError = resourceManager.LastError ?? DanceLocale.T("error.load"); return false; }
            if (DanceBootstrap.Root != null)
            {
                foreach (var follower in DanceBootstrap.Root.GetComponentsInChildren<DanceWindowFollower>(true)) if (follower.isEnabled) follower.BeginDanceTransition();
                foreach (var keeper in DanceBootstrap.Root.GetComponentsInChildren<DanceCameraDistKeeper>(true)) if (keeper.enabled) keeper.BeginDanceTransition();
            }
            CurrentResourceId = id; settings.data.currentPlayIndex = index; settings.data.lastResourceId = id;
            leasedAnimator = avatarHelper.CurrentAnimator;
            originalScene = DanceSceneState.Capture(leasedAnimator);
            official = FindFirstObjectByType<CustomDancePlayer.AvatarDanceHandler>();
            if (official != null)
            {
                officialWasEnabled = official.enabled;
                var stop = official.GetType().GetMethod("StopDance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (stop != null && stop.GetParameters().Length == 0) stop.Invoke(official, null);
                official.enabled = false;
            }
            originalController = leasedAnimator.runtimeAnimatorController;
            originalAnimatorSpeed = leasedAnimator.speed;
            hostAnimator=leasedAnimator.GetComponent<AvatarAnimatorController>();
            if(hostAnimator!=null){hostAnimatorWasEnabled=hostAnimator.enabled;hostAnimator.enabled=false;}
            blendshapes = leasedAnimator.GetComponent<UniversalBlendshapes>();
            originalExpression = DanceExpressionState.Capture(avatarHelper.CurrentAvatar);
            if (blendshapes != null)
            {
                originalBlendshapesEnabled = blendshapes.enabled;
                // The dummy bridge writes UniversalBlendshapes inputs. Its LateUpdate
                // must remain enabled to apply those inputs to VRM 0.x / 1.x faces.
                blendshapes.enabled = !resourceManager.IsVmdResource && avatarHelper.TargetSMR == null;
            }
            if (!resourceManager.IsVmdResource) { avatarHelper.PrepareBodyForDance(); if (avatarHelper.TargetSMR == null) avatarHelper.SetupDummyForDance(); avatarHelper.ResetMMDCameraHierarchy(); }
            IsPlaying = true; Paused = false; LastError = null; endQueued = false;
            int ticket = ++generation; loading = StartCoroutine(StartSequence(ticket)); DanceSettingsHandler.OnSettingChanged(); return true;
        }
        private IEnumerator StartSequence(int ticket)
        {
            if (resourceManager.IsVmdResource)
            {
                string error = null; var sync = uiManager.CameraSync;
                if (settings.data.enableMMDCamera && sync != null) sync.PrepareSwitch();
                Camera camera = settings.data.enableMMDCamera && sync != null ? sync.RenderCamera : null;
                string pmx = resourceManager.CurrentVmdReferencePmxPath;
                bool useNative = !string.IsNullOrEmpty(pmx) && File.Exists(pmx) && NativeMmdSession.FindDefaultLibraryPath() != null;
                if (useNative)
                {
                    native = leasedAnimator.GetComponent<VmdNativePmxPlayer>() ?? leasedAnimator.gameObject.AddComponent<VmdNativePmxPlayer>();
                    native.CameraOverrideAllowed=AllowRuntimeCamera;
                    native.enabled = true; native.TargetAnimator = leasedAnimator; native.TargetCamera = camera; native.CameraOrigin = avatarHelper.CurrentAvatar.transform;
                    native.NativeLibraryPath = NativeMmdSession.FindDefaultLibraryPath(); native.NativePhysicsMode = VmdNativePhysicsMode.Off;
                    native.UseExternalCameraScale=true;native.ExternalCameraGroundHeight=avatarHelper.MeasureAvatarGroundHeight();
                    native.ReferenceImportScale = resourceManager.CurrentVmdPositionScale ?? 0.08f; native.RespectAnimatorSpeed = false; native.Loop = false;
                    native.ApplyIk = resourceManager.CurrentVmdFootIk ?? settings.data.enableVmdFootIk; native.KeepBodyUpright = settings.data.keepVmdRootUpright; native.LockBodyYaw = settings.data.lockVmdFacingForward;
                    native.CameraDistanceScale = GetEffectiveCameraScale();
                    Task<VmdNativePreparedData> prepareTask = null;
                    try { prepareTask = VmdNativePmxPlayer.PrepareCachedAsync(pmx, resourceManager.CurrentVmdPath, resourceManager.CurrentVmdOverlayPaths); }
                    catch (Exception exception) { error = exception.ToString(); }
                    while (prepareTask != null && !prepareTask.IsCompleted)
                    {
                        if (ticket != generation || !IsPlaying) yield break;
                        yield return null;
                    }
                    if (prepareTask != null)
                    {
                        if (prepareTask.IsCanceled) error = "VMD preparation was cancelled.";
                        else if (prepareTask.IsFaulted) error = prepareTask.Exception.GetBaseException().ToString();
                        else
                        {
                            Task<VmdNativePreparedSession> sessionTask = VmdNativePmxPlayer.CreateSessionAsync(
                                prepareTask.Result, native.NativeLibraryPath, native.NativePhysicsMode);
                            pendingNativeSession = sessionTask;
                            try
                            {
                                while (!sessionTask.IsCompleted)
                                {
                                    if (ticket != generation || !IsPlaying) yield break;
                                    yield return null;
                                }
                                if (sessionTask.IsCanceled) error = "Native VMD session preparation was cancelled.";
                                else if (sessionTask.IsFaulted) error = sessionTask.Exception.GetBaseException().ToString();
                                else native.TryLoadPrepared(prepareTask.Result, sessionTask.Result, out error);
                            }
                            finally
                            {
                                DisposePreparedSessionWhenReady(sessionTask);
                                if (ReferenceEquals(pendingNativeSession, sessionTask)) pendingNativeSession = null;
                            }
                        }
                    }
                    if (error != null) { native.Unload(); native.enabled = false; native = null; }
                    if(native!=null){native.PoseApplied-=AfterVmdPose;native.PoseApplied+=AfterVmdPose;}
                }
                if (native == null)
                {
                    LastError = DanceLocale.T("error.native", error ?? DanceLocale.T("error.native.missing"));
                    Debug.LogError("[CustomDancePlayer] " + LastError);
                    StopPlay(); yield break;
                }
                leasedAnimator.speed = 0; SetDanceFlag(true); yield return resourceManager.LoadVmdAudio();
            }
            else { avatarHelper.SetupAnimation(resourceManager.CurrentAnimationClip); leasedAnimator.speed = 0; }
            if (ticket != generation || !IsPlaying) yield break;
            var audio = avatarHelper.CurrentAudioSource;
            if (audio != null && audio.clip != null)
            {
                audio.volume = 0; audio.Play(); float timeout = Time.realtimeSinceStartup + 1;
                while (audio.time < 0.05f && Time.realtimeSinceStartup < timeout && ticket == generation) yield return null;
                if (ticket != generation) yield break;
                audio.Stop(); avatarHelper.UpdateAudioVolume();
            }
            yield return new WaitForSecondsRealtime(Mathf.Clamp(settings.data.animationStartDelay, 0, 1));
            if (ticket != generation || !IsPlaying) yield break;
            settings.data.audioStartTime = Time.time;
            if (native != null) native.Play(); else if (managed != null) managed.Play(); else leasedAnimator.speed = 1;
            if (audio != null && audio.clip != null)
            {
                float offset = resourceManager.IsVmdResource ? resourceManager.CurrentVmdAudioOffsetSeconds : 0;
                if (offset < 0) audio.time = Mathf.Clamp(-offset, 0, Mathf.Max(0, audio.clip.length - 0.001f));
                audioScheduled = offset > 0; if (!audioScheduled) audio.Play();
            }
            loading = null; Debug.Log("[CustomDancePlayer] PLAY " + CurrentResourceId + "; backend=" + (native != null ? "native-pmx" : managed != null ? "managed-vmd" : "unity-animation"));
        }
        private static void DisposePreparedSessionWhenReady(Task<VmdNativePreparedSession> task)
        {
            if (task == null) return;
            if (task.Status == TaskStatus.RanToCompletion) task.Result.Dispose();
            else if (!task.IsCompleted)
                task.ContinueWith(completed => { if (completed.Status == TaskStatus.RanToCompletion) completed.Result.Dispose(); }, TaskScheduler.Default);
        }
        void Update()
        { if(IsPlaying&&!Paused&&loading==null&&audioScheduled&&PlaybackTime>=resourceManager.CurrentVmdAudioOffsetSeconds){audioScheduled=false;var audio=avatarHelper.CurrentAudioSource;if(audio!=null&&audio.clip!=null){audio.Play();audio.time=Mathf.Clamp(PlaybackTime-resourceManager.CurrentVmdAudioOffsetSeconds,0,Mathf.Max(0,audio.clip.length-0.001f));}} if (IsPlaying && !Paused && loading == null && Duration > 0 && !endQueued && PlaybackTime >= Duration - 0.01f) OnLegacyAnimationCompleted(); }
        public void OnLegacyAnimationCompleted()
        { if (!IsPlaying || Paused || endQueued) return; endQueued = true; StartCoroutine(Advance(generation)); }
        private IEnumerator Advance(int ticket) { yield return null; if (ticket == generation && IsPlaying && !Paused) PlayNext(); }
        public void TogglePause()
        {
            if (!IsPlaying || loading != null) return; var audio = avatarHelper.CurrentAudioSource;
            if (!Paused) { timeAtPause = PlaybackTime; pausedAudioTime=audio==null?0:audio.time; Paused = true; native?.Pause(); managed?.Pause(); if (leasedAnimator != null) leasedAnimator.speed = 0; audio?.Pause(); }
            else { settings.data.audioStartTime = Time.time - timeAtPause; Paused = false; native?.Play(); managed?.Play(); if (native == null && managed == null && leasedAnimator != null) leasedAnimator.speed = 1; if (audio != null && audio.clip != null && !audioScheduled) { var resume=typeof(AudioSource).GetMethod("UnPause");if(resume!=null)resume.Invoke(audio,null);else{audio.Play();audio.time=pausedAudioTime;} } }
        }
        public void Seek(float normalized)
        {
            if (!IsPlaying || loading != null || Duration <= 0) return; float seconds = Mathf.Clamp01(normalized) * Duration;
            native?.Seek(seconds, true); managed?.Seek(seconds, true);
            if (native != null || managed != null) AfterVmdPose();
            if (native == null && managed == null && leasedAnimator != null)
            {
                // The controller state is not guaranteed to be named like its
                // override clip. Seek the active state hash so old .unity3d and
                // official .me packages follow the progress bar consistently.
                AnimatorStateInfo state = leasedAnimator.IsInTransition(0)
                    ? leasedAnimator.GetNextAnimatorStateInfo(0)
                    : leasedAnimator.GetCurrentAnimatorStateInfo(0);
                int stateHash = state.shortNameHash;
                if (AnimatorFullPathHashField != null)
                {
                    object fullPathHash = AnimatorFullPathHashField.GetValue(state);
                    if (fullPathHash is int value && value != 0) stateHash = value;
                }
                if (stateHash == 0) { LastError = DanceLocale.T("error.seek"); return; }
                leasedAnimator.speed = 1f;
                leasedAnimator.CrossFadeInFixedTime(stateHash, 0f, 0, seconds, 0f);
                // A tiny evaluation step commits a zero-duration cross-fade in
                // the stripped MateEngine Animator API before we freeze again.
                leasedAnimator.Update(0.0001f);
                leasedAnimator.speed = Paused ? 0f : 1f;
                uiManager.CameraSync?.ApplyPose();
            }
            settings.data.audioStartTime = Time.time - seconds; timeAtPause = seconds; var audio = avatarHelper.CurrentAudioSource;
            if (audio != null && audio.clip != null)
            {
                float audioSeconds = seconds - resourceManager.CurrentVmdAudioOffsetSeconds;
                audioScheduled = audioSeconds < 0;
                pausedAudioTime = Mathf.Clamp(audioSeconds, 0, Mathf.Max(0, audio.clip.length - 0.001f));
                audio.Stop();
                if (!audioScheduled && audioSeconds < audio.clip.length) { audio.Play(); audio.time = pausedAudioTime; if (Paused) audio.Pause(); }
            }
        }
        public bool AdjustPreviewAudioOffset(string resourceId, float seconds)
        {
            if (!IsPlaying || CurrentResourceId != resourceId || !resourceManager.IsVmdResource || IsLoading) return false;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || Mathf.Abs(seconds) > 3600f) return false;
            resourceManager.SetPreviewAudioOffset(seconds);
            // Seek re-synchronizes the audio while retaining the current pose,
            // pause state and scheduled positive-offset start.
            if (Duration > 0f) Seek(PlaybackTime / Duration);
            return true;
        }
        public void PlayNext()
        {
            if (playlistManager == null || playlistManager.CurrentPlaylistData.Count == 0) { StopPlay(); return; }
            int index = playlistManager.GetIndexByFile(CurrentResourceId); int next = index + 1;
            if (settings.data.currentPlayMode == PlayMode.Loop) next = Mathf.Max(0, index);
            if (settings.data.currentPlayMode == PlayMode.Random) { next = UnityEngine.Random.Range(0, playlistManager.CurrentPlaylistData.Count); if (playlistManager.CurrentPlaylistData.Count > 1 && next == index) next = (next + 1) % playlistManager.CurrentPlaylistData.Count; }
            if (next >= playlistManager.CurrentPlaylistData.Count) StopPlay(); else PlayDanceByIndex(next);
        }
        public void PlayPrev() { if (playlistManager != null) PlayDanceByIndex(Mathf.Max(0, playlistManager.GetIndexByFile(CurrentResourceId) - 1)); }
        public void StopPlay()
        {
            authoringPreviewId=null;
            generation++; StopAllCoroutines(); loading = null; Paused = false; endQueued = false; audioScheduled=false;
            Task<VmdNativePreparedSession> abandonedSession = pendingNativeSession; pendingNativeSession = null; DisposePreparedSessionWhenReady(abandonedSession);
            if (native != null) { native.PoseApplied-=AfterVmdPose; native.Stop(); native.Unload(); native.enabled = false; native = null; }
            if (managed != null) { managed.PoseApplied-=AfterVmdPose; managed.Stop(); managed.enabled = false; managed = null; }
            if (avatarHelper != null && avatarHelper.CurrentAudioSource != null) avatarHelper.CurrentAudioSource.Stop();
            if (leasedAnimator != null) { leasedAnimator.speed = originalAnimatorSpeed; leasedAnimator.runtimeAnimatorController = originalController; SetDanceFlag(false); }
            if(hostAnimator!=null)hostAnimator.enabled=hostAnimatorWasEnabled;hostAnimator=null;
            if (blendshapes != null) blendshapes.enabled = originalBlendshapesEnabled;
            if (official != null) official.enabled = officialWasEnabled;
            if (avatarHelper != null) { avatarHelper.RestoreOriginalBody(); avatarHelper.ResetMMDCameraHierarchy(); }
            originalExpression?.Restore(); originalExpression = null;
            originalScene?.Restore(); originalScene = null;
            uiManager.CameraSync?.RestoreFrontView();
            if (DanceBootstrap.Root != null)
            {
                foreach (var keeper in DanceBootstrap.Root.GetComponentsInChildren<DanceCameraDistKeeper>(true)) keeper.RestoreForDanceTransition();
                foreach (var follower in DanceBootstrap.Root.GetComponentsInChildren<DanceWindowFollower>(true)) follower.RestoreForDanceTransition();
            }
            resourceManager?.UnloadCurrentResource(); IsPlaying = false; leasedAnimator = null; blendshapes = null; official = null; DanceSettingsHandler.OnSettingChanged();
        }
        private void SetDanceFlag(bool value) { if (leasedAnimator != null) foreach (var p in leasedAnimator.parameters) if (p.name == "isDancing") { leasedAnimator.SetBool("isDancing", value); break; } }
        private void AfterVmdPose()
        {
            if(DanceBootstrap.Root!=null)
            {
                bool authoredCamera = uiManager.CameraSync != null && uiManager.CameraSync.enabled && HasRuntimeCameraPose;
                if (!authoredCamera)
                    foreach(var c in DanceBootstrap.Root.GetComponentsInChildren<DanceCameraDistKeeper>(true))if(c.enabled)c.ApplyFollow();
                foreach(var c in DanceBootstrap.Root.GetComponentsInChildren<DanceShadowFollower>(true))if(c.enabled)c.ApplyFollow();
            }
            if(uiManager.CameraSync!=null&&uiManager.CameraSync.enabled)uiManager.CameraSync.ApplyPose();
            var follow=settings.hipsFollower;if(follow!=null&&follow.enabled)follow.ApplyAfterVmdPose();
        }
        private bool AllowRuntimeCamera()
        {
            if (DanceNativeMenus.IsOpen()) return false;
            // Seeking can run inside a UI event before CameraSync.Update.
            // Capture the host view before the first resumed camera write.
            var sync = uiManager.CameraSync;
            if (sync != null && sync.enabled) sync.PrepareSwitch();
            return true;
        }
        public string GetCurrentPlayFileName() { return CurrentResourceId != null && resourceManager.Descriptors.TryGetValue(CurrentResourceId, out var d) ? d.Title : DanceLocale.T("player.idle"); }
        void OnDestroy() { StopPlay(); }
    }
}
