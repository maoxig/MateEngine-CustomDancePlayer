using System.Collections.Generic;
using Maoxig.RuntimeVmd;
using UnityEngine;

namespace CustomDancePlayer
{
    [DefaultExecutionOrder(10000)]
    public sealed class DanceLegacyExpressionDriver:MonoBehaviour
    {
        private SkinnedMeshRenderer source;
        private VmdExpressionBindings bindings;
        private readonly Dictionary<string,float> sampled=new Dictionary<string,float>();
        private System.Func<string,float?> sample;
        public void Bind(SkinnedMeshRenderer renderer,VmdExpressionBindings value)
        {
            source=renderer;bindings=value;sampled.Clear();sample=name=>{float weight;return sampled.TryGetValue(name,out weight)?(float?)weight:null;};
            var driver=GetComponent<VmdExpressionLateDriver>()??gameObject.AddComponent<VmdExpressionLateDriver>();driver.LegacyCommit=Commit;
            enabled=true;
        }
        private void LateUpdate()
        {
            if(source==null||source.sharedMesh==null)return;
            var mesh=source.sharedMesh;for(int i=0;i<mesh.blendShapeCount;i++)sampled[mesh.GetBlendShapeName(i)]=source.GetBlendShapeWeight(i)*0.01f;
        }
        private void Commit(){if(isActiveAndEnabled&&source!=null&&bindings!=null)bindings.Apply(sample);}
        private void OnDisable(){if(bindings!=null)bindings.Restore();sampled.Clear();}
    }
}
