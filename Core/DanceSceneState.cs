using System.Collections.Generic;
using UnityEngine;
namespace CustomDancePlayer
{
    internal sealed class DanceSceneState
    {
        private struct Pose { public Transform target; public Vector3 position,scale; public Quaternion rotation; }
        private readonly List<Pose> poses=new List<Pose>();
        private Transform avatarRoot;
        private Camera camera; private Vector3 cameraPosition;private Quaternion cameraRotation;private float fov;
        public static DanceSceneState Capture(Animator animator)
        {
            var result=new DanceSceneState();result.avatarRoot=animator.transform;result.Add(animator.transform);
            if(animator.isHuman)for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var bone=animator.GetBoneTransform((HumanBodyBones)i);if(bone!=null)result.Add(bone);}
            result.camera=Camera.main;
            if(result.camera!=null){result.cameraPosition=result.camera.transform.position;result.cameraRotation=result.camera.transform.rotation;result.fov=result.camera.fieldOfView;}
            return result;
        }
        private void Add(Transform target) {poses.Add(new Pose{target=target,position=target.localPosition,rotation=target.localRotation,scale=target.localScale});}
        public void Restore()
        {
            foreach(var pose in poses)if(pose.target!=null){pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;if(pose.target!=avatarRoot)pose.target.localScale=pose.scale;}
            if(camera!=null){camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.fieldOfView=fov;}
        }
    }
}
