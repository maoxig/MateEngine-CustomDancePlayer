using System.Collections.Generic;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    internal sealed class VmdHumanoidMapEntry
    {
        public readonly HumanBodyBones Bone;
        public readonly string[] VmdNames;
        public readonly HumanBodyBones FallbackBone;

        public VmdHumanoidMapEntry(HumanBodyBones bone, params string[] vmdNames)
            : this(bone, HumanBodyBones.LastBone, vmdNames)
        {
        }

        public VmdHumanoidMapEntry(HumanBodyBones bone, HumanBodyBones fallbackBone, params string[] vmdNames)
        {
            Bone = bone;
            FallbackBone = fallbackBone;
            VmdNames = vmdNames;
        }
    }

    internal static class VmdHumanoidMap
    {
        public static readonly IList<VmdHumanoidMapEntry> Entries = new VmdHumanoidMapEntry[]
        {
            new VmdHumanoidMapEntry(HumanBodyBones.Hips, "下半身", "lower body", "LowerBody", "腰", "waist", "Waist", "hips", "Hips"),
            new VmdHumanoidMapEntry(HumanBodyBones.Spine, "上半身", "upper body", "UpperBody", "spine", "Spine"),
            new VmdHumanoidMapEntry(HumanBodyBones.Chest, HumanBodyBones.Spine, "上半身2", "上半身２", "upper body2", "UpperBody2", "chest", "Chest"),
            new VmdHumanoidMapEntry(HumanBodyBones.UpperChest, HumanBodyBones.Chest, "上半身3", "上半身３", "upper body3", "UpperBody3", "upper chest", "UpperChest"),
            new VmdHumanoidMapEntry(HumanBodyBones.Neck, HumanBodyBones.Head, "首", "neck", "Neck"),
            new VmdHumanoidMapEntry(HumanBodyBones.Head, "頭", "head", "Head"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftEye, "左目", "left eye", "LeftEye"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightEye, "右目", "right eye", "RightEye"),
            new VmdHumanoidMapEntry(HumanBodyBones.Jaw, "あご", "顎", "jaw", "Jaw"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, "左肩", "left shoulder", "LeftShoulder"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, "右肩", "right shoulder", "RightShoulder"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftUpperArm, "左腕", "left arm", "LeftArm", "LeftUpperArm"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightUpperArm, "右腕", "right arm", "RightArm", "RightUpperArm"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftLowerArm, "左ひじ", "左肘", "left elbow", "LeftElbow", "LeftLowerArm"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightLowerArm, "右ひじ", "右肘", "right elbow", "RightElbow", "RightLowerArm"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftHand, "左手首", "left wrist", "LeftWrist", "LeftHand"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightHand, "右手首", "right wrist", "RightWrist", "RightHand"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftUpperLeg, "左足D", "左足Ｄ", "左足", "left leg", "LeftLeg", "LeftUpperLeg"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightUpperLeg, "右足D", "右足Ｄ", "右足", "right leg", "RightLeg", "RightUpperLeg"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftLowerLeg, "左ひざD", "左膝D", "左ひざＤ", "左ひざ", "左膝", "left knee", "LeftKnee", "LeftLowerLeg"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightLowerLeg, "右ひざD", "右膝D", "右ひざＤ", "右ひざ", "右膝", "right knee", "RightKnee", "RightLowerLeg"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftFoot, "左足首D", "左足首Ｄ", "左足首", "left ankle", "LeftAnkle", "LeftFoot"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightFoot, "右足首D", "右足首Ｄ", "右足首", "right ankle", "RightAnkle", "RightFoot"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot, "左つま先EX", "左つま先", "left toe", "LeftToe", "LeftToes"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightToes, HumanBodyBones.RightFoot, "右つま先EX", "右つま先", "right toe", "RightToe", "RightToes"),

            new VmdHumanoidMapEntry(HumanBodyBones.LeftThumbProximal, "左親指0", "左親指０", "left thumb0", "LeftThumb0"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftThumbIntermediate, "左親指1", "左親指１", "left thumb1", "LeftThumb1"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftThumbDistal, "左親指2", "左親指２", "left thumb2", "LeftThumb2"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightThumbProximal, "右親指0", "右親指０", "right thumb0", "RightThumb0"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightThumbIntermediate, "右親指1", "右親指１", "right thumb1", "RightThumb1"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightThumbDistal, "右親指2", "右親指２", "right thumb2", "RightThumb2"),

            new VmdHumanoidMapEntry(HumanBodyBones.LeftIndexProximal, "左人指1", "左人指１", "左人差指1", "左人差指１", "left index1", "LeftIndex1"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftIndexIntermediate, "左人指2", "左人指２", "左人差指2", "左人差指２", "left index2", "LeftIndex2"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftIndexDistal, "左人指3", "左人指３", "左人差指3", "左人差指３", "left index3", "LeftIndex3"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightIndexProximal, "右人指1", "右人指１", "右人差指1", "右人差指１", "right index1", "RightIndex1"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightIndexIntermediate, "右人指2", "右人指２", "右人差指2", "右人差指２", "right index2", "RightIndex2"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightIndexDistal, "右人指3", "右人指３", "右人差指3", "右人差指３", "right index3", "RightIndex3"),

            new VmdHumanoidMapEntry(HumanBodyBones.LeftMiddleProximal, "左中指1", "左中指１", "left middle1", "LeftMiddle1"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftMiddleIntermediate, "左中指2", "左中指２", "left middle2", "LeftMiddle2"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftMiddleDistal, "左中指3", "左中指３", "left middle3", "LeftMiddle3"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightMiddleProximal, "右中指1", "右中指１", "right middle1", "RightMiddle1"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightMiddleIntermediate, "右中指2", "右中指２", "right middle2", "RightMiddle2"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightMiddleDistal, "右中指3", "右中指３", "right middle3", "RightMiddle3"),

            new VmdHumanoidMapEntry(HumanBodyBones.LeftRingProximal, "左薬指1", "左薬指１", "left ring1", "LeftRing1"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftRingIntermediate, "左薬指2", "左薬指２", "left ring2", "LeftRing2"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftRingDistal, "左薬指3", "左薬指３", "left ring3", "LeftRing3"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightRingProximal, "右薬指1", "右薬指１", "right ring1", "RightRing1"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightRingIntermediate, "右薬指2", "右薬指２", "right ring2", "RightRing2"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightRingDistal, "右薬指3", "右薬指３", "right ring3", "RightRing3"),

            new VmdHumanoidMapEntry(HumanBodyBones.LeftLittleProximal, "左小指1", "左小指１", "left little1", "LeftLittle1"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftLittleIntermediate, "左小指2", "左小指２", "left little2", "LeftLittle2"),
            new VmdHumanoidMapEntry(HumanBodyBones.LeftLittleDistal, "左小指3", "左小指３", "left little3", "LeftLittle3"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightLittleProximal, "右小指1", "右小指１", "right little1", "RightLittle1"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightLittleIntermediate, "右小指2", "右小指２", "right little2", "RightLittle2"),
            new VmdHumanoidMapEntry(HumanBodyBones.RightLittleDistal, "右小指3", "右小指３", "right little3", "RightLittle3")
        };
    }
}
