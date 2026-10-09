using UnityEngine;

namespace CustomDancePlayer
{
    [RequireComponent(typeof(UniversalBlendshapes))]
    [DefaultExecutionOrder(-100)]
    public class DummyToUniversalSync : MonoBehaviour
    {
        public SkinnedMeshRenderer dummySmr;

        private UniversalBlendshapes ub;
        private Mesh dummyMesh;

        // Japanese MMD-like blendshape name to UniversalBlendshapes property setter mapping
        private readonly (string mmd, System.Action<UniversalBlendshapes, float> setter)[] map;

        public DummyToUniversalSync()
        {
            map = new (string, System.Action<UniversalBlendshapes, float>)[]
            {
            ("まばたき",   (u, v) => u.Blink = v),
            ("ウィンク",   (u, v) => u.Blink_L = v),
            ("ウィンク２", (u, v) => u.Blink_L = v),
            ("ウィンク右", (u, v) => u.Blink_R = v),
            ("ｳｨﾝｸ２右", (u, v) => u.Blink_R = v),

            ("あ", (u, v) => u.A = v),
            ("い", (u, v) => u.I = v),
            ("う", (u, v) => u.U = v),
            ("え", (u, v) => u.E = v),
            ("お", (u, v) => u.O = v),

            ("にこり", (u, v) => u.Joy = v),
            ("怒り",   (u, v) => u.Angry = v),
            ("困る",   (u, v) => u.Sorrow = v),
            ("真面目", (u, v) => u.Neutral = v),
            ("笑い",  (u, v) => u.Fun = v),
            };
        }

        void Start()
        {
            ub = GetComponent<UniversalBlendshapes>();
            if (ub == null)
            {
                Debug.LogError("UniversalBlendshapes component not found!");
                return;
            }

            if (dummySmr != null)
            {
                dummyMesh = dummySmr.sharedMesh;
            }
        }

        void LateUpdate()
        {
            if (ub == null) ub = GetComponent<UniversalBlendshapes>();
            if (dummySmr == null || dummySmr.sharedMesh == null || ub == null) return;
            dummyMesh = dummySmr.sharedMesh;
            // Several MMD names are aliases of one preset. A zero-valued alias
            // must not overwrite the active one; single vowel names need exact matches.
            ub.Blink = Maximum("まばたき");
            ub.Blink_L = Maximum("ウィンク", "ウィンク２");
            ub.Blink_R = Maximum("ウィンク右", "ｳｨﾝｸ２右");
            ub.A = Maximum("あ"); ub.I = Maximum("い"); ub.U = Maximum("う"); ub.E = Maximum("え"); ub.O = Maximum("お");
            ub.Joy = Maximum("にこり"); ub.Angry = Maximum("怒り"); ub.Sorrow = Maximum("困る"); ub.Neutral = Maximum("真面目"); ub.Fun = Maximum("笑い");
        }

        private float Maximum(params string[] names)
        {
            float value = 0;
            for (int i = 0; i < dummyMesh.blendShapeCount; i++)
            {
                string name = dummyMesh.GetBlendShapeName(i).Normalize(System.Text.NormalizationForm.FormKC);
                foreach (string alias in names) if (name == alias.Normalize(System.Text.NormalizationForm.FormKC))
                    value = Mathf.Max(value, dummySmr.GetBlendShapeWeight(i) / 100f);
            }
            return Mathf.Clamp01(value);
        }
    }
}
