using System;
using System.Collections.Generic;
using System.IO;

namespace Maoxig.VmdDanceStudio
{
    public enum VmdTrackKind
    {
        Invalid,
        Motion,
        FaceOrLip,
        Camera,
        LightOrShadow,
        Mixed
    }

    public sealed class VmdTrackInfo
    {
        public string Path { get; set; }
        public string ModelName { get; set; }
        public long FileSize { get; set; }
        public uint BoneKeys { get; set; }
        public uint MorphKeys { get; set; }
        public uint CameraKeys { get; set; }
        public uint LightKeys { get; set; }
        public uint ShadowKeys { get; set; }
        public uint IkFrames { get; set; }
        public uint IkToggles { get; set; }
        public uint IkDisableToggles { get; set; }
        public uint MaximumFrame { get; set; }
        public string Error { get; set; }
        public List<string> BoneNames { get; set; } = new List<string>();
        public List<string> MorphNames { get; set; } = new List<string>();
        public List<string> IkNames { get; set; } = new List<string>();
        public bool HasFk { get; set; }
        public int FkBoneNameCount { get; set; }
        public bool HasRootFk { get; set; }
        public bool HasTorsoFk { get; set; }
        public bool HasLeftArmFk { get; set; }
        public bool HasRightArmFk { get; set; }
        public bool HasLeftLegFk { get; set; }
        public bool HasRightLegFk { get; set; }
        public bool HasFingerFk { get; set; }
        public bool HasEyeFk { get; set; }
        public bool HasLegIk { get; set; }
        public bool HasToeIk { get; set; }
        public bool HasLipMorph { get; set; }
        public bool HasExpressionMorph { get; set; }
        internal VmdTrackInfo Copy()
        {
            var copy=(VmdTrackInfo)MemberwiseClone();
            copy.BoneNames=new List<string>(BoneNames);copy.MorphNames=new List<string>(MorphNames);copy.IkNames=new List<string>(IkNames);
            return copy;
        }

        public bool IsValid { get { return string.IsNullOrEmpty(Error); } }
        public float DurationSeconds { get { return MaximumFrame / 30f; } }
        public int FkCoverageScore
        {
            get
            {
                int score = 0;
                if (HasRootFk) score++;
                if (HasTorsoFk) score++;
                if (HasLeftArmFk) score++;
                if (HasRightArmFk) score++;
                if (HasLeftLegFk) score++;
                if (HasRightLegFk) score++;
                return score;
            }
        }
        public bool HasFullBodyFk
        {
            get
            {
                return HasRootFk && HasTorsoFk && HasLeftArmFk && HasRightArmFk
                    && HasLeftLegFk && HasRightLegFk;
            }
        }
        public string FkScope
        {
            get
            {
                if (!HasFk) return string.Empty;
                if (HasFullBodyFk) return "Full-body FK";
                if (HasFingerFk && FkCoverageScore <= 2) return "Hand/finger FK";
                if (HasEyeFk && FkCoverageScore <= 1) return "Eye/gaze FK";
                if (HasRootFk && FkCoverageScore <= 1 && FkBoneNameCount <= 2) return "Root adjustment";
                if (FkCoverageScore >= 4) return "Body-partial FK";
                return "Partial FK";
            }
        }

        public VmdTrackKind Kind
        {
            get
            {
                if (!IsValid) return VmdTrackKind.Invalid;
                int categories = 0;
                if (BoneKeys > 0 || IkFrames > 0) categories++;
                if (MorphKeys > 0) categories++;
                if (CameraKeys > 0) categories++;
                if (LightKeys > 0 || ShadowKeys > 0) categories++;
                if (categories > 1) return VmdTrackKind.Mixed;
                if (BoneKeys > 0 || IkFrames > 0) return VmdTrackKind.Motion;
                if (MorphKeys > 0) return VmdTrackKind.FaceOrLip;
                if (CameraKeys > 0) return VmdTrackKind.Camera;
                if (LightKeys > 0 || ShadowKeys > 0) return VmdTrackKind.LightOrShadow;
                return VmdTrackKind.Invalid;
            }
        }

