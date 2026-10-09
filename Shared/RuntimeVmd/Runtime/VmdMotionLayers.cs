using System;
using System.Collections.Generic;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Deterministically combines VMD files on one timeline. A later layer replaces
    /// complete bone/morph tracks with the same normalized MMD name. Camera, light,
    /// self-shadow and IK/property sections are each replaced when the later layer
    /// contains that section.
    /// </summary>
    public static class VmdMotionLayers
    {
        public static VmdMotion Merge(IList<VmdMotion> layers, out VmdMotionMergeReport report)
        {
            if (layers == null) throw new ArgumentNullException("layers");
            if (layers.Count == 0) throw new ArgumentException("At least one VMD layer is required.", "layers");
            for (int index = 0; index < layers.Count; index++)
                if (layers[index] == null) throw new ArgumentException("VMD layer " + index + " is null.", "layers");

            VmdMotion result = new VmdMotion();
            result.Header = layers[0].Header;
            result.ModelName = layers[0].ModelName;
            report = new VmdMotionMergeReport { LayerCount = layers.Count };

            CopyAll(layers[0], result);
            for (int layerIndex = 1; layerIndex < layers.Count; layerIndex++)
            {
                VmdMotion overlay = layers[layerIndex];
                report.BoneTrackOverrides += ReplaceBoneTracks(result, overlay);
                report.MorphTrackOverrides += ReplaceMorphTracks(result, overlay);
                if (overlay.CameraFrames.Count > 0)
                {
                    if (result.CameraFrames.Count > 0) report.CameraSectionOverrides++;
                    result.CameraFrames.Clear();
                    result.CameraFrames.AddRange(overlay.CameraFrames);
                }
                if (overlay.LightFrames.Count > 0)
                {
                    if (result.LightFrames.Count > 0) report.LightSectionOverrides++;
                    result.LightFrames.Clear();
                    result.LightFrames.AddRange(overlay.LightFrames);
                }
                if (overlay.SelfShadowFrames.Count > 0)
                {
                    if (result.SelfShadowFrames.Count > 0) report.SelfShadowSectionOverrides++;
                    result.SelfShadowFrames.Clear();
                    result.SelfShadowFrames.AddRange(overlay.SelfShadowFrames);
                }
                if (overlay.IkFrames.Count > 0)
                {
                    if (result.IkFrames.Count > 0) report.IkSectionOverrides++;
                    result.IkFrames.Clear();
                    result.IkFrames.AddRange(overlay.IkFrames);
                }
            }

            result.MaxFrameNumber = FindMaximumFrame(result);
            return result;
        }

        private static int ReplaceBoneTracks(VmdMotion target, VmdMotion overlay)
        {
            if (overlay.BoneFrames.Count == 0) return 0;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < overlay.BoneFrames.Count; index++)
                names.Add(VmdMotionSampler.NormalizeMmdName(overlay.BoneFrames[index].BoneName));
            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < target.BoneFrames.Count; index++)
            {
                string name = VmdMotionSampler.NormalizeMmdName(target.BoneFrames[index].BoneName);
                if (names.Contains(name)) existing.Add(name);
            }
            target.BoneFrames.RemoveAll(delegate(VmdBoneKeyframe frame)
            {
                return names.Contains(VmdMotionSampler.NormalizeMmdName(frame.BoneName));
            });
            target.BoneFrames.AddRange(overlay.BoneFrames);
            return existing.Count;
        }

        private static int ReplaceMorphTracks(VmdMotion target, VmdMotion overlay)
        {
            if (overlay.MorphFrames.Count == 0) return 0;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < overlay.MorphFrames.Count; index++)
                names.Add(VmdMotionSampler.NormalizeMmdName(overlay.MorphFrames[index].MorphName));
            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < target.MorphFrames.Count; index++)
            {
                string name = VmdMotionSampler.NormalizeMmdName(target.MorphFrames[index].MorphName);
                if (names.Contains(name)) existing.Add(name);
            }
            target.MorphFrames.RemoveAll(delegate(VmdMorphKeyframe frame)
            {
                return names.Contains(VmdMotionSampler.NormalizeMmdName(frame.MorphName));
            });
            target.MorphFrames.AddRange(overlay.MorphFrames);
            return existing.Count;
        }

        private static void CopyAll(VmdMotion source, VmdMotion target)
        {
            target.BoneFrames.AddRange(source.BoneFrames);
            target.MorphFrames.AddRange(source.MorphFrames);
            target.CameraFrames.AddRange(source.CameraFrames);
            target.LightFrames.AddRange(source.LightFrames);
            target.SelfShadowFrames.AddRange(source.SelfShadowFrames);
            target.IkFrames.AddRange(source.IkFrames);
        }

        private static uint FindMaximumFrame(VmdMotion motion)
        {
            uint maximum = 0;
            for (int index = 0; index < motion.BoneFrames.Count; index++) maximum = Math.Max(maximum, motion.BoneFrames[index].FrameNumber);
            for (int index = 0; index < motion.MorphFrames.Count; index++) maximum = Math.Max(maximum, motion.MorphFrames[index].FrameNumber);
            for (int index = 0; index < motion.CameraFrames.Count; index++) maximum = Math.Max(maximum, motion.CameraFrames[index].FrameNumber);
            for (int index = 0; index < motion.LightFrames.Count; index++) maximum = Math.Max(maximum, motion.LightFrames[index].FrameNumber);
            for (int index = 0; index < motion.SelfShadowFrames.Count; index++) maximum = Math.Max(maximum, motion.SelfShadowFrames[index].FrameNumber);
            for (int index = 0; index < motion.IkFrames.Count; index++) maximum = Math.Max(maximum, motion.IkFrames[index].FrameNumber);
            return maximum;
        }
    }

    public sealed class VmdMotionMergeReport
    {
        public int LayerCount { get; internal set; }
        public int BoneTrackOverrides { get; internal set; }
        public int MorphTrackOverrides { get; internal set; }
        public int CameraSectionOverrides { get; internal set; }
        public int LightSectionOverrides { get; internal set; }
        public int SelfShadowSectionOverrides { get; internal set; }
        public int IkSectionOverrides { get; internal set; }

        public override string ToString()
        {
            return "layers=" + LayerCount
                + "; boneOverrides=" + BoneTrackOverrides
                + "; morphOverrides=" + MorphTrackOverrides
                + "; cameraOverrides=" + CameraSectionOverrides
                + "; lightOverrides=" + LightSectionOverrides
                + "; shadowOverrides=" + SelfShadowSectionOverrides
                + "; ikOverrides=" + IkSectionOverrides;
        }
    }
}
