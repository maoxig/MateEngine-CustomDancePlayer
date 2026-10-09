using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Reusable Unity component that materializes a directory/.vmdance package,
    /// loads its ordered VMD layers through the native PMX backend, and starts
    /// its optional audio on the same timeline.
    /// </summary>
    public sealed class VmdDancePackagePlayer : MonoBehaviour
    {
        [Header("Targets")]
        public Animator TargetAnimator;
        public Camera TargetCamera;
        public AudioSource TargetAudioSource;

        [Header("Native MMD")]
        [Tooltip("Fallback PMX reference rig when dance.json does not specify referencePmx.")]
        public string ReferencePmxPath;
        public string NativeLibraryPath;
        public float DefaultPositionScale = 0.1f;
        public bool DefaultLoop;

        public VmdDancePackageDescriptor Package { get; private set; }
        public VmdNativePmxPlayer MotionPlayer { get; private set; }
        public string LastError { get; private set; }
        public bool IsPlaying { get { return MotionPlayer != null && MotionPlayer.IsPlaying; } }

        private AudioClip ownedAudioClip;
        private Coroutine delayedAudioCoroutine;

        public IEnumerator LoadAndPlay(string packagePath)
        {
            LastError = null;
            Stop();
            VmdDancePackageDescriptor package;
            string error;
            if (!TryOpenPackage(packagePath, out package, out error))
            {
                LastError = error;
                yield break;
            }
            if (TargetAnimator == null)
            {
                LastError = "A target Animator is required.";
                yield break;
            }

            string pmxPath = ResolveReferencePmx(package);
            if (string.IsNullOrEmpty(pmxPath))
            {
                LastError = "The package does not contain referencePmx and no fallback PMX reference rig was found.";
                yield break;
            }

            MotionPlayer = TargetAnimator.GetComponent<VmdNativePmxPlayer>();
            if (MotionPlayer == null) MotionPlayer = TargetAnimator.gameObject.AddComponent<VmdNativePmxPlayer>();
            MotionPlayer.TargetAnimator = TargetAnimator;
            MotionPlayer.TargetCamera = TargetCamera;
            MotionPlayer.ReferenceImportScale = package.PositionScale ?? DefaultPositionScale;
            MotionPlayer.Loop = package.Loop ?? DefaultLoop;
            MotionPlayer.RespectAnimatorSpeed = false;
            if (!string.IsNullOrWhiteSpace(NativeLibraryPath)) MotionPlayer.NativeLibraryPath = Path.GetFullPath(NativeLibraryPath);
            if (!MotionPlayer.TryLoad(pmxPath, package.PrimaryVmdPath, package.OverlayVmdPaths, out error))
            {
                LastError = error;
                yield break;
            }

            EnsureAudioSource();
            if (!string.IsNullOrEmpty(package.AudioPath))
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                    new Uri(package.AudioPath).AbsoluteUri, GetAudioType(package.AudioPath)))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        LastError = "Could not load package audio: " + request.error;
                        yield break;
                    }
                    request.disposeDownloadHandlerOnDispose = false;
                    ownedAudioClip = DownloadHandlerAudioClip.GetContent(request);
                    TargetAudioSource.clip = ownedAudioClip;
                    TargetAudioSource.loop = MotionPlayer.Loop;
                    TargetAudioSource.time = package.AudioOffsetSeconds < 0f
                        ? Mathf.Clamp(-package.AudioOffsetSeconds, 0f, Mathf.Max(0f, ownedAudioClip.length - 0.001f))
                        : 0f;
                }
            }

            Package = package;
            MotionPlayer.Seek(0f, true);
            MotionPlayer.Play();
            if (TargetAudioSource != null && TargetAudioSource.clip != null)
            {
                if (package.AudioOffsetSeconds > 0f)
                    delayedAudioCoroutine = StartCoroutine(PlayAudioAfterDelay(package.AudioOffsetSeconds));
                else TargetAudioSource.Play();
            }
        }

        private IEnumerator PlayAudioAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (TargetAudioSource != null && TargetAudioSource.clip != null) TargetAudioSource.Play();
            delayedAudioCoroutine = null;
        }

        public void Stop()
        {
            if (delayedAudioCoroutine != null)
            {
                StopCoroutine(delayedAudioCoroutine);
                delayedAudioCoroutine = null;
            }
            if (MotionPlayer != null) MotionPlayer.Stop();
            if (TargetAudioSource != null)
            {
                TargetAudioSource.Stop();
                if (TargetAudioSource.clip == ownedAudioClip) TargetAudioSource.clip = null;
            }
            if (ownedAudioClip != null)
            {
                Destroy(ownedAudioClip);
                ownedAudioClip = null;
            }
            Package = null;
        }

        private bool TryOpenPackage(string packagePath, out VmdDancePackageDescriptor package, out string error)
        {
            package = null;
            error = null;
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                error = "A VMD dance package path is required.";
                return false;
            }
            if (Directory.Exists(packagePath)) return VmdDancePackage.TryOpenDirectory(packagePath, out package, out error);
            string cache = Path.Combine(Application.temporaryCachePath, "RuntimeVmdPackages");
            return VmdDancePackage.TryOpenArchive(packagePath, cache, out package, out error);
        }

        private string ResolveReferencePmx(VmdDancePackageDescriptor package)
        {
            if (!string.IsNullOrEmpty(package.ReferencePmxPath) && File.Exists(package.ReferencePmxPath))
                return Path.GetFullPath(package.ReferencePmxPath);
            string local = Path.Combine(package.PackageRoot, "_model.pmx");
            if (File.Exists(local)) return Path.GetFullPath(local);
            if (string.IsNullOrWhiteSpace(ReferencePmxPath)) return null;
            string configured = Path.IsPathRooted(ReferencePmxPath)
                ? ReferencePmxPath
                : Path.Combine(package.PackageRoot, ReferencePmxPath);
            return File.Exists(configured) ? Path.GetFullPath(configured) : null;
        }

        private void EnsureAudioSource()
        {
            if (TargetAudioSource != null) return;
            TargetAudioSource = GetComponent<AudioSource>();
            if (TargetAudioSource == null) TargetAudioSource = gameObject.AddComponent<AudioSource>();
            TargetAudioSource.playOnAwake = false;
        }

        private static AudioType GetAudioType(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)) return AudioType.WAV;
            if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)) return AudioType.MPEG;
            return AudioType.OGGVORBIS;
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
