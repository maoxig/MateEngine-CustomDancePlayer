using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Maoxig.VmdDanceStudio;
using Newtonsoft.Json;
using UnityEngine;
public partial class DanceAudit
{
 private IEnumerator Regression1103Only()
 {
  output=Path.Combine(OutputDirectory(),"regression1103-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();var core=ui.playerCore;core.InitPlayer();core.StopPlay();ui.SetPanelVisible(false);ui.SetCameraEnabled(false);
  string model=Environment.GetCommandLineArgs().Contains("--cdp-model-kokoro")?"E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me":Environment.GetCommandLineArgs().Contains("--cdp-model-ayrina")?"Ayrina":"C:/Users/xp/AppData/LocalLow/Shinymoon/MateEngineX/Steam Workshop/YYBMikuV2.me";
  UnityEngine.Object.FindFirstObjectByType<VRMLoader>().LoadVRM(model);yield return new WaitForSecondsRealtime(3);
  var data=DanceSettingsHandler.Instance.data;data.keepVmdRootUpright=true;data.lockVmdFacingForward=false;data.enableVmdFootIk=true;data.danceVolume=0;
  var logs=new List<object>();var actor=ui.avatarHelper.CurrentAvatar.transform;var originalScale=actor.localScale;
  string root="F:/BaiduNetdiskDownload/MMD与模型/KiiiKiii - 404 (New Era) (Solo Motion) by Winter_Jirachee";
  var roles=new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
  foreach(string variant in new[]{"MV","STAGE"})
  {
   string motion=Directory.GetFiles(root,"*Motion - "+variant+" VERSION.vmd").Single();
   var draft=new VmdDanceDraft{Id="regression1103-"+variant,Title="404 "+variant,MotionVmd=motion,CameraVmd=Path.Combine(root,"Camera.vmd"),PositionScale=.08f};
   string package=Path.Combine(ui.resourceManager.LibraryFolder,"Regression1103",draft.Id+".vmdance");VmdDancePackageBuilder.Build(draft,package);
   core.StopPlay();actor.localScale=originalScale;ui.RefreshAndPlayPackage(package);yield return Wait1103();if(!core.Paused)core.TogglePause();
   var player=core.CurrentVmdPlayer;Record("load-"+variant,player!=null&&player.IsLoaded,core.LastError);if(player==null)continue;
   core.Seek(0);yield return new WaitForEndOfFrame();float firstYaw=player.LastAppliedBodyYawFromRest;
   Record("authored-first-yaw-"+variant,Math.Abs(Mathf.DeltaAngle(89.04f,firstYaw))<2f,new{firstYaw,player.LastSourceBodyYawFromRest});yield return Capture("404-"+variant+"-first");
   var bones=roles.Select(ui.avatarHelper.CurrentAnimator.GetBoneTransform).ToArray();var baseline=new Dictionary<float,Vector3[]>();
   var times=new[]{0f,1f,5f,15f,30f,60f};
   foreach(float time in times){core.Seek(time/core.Duration);yield return new WaitForEndOfFrame();baseline[time]=bones.Select(b=>actor.InverseTransformPoint(b.position)).ToArray();}
   foreach(float size in new[]{.25f,.1f,.03f,.01f,.003f})
   {
    actor.localScale=originalScale*size;float maximum=0;int solved=2;
    foreach(float time in times){core.Seek(time/core.Duration);yield return new WaitForEndOfFrame();var positions=bones.Select(b=>actor.InverseTransformPoint(b.position)).ToArray();for(int i=0;i<positions.Length;i++)maximum=Mathf.Max(maximum,Vector3.Distance(positions[i],baseline[time][i]));solved=Math.Min(solved,player.LastTargetLegIkSolvedCount);logs.Add(new{variant,size,time,maximum,solved,support=Vec(player.LastSupportRootCorrection),positions=positions.Select(Vec).ToArray()});}
    Record("scale-invariant-"+variant+"-"+size,maximum<.003f&&solved==2,new{maximum,solved});
   }
   actor.localScale=originalScale;
   float denseMaximum=0;int denseCount=0;
   for(int sample=0;sample<=240;sample++)
   {
    float time=Mathf.Min(core.Duration-.01f,sample*.5f);actor.localScale=originalScale;core.Seek(time/core.Duration);
    var normal=bones.Select(b=>actor.InverseTransformPoint(b.position)).ToArray();
    foreach(float size in new[]{.01f,.003f}){actor.localScale=originalScale*size;core.Seek(time/core.Duration);for(int i=0;i<bones.Length;i++)denseMaximum=Mathf.Max(denseMaximum,Vector3.Distance(normal[i],actor.InverseTransformPoint(bones[i].position)));denseCount++;}
    if(sample%24==0)yield return null;
   }
   Record("dense-small-scale-"+variant,denseMaximum<.003f,new{denseMaximum,denseCount});actor.localScale=originalScale;
   core.StopPlay();actor.localScale=originalScale*.01f;ui.RefreshAndPlayPackage(package);yield return Wait1103();if(!core.Paused)core.TogglePause();
   core.Seek(15f/core.Duration);yield return new WaitForEndOfFrame();float reloadError=0;for(int i=0;i<bones.Length;i++)reloadError=Mathf.Max(reloadError,Vector3.Distance(actor.InverseTransformPoint(bones[i].position),baseline[15f][i]));
   Record("initially-small-"+variant,reloadError<.003f,new{reloadError});core.StopPlay();actor.localScale=originalScale;
   ui.RefreshAndPlayPackage(package);yield return Wait1103();if(!core.Paused)core.TogglePause();ui.SetCameraEnabled(false);yield return null;
   var camera=Camera.main;var before=camera.transform.position;var rotation=camera.transform.rotation;var projection=camera.projectionMatrix;
   foreach(float time in new[]{0f,5f,25f}){core.Seek(time/core.Duration);yield return new WaitForEndOfFrame();Record("disabled-vmd-camera-"+variant+"-"+time,Math.Abs(before.x-camera.transform.position.x)<.001f&&Math.Abs(before.y-camera.transform.position.y)<.001f&&QuaternionDegrees(rotation,camera.transform.rotation)<.01f&&MatrixDifference(projection,camera.projectionMatrix)<.001f,new{time,angle=QuaternionDegrees(rotation,camera.transform.rotation),projection=MatrixDifference(projection,camera.projectionMatrix)});}
   ui.SetCameraEnabled(true);core.Seek(10f/core.Duration);yield return new WaitForEndOfFrame();Record("enabled-vmd-camera-"+variant,core.HasRuntimeCameraPose&&ui.CameraSync.IsUsingDanceView);
   ui.SetCameraEnabled(false);
   core.StopPlay();actor.localScale=originalScale;
  }
  var synthetic=new VmdMotion{ModelName="Orientation regression"};
  var angles=new[]{30f,90f,-45f};
  for(int i=0;i<angles.Length;i++)synthetic.BoneFrames.Add(new VmdBoneKeyframe{BoneName="全ての親",FrameNumber=(uint)(i*30),Position=Vector3.zero,Rotation=Quaternion.Euler(0,angles[i],0)});
  string syntheticPath=Path.Combine(output,"authored-root.vmd");File.WriteAllBytes(syntheticPath,VmdWriter.Write(synthetic));
  string syntheticPackage=Path.Combine(ui.resourceManager.LibraryFolder,"Regression1103","authored-root.vmdance");VmdDancePackageBuilder.Build(new VmdDanceDraft{Id="authored-root",MotionVmd=syntheticPath},syntheticPackage);
  ui.RefreshAndPlayPackage(syntheticPackage);yield return Wait1103();if(!core.Paused)core.TogglePause();
  for(int i=0;i<angles.Length;i++){core.SetVmdRootOptions(true,false);core.Seek(i/core.Duration);Record("authored-allparent-yaw-"+i,Math.Abs(Mathf.DeltaAngle(angles[i],core.CurrentVmdPlayer.LastAppliedBodyYawFromRest))<.1f,new{expected=angles[i],actual=core.CurrentVmdPlayer.LastAppliedBodyYawFromRest});core.SetVmdRootOptions(true,true);core.Seek(i/core.Duration);Record("explicit-facing-lock-"+i,Math.Abs(core.CurrentVmdPlayer.LastAppliedBodyYawFromRest)<.1f);}
  core.SetVmdRootOptions(true,false);core.StopPlay();
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var legacy=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("MMD-CatchTheWave.unity3d"));core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(legacy.Id));yield return Wait1103();if(!core.Paused)core.TogglePause();ui.SetCameraEnabled(false);yield return null;
  var main=Camera.main;var position=main.transform.position;var facing=main.transform.rotation;var matrix=main.projectionMatrix;
  foreach(float time in new[]{5f,30f,60f}){core.Seek(time/core.Duration);yield return new WaitForEndOfFrame();Record("disabled-legacy-camera-"+time,Math.Abs(position.x-main.transform.position.x)<.001f&&Math.Abs(position.y-main.transform.position.y)<.001f&&QuaternionDegrees(facing,main.transform.rotation)<.01f&&MatrixDifference(matrix,main.projectionMatrix)<.001f,new{time,delta=Vector3.Distance(position,main.transform.position),angle=QuaternionDegrees(facing,main.transform.rotation),projection=MatrixDifference(matrix,main.projectionMatrix)});}
  ui.SetCameraEnabled(true);core.Seek(30f/core.Duration);yield return new WaitForEndOfFrame();Record("enabled-legacy-camera",ui.CameraSync.IsUsingDanceView);
  ui.SetCameraEnabled(false);var restored=main.projectionMatrix;var restoredRotation=main.transform.rotation;core.Seek(5f/core.Duration);yield return new WaitForEndOfFrame();Record("disabled-after-enabled-camera",!ui.CameraSync.IsUsingDanceView&&QuaternionDegrees(restoredRotation,main.transform.rotation)<.01f&&MatrixDifference(restored,main.projectionMatrix)<.001f);
  core.StopPlay();File.WriteAllText(Path.Combine(output,"details.json"),JsonConvert.SerializeObject(logs,Formatting.Indented));File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("REGRESSION1103_COMPLETE");
 }
 private IEnumerator Wait1103(){float until=Time.realtimeSinceStartup+60;while(ui.playerCore.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForSecondsRealtime(.6f);}
}
