using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    // UniVRM 1.x evaluates expressions and spring bones at order 11000.
    // Commit only the dance's face afterward; do not re-evaluate body/physics.
    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    public sealed class VmdExpressionLateDriver : MonoBehaviour
    {
        internal VmdNativePmxPlayer Owner;
        public System.Action LegacyCommit;
        private void LateUpdate()
        {
            if(Owner!=null&&Owner.isActiveAndEnabled)Owner.ApplyLateExpressions();
            else if(LegacyCommit!=null)LegacyCommit();
        }
    }
}
