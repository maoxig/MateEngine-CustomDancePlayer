using System.Collections.Generic;
using UnityEngine;
namespace Maoxig.RuntimeVmd {
 // Avatar.skeleton can retain pre-prefab scales. Capture imported internal
 // bone scales once; the Animator root remains the host display-size control.
 public sealed class VmdRigScaleSnapshot : MonoBehaviour {
  private readonly Dictionary<Transform,Vector3> scales=new Dictionary<Transform,Vector3>();
  public static VmdRigScaleSnapshot Capture(Animator animator){var snapshot=animator.GetComponent<VmdRigScaleSnapshot>();if(snapshot!=null)return snapshot;snapshot=animator.gameObject.AddComponent<VmdRigScaleSnapshot>();snapshot.hideFlags=HideFlags.DontSave;foreach(var node in animator.GetComponentsInChildren<Transform>(true))if(node!=animator.transform)snapshot.scales[node]=node.localScale;return snapshot;}
  public Vector3 ScaleFor(Transform bone,Vector3 fallback)=>scales.TryGetValue(bone,out var scale)?scale:fallback;
 }
}
