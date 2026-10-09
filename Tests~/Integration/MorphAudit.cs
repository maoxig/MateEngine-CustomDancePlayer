using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Newtonsoft.Json;
using UnityEngine;

public partial class DanceAudit
{
 private IEnumerator MorphOnly()
 {
  output=Path.Combine(OutputDirectory(),"morph-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(false);
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("03-catch-the-wave")&&d.Format=="vmdance");
  core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(4);
  if(!core.Paused)core.TogglePause();
  var avatar=ui.avatarHelper.CurrentAvatar;
  var shapes=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.sharedMesh!=null&&s.sharedMesh.blendShapeCount>0).ToArray();
  File.WriteAllText(Path.Combine(output,"model.json"),JsonConvert.SerializeObject(new{avatar=avatar.name,components=avatar.GetComponentsInChildren<Component>(true).Where(c=>c!=null).Select(c=>c.GetType().FullName).Distinct().ToArray(),renderers=shapes.Select(s=>new{path=Relative(s.transform,avatar.transform),shapes=Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(s.sharedMesh.GetBlendShapeName).ToArray()}).ToArray()},Formatting.Indented));
  var sampler=(VmdMotionSampler)typeof(VmdNativePmxPlayer).GetField("auxiliarySampler",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(core.CurrentVmdPlayer);
  var frames=new List<object>();
  float mouthPeak=0,eyePeak=0,detailEyePeak=0;bool hasDetailEye=false;float maxRenderedDifference=0;
  foreach(float seconds in new[]{1f,5f,10f,15f,20f,25f,30f,35f,40f,50f,60f})
  {
   core.Seek(seconds/core.Duration);
   var immediate=shapes.Select(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(s.GetBlendShapeWeight).ToArray()).ToArray();
   yield return new WaitForEndOfFrame();
   for(int r=0;r<shapes.Length;r++)for(int i=0;i<shapes[r].sharedMesh.blendShapeCount;i++)
   {
    float weight=shapes[r].GetBlendShapeWeight(i);string shape=shapes[r].sharedMesh.GetBlendShapeName(i);
    if(shape=="x Oh"||shape=="x Ah"||VmdMorphNameMatcher.IsMatch(shape,"お")||VmdMorphNameMatcher.IsMatch(shape,"あ"))mouthPeak=Mathf.Max(mouthPeak,weight);
    if(VmdMorphNameMatcher.IsMatch(shape,"まばたき"))eyePeak=Mathf.Max(eyePeak,weight);
    if(shape=="Hachu Eye"){hasDetailEye=true;detailEyePeak=Mathf.Max(detailEyePeak,weight);}
    maxRenderedDifference=Mathf.Max(maxRenderedDifference,Mathf.Abs(weight-immediate[r][i]));
   }
   frames.Add(new{seconds,bound=core.CurrentVmdPlayer.BoundMorphCount,source=sampler.MorphNames.Select(n=>{float w;sampler.TrySampleMorph(n,seconds*30,out w);return new{name=n,weight=w};}).Where(w=>w.weight>0.01).ToArray(),immediate,rendered=shapes.Select(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(s.GetBlendShapeWeight).ToArray()).ToArray()});
   if(seconds==10||seconds==30)yield return Capture("morph-"+seconds);
  }
  Record("authored-mouth-visible",mouthPeak>20,new{mouthPeak});Record("authored-blink-visible",eyePeak>20,new{eyePeak});
  Record("rendered-expression-preserved",maxRenderedDifference<0.1f,new{maxRenderedDifference});
  if(hasDetailEye)Record("translated-mmd-detailed-eye",detailEyePeak>90,new{detailEyePeak});
  File.WriteAllText(Path.Combine(output,"morph.json"),JsonConvert.SerializeObject(frames,Formatting.Indented));core.StopPlay();
  Record("morph-stop-idle",!core.IsPlaying);
  yield return MorphFixtures();
  File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("MORPH_AUDIT_COMPLETE");
 }
 private IEnumerator LegacyMorphOnly()
 {
  output=Path.Combine(OutputDirectory(),"legacy-morph-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.playerCore.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(false);
  yield return Expressions();yield return Capture("legacy-expression");
  File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("LEGACY_MORPH_AUDIT_COMPLETE");
 }
 private static string Relative(Transform t,Transform root){var names=new List<string>();while(t!=null&&t!=root){names.Insert(0,t.name);t=t.parent;}return string.Join("/",names);}
 private IEnumerator MorphFixtures()
 {
  var go=new GameObject("ExpressionFixture");go.SetActive(false);var animator=go.AddComponent<Animator>();
  var face=new GameObject("Face");face.transform.SetParent(go.transform);var nested=new GameObject("Geometry");nested.transform.SetParent(face.transform);
  var mouth=new GameObject("Tongue");mouth.transform.SetParent(go.transform);
  var faceRenderer=nested.AddComponent<SkinnedMeshRenderer>();var tongueRenderer=mouth.AddComponent<SkinnedMeshRenderer>();
  faceRenderer.sharedMesh=FixtureMesh(new[]{"あ","OpaqueLip","OpaqueEye","Unrelated"});tongueRenderer.sharedMesh=FixtureMesh(new[]{"OpaqueTongue"});
  faceRenderer.SetBlendShapeWeight(3,42);
  var proxy=go.AddComponent<VRM.VRMBlendShapeProxy>();proxy.BlendShapeAvatar=ScriptableObject.CreateInstance<VRM.BlendShapeAvatar>();
  var a=ScriptableObject.CreateInstance<VRM.BlendShapeClip>();a.Preset=VRM.BlendShapePreset.A;a.Values=new[]{new VRM.BlendShapeBinding{RelativePath="Face/Geometry",Index=1,Weight=50},new VRM.BlendShapeBinding{RelativePath="Tongue",Index=0,Weight=25}};
  var i=ScriptableObject.CreateInstance<VRM.BlendShapeClip>();i.Preset=VRM.BlendShapePreset.I;i.Values=new[]{new VRM.BlendShapeBinding{RelativePath="Face/Geometry",Index=1,Weight=80},new VRM.BlendShapeBinding{RelativePath="Tongue",Index=0,Weight=50}};
  var blink=ScriptableObject.CreateInstance<VRM.BlendShapeClip>();blink.Preset=VRM.BlendShapePreset.Blink;blink.IsBinary=true;blink.Values=new[]{new VRM.BlendShapeBinding{RelativePath="Face/Geometry",Index=2,Weight=33}};
  proxy.BlendShapeAvatar.Clips=new List<VRM.BlendShapeClip>{a,i,blink};
  var bindings=VmdExpressionBindings.Create(animator,new[]{"あ","A","い","まばたき"});
  bindings.Apply(n=>n=="あ"||n=="A"?1f:n=="い"?0.5f:n=="まばたき"?0.4f:0f);
  Record("japanese-priority-no-double",Mathf.Abs(faceRenderer.GetBlendShapeWeight(0)-100)<0.01f&&Mathf.Abs(faceRenderer.GetBlendShapeWeight(1)-40)<0.01f);
  Record("vrm0-weighted-multimesh",Mathf.Abs(tongueRenderer.GetBlendShapeWeight(0)-50)<0.01f);
  Record("binary-expression-below-threshold",faceRenderer.GetBlendShapeWeight(2)==0);
  bindings.Apply(n=>n=="まばたき"?0.6f:0f);
  Record("binary-expression-above-threshold",Mathf.Abs(faceRenderer.GetBlendShapeWeight(2)-33)<0.01f);
  Record("unrelated-shape-preserved",faceRenderer.GetBlendShapeWeight(3)==42);
  bindings.Restore();Record("restore-bound-shapes",faceRenderer.GetBlendShapeWeight(0)==0&&tongueRenderer.GetBlendShapeWeight(0)==0);
  var authored=proxy.BlendShapeAvatar;proxy.BlendShapeAvatar=null;var direct=VmdExpressionBindings.Create(animator,new[]{"あ"});direct.Apply(n=>0.5f);
  Record("pure-mmd-nested-without-body",Mathf.Abs(faceRenderer.GetBlendShapeWeight(0)-50)<0.01f);direct.Restore();proxy.BlendShapeAvatar=authored;
  Destroy(faceRenderer.sharedMesh);Destroy(tongueRenderer.sharedMesh);Destroy(a);Destroy(i);Destroy(blink);Destroy(proxy.BlendShapeAvatar);Destroy(go);yield return null;
 }
 private static Mesh FixtureMesh(string[] names)
 {
  var mesh=new Mesh();mesh.vertices=new Vector3[3];var add=typeof(Mesh).GetMethod("AddBlendShapeFrame",new[]{typeof(string),typeof(float),typeof(Vector3[]),typeof(Vector3[]),typeof(Vector3[])});foreach(string name in names)add.Invoke(mesh,new object[]{name,100f,new Vector3[3],new Vector3[3],new Vector3[3]});return mesh;
 }
}
