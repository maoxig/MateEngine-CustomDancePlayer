using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    [DisallowMultipleComponent]
    public sealed class VmdHumanoidPlayer : MonoBehaviour
    {
        private static readonly PropertyInfo CameraOrthographicProperty = typeof(Camera).GetProperty("orthographic", BindingFlags.Instance | BindingFlags.Public);
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private string vmdPath;
        [SerializeField] private string[] additionalVmdPaths = new string[0];
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool restorePoseOnStop = true;
        [SerializeField] private bool convertMmdCoordinates = true;
        [SerializeField] private bool applyRootPosition = true;
        [SerializeField] private bool applyMorphs = true;
        [SerializeField] private bool applyLegIk = true;
        [SerializeField] private bool applyToeIk = true;
        [SerializeField] private float positionScale = 0.08f;
        [SerializeField] private float cameraDistanceScale = 1f;
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool respectAnimatorSpeed;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform cameraOrigin = null;
        public Transform CameraOrigin { get { return cameraOrigin; } set { cameraOrigin = value; } }

        private readonly List<BoundBone> boundBones = new List<BoundBone>();
        private readonly List<BoundMorph> boundMorphs = new List<BoundMorph>();
        private readonly List<BoundLegIk> boundLegIks = new List<BoundLegIk>();
        private VmdMotionSampler sampler;
        private float playbackTime;
        private bool isPlaying;
        private bool isPrepared;
        private VmdMotionMergeReport motionMergeReport;

        public event Action PlaybackCompleted;

        public Animator TargetAnimator
        {
            get { return targetAnimator; }
            set
            {
                if (targetAnimator == value) return;
                Stop();
                targetAnimator = value;
                isPrepared = false;
            }
        }

        public VmdMotion Motion
        {
            get { return sampler == null ? null : sampler.Motion; }
        }

        public bool IsLoaded
        {
            get { return sampler != null; }
        }

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        public bool IsActive
        {
            get { return sampler != null && isPrepared; }
        }

        public bool Loop
        {
            get { return loop; }
            set { loop = value; }
        }

        public bool ConvertMmdCoordinates
        {
            get { return convertMmdCoordinates; }
            set { convertMmdCoordinates = value; }
        }

        public bool ApplyRootPosition
        {
            get { return applyRootPosition; }
            set { applyRootPosition = value; }
        }

        public bool ApplyMorphs
        {
            get { return applyMorphs; }
            set { applyMorphs = value; }
        }

        public bool ApplyLegIk
        {
            get { return applyLegIk; }
            set { applyLegIk = value; }
        }

        public bool ApplyToeIk
        {
            get { return applyToeIk; }
            set { applyToeIk = value; }
        }

        public int LastSolvedIkCount { get; private set; }

        public int BoundLegIkCount
        {
            get { return boundLegIks.Count; }
        }

        public int BoundBoneCount
        {
            get { return boundBones.Count; }
        }

        public int BoundMorphCount
        {
            get { return boundMorphs.Count; }
        }

        public string[] AdditionalVmdPaths
        {
            get { return additionalVmdPaths == null ? new string[0] : (string[])additionalVmdPaths.Clone(); }
            set { additionalVmdPaths = value == null ? new string[0] : (string[])value.Clone(); }
        }

        public int MotionLayerCount { get { return motionMergeReport == null ? 0 : motionMergeReport.LayerCount; } }

        public VmdMotionMergeReport MotionMergeReport { get { return motionMergeReport; } }
        public bool HasAppliedCameraPose { get; private set; }
        public bool LastAppliedCameraPerspective { get; private set; }
        public float LastAppliedCameraFrame { get; private set; }

        public float LastIkErrorBefore { get; private set; }

        public float LastIkErrorAfter { get; private set; }

        public float PositionScale
        {
            get { return positionScale; }
            set { positionScale = value; }
        }

        public float CameraDistanceScale
        {
            get { return cameraDistanceScale; }
            set { cameraDistanceScale = Mathf.Clamp(value, 0.1f, 10f); }
        }

        public float PlaybackSpeed
        {
            get { return playbackSpeed; }
            set { playbackSpeed = value; }
        }

        public bool RespectAnimatorSpeed
        {
            get { return respectAnimatorSpeed; }
            set { respectAnimatorSpeed = value; }
        }

        public float PlaybackTime
        {
            get { return playbackTime; }
        }

        public float Duration
        {
            get { return sampler == null ? 0f : sampler.Motion.Duration; }
        }

        public Camera TargetCamera
        {
            get { return targetCamera; }
            set { targetCamera = value; }
        }

        private void Reset()
        {
            targetAnimator = GetComponent<Animator>();
        }

        private void Start()
        {
            if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
            if (!string.IsNullOrEmpty(vmdPath) || (playOnStart && sampler != null))
            {
                try
                {
                    // A host may AddComponent, Load and Play before Unity invokes Start.
                    // Do not reload and stop that already prepared runtime motion.
                    if (sampler == null && !string.IsNullOrEmpty(vmdPath)) Load(vmdPath, additionalVmdPaths);
                    if (playOnStart && sampler != null && !isPlaying) Play();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[RuntimeVmd] Failed to load '" + vmdPath + "': " + exception, this);
                }
            }
        }

        private void Update()
        {
            if (!isPlaying || sampler == null) return;
            // Some existing dance players pause by setting Animator.speed to zero. This must
            // be opt-in because an Animator without a controller can also report speed zero.
            if (respectAnimatorSpeed && targetAnimator != null && targetAnimator.speed <= 0f) return;
            float duration = Duration;
            playbackTime += Time.deltaTime * playbackSpeed;

            if (duration <= 0f)
            {
                playbackTime = 0f;
                isPlaying = false;
                RaiseCompleted();
                return;
            }

            if (loop)
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
            if (sampler == null || (!isPlaying && !isPrepared)) return;
            ApplyAtTime(playbackTime);
            PoseApplied?.Invoke();
        }
        public event Action PoseApplied;

        private void OnDisable()
        {
            if (restorePoseOnStop) RestoreBasePose();
            isPrepared = false;
        }

        public void Load(string path)
        {
            Load(path, null);
        }

        public void Load(string path, IEnumerable<string> overlayPaths)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A VMD path is required.", "path");
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("The VMD was not found.", fullPath);
            List<string> fullPaths = new List<string> { fullPath };
            List<VmdMotion> layers = new List<VmdMotion> { VmdReader.Read(fullPath) };
            if (overlayPaths != null)
            {
                foreach (string overlayPath in overlayPaths)
                {
                    if (string.IsNullOrWhiteSpace(overlayPath)) continue;
                    string fullOverlayPath = Path.GetFullPath(overlayPath);
                    if (!File.Exists(fullOverlayPath)) throw new FileNotFoundException("An additional VMD was not found.", fullOverlayPath);
                    fullPaths.Add(fullOverlayPath);
                    layers.Add(VmdReader.Read(fullOverlayPath));
                }
            }
            Load(layers);
            vmdPath = fullPath;
            additionalVmdPaths = fullPaths.GetRange(1, fullPaths.Count - 1).ToArray();
        }

        public bool TryLoad(string path, out string error)
        {
            return TryLoad(path, null, out error);
        }

        public bool TryLoad(string path, IEnumerable<string> overlayPaths, out string error)
        {
            try
            {
                Load(path, overlayPaths);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        public void Load(byte[] bytes)
        {
            Load(VmdReader.Read(bytes));
        }

        public void Load(byte[] bytes, IEnumerable<byte[]> overlayBytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            List<VmdMotion> layers = new List<VmdMotion> { VmdReader.Read(bytes) };
            if (overlayBytes != null)
            {
                foreach (byte[] overlay in overlayBytes)
                {
                    if (overlay == null || overlay.Length == 0)
                        throw new ArgumentException("Additional VMD data must not be null or empty.", "overlayBytes");
                    layers.Add(VmdReader.Read(overlay));
                }
            }
            Load(layers);
        }

        public void Load(VmdMotion motion)
        {
            if (motion == null) throw new ArgumentNullException("motion");
            Load(new[] { motion });
        }

        public void Load(IList<VmdMotion> layers)
        {
            Stop();
            VmdMotion merged = VmdMotionLayers.Merge(layers, out motionMergeReport);
            sampler = new VmdMotionSampler(merged);
            playbackTime = 0f;
            PrepareBindings();
            ApplyAtTime(0f);
        }

        public void Play()
        {
            if (sampler == null) throw new InvalidOperationException("Load a VMD motion before calling Play.");
            if (!isPrepared) PrepareBindings();
            if (!loop && playbackTime >= Duration) playbackTime = 0f;
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
            HasAppliedCameraPose = false;
            LastAppliedCameraFrame = 0f;
            if (restorePoseOnStop) RestoreBasePose();
            isPrepared = false;
        }

        public void Seek(float timeSeconds, bool applyImmediately)
        {
            float duration = Duration;
            playbackTime = loop && duration > 0f
                ? Mathf.Repeat(timeSeconds, duration)
                : Mathf.Clamp(timeSeconds, 0f, duration);
            if (applyImmediately && sampler != null) ApplyAtTime(playbackTime);
        }

        public void CaptureBasePose()
        {
            if (sampler == null) throw new InvalidOperationException("Load a VMD motion before capturing bindings.");
            PrepareBindings();
        }

        public void ApplyAtTime(float timeSeconds)
        {
            if (sampler == null) return;
            if (!isPrepared) PrepareBindings();
            float frameNumber = Mathf.Max(0f, timeSeconds * VmdMotion.DefaultFramesPerSecond);

            for (int boneIndex = 0; boneIndex < boundBones.Count; boneIndex++)
            {
                BoundBone boundBone = boundBones[boneIndex];
                Quaternion rotation = Quaternion.identity;
                Vector3 translation = Vector3.zero;
                for (int sourceIndex = 0; sourceIndex < boundBone.SourceNames.Count; sourceIndex++)
                {
                    VmdBonePose pose;
                    if (!sampler.TrySampleBone(boundBone.SourceNames[sourceIndex], frameNumber, out pose)) continue;
                    rotation = rotation * ConvertRotation(pose.Rotation);
                    translation += ConvertPosition(pose.Position) * positionScale;
                }

                if (boundBone.Transform != null)
                {
                    if (boundBone.IsHips)
                    {
                        rotation = SampleRootRotation(frameNumber) * rotation;
                    }
                    boundBone.Transform.localRotation = boundBone.BaseLocalRotation * rotation;
                    if (boundBone.IsHips && applyRootPosition)
                    {
                        boundBone.Transform.localPosition = boundBone.BaseLocalPosition + SampleRootTranslation(frameNumber) + translation;
                    }
                }
            }

            ApplyLegIkAtFrame(frameNumber);

            if (applyMorphs) ApplyMorphsAtFrame(frameNumber);
            ApplyCameraAtFrame(frameNumber);
        }

        public void RestoreBasePose()
        {
            for (int index = 0; index < boundBones.Count; index++)
            {
                BoundBone bone = boundBones[index];
                if (bone.Transform == null) continue;
                bone.Transform.localRotation = bone.BaseLocalRotation;
                bone.Transform.localPosition = bone.BaseLocalPosition;
            }
            for (int index = 0; index < boundMorphs.Count; index++)
            {
                BoundMorph morph = boundMorphs[index];
                if (morph.Renderer != null) morph.Renderer.SetBlendShapeWeight(morph.BlendShapeIndex, morph.BaseWeight);
            }
            for (int index = 0; index < boundLegIks.Count; index++)
            {
                BoundLegIk leg = boundLegIks[index];
                if (leg.UpperLeg != null) leg.UpperLeg.localRotation = leg.BaseUpperLegRotation;
                if (leg.LowerLeg != null) leg.LowerLeg.localRotation = leg.BaseLowerLegRotation;
                if (leg.Foot != null) leg.Foot.localRotation = leg.BaseFootLocalRotation;
                if (leg.Toes != null) leg.Toes.localRotation = leg.BaseToesLocalRotation;
            }
        }

        private void PrepareBindings()
        {
            boundBones.Clear();
            boundMorphs.Clear();
            boundLegIks.Clear();
            if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null)
            {
                throw new InvalidOperationException("A target Animator is required for Humanoid VMD playback.");
            }
            if (!targetAnimator.isHuman)
            {
                throw new InvalidOperationException("The target Animator must use a valid Humanoid Avatar.");
            }
            Dictionary<Transform, BoundBone> byTransform = new Dictionary<Transform, BoundBone>();
            for (int index = 0; index < VmdHumanoidMap.Entries.Count; index++)
            {
                VmdHumanoidMapEntry entry = VmdHumanoidMap.Entries[index];
                string sourceName = FindFirstTrack(entry.VmdNames);
                if (sourceName == null) continue;

                Transform transform = targetAnimator.GetBoneTransform(entry.Bone);
                if (transform == null && entry.FallbackBone != HumanBodyBones.LastBone)
                {
                    transform = targetAnimator.GetBoneTransform(entry.FallbackBone);
                }
                if (transform == null) continue;

                BoundBone binding;
                if (!byTransform.TryGetValue(transform, out binding))
                {
                    binding = new BoundBone();
                    binding.Transform = transform;
                    binding.BaseLocalPosition = transform.localPosition;
                    binding.BaseLocalRotation = transform.localRotation;
                    binding.IsHips = transform == targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    byTransform.Add(transform, binding);
                    boundBones.Add(binding);
                }
                binding.SourceNames.Add(sourceName);
            }

            BindBothEyes(byTransform);
            EnsureRootBinding(byTransform);
            BindLegIks();
            BindMorphs();
            isPrepared = true;
        }

        private void BindBothEyes(Dictionary<Transform, BoundBone> byTransform)
        {
            if (!sampler.HasBoneTrack("両目")) return;
            AddTrackToBone(byTransform, targetAnimator.GetBoneTransform(HumanBodyBones.LeftEye), "両目");
            AddTrackToBone(byTransform, targetAnimator.GetBoneTransform(HumanBodyBones.RightEye), "両目");
        }

        private void EnsureRootBinding(Dictionary<Transform, BoundBone> byTransform)
        {
            if (!sampler.HasBoneTrack("全ての親") && !sampler.HasBoneTrack("センター") && !sampler.HasBoneTrack("グルーブ")) return;
            Transform hips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || byTransform.ContainsKey(hips)) return;

            BoundBone binding = new BoundBone();
            binding.Transform = hips;
            binding.BaseLocalPosition = hips.localPosition;
            binding.BaseLocalRotation = hips.localRotation;
            binding.IsHips = true;
            byTransform.Add(hips, binding);
            boundBones.Add(binding);
        }

        private void AddTrackToBone(Dictionary<Transform, BoundBone> byTransform, Transform transform, string sourceName)
        {
            if (transform == null) return;
            BoundBone binding;
            if (!byTransform.TryGetValue(transform, out binding))
            {
                binding = new BoundBone();
                binding.Transform = transform;
                binding.BaseLocalPosition = transform.localPosition;
                binding.BaseLocalRotation = transform.localRotation;
                byTransform.Add(transform, binding);
                boundBones.Add(binding);
            }
            binding.SourceNames.Add(sourceName);
        }

        private string FindFirstTrack(string[] aliases)
        {
            for (int index = 0; index < aliases.Length; index++)
            {
                if (sampler.HasBoneTrack(aliases[index])) return aliases[index];
            }
            return null;
        }

        private Vector3 SampleRootTranslation(float frameNumber)
        {
            Vector3 result = Vector3.zero;
            result += SamplePosition("全ての親", frameNumber);
            result += SamplePosition("センター", frameNumber);
            result += SamplePosition("グルーブ", frameNumber);
            return result * positionScale;
        }

        private Quaternion SampleRootRotation(float frameNumber)
        {
            Quaternion result = Quaternion.identity;
            result = result * SampleRotation("全ての親", frameNumber);
            result = result * SampleRotation("センター", frameNumber);
            result = result * SampleRotation("グルーブ", frameNumber);
            return result;
        }

        private Vector3 SamplePosition(string trackName, float frameNumber)
        {
            VmdBonePose pose;
            return sampler.TrySampleBone(trackName, frameNumber, out pose) ? ConvertPosition(pose.Position) : Vector3.zero;
        }

        private Quaternion SampleRotation(string trackName, float frameNumber)
        {
            VmdBonePose pose;
            return sampler.TrySampleBone(trackName, frameNumber, out pose) ? ConvertRotation(pose.Rotation) : Quaternion.identity;
        }

        private Vector3 ConvertPosition(Vector3 value)
        {
            return convertMmdCoordinates ? new Vector3(-value.x, value.y, -value.z) : value;
        }

        private Quaternion ConvertRotation(Quaternion value)
        {
            return convertMmdCoordinates
                ? new Quaternion(-value.x, value.y, -value.z, value.w)
                : value;
        }

        private void BindLegIks()
        {
            BindLegIk(
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes,
                new[] { "左足ＩＫ", "左足IK", "LeftLegIK" },
                new[] { "左つま先ＩＫ", "左つま先IK", "左爪先ＩＫ", "LeftToeIK" });
            BindLegIk(
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes,
                new[] { "右足ＩＫ", "右足IK", "RightLegIK" },
                new[] { "右つま先ＩＫ", "右つま先IK", "右爪先ＩＫ", "RightToeIK" });
        }

        private void BindLegIk(
            HumanBodyBones upperLegBone,
            HumanBodyBones lowerLegBone,
            HumanBodyBones footBone,
            HumanBodyBones toesBone,
            string[] legAliases,
            string[] toeAliases)
        {
            string legTrackName = FindFirstTrack(legAliases);
            string toeTrackName = FindFirstTrack(toeAliases);
            if (legTrackName == null && toeTrackName == null) return;

            Transform upperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform lowerLeg = targetAnimator.GetBoneTransform(lowerLegBone);
            Transform foot = targetAnimator.GetBoneTransform(footBone);
            if (upperLeg == null || lowerLeg == null || foot == null) return;

            Transform ikSpace = targetAnimator.transform;
            Transform toes = targetAnimator.GetBoneTransform(toesBone);
            BoundLegIk binding = new BoundLegIk();
            binding.UpperLeg = upperLeg;
            binding.LowerLeg = lowerLeg;
            binding.Foot = foot;
            binding.Toes = toes;
            binding.IkSpace = ikSpace;
            binding.LegTrackName = legTrackName;
            binding.ToeTrackName = toeTrackName;
            binding.BaseFootPositionInIkSpace = ikSpace.InverseTransformPoint(foot.position);
            binding.BaseFootRotationInIkSpace = Quaternion.Inverse(ikSpace.rotation) * foot.rotation;
            binding.BasePolePositionInIkSpace = ikSpace.InverseTransformPoint(lowerLeg.position);
            binding.BaseUpperLegRotation = upperLeg.localRotation;
            binding.BaseLowerLegRotation = lowerLeg.localRotation;
            binding.BaseFootLocalRotation = foot.localRotation;
            if (toes != null)
            {
                binding.BaseToePositionInIkSpace = ikSpace.InverseTransformPoint(toes.position);
                binding.BaseToesLocalRotation = toes.localRotation;
            }
            boundLegIks.Add(binding);
        }

        private void ApplyLegIkAtFrame(float frameNumber)
        {
            LastSolvedIkCount = 0;
            LastIkErrorBefore = 0f;
            LastIkErrorAfter = 0f;
            if ((!applyLegIk && !applyToeIk) || boundLegIks.Count == 0) return;

            Vector3 allParentPosition = SamplePosition("全ての親", frameNumber) * positionScale;
            Quaternion allParentRotation = SampleRotation("全ての親", frameNumber);
            for (int index = 0; index < boundLegIks.Count; index++)
            {
                BoundLegIk leg = boundLegIks[index];
                if (leg.IkSpace == null || leg.UpperLeg == null || leg.LowerLeg == null || leg.Foot == null) continue;

                if (applyLegIk && leg.LegTrackName != null && IsIkEnabled(leg.LegTrackName, frameNumber))
                {
                    VmdBonePose pose;
                    if (sampler.TrySampleBone(leg.LegTrackName, frameNumber, out pose))
                    {
                        Vector3 targetLocal = allParentPosition + allParentRotation *
                            (leg.BaseFootPositionInIkSpace + ConvertPosition(pose.Position) * positionScale);
                        Vector3 targetWorld = leg.IkSpace.TransformPoint(targetLocal);
                        Vector3 poleLocal = allParentPosition + allParentRotation * leg.BasePolePositionInIkSpace;
                        Vector3 poleWorld = leg.IkSpace.TransformPoint(poleLocal);
                        float before = Vector3.Distance(leg.Foot.position, targetWorld);
                        SolveTwoBone(leg.UpperLeg, leg.LowerLeg, leg.Foot, targetWorld, poleWorld, leg.IkSpace.forward);
                        leg.Foot.rotation = leg.IkSpace.rotation * allParentRotation *
                            leg.BaseFootRotationInIkSpace * ConvertRotation(pose.Rotation);
                        float after = Vector3.Distance(leg.Foot.position, targetWorld);
                        LastIkErrorBefore += before;
                        LastIkErrorAfter += after;
                        LastSolvedIkCount++;
                    }
                }

                if (applyToeIk && leg.Toes != null && leg.ToeTrackName != null && IsIkEnabled(leg.ToeTrackName, frameNumber))
                {
                    VmdBonePose toePose;
                    if (sampler.TrySampleBone(leg.ToeTrackName, frameNumber, out toePose))
                    {
                        Vector3 targetLocal = allParentPosition + allParentRotation *
                            (leg.BaseToePositionInIkSpace + ConvertPosition(toePose.Position) * positionScale);
                        Vector3 targetWorld = leg.IkSpace.TransformPoint(targetLocal);
                        float before = Vector3.Distance(leg.Toes.position, targetWorld);
                        SolveOneBone(leg.Foot, leg.Toes, targetWorld);
                        float after = Vector3.Distance(leg.Toes.position, targetWorld);
                        LastIkErrorBefore += before;
                        LastIkErrorAfter += after;
                        LastSolvedIkCount++;
                    }
                }
            }
        }

        private bool IsIkEnabled(string ikName, float frameNumber)
        {
            bool enabled;
            return !sampler.TrySampleIkEnabled(ikName, frameNumber, out enabled) || enabled;
        }

        private static void SolveTwoBone(
            Transform upper,
            Transform lower,
            Transform end,
            Vector3 target,
            Vector3 pole,
            Vector3 fallbackPoleDirection)
        {
            Vector3 rootPosition = upper.position;
            Vector3 lowerPosition = lower.position;
            Vector3 endPosition = end.position;
            float upperLength = Vector3.Distance(rootPosition, lowerPosition);
            float lowerLength = Vector3.Distance(lowerPosition, endPosition);
            Vector3 toTarget = target - rootPosition;
            float rawDistance = toTarget.magnitude;
            if (upperLength < 0.000001f || lowerLength < 0.000001f || rawDistance < 0.000001f) return;

            Vector3 direction = toTarget / rawDistance;
            float minimumDistance = Mathf.Abs(upperLength - lowerLength) + 0.00001f;
            float maximumDistance = upperLength + lowerLength - 0.00001f;
            float distance = Mathf.Clamp(rawDistance, minimumDistance, maximumDistance);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float heightSquared = Mathf.Max(0f, upperLength * upperLength - along * along);
            float height = Mathf.Sqrt(heightSquared);

            Vector3 poleDirection = ProjectOnPlane(pole - rootPosition, direction);
            if (poleDirection.sqrMagnitude < 0.000001f)
            {
                poleDirection = ProjectOnPlane(fallbackPoleDirection, direction);
            }
            if (poleDirection.sqrMagnitude < 0.000001f)
            {
                poleDirection = ProjectOnPlane(Vector3.right, direction);
            }
            float poleMagnitude = Mathf.Sqrt(poleDirection.sqrMagnitude);
            if (poleMagnitude > 0.000001f) poleDirection /= poleMagnitude;

            Vector3 desiredLowerPosition = rootPosition + direction * along + poleDirection * height;
            Vector3 currentUpperDirection = lower.position - rootPosition;
            Vector3 desiredUpperDirection = desiredLowerPosition - rootPosition;
            if (currentUpperDirection.sqrMagnitude > 0.000001f && desiredUpperDirection.sqrMagnitude > 0.000001f)
            {
                upper.rotation = Quaternion.FromToRotation(currentUpperDirection, desiredUpperDirection) * upper.rotation;
            }

            Vector3 reachableTarget = rootPosition + direction * distance;
            Vector3 currentLowerDirection = end.position - lower.position;
            Vector3 desiredLowerDirection = reachableTarget - lower.position;
            if (currentLowerDirection.sqrMagnitude > 0.000001f && desiredLowerDirection.sqrMagnitude > 0.000001f)
            {
                lower.rotation = Quaternion.FromToRotation(currentLowerDirection, desiredLowerDirection) * lower.rotation;
            }
        }

        private static void SolveOneBone(Transform root, Transform end, Vector3 target)
        {
            Vector3 currentDirection = end.position - root.position;
            Vector3 desiredDirection = target - root.position;
            if (currentDirection.sqrMagnitude < 0.000001f || desiredDirection.sqrMagnitude < 0.000001f) return;
            root.rotation = Quaternion.FromToRotation(currentDirection, desiredDirection) * root.rotation;
        }

        private static Vector3 ProjectOnPlane(Vector3 vector, Vector3 unitPlaneNormal)
        {
            return vector - unitPlaneNormal * Vector3.Dot(vector, unitPlaneNormal);
        }

        private void BindMorphs()
        {
            SkinnedMeshRenderer[] renderers = targetAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (string morphName in sampler.MorphNames)
            {
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    SkinnedMeshRenderer renderer = renderers[rendererIndex];
                    Mesh mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    int bestScore = VmdMorphNameMatcher.NoMatch;
                    List<int> bestShapeIndices = new List<int>();
                    for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                    {
                        string shapeName = mesh.GetBlendShapeName(shapeIndex);
                        int score = VmdMorphNameMatcher.GetMatchScore(shapeName, morphName);
                        if (score <= VmdMorphNameMatcher.NoMatch || score < bestScore) continue;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestShapeIndices.Clear();
                        }
                        bestShapeIndices.Add(shapeIndex);
                    }
                    for (int index = 0; index < bestShapeIndices.Count; index++)
                        AddMorphBinding(morphName, renderer, bestShapeIndices[index]);
                }
            }
        }

        private void AddMorphBinding(string morphName, SkinnedMeshRenderer renderer, int shapeIndex)
        {
            for (int index = 0; index < boundMorphs.Count; index++)
            {
                BoundMorph existing = boundMorphs[index];
                if (existing.Renderer != renderer || existing.BlendShapeIndex != shapeIndex) continue;
                if (!existing.MorphNames.Contains(morphName)) existing.MorphNames.Add(morphName);
                return;
            }
            BoundMorph binding = new BoundMorph();
            binding.MorphNames.Add(morphName);
            binding.Renderer = renderer;
            binding.BlendShapeIndex = shapeIndex;
            binding.BaseWeight = renderer.GetBlendShapeWeight(shapeIndex);
            boundMorphs.Add(binding);
        }

        private void ApplyMorphsAtFrame(float frameNumber)
        {
            for (int index = 0; index < boundMorphs.Count; index++)
            {
                BoundMorph binding = boundMorphs[index];
                if (binding.Renderer == null) continue;
                float maximumWeight = 0f;
                bool sampled = false;
                for (int nameIndex = 0; nameIndex < binding.MorphNames.Count; nameIndex++)
                {
                    float weight;
                    if (!sampler.TrySampleMorph(binding.MorphNames[nameIndex], frameNumber, out weight)) continue;
                    maximumWeight = Mathf.Max(maximumWeight, weight);
                    sampled = true;
                }
                if (sampled)
                    binding.Renderer.SetBlendShapeWeight(binding.BlendShapeIndex, Mathf.Clamp01(maximumWeight) * 100f);
            }
        }

        private void ApplyCameraAtFrame(float frameNumber)
        {
            if (targetCamera == null) return;
            VmdCameraPose pose;
            if (!sampler.TrySampleCamera(frameNumber, out pose)) return;
            VmdUnityCameraPose unityPose;
            if (!VmdCameraConverter.TryConvert(pose, positionScale, convertMmdCoordinates, out unityPose)) return;
            unityPose.Position *= Mathf.Clamp(cameraDistanceScale, 0.1f, 10f);

            if (cameraOrigin != null)
            {
                targetCamera.transform.position = cameraOrigin.TransformPoint(unityPose.Position);
                targetCamera.transform.rotation = cameraOrigin.rotation * unityPose.Rotation;
            }
            else
            {
                targetCamera.transform.localPosition = unityPose.Position;
                targetCamera.transform.localRotation = unityPose.Rotation;
            }
            targetCamera.fieldOfView = unityPose.FieldOfView;
            if (CameraOrthographicProperty != null && CameraOrthographicProperty.CanWrite)
            {
                CameraOrthographicProperty.SetValue(targetCamera, !unityPose.Perspective, null);
            }
            HasAppliedCameraPose = true;
            LastAppliedCameraPerspective = unityPose.Perspective;
            LastAppliedCameraFrame = frameNumber;
        }

        private void RaiseCompleted()
        {
            Action handler = PlaybackCompleted;
            if (handler != null) handler();
        }

        private sealed class BoundBone
        {
            public Transform Transform;
            public Vector3 BaseLocalPosition;
            public Quaternion BaseLocalRotation;
            public bool IsHips;
            public readonly List<string> SourceNames = new List<string>();
        }

        private sealed class BoundMorph
        {
            public readonly List<string> MorphNames = new List<string>();
            public SkinnedMeshRenderer Renderer;
            public int BlendShapeIndex;
            public float BaseWeight;
        }

        private sealed class BoundLegIk
        {
            public Transform UpperLeg;
            public Transform LowerLeg;
            public Transform Foot;
            public Transform Toes;
            public Transform IkSpace;
            public string LegTrackName;
            public string ToeTrackName;
            public Vector3 BaseFootPositionInIkSpace;
            public Quaternion BaseFootRotationInIkSpace;
            public Vector3 BaseToePositionInIkSpace;
            public Vector3 BasePolePositionInIkSpace;
            public Quaternion BaseUpperLegRotation;
            public Quaternion BaseLowerLegRotation;
            public Quaternion BaseFootLocalRotation;
            public Quaternion BaseToesLocalRotation;
        }
    }
}
