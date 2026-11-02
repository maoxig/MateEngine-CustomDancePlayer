using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Maoxig.VmdDanceStudio;
using Newtonsoft.Json;
using UnityEngine;

public partial class DanceAudit
{
 private IEnumerator CameraCadenceOnly()
 {
  output=Path.Combine(OutputDirectory(),"camera-cadence-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();var core=ui.playerCore;core.InitPlayer();core.StopPlay();ui.SetPanelVisible(false);
  UnityEngine.Object.FindFirstObjectByType<VRMLoader>().LoadVRM("C:/Users/xp/AppData/LocalLow/Shinymoon/MateEngineX/Steam Workshop/YYBMikuV2.me");yield return new WaitForSecondsRealtime(3);
  var dense=new VmdMotion();for(uint i=0;i<16;i++)dense.CameraFrames.Add(new VmdCameraKeyframe{FrameNumber=i,Position=new Vector3(i*.1f,14,0),Distance=-30,FieldOfView=30,Perspective=true});
  var sampler=new VmdMotionSampler(dense);VmdCameraPose p;sampler.TrySampleCamera(4.5f,out p);Record("dense-half-frame",Math.Abs(p.Position.x-.45f)<.001f,new{p.Position.x});Record("dense-not-cut",!sampler.HasCameraHardCutBetween(4.5f,5f));
  for(int i=8;i<dense.CameraFrames.Count;i++)dense.CameraFrames[i].Position.x+=20;
  sampler=new VmdMotionSampler(dense);sampler.TrySampleCamera(7.5f,out p);Record("dense-real-cut-holds",Math.Abs(p.Position.x-.7f)<.001f);Record("dense-real-cut-detected",sampler.HasCameraHardCutBetween(7.5f,8f));
  var clustered=new VmdMotion();for(uint i=0;i<12;i++)clustered.CameraFrames.Add(new VmdCameraKeyframe{FrameNumber=i,Position=new Vector3(i%2==0?0:20,14,0),Distance=-30,FieldOfView=30,Perspective=true});
  sampler=new VmdMotionSampler(clustered);sampler.TrySampleCamera(4.5f,out p);Record("clustered-cuts-hold",p.Position.x==0&&sampler.HasCameraHardCutBetween(4.5f,5f));
  var sparse=new VmdMotion();foreach(uint frame in new uint[]{0,30,31,60})sparse.CameraFrames.Add(new VmdCameraKeyframe{FrameNumber=frame,Position=new Vector3(frame<31?0:20,14,0),Distance=-30,FieldOfView=30,Perspective=true});
  sampler=new VmdMotionSampler(sparse);sampler.TrySampleCamera(30.5f,out p);Record("sparse-cut-holds",p.Position.x==0);Record("sparse-cut-detected",sampler.HasCameraHardCutBetween(30.5f,31f));
  var matrix=new List<object>();
  foreach(string localPackage in Directory.GetFiles(Path.Combine(ui.resourceManager.LibraryFolder,"RuntimeVmdPackages"),"*.vmdance"))
  {
   using(var input=File.OpenRead(localPackage))using(var zip=new ZipArchive(input,ZipArchiveMode.Read))
   foreach(var entry in zip.Entries.Where(e=>e.FullName.EndsWith(".vmd",StringComparison.OrdinalIgnoreCase)))
   using(var buffer=new MemoryStream())
   {
    using(var source=entry.Open())source.CopyTo(buffer);var cameraMotion=VmdReader.Read(buffer.ToArray());if(cameraMotion.CameraFrames.Count==0)continue;
    var keys=cameraMotion.CameraFrames.OrderBy(k=>k.FrameNumber).ToArray();var cameraSampler=new VmdMotionSampler(cameraMotion);var changed=new List<object>();float maximumError=0;int originalCuts=0,retainedCuts=0;
    foreach(var key in keys){cameraSampler.TrySampleCamera(key.FrameNumber,out var exact);maximumError=Mathf.Max(maximumError,Vector3.Distance(exact.Position,key.Position),Vector3.Distance(exact.RotationRadians,key.RotationRadians),Math.Abs(exact.Distance-key.Distance),Math.Abs(exact.FieldOfView-key.FieldOfView));}
    for(int i=0;i<keys.Length-1;i++)if(keys[i+1].FrameNumber-keys[i].FrameNumber==1){originalCuts++;if(cameraSampler.HasCameraHardCutBetween(keys[i].FrameNumber+.5f,keys[i+1].FrameNumber))retainedCuts++;else changed.Add(new{frame=keys[i].FrameNumber,positionDelta=Vec(keys[i+1].Position-keys[i].Position),rotationDelta=Vec(keys[i+1].RotationRadians-keys[i].RotationRadians),distanceDelta=keys[i+1].Distance-keys[i].Distance,fovDelta=(float)keys[i+1].FieldOfView-keys[i].FieldOfView});}
    Record("camera-key-values-"+Path.GetFileName(localPackage),maximumError<.0001f,new{maximumError});matrix.Add(new{package=Path.GetFileName(localPackage),entry=entry.FullName,keys=keys.Length,originalCuts,retainedCuts,changed});
    Record("existing-library-cuts-"+Path.GetFileName(localPackage),retainedCuts==originalCuts,new{originalCuts,retainedCuts});
   }
  }
  File.WriteAllText(Path.Combine(output,"library-camera-matrix.json"),JsonConvert.SerializeObject(matrix,Formatting.Indented));
  var root="F:/BaiduNetdiskDownload/MMD与模型/KiiiKiii - 404 (New Era) (Solo Motion) by Winter_Jirachee";
  string path=Path.Combine(ui.resourceManager.LibraryFolder,"Regression1103","cadence404.vmdance");VmdDancePackageBuilder.Build(new VmdDanceDraft{Id="cadence404",MotionVmd=Directory.GetFiles(root,"*Motion - MV VERSION.vmd").Single(),CameraVmd=Path.Combine(root,"Camera.vmd"),PositionScale=.08f},path);
  var reports=new List<object>();
  foreach(string packagePath in new[]{path,Path.Combine(ui.resourceManager.LibraryFolder,"RuntimeVmdPackages/06-primary-star.vmdance")})
  {
   ui.RefreshAndPlayPackage(packagePath);yield return Wait1103();if(!core.Paused)core.TogglePause();ui.SetCameraEnabled(true);yield return null;
   var player=core.CurrentVmdPlayer;var motion=player.Motion;var track=motion.CameraFrames.OrderBy(k=>k.FrameNumber).ToArray();sampler=new VmdMotionSampler(motion);
   int adjacent=0,held=0,cuts=0;foreach(var pair in track.Zip(track.Skip(1),(a,b)=>new{a,b}))if(pair.b.FrameNumber-pair.a.FrameNumber==1){adjacent++;sampler.TrySampleCamera(pair.a.FrameNumber+.25f,out var a);sampler.TrySampleCamera(pair.a.FrameNumber+.75f,out var b);if((a.Position-b.Position).sqrMagnitude<1e-12f&&(a.RotationRadians-b.RotationRadians).sqrMagnitude<1e-12f&&Math.Abs(a.Distance-b.Distance)<1e-6f&&Math.Abs(a.FieldOfView-b.FieldOfView)<1e-6f)held++;if(sampler.HasCameraHardCutBetween(pair.a.FrameNumber+.5f,pair.b.FrameNumber))cuts++;}
   var adjacentStarts=track.Zip(track.Skip(1),(a,b)=>new{a,b}).Where(pair=>pair.b.FrameNumber-pair.a.FrameNumber==1).Select(pair=>pair.a.FrameNumber).ToArray();
   float first=adjacentStarts.Length==0?0:adjacentStarts.OrderByDescending(frame=>adjacentStarts.Count(n=>n>=frame&&n<frame+60)).First();
   bool is404=packagePath==path;
   Record("continuous-dense-keys-"+Path.GetFileName(packagePath),is404?held<adjacent/2:held==adjacent,new{adjacent,held,cuts});
   float keyError=0;foreach(var key in track){sampler.TrySampleCamera(key.FrameNumber,out var exact);keyError=Mathf.Max(keyError,Vector3.Distance(exact.Position,key.Position));}Record("authored-integer-keys-"+Path.GetFileName(packagePath),keyError<.0001f,new{keyError});
   var samples=new List<object>();Vector3 previous=Vector3.zero;int repeated=0;int resets=player.CameraHistoryResetCount;
   for(int i=0;i<=240;i++){float frame=first+i*.25f;core.Seek(frame/30f/core.Duration);yield return new WaitForEndOfFrame();var camera=Camera.main;var eye=camera.transform.position;var hips=ui.avatarHelper.CurrentAvatarHips.position;if(i>0&&Vector3.Distance(eye,previous)<1e-7f)repeated++;previous=eye;samples.Add(new{frame,eye=Vec(eye),hips=Vec(hips),screen=Vec(camera.WorldToScreenPoint(hips)),cameraFrame=player.LastAppliedCameraFrame});}
   Record("camera-body-timeline-"+Path.GetFileName(packagePath),Math.Abs(player.LastAppliedCameraFrame-player.PlaybackTime*30f)<.001f);
   reports.Add(new{package=packagePath,keys=track.Length,adjacent,held,cuts,first,repeated,resets=player.CameraHistoryResetCount-resets,samples});core.StopPlay();
  }
  File.WriteAllText(Path.Combine(output,"details.json"),JsonConvert.SerializeObject(reports,Formatting.Indented));File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("CAMERA_CADENCE_COMPLETE");
 }
}
