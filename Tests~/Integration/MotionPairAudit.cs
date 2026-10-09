using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomDancePlayer;
using Newtonsoft.Json;
using UnityEngine;

public partial class DanceAudit
{
 private IEnumerator MotionPairOnly()
 {
  output=Path.Combine(OutputDirectory(),"motion-pair-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(false);
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  foreach(string id in new[]{"MMD-CatchTheWave.unity3d","03-catch-the-wave"})
  {
   var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains(id));
   core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(4);if(!core.Paused)core.TogglePause();
   if(core.CurrentVmdPlayer!=null&&Environment.GetCommandLineArgs().Contains("--cdp-converter-reference"))
    core.CurrentVmdPlayer.Load("F:/Program Files/7D2D/MMD2VRM/Assets/UnityMMDConverter/Model/Default.pmx",ui.resourceManager.CurrentVmdPath,ui.resourceManager.CurrentVmdOverlayPaths);
   if(core.CurrentVmdPlayer!=null&&Environment.GetCommandLineArgs().Contains("--cdp-canonical-reference")) {
    core.CurrentVmdPlayer.DirectBoneRetargeting=false;core.CurrentVmdPlayer.TargetCalibratedRetargeting=false;core.CurrentVmdPlayer.KeepBodyUpright=false;
   }
   var animator=ui.avatarHelper.CurrentAnimator;var root=animator.transform;var roles=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
   var bones=roles.Select(animator.GetBoneTransform).ToArray();var samples=new List<object>();
   float end=Mathf.Min(core.Duration,180);
   for(int sample=0;sample<=end*120;sample++)
   {
    float time=sample/120f;core.Seek(time/core.Duration);
    var positions=bones.Select(b=>{var p=root.InverseTransformPoint(b.position);return new[]{p.x,p.y,p.z};}).ToArray();
    var rotations=bones.Select(b=>new[]{b.localRotation.x,b.localRotation.y,b.localRotation.z,b.localRotation.w}).ToArray();
    var correction=core.CurrentVmdPlayer==null?Vector3.zero:root.worldToLocalMatrix.MultiplyVector(core.CurrentVmdPlayer.LastSupportRootCorrection);
    var source=core.CurrentVmdPlayer==null?Vector3.zero:core.CurrentVmdPlayer.LastSourceBodyDeltaNormalized;
    samples.Add(new{time,positions,rotations,support=new[]{correction.x,correction.y,correction.z},sourceRoot=new[]{source.x,source.y,source.z}});
    if(sample%120==0)yield return null;
    if(sample==1200||sample==2400||sample==3600)yield return Capture((id.EndsWith("unity3d")?"unity":"vmd")+"-"+time.ToString("0"));
   }
   File.WriteAllText(Path.Combine(output,id.EndsWith("unity3d")?"unity.json":"vmd.json"),JsonConvert.SerializeObject(new{descriptor.Id,model=root.name,duration=core.Duration,reference=core.CurrentVmdPlayer==null?null:core.CurrentVmdPlayer.ReferencePmxPath,clip=ui.resourceManager.CurrentAnimationClip==null?null:ui.resourceManager.CurrentAnimationClip.name,height=ui.avatarHelper.MeasureAvatarHeight(),samples}));
   core.StopPlay();yield return null;
  }
  Logger.LogInfo("MOTION_PAIR_AUDIT_COMPLETE");
 }
}
