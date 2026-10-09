using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public struct VmdBezierCurve
    {
        public readonly byte X1;
        public readonly byte Y1;
        public readonly byte X2;
        public readonly byte Y2;

        public VmdBezierCurve(byte x1, byte y1, byte x2, byte y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public static VmdBezierCurve Linear
        {
            get { return new VmdBezierCurve(20, 20, 107, 107); }
        }
    }

    public struct VmdBoneInterpolation
    {
        public readonly VmdBezierCurve X;
        public readonly VmdBezierCurve Y;
        public readonly VmdBezierCurve Z;
        public readonly VmdBezierCurve Rotation;

        public VmdBoneInterpolation(
            VmdBezierCurve x,
            VmdBezierCurve y,
            VmdBezierCurve z,
            VmdBezierCurve rotation)
        {
            X = x;
            Y = y;
            Z = z;
            Rotation = rotation;
        }
    }

    public struct VmdCameraInterpolation
    {
        public readonly VmdBezierCurve X;
        public readonly VmdBezierCurve Y;
        public readonly VmdBezierCurve Z;
        public readonly VmdBezierCurve Rotation;
        public readonly VmdBezierCurve Distance;
        public readonly VmdBezierCurve FieldOfView;

        public VmdCameraInterpolation(
            VmdBezierCurve x,
            VmdBezierCurve y,
            VmdBezierCurve z,
            VmdBezierCurve rotation,
            VmdBezierCurve distance,
            VmdBezierCurve fieldOfView)
        {
            X = x;
            Y = y;
            Z = z;
            Rotation = rotation;
            Distance = distance;
            FieldOfView = fieldOfView;
        }
    }

    public sealed class VmdBoneKeyframe
    {
        public string BoneName;
        public uint FrameNumber;
        public Vector3 Position;
        public Quaternion Rotation;
        public VmdBoneInterpolation Interpolation;
    }

    public sealed class VmdMorphKeyframe
    {
        public string MorphName;
        public uint FrameNumber;
        public float Weight;
    }

    public sealed class VmdCameraKeyframe
    {
        public uint FrameNumber;
        public float Distance;
        public Vector3 Position;
        public Vector3 RotationRadians;
        public VmdCameraInterpolation Interpolation;
        public uint FieldOfView;
        public bool Perspective;
    }

    public sealed class VmdLightKeyframe
    {
        public uint FrameNumber;
        public Color Color;
        public Vector3 Position;
    }

    public struct VmdLightPose
    {
        public Color Color;
        public Vector3 Direction;

        public VmdLightPose(Color color, Vector3 direction)
        {
            Color = color;
            Direction = direction;
        }
    }

    public sealed class VmdSelfShadowKeyframe
    {
        public uint FrameNumber;
        public byte Mode;
        public float Distance;
    }

    public struct VmdSelfShadowPose
    {
        public byte Mode;
        public float Distance;

        public VmdSelfShadowPose(byte mode, float distance)
        {
            Mode = mode;
            Distance = distance;
        }
    }

    public sealed class VmdIkToggle
    {
        public string Name;
        public bool Enabled;
    }

    public sealed class VmdIkKeyframe
    {
        public uint FrameNumber;
        public bool Visible;
        public readonly List<VmdIkToggle> Toggles = new List<VmdIkToggle>();
    }

    public sealed class VmdMotion
    {
        public const float DefaultFramesPerSecond = 30f;

        public string Header;
        public string ModelName;
        public readonly List<VmdBoneKeyframe> BoneFrames = new List<VmdBoneKeyframe>();
        public readonly List<VmdMorphKeyframe> MorphFrames = new List<VmdMorphKeyframe>();
        public readonly List<VmdCameraKeyframe> CameraFrames = new List<VmdCameraKeyframe>();
        public readonly List<VmdLightKeyframe> LightFrames = new List<VmdLightKeyframe>();
        public readonly List<VmdSelfShadowKeyframe> SelfShadowFrames = new List<VmdSelfShadowKeyframe>();
        public readonly List<VmdIkKeyframe> IkFrames = new List<VmdIkKeyframe>();

        public uint MaxFrameNumber { get; internal set; }

        public float Duration
        {
            get { return MaxFrameNumber / DefaultFramesPerSecond; }
        }
    }

    public struct VmdBonePose
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public VmdBonePose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public struct VmdCameraPose
    {
        public Vector3 Position;
        public Vector3 RotationRadians;
        public float Distance;
        public float FieldOfView;
        public bool Perspective;
    }
}
