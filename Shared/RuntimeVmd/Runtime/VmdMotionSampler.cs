using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public sealed class VmdMotionSampler
    {
        private readonly Dictionary<string, List<VmdBoneKeyframe>> boneTracks;
        private readonly Dictionary<string, List<VmdBoneKeyframe>> normalizedBoneTracks;
        private readonly Dictionary<string, List<VmdMorphKeyframe>> morphTracks;
        private readonly Dictionary<string, List<VmdMorphKeyframe>> normalizedMorphTracks;
        private readonly Dictionary<string, List<IkToggleKeyframe>> ikTracks;
        private readonly List<VmdCameraKeyframe> cameraTrack;
        private readonly bool[] cameraCuts;
        private readonly List<VmdLightKeyframe> lightTrack;
        private readonly List<VmdSelfShadowKeyframe> selfShadowTrack;

        public VmdMotion Motion { get; private set; }

        public IEnumerable<string> BoneNames
        {
            get { return boneTracks.Keys; }
        }

        public IEnumerable<string> MorphNames
        {
            get { return morphTracks.Keys; }
        }

        public IEnumerable<string> IkNames
        {
            get { return ikTracks.Keys; }
        }

        public VmdMotionSampler(VmdMotion motion)
        {
            if (motion == null) throw new ArgumentNullException("motion");
            Motion = motion;
            boneTracks = GroupAndSort(motion.BoneFrames, delegate(VmdBoneKeyframe frame) { return frame.BoneName; }, delegate(VmdBoneKeyframe frame) { return frame.FrameNumber; });
            normalizedBoneTracks = BuildNormalizedLookup(boneTracks);
            morphTracks = GroupAndSort(motion.MorphFrames, delegate(VmdMorphKeyframe frame) { return frame.MorphName; }, delegate(VmdMorphKeyframe frame) { return frame.FrameNumber; });
            normalizedMorphTracks = BuildNormalizedLookup(morphTracks);
            ikTracks = BuildIkTracks(motion.IkFrames);
            cameraTrack = new List<VmdCameraKeyframe>(motion.CameraFrames);
            cameraTrack.Sort(delegate(VmdCameraKeyframe left, VmdCameraKeyframe right) { return left.FrameNumber.CompareTo(right.FrameNumber); });
            cameraCuts = BuildCameraCuts();
            lightTrack = new List<VmdLightKeyframe>(motion.LightFrames);
            lightTrack.Sort(delegate(VmdLightKeyframe left, VmdLightKeyframe right) { return left.FrameNumber.CompareTo(right.FrameNumber); });
            selfShadowTrack = new List<VmdSelfShadowKeyframe>(motion.SelfShadowFrames);
            selfShadowTrack.Sort(delegate(VmdSelfShadowKeyframe left, VmdSelfShadowKeyframe right) { return left.FrameNumber.CompareTo(right.FrameNumber); });
        }

        public bool HasBoneTrack(string boneName)
        {
            List<VmdBoneKeyframe> ignored;
            return TryResolveTrack(boneTracks, normalizedBoneTracks, boneName, out ignored);
        }

        public bool HasMorphTrack(string morphName)
        {
            List<VmdMorphKeyframe> ignored;
            return TryResolveTrack(morphTracks, normalizedMorphTracks, morphName, out ignored);
        }

        public bool TrySampleBone(string boneName, float frameNumber, out VmdBonePose pose)
        {
            List<VmdBoneKeyframe> track;
            if (!TryResolveTrack(boneTracks, normalizedBoneTracks, boneName, out track) || track.Count == 0)
            {
                pose = new VmdBonePose(Vector3.zero, Quaternion.identity);
                return false;
            }

            int previousIndex = FindPreviousIndex(track, frameNumber, delegate(VmdBoneKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0)
            {
                VmdBoneKeyframe first = track[0];
                pose = new VmdBonePose(first.Position, first.Rotation);
                return true;
            }
            if (previousIndex >= track.Count - 1)
            {
                VmdBoneKeyframe last = track[track.Count - 1];
                pose = new VmdBonePose(last.Position, last.Rotation);
                return true;
            }

            VmdBoneKeyframe previous = track[previousIndex];
            VmdBoneKeyframe next = track[previousIndex + 1];
            float span = next.FrameNumber - previous.FrameNumber;
            float time = span <= 0f ? 1f : Mathf.Clamp01((frameNumber - previous.FrameNumber) / span);
            Vector3 position = new Vector3(
                Mathf.LerpUnclamped(previous.Position.x, next.Position.x, VmdBezier.Evaluate(next.Interpolation.X, time)),
                Mathf.LerpUnclamped(previous.Position.y, next.Position.y, VmdBezier.Evaluate(next.Interpolation.Y, time)),
                Mathf.LerpUnclamped(previous.Position.z, next.Position.z, VmdBezier.Evaluate(next.Interpolation.Z, time)));
            Quaternion rotation = SlerpUnclampedManaged(
                previous.Rotation,
                EnsureShortestPath(previous.Rotation, next.Rotation),
                VmdBezier.Evaluate(next.Interpolation.Rotation, time));
            pose = new VmdBonePose(position, rotation);
            return true;
        }

        public bool TrySampleMorph(string morphName, float frameNumber, out float weight)
        {
            List<VmdMorphKeyframe> track;
            if (!TryResolveTrack(morphTracks, normalizedMorphTracks, morphName, out track) || track.Count == 0)
            {
                weight = 0f;
                return false;
            }

            int previousIndex = FindPreviousIndex(track, frameNumber, delegate(VmdMorphKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0)
            {
                weight = track[0].Weight;
                return true;
            }
            if (previousIndex >= track.Count - 1)
            {
                weight = track[track.Count - 1].Weight;
                return true;
            }

            VmdMorphKeyframe previous = track[previousIndex];
            VmdMorphKeyframe next = track[previousIndex + 1];
            float span = next.FrameNumber - previous.FrameNumber;
            float time = span <= 0f ? 1f : Mathf.Clamp01((frameNumber - previous.FrameNumber) / span);
            weight = Mathf.LerpUnclamped(previous.Weight, next.Weight, time);
            return true;
        }

        public bool TrySampleCamera(float frameNumber, out VmdCameraPose pose)
        {
            if (cameraTrack.Count == 0)
            {
                pose = default(VmdCameraPose);
                return false;
            }

            int previousIndex = FindPreviousIndex(cameraTrack, frameNumber, delegate(VmdCameraKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0)
            {
                pose = ToPose(cameraTrack[0]);
                return true;
            }
            if (previousIndex >= cameraTrack.Count - 1)
            {
                pose = ToPose(cameraTrack[cameraTrack.Count - 1]);
                return true;
            }

            VmdCameraKeyframe previous = cameraTrack[previousIndex];
            VmdCameraKeyframe next = cameraTrack[previousIndex + 1];
            float span = next.FrameNumber - previous.FrameNumber;
            // Preserve isolated one-frame cuts. Dense baked camera tracks also
            // contain one-frame intervals; interpolate their continuous motion
            // at render cadence instead of holding every other sample at 30 Hz.
            float time = cameraCuts[previousIndex]
                ? (frameNumber >= next.FrameNumber ? 1f : 0f)
                : Mathf.Clamp01((frameNumber - previous.FrameNumber) / Mathf.Max(1f, span));

            pose = new VmdCameraPose();
            pose.Position = new Vector3(
                Mathf.LerpUnclamped(previous.Position.x, next.Position.x, VmdBezier.Evaluate(next.Interpolation.X, time)),
                Mathf.LerpUnclamped(previous.Position.y, next.Position.y, VmdBezier.Evaluate(next.Interpolation.Y, time)),
                Mathf.LerpUnclamped(previous.Position.z, next.Position.z, VmdBezier.Evaluate(next.Interpolation.Z, time)));
            float rotationTime = VmdBezier.Evaluate(next.Interpolation.Rotation, time);
            pose.RotationRadians = Vector3.LerpUnclamped(previous.RotationRadians, next.RotationRadians, rotationTime);
            pose.Distance = Mathf.LerpUnclamped(previous.Distance, next.Distance, VmdBezier.Evaluate(next.Interpolation.Distance, time));
            pose.FieldOfView = Mathf.LerpUnclamped(previous.FieldOfView, next.FieldOfView, VmdBezier.Evaluate(next.Interpolation.FieldOfView, time));
            pose.Perspective = time < 1f ? previous.Perspective : next.Perspective;
            return true;
        }

        /// <summary>
        /// Returns true when playback crossed a camera cut. Uses the same
        /// classification as sampling so continuous baked keys do not repeatedly
        /// reset the renderer's temporal history.
        /// </summary>
        public bool HasCameraHardCutBetween(float previousFrame, float currentFrame)
        {
            if (cameraTrack.Count < 2 || currentFrame <= previousFrame) return false;
            int index = FindPreviousIndex(cameraTrack, previousFrame,
                delegate(VmdCameraKeyframe frame) { return frame.FrameNumber; });
            if (index < 0) index = 0;
            else if (cameraTrack[index].FrameNumber <= previousFrame) index++;

            for (; index < cameraTrack.Count && cameraTrack[index].FrameNumber <= currentFrame; index++)
            {
                if (index > 0 && cameraCuts[index - 1])
                    return true;
            }
            return false;
        }

        private bool[] BuildCameraCuts()
        {
            var cuts = new bool[Math.Max(0, cameraTrack.Count - 1)];
            for (int index = 0; index < cuts.Length; index++)
            {
                var left = cameraTrack[index]; var right = cameraTrack[index + 1];
                if (right.FrameNumber - left.FrameNumber > 1u) continue;
                cuts[index] = true;
                if (IsLargeCameraStep(left, right)) continue;
                // A nearby run of 1-3 frame samples distinguishes baked motion
                // from the isolated adjacent pair used by hand-keyed cuts.
                int neighbours = 0;
                Vector3 positionSpeed = Vector3.zero, rotationSpeed = Vector3.zero;
                float distanceSpeed = 0f, fovSpeed = 0f;
                for (int edge = Math.Max(0, index - 2); edge <= Math.Min(cuts.Length - 1, index + 2); edge++)
                {
                    if (edge == index) continue;
                    var a = cameraTrack[edge]; var b = cameraTrack[edge + 1];
                    uint span = b.FrameNumber - a.FrameNumber;
                    if (span == 0u || span > 3u || IsLargeCameraStep(a, b)) continue;
                    neighbours++;
                    Vector3 dp = (b.Position - a.Position) / span, dr = (b.RotationRadians - a.RotationRadians) / span;
                    positionSpeed.x = Mathf.Max(positionSpeed.x, Mathf.Abs(dp.x));
                    positionSpeed.y = Mathf.Max(positionSpeed.y, Mathf.Abs(dp.y));
                    positionSpeed.z = Mathf.Max(positionSpeed.z, Mathf.Abs(dp.z));
                    rotationSpeed.x = Mathf.Max(rotationSpeed.x, Mathf.Abs(dr.x));
                    rotationSpeed.y = Mathf.Max(rotationSpeed.y, Mathf.Abs(dr.y));
                    rotationSpeed.z = Mathf.Max(rotationSpeed.z, Mathf.Abs(dr.z));
                    distanceSpeed = Mathf.Max(distanceSpeed, Mathf.Abs(b.Distance - a.Distance) / span);
                    fovSpeed = Mathf.Max(fovSpeed, Mathf.Abs((float)b.FieldOfView - a.FieldOfView) / span);
                }
                if (neighbours < 3) continue;
                Vector3 positionDelta = right.Position - left.Position, rotationDelta = right.RotationRadians - left.RotationRadians;
                // Discontinuous steps inside a baked track remain cuts. Floors
                // are in source MMD units/radians, independent of avatar zoom.
                bool jump = IsCameraJump(positionDelta.x, positionSpeed.x, .2f) ||
                    IsCameraJump(positionDelta.y, positionSpeed.y, .2f) || IsCameraJump(positionDelta.z, positionSpeed.z, .2f) ||
                    IsCameraJump(rotationDelta.x, rotationSpeed.x, .017453293f) ||
                    IsCameraJump(rotationDelta.y, rotationSpeed.y, .017453293f) ||
                    IsCameraJump(rotationDelta.z, rotationSpeed.z, .017453293f) ||
                    IsCameraJump(right.Distance - left.Distance, distanceSpeed, .2f) ||
                    IsCameraJump((float)right.FieldOfView - left.FieldOfView, fovSpeed, 1f);
                cuts[index] = jump;
            }
            return cuts;
        }

        private static bool IsCameraJump(float delta, float neighbouringSpeed, float floor)
        {
            return Mathf.Abs(delta) > Mathf.Max(floor, neighbouringSpeed * 4f);
        }

        private static bool IsLargeCameraStep(VmdCameraKeyframe left, VmdCameraKeyframe right)
        {
            // Conservatively preserve substantial steps, even inside a dense
            // run. Nearby cuts must not inflate the continuous-speed estimate.
            // Position/distance are source MMD units, not displayed avatar size.
            return left.Perspective != right.Perspective || left.Distance * right.Distance < 0f ||
                (right.Position - left.Position).sqrMagnitude > 36f ||
                (right.RotationRadians - left.RotationRadians).sqrMagnitude > .06853892f ||
                Mathf.Abs(right.Distance - left.Distance) > 6f ||
                Mathf.Abs((float)right.FieldOfView - left.FieldOfView) > 8f;
        }

        public bool TrySampleLight(float frameNumber, out VmdLightPose pose)
        {
            if (lightTrack.Count == 0)
            {
                pose = default(VmdLightPose);
                return false;
            }
            int previousIndex = FindPreviousIndex(lightTrack, frameNumber,
                delegate(VmdLightKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0) previousIndex = 0;
            VmdLightKeyframe previous = lightTrack[previousIndex];
            VmdLightKeyframe next = previousIndex + 1 < lightTrack.Count ? lightTrack[previousIndex + 1] : previous;
            float time = InterpolationRatio(previous.FrameNumber, next.FrameNumber, frameNumber);
            pose = new VmdLightPose(
                Color.LerpUnclamped(previous.Color, next.Color, time),
                Vector3.LerpUnclamped(previous.Position, next.Position, time));
            return true;
        }

        public bool TrySampleSelfShadow(float frameNumber, out VmdSelfShadowPose pose)
        {
            if (selfShadowTrack.Count == 0)
            {
                pose = default(VmdSelfShadowPose);
                return false;
            }
            int previousIndex = FindPreviousIndex(selfShadowTrack, frameNumber,
                delegate(VmdSelfShadowKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0) previousIndex = 0;
            VmdSelfShadowKeyframe previous = selfShadowTrack[previousIndex];
            VmdSelfShadowKeyframe next = previousIndex + 1 < selfShadowTrack.Count ? selfShadowTrack[previousIndex + 1] : previous;
            float time = InterpolationRatio(previous.FrameNumber, next.FrameNumber, frameNumber);
            pose = new VmdSelfShadowPose(
                time < 1f ? previous.Mode : next.Mode,
                Mathf.LerpUnclamped(previous.Distance, next.Distance, time));
            return true;
        }

        public bool TrySampleIkEnabled(string ikName, float frameNumber, out bool enabled)
        {
            enabled = true;
            if (string.IsNullOrEmpty(ikName)) return false;
            List<IkToggleKeyframe> track;
            if (!ikTracks.TryGetValue(NormalizeMmdName(ikName), out track) || track.Count == 0) return false;

            int previousIndex = FindPreviousIndex(track, frameNumber, delegate(IkToggleKeyframe frame) { return frame.FrameNumber; });
            if (previousIndex < 0) return false;
            enabled = track[previousIndex].Enabled;
            return true;
        }

        public static string NormalizeMmdName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Trim().Normalize(NormalizationForm.FormKC);
        }

        private static VmdCameraPose ToPose(VmdCameraKeyframe frame)
        {
            VmdCameraPose pose = new VmdCameraPose();
            pose.Position = frame.Position;
            pose.RotationRadians = frame.RotationRadians;
            pose.Distance = frame.Distance;
            pose.FieldOfView = frame.FieldOfView;
            pose.Perspective = frame.Perspective;
            return pose;
        }

        private static Quaternion EnsureShortestPath(Quaternion from, Quaternion to)
        {
            if (Quaternion.Dot(from, to) >= 0f) return to;
            return new Quaternion(-to.x, -to.y, -to.z, -to.w);
        }

        private static Quaternion SlerpUnclampedManaged(Quaternion from, Quaternion to, float time)
        {
            float dot = from.x * to.x + from.y * to.y + from.z * to.z + from.w * to.w;
            if (dot < 0f)
            {
                dot = -dot;
                to = new Quaternion(-to.x, -to.y, -to.z, -to.w);
            }
            dot = Math.Max(-1f, Math.Min(1f, dot));
            if (dot > 0.9995f)
            {
                return NormalizeQuaternion(new Quaternion(
                    from.x + (to.x - from.x) * time,
                    from.y + (to.y - from.y) * time,
                    from.z + (to.z - from.z) * time,
                    from.w + (to.w - from.w) * time));
            }

            float angle = (float)Math.Acos(dot);
            float sine = (float)Math.Sin(angle);
            if (Math.Abs(sine) < 0.000001f) return from;
            float fromWeight = (float)Math.Sin((1f - time) * angle) / sine;
            float toWeight = (float)Math.Sin(time * angle) / sine;
            return NormalizeQuaternion(new Quaternion(
                from.x * fromWeight + to.x * toWeight,
                from.y * fromWeight + to.y * toWeight,
                from.z * fromWeight + to.z * toWeight,
                from.w * fromWeight + to.w * toWeight));
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float magnitude = (float)Math.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 0.000001f) return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }

        private static bool TryResolveTrack<T>(
            Dictionary<string, List<T>> exactTracks,
            Dictionary<string, List<T>> normalizedTracks,
            string name,
            out List<T> track)
        {
            if (string.IsNullOrEmpty(name))
            {
                track = null;
                return false;
            }
            if (exactTracks.TryGetValue(name, out track)) return true;
            return normalizedTracks.TryGetValue(NormalizeMmdName(name), out track);
        }

        private static Dictionary<string, List<T>> BuildNormalizedLookup<T>(Dictionary<string, List<T>> exactTracks)
        {
            Dictionary<string, List<T>> normalized = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<T>> pair in exactTracks)
            {
                string name = NormalizeMmdName(pair.Key);
                if (!normalized.ContainsKey(name)) normalized.Add(name, pair.Value);
            }
            return normalized;
        }

        private static Dictionary<string, List<IkToggleKeyframe>> BuildIkTracks(IEnumerable<VmdIkKeyframe> frames)
        {
            Dictionary<string, List<IkToggleKeyframe>> tracks = new Dictionary<string, List<IkToggleKeyframe>>(StringComparer.Ordinal);
            foreach (VmdIkKeyframe frame in frames)
            {
                for (int index = 0; index < frame.Toggles.Count; index++)
                {
                    VmdIkToggle toggle = frame.Toggles[index];
                    string name = NormalizeMmdName(toggle.Name);
                    List<IkToggleKeyframe> track;
                    if (!tracks.TryGetValue(name, out track))
                    {
                        track = new List<IkToggleKeyframe>();
                        tracks.Add(name, track);
                    }
                    track.Add(new IkToggleKeyframe(frame.FrameNumber, toggle.Enabled));
                }
            }
            foreach (List<IkToggleKeyframe> track in tracks.Values)
            {
                track.Sort(delegate(IkToggleKeyframe left, IkToggleKeyframe right) { return left.FrameNumber.CompareTo(right.FrameNumber); });
            }
            return tracks;
        }

        private static int FindPreviousIndex<T>(List<T> frames, float frameNumber, Func<T, uint> getFrameNumber)
        {
            int lower = 0;
            int upper = frames.Count - 1;
            int result = -1;
            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                if (getFrameNumber(frames[middle]) <= frameNumber)
                {
                    result = middle;
                    lower = middle + 1;
                }
                else
                {
                    upper = middle - 1;
                }
            }
            return result;
        }

        private static float InterpolationRatio(uint previousFrame, uint nextFrame, float frameNumber)
        {
            if (nextFrame <= previousFrame) return 0f;
            uint span = nextFrame - previousFrame;
            if (span <= 1u) return frameNumber >= nextFrame ? 1f : 0f;
            return Mathf.Clamp01((frameNumber - previousFrame) / span);
        }

        private static Dictionary<string, List<T>> GroupAndSort<T>(
            IEnumerable<T> frames,
            Func<T, string> getName,
            Func<T, uint> getFrameNumber)
        {
            Dictionary<string, List<T>> tracks = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            foreach (T frame in frames)
            {
                string name = getName(frame) ?? string.Empty;
                List<T> track;
                if (!tracks.TryGetValue(name, out track))
                {
                    track = new List<T>();
                    tracks.Add(name, track);
                }
                track.Add(frame);
            }

            foreach (List<T> track in tracks.Values)
            {
                track.Sort(delegate(T left, T right) { return getFrameNumber(left).CompareTo(getFrameNumber(right)); });
            }
            return tracks;
        }

        private struct IkToggleKeyframe
        {
            public readonly uint FrameNumber;
            public readonly bool Enabled;

            public IkToggleKeyframe(uint frameNumber, bool enabled)
            {
                FrameNumber = frameNumber;
                Enabled = enabled;
            }
        }
    }
}
