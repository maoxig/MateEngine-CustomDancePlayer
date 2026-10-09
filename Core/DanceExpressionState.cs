using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CustomDancePlayer
{
    // A playback lease must restore the user's face as well as the controller.
    internal sealed class DanceExpressionState
    {
        private readonly Dictionary<SkinnedMeshRenderer, float[]> weights = new Dictionary<SkinnedMeshRenderer, float[]>();
        private readonly Dictionary<FieldInfo, float> inputs = new Dictionary<FieldInfo, float>();
        private UniversalBlendshapes universal;
        public static DanceExpressionState Capture(GameObject avatar)
        {
            var state = new DanceExpressionState();
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) state.weights[smr] = Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Select(smr.GetBlendShapeWeight).ToArray();
            state.universal = avatar.GetComponent<UniversalBlendshapes>();
            if (state.universal != null) foreach (var field in typeof(UniversalBlendshapes).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (field.FieldType == typeof(float)) state.inputs[field] = (float)field.GetValue(state.universal);
            return state;
        }
        public void Restore()
        {
            if (universal != null) foreach (var pair in inputs) pair.Key.SetValue(universal, pair.Value);
            foreach (var pair in weights) if (pair.Key != null && pair.Key.sharedMesh != null)
                for (int i = 0; i < Mathf.Min(pair.Value.Length, pair.Key.sharedMesh.blendShapeCount); i++) pair.Key.SetBlendShapeWeight(i, pair.Value[i]);
        }
    }
}