        public override string ToString()
        {
            return System.IO.Path.GetFileName(Path) + "  [" + Features + "; bones=" + BoneKeys
                + ", morphs=" + MorphKeys + ", camera=" + CameraKeys + ", light=" + LightKeys
                + ", shadow=" + ShadowKeys + ", IK=" + IkFrames + "/off=" + IkDisableToggles + "]";
        }

        public string Features
        {
            get
            {
                List<string> values = new List<string>();
                if (HasFk) values.Add(FkScope);
                if (HasLegIk) values.Add("Leg IK");
                if (HasToeIk) values.Add("Toe IK");
                if (HasLipMorph) values.Add("Lip");
                if (HasExpressionMorph) values.Add("Expression");
                else if (MorphKeys > 0 && !HasLipMorph) values.Add("Morph");
                if (CameraKeys > 0) values.Add("Camera");
                if (LightKeys > 0) values.Add("Light");
                if (ShadowKeys > 0) values.Add("Shadow");
                if (IkFrames > 0) values.Add("IK toggle");
                if (IkDisableToggles > 0) values.Add("IK off");
                return values.Count == 0 ? Kind.ToString() : string.Join(", ", values.ToArray());
            }
        }
    }

    public sealed class VmdWorkspaceGroup
    {
        public string DirectoryPath { get; set; }
        public List<VmdTrackInfo> Tracks { get; set; } = new List<VmdTrackInfo>();
        public List<string> AudioFiles { get; set; } = new List<string>();
        public List<string> PmxFiles { get; set; } = new List<string>();

        public string Name { get { return new DirectoryInfo(DirectoryPath).Name; } }
        public bool HasMotion { get { return HasTrack(track => track.BoneKeys > 0); } }
        public bool HasCamera { get { return HasTrack(track => track.CameraKeys > 0); } }
        public bool HasMorphs { get { return HasTrack(track => track.MorphKeys > 0); } }
        public bool HasAudio { get { return AudioFiles.Count > 0; } }

        private bool HasTrack(Func<VmdTrackInfo, bool> predicate)
        {
            for (int index = 0; index < Tracks.Count; index++) if (predicate(Tracks[index])) return true;
            return false;
        }

        public string Summary
        {
            get
            {
                return Name + "  —  VMD " + Tracks.Count + ", audio " + AudioFiles.Count
                    + (HasMotion ? ", motion" : string.Empty)
                    + (HasMorphs ? ", morph" : string.Empty)
                    + (HasCamera ? ", camera" : string.Empty);
            }
        }
    }

    public sealed class VmdDanceDraft
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Credits { get; set; }
        public string MotionVmd { get; set; }
        public string FaceVmd { get; set; }
        public string LipVmd { get; set; }
        public string CameraVmd { get; set; }
        public List<string> AdditionalVmdFiles { get; set; } = new List<string>();
        public string AudioFile { get; set; }
        public string ReferencePmx { get; set; }
        public float AudioOffsetSeconds { get; set; }
        public float? CameraReferenceEyeHeight { get; set; }
        public float? CameraReferenceBodyHeight { get; set; }
        public float? CameraAuthoringScale { get; set; }
        public float PositionScale { get; set; } = 0.08f;
        public bool Loop { get; set; }
        public bool? FootIk { get; set; }
        public bool PreserveSlotTracks { get; set; }
        public string OriginalPackageRoot { get; set; }
        public string OriginalManifestJson { get; set; }
        public string OriginalFaceVmd { get; set; }
        public string OriginalLipVmd { get; set; }
        public string OriginalCameraVmd { get; set; }
    }

    public sealed class VmdDanceBuildResult
    {
        public string OutputPath { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Entries { get; set; } = new List<string>();
    }
}
