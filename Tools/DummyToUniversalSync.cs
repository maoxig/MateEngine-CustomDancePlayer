using UnityEngine;

namespace CustomDancePlayer
{
    [RequireComponent(typeof(UniversalBlendshapes))]
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

            if (dummySmr == null || dummyMesh == null || ub == null) return;

            for (int i = 0; i < dummyMesh.blendShapeCount; i++)
            {
                string bsName = dummyMesh.GetBlendShapeName(i);
                float weight01 = dummySmr.GetBlendShapeWeight(i) / 100f;

                foreach (var (mmd, setter) in map)
                {
                    if (bsName.Contains(mmd))
                    {
                        setter(ub, weight01);
                    }
                }
            }
        }
    }
}