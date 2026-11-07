using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Newtonsoft.Json;
using UnityEngine;

public partial class DanceAudit
{
 private IEnumerator CameraCurve1107()
 {
  output=Path.Combine(OutputDirectory(),"camera-curve1107");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();core.StopPlay();ui.SetPanelVisible(false);
  var package=Path.Combine(ui.resourceManager.LibraryFolder,"camera-curve1107-local.vmdance");
  using(var stream=File.OpenRead(package))
  using(var archive=new ZipArchive(stream,ZipArchiveMode.Read))
  using(var buffer=new MemoryStream())
  {
   using(var camera=archive.GetEntry("camera.vmd").Open())camera.CopyTo(buffer);
   byte[] bytes=buffer.ToArray();
   var motion=VmdReader.Read(bytes);
   var keys=motion.CameraFrames.OrderBy(k=>k.FrameNumber).ToArray();
   var sampler=new VmdMotionSampler(motion);
   int tested=0;float maximumError=0;
   for(int i=1;i<keys.Length;i++)
   {
    var a=keys[i-1];var b=keys[i];if(b.FrameNumber-a.FrameNumber!=2||b.FrameNumber>200)continue;
    for(int step=1;step<6;step++)
    {
     float t=step/6f;
     sampler.TrySampleCamera(a.FrameNumber+2f*t,out var sample);
     maximumError=Mathf.Max(maximumError,Vector3.Distance(sample.Position,Vector3.LerpUnclamped(a.Position,b.Position,t)));
     tested++;
    }
   }
   Record("404-two-frame-linear-position",tested>=100&&maximumError<.0001f,new{tested,maximumError});
   var encoded=VmdWriter.Write(motion);
   // This camera-only file has zero bone/morph tracks and 61 bytes per camera key.
   bool sameCurveBytes=true;
   for(int i=0;i<keys.Length;i++)
    for(int j=0;j<24;j++)sameCurveBytes&=bytes[94+61*i+j]==encoded[94+61*i+j];
   Record("camera-curves-byte-round-trip",sameCurveBytes);
   var curve=keys.SelectMany(k=>new[]{k.Interpolation.X,k.Interpolation.Y,k.Interpolation.Z,
    k.Interpolation.Rotation,k.Interpolation.Distance,k.Interpolation.FieldOfView}).First(c=>c.X1==68);
   Record("nonlinear-camera-control-points",curve.Y1==13&&curve.X2==65&&curve.Y2==116,new{curve.X1,curve.Y1,curve.X2,curve.Y2});
  }
  ui.SetCameraEnabled(true);
  ui.RefreshAndPlayPackage(package);yield return Wait1103();
  if(core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded)
  {
   var runtime=core.CurrentVmdPlayer;
   Application.targetFrameRate=90;
   core.Seek(0f);
   var samples=new List<object>();float until=Time.realtimeSinceStartup+9;
   float maximumTimelineError=0;
   while(Time.realtimeSinceStartup<until)
   {
    yield return new WaitForEndOfFrame();
    var camera=Camera.main;
    maximumTimelineError=Mathf.Max(maximumTimelineError,Mathf.Abs(runtime.LastAppliedCameraFrame-core.PlaybackTime*30f));
    samples.Add(new{elapsed=Time.realtimeSinceStartup,delta=Time.unscaledDeltaTime,time=core.PlaybackTime,
     frame=runtime.LastAppliedCameraFrame,position=Vec(camera.transform.position),hips=Vec(ui.avatarHelper.CurrentAvatarHips.position)});
   }
   File.WriteAllText(Path.Combine(output,"live.json"),JsonConvert.SerializeObject(samples,Formatting.Indented));
   Record("real-playback-camera-timeline",samples.Count>250&&maximumTimelineError<.001f,new{count=samples.Count,maximumTimelineError});
  }
  else Record("real-playback-camera-timeline",false,core.LastError);
  core.StopPlay();
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
  Logger.LogInfo("CAMERA_CURVE1107_COMPLETE");
 }
}
