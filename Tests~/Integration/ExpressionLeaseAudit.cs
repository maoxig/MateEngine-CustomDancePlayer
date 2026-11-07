using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Maoxig.VmdDanceStudio;
using Newtonsoft.Json;
using UniVRM10;
using UnityEngine;
using VRM;

public partial class DanceAudit
{
 private IEnumerator ExpressionLeaseOnly()
 {
  output=Path.Combine(OutputDirectory(),"expression-lease-current");
  Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return WaitForLibrary();
  var core=ui.playerCore;
  core.InitPlayer();
  core.StopPlay();
  ui.SetPanelVisible(false);
  var avatar=ui.avatarHelper.CurrentAvatar;
  var universal=ui.avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
  var fixture=new GameObject("ExpressionLeaseFixture");
  fixture.SetActive(false);
  fixture.transform.SetParent(avatar.transform,false);
  var renderer=fixture.AddComponent<SkinnedMeshRenderer>();
  renderer.sharedMesh=FixtureMesh(new[]{"blink","OpaqueWink","Outfit"});
  var proxy=fixture.AddComponent<VRMBlendShapeProxy>();
  proxy.BlendShapeAvatar=ScriptableObject.CreateInstance<BlendShapeAvatar>();
  var blink=ScriptableObject.CreateInstance<BlendShapeClip>();
  blink.Preset=BlendShapePreset.Blink;
  blink.Values=new[]{new BlendShapeBinding{RelativePath="",Index=1,Weight=100}};
  proxy.BlendShapeAvatar.Clips=new List<BlendShapeClip>{blink};
  fixture.SetActive(true);
  yield return null;
  var key=BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink);
  var vrm1=avatar.GetComponentInChildren<Vrm10Instance>(true)?.Runtime?.Expression;
  bool hasVrm1Blink=vrm1!=null&&vrm1.ExpressionKeys.Contains(ExpressionKey.Blink);
  var motion=new VmdMotion();
  motion.BoneFrames.Add(new VmdBoneKeyframe{BoneName="センター",FrameNumber=0,Rotation=Quaternion.identity});
  motion.BoneFrames.Add(new VmdBoneKeyframe{BoneName="センター",FrameNumber=180,Rotation=Quaternion.identity});
  var motionPath=Path.Combine(output,"neutral-face.vmd");
  File.WriteAllBytes(motionPath,VmdWriter.Write(motion));
  var package=Path.Combine(ui.resourceManager.LibraryFolder,"expression-lease-audit.vmdance");
  VmdDancePackageBuilder.Build(new VmdDanceDraft{Id="expression-lease-audit",MotionVmd=motionPath},package);
  core.playlistManager.Search="";
  core.playlistManager.Format="";
  core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);
  core.playlistManager.ApplyFilters();
  var legacy=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("MMD-CatchTheWave.unity3d"));
  foreach(bool native in new[]{true,false})
  {
   var label=native?"vmd":"legacy";
   bool originalEnabled=universal!=null&&universal.enabled;
   float cleanDirect=0,cleanOpaque=0;
   if(!native)
   {
    core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(legacy.Id));
    yield return Wait1103();
    core.TogglePause();
    core.Seek(0);
    yield return new WaitForEndOfFrame();
    cleanDirect=renderer.GetBlendShapeWeight(0);
    cleanOpaque=renderer.GetBlendShapeWeight(1);
    core.StopPlay();
   }
   if(universal!=null)universal.Blink=.8f;
   if(hasVrm1Blink)vrm1.SetWeight(ExpressionKey.Blink,.7f);
   proxy.ImmediatelySetValue(key,.9f);
   renderer.SetBlendShapeWeight(0,83);
   renderer.SetBlendShapeWeight(2,42);
   Record(label+"-precondition",Math.Abs(proxy.GetValue(key)-.9f)<.01f&&renderer.GetBlendShapeWeight(1)>85);
   if(native)ui.RefreshAndPlayPackage(package);
   else core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(legacy.Id));
   // The legacy Animator can already have written an authored input here.
   // Check its rendered result against a clean run at the same playback time.
   Record(label+"-neutral-on-acquire",renderer.GetBlendShapeWeight(0)==0&&renderer.GetBlendShapeWeight(1)==0&&
    proxy.GetValue(key)==0&&(!hasVrm1Blink||vrm1.GetWeight(ExpressionKey.Blink)==0)&&
    (!native||universal==null||universal.Blink==0)&&renderer.GetBlendShapeWeight(2)==42,
    new{direct=renderer.GetBlendShapeWeight(0),opaque=renderer.GetBlendShapeWeight(1),proxy=proxy.GetValue(key),hasVrm1Blink,
     vrm1Weight=hasVrm1Blink?vrm1.GetWeight(ExpressionKey.Blink):0f,universalWeight=universal?.Blink,outfit=renderer.GetBlendShapeWeight(2)});
   yield return Wait1103();
   if(!core.Paused)core.TogglePause();
   core.Seek(0);
   yield return new WaitForEndOfFrame();
   float direct=renderer.GetBlendShapeWeight(0),opaque=renderer.GetBlendShapeWeight(1);
   Record(label+"-no-residual-face",core.IsPlaying&&!core.IsLoading&&Math.Abs(direct-cleanDirect)<.1f&&
    Math.Abs(opaque-cleanOpaque)<.1f&&renderer.GetBlendShapeWeight(2)==42&&
    (!native||!hasVrm1Blink||vrm1.GetWeight(ExpressionKey.Blink)==0),
    new{direct,opaque,cleanDirect,cleanOpaque,core.LastError});
   core.StopPlay();
   Record(label+"-restore-face-lease",renderer.GetBlendShapeWeight(0)==83&&renderer.GetBlendShapeWeight(1)>85&&
    renderer.GetBlendShapeWeight(2)==42&&Math.Abs(proxy.GetValue(key)-.9f)<.01f&&
    (!hasVrm1Blink||Math.Abs(vrm1.GetWeight(ExpressionKey.Blink)-.7f)<.01f)&&
    (universal==null||universal.enabled==originalEnabled&&Math.Abs(universal.Blink-.8f)<.01f));
  }
  Destroy(renderer.sharedMesh);
  Destroy(proxy.BlendShapeAvatar);
  Destroy(blink);
  Destroy(fixture);
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
  Logger.LogInfo("EXPRESSION_LEASE_COMPLETE");
 }
}
