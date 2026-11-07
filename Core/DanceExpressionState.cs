using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Maoxig.RuntimeVmd;
using UniVRM10;
using UnityEngine;
using VRM;

namespace CustomDancePlayer
{
    // A playback lease must restore the user's face as well as the controller.
    internal sealed class DanceExpressionState
    {
        private static readonly string[] FaceChannels =
        {
            "Blink", "Blink_L", "Blink_R", "LookUp", "LookDown", "LookLeft", "LookRight",
            "Neutral", "A", "I", "U", "E", "O", "Joy", "Angry", "Sorrow", "Fun", "Surprised"
        };
        private readonly Dictionary<SkinnedMeshRenderer, float[]> weights = new Dictionary<SkinnedMeshRenderer, float[]>();
        private readonly Dictionary<FieldInfo, float> inputs = new Dictionary<FieldInfo, float>();
        private readonly Dictionary<VRMBlendShapeProxy, KeyValuePair<BlendShapeKey, float>[]> vrm0 =
            new Dictionary<VRMBlendShapeProxy, KeyValuePair<BlendShapeKey, float>[]>();
        private readonly Dictionary<Vrm10RuntimeExpression, KeyValuePair<ExpressionKey, float>[]> vrm1 =
            new Dictionary<Vrm10RuntimeExpression, KeyValuePair<ExpressionKey, float>[]>();
        private readonly UniversalBlendshapes universal;

        private DanceExpressionState(UniversalBlendshapes universal) { this.universal = universal; }

        public static DanceExpressionState Capture(GameObject avatar, UniversalBlendshapes universal)
        {
            var state = new DanceExpressionState(universal);
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) state.weights[smr] = Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Select(smr.GetBlendShapeWeight).ToArray();
            if (universal != null)
                foreach (string channel in FaceChannels)
                {
                    var field = typeof(UniversalBlendshapes).GetField(channel, BindingFlags.Instance | BindingFlags.Public);
                    if (field?.FieldType == typeof(float)) state.inputs[field] = (float)field.GetValue(universal);
                }
            foreach (var proxy in avatar.GetComponentsInChildren<VRMBlendShapeProxy>(true))
            {
                var values = proxy.GetValues();
                if (values != null) state.vrm0[proxy] = values.Where(pair => pair.Key.Preset != BlendShapePreset.Unknown).ToArray();
            }
            foreach (var instance in avatar.GetComponentsInChildren<Vrm10Instance>(true))
            {
                var expression = instance.Runtime?.Expression;
                if (expression != null)
                    state.vrm1[expression] = expression.GetWeights().Where(pair => pair.Key.Preset != ExpressionPreset.custom).ToArray();
            }
            return state;
        }

        public void ClearHostFace(Animator animator)
        {
            if (universal != null) foreach (var field in inputs.Keys) field.SetValue(universal, 0f);
            foreach (var pair in vrm0)
                if (pair.Key != null && pair.Value.Length > 0)
                    pair.Key.SetValues(pair.Value.Select(weight => new KeyValuePair<BlendShapeKey, float>(weight.Key, 0f)));
            foreach (var pair in vrm1)
                foreach (var weight in pair.Value)
                    if (weight.Value != 0f) pair.Key.SetWeight(weight.Key, 0f);

            // Animator curves can leave weights behind without a VRM input.
            // Clear face targets even when this dance has no corresponding track.
            VmdExpressionBindings.ClearFaceTargets(animator, FaceChannels);
        }

        public void Restore()
        {
            if (universal != null) foreach (var pair in inputs) pair.Key.SetValue(universal, pair.Value);
            foreach (var pair in vrm0)
                if (pair.Key != null && pair.Value.Length > 0) pair.Key.SetValues(pair.Value);
            foreach (var pair in vrm1) pair.Key.SetWeights(pair.Value);
            foreach (var pair in weights) if (pair.Key != null && pair.Key.sharedMesh != null)
                for (int i = 0; i < Mathf.Min(pair.Value.Length, pair.Key.sharedMesh.blendShapeCount); i++) pair.Key.SetBlendShapeWeight(i, pair.Value[i]);
        }
    }
}
