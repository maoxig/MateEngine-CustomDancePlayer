using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomDancePlayer;
using Maoxig.VmdDanceStudio;
using Newtonsoft.Json;
using UnityEngine;
public partial class DanceAudit {
 private Vector3 Camera26Eyes(){var a=ui.avatarHelper.CurrentAnimator;var l=a.GetBoneTransform(HumanBodyBones.LeftEye);var r=a.GetBoneTransform(HumanBodyBones.RightEye);return l!=null&&r!=null?(l.position+r.position)*.5f:a.GetBoneTransform(HumanBodyBones.Head).position;}
 private IEnumerator Camera26Diagnostic(){
  output=Path.Combine(OutputDirectory(),"camera26-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();var core=ui.playerCore;core.InitPlayer();ui.SetPanelVisible(false);var data=DanceSettingsHandler.Instance.data;data.autoMmdCameraScale=true;core.SetCameraScale(1);ui.SetCameraEnabled(true);
  var models=new List<string>{"E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me","Lazuli_VRM_Clothes"};
  models.Add("Ayrina");var keys=new List<string>();foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var t=assembly.GetType("UnityEngine.AddressableAssets.Addressables");if(t==null)continue;var locators=(IEnumerable)t.GetProperty("ResourceLocators",BindingFlags.Public|BindingFlags.Static).GetValue(null);foreach(var loc in locators)foreach(var key in (IEnumerable)loc.GetType().GetProperty("Keys").GetValue(loc)){var s=key.ToString();if(s.Contains("VRM")){keys.Add(s);if(s.EndsWith("_VRM_Clothes")&&!models.Contains(s))models.Add(s);}}}
  File.WriteAllText(Path.Combine(output,"keys.json"),JsonConvert.SerializeObject(keys,Formatting.Indented));
  string fixture="F:/Program Files/ME/.audit/v0.2/camera26-fixtures/";var draft=new VmdDanceDraft{Id="camera26-neutral",MotionVmd=fixture+"neutral.vmd",CameraVmd=fixture+"eye187.vmd",CameraReferenceEyeHeight=1.48891115f,CameraAuthoringScale=.796948195f,PositionScale=.08f};
  draft.FootIk=false;string package=Path.Combine(ui.resourceManager.LibraryFolder,"Camera26","neutral.vmdance");VmdDancePackageBuilder.Build(draft,package);
  string realPackage=Path.Combine(ui.resourceManager.LibraryFolder,"Camera26","donut-real.vmdance");File.Copy("E:/SteamLibrary/steamapps/common/MateEngine/MateEngineX_Data/StreamingAssets/CustomDances/VmdComposer/配布用ドーナツホール-3.vmdance",realPackage,true);
  foreach(string model in models.Take(8)){
   var helper=ui.avatarHelper;core.StopPlay();UnityEngine.Object.FindFirstObjectByType<VRMLoader>().LoadVRM(model);yield return new WaitForSecondsRealtime(3);var avatar=helper.CurrentAvatar;
   var skeleton=helper.CurrentAnimator.avatar.humanDescription.skeleton.ToDictionary(b=>b.name,b=>b);
   var scaleDiff=new List<object>();foreach(var node in helper.CurrentAvatar.GetComponentsInChildren<Transform>(true)){if(node==avatar.transform)continue;if(skeleton.TryGetValue(node.name,out var rest)&&(node.localScale-rest.scale).sqrMagnitude>.00001f)scaleDiff.Add(new{name=node.name,actual=Vec(node.localScale),rest=Vec(rest.scale)});}
   File.WriteAllText(Path.Combine(output,"scales-"+Path.GetFileNameWithoutExtension(model)+".json"),JsonConvert.SerializeObject(scaleDiff,Formatting.Indented));
   float eye=helper.MeasureAvatarEyeHeight();var before=avatar.transform.InverseTransformPoint(Camera26Eyes());float ground=(float)typeof(DanceAvatarHelper).GetField("currentAvatarGroundHeight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(helper);
   ui.RefreshAndPlayPackage(package);float until=Time.realtimeSinceStartup+25;while(core.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForSecondsRealtime(.3f);if(!core.Paused)core.TogglePause();core.Seek(0);yield return new WaitForEndOfFrame();
   var camera=core.CurrentVmdPlayer.TargetCamera;var eyes=Camera26Eyes();var localEyes=avatar.transform.InverseTransformPoint(eyes);var localCamera=avatar.transform.InverseTransformPoint(camera.transform.position);
   Record("neutral-"+model,true,new{eye,ground,before=Vec(before),localEyes=Vec(localEyes),localCamera=Vec(localCamera),eyeView=Vec(camera.WorldToScreenPoint(eyes)),effective=core.GetEffectiveCameraScale(),native=core.CurrentVmdPlayer.CameraDistanceScale,rootScale=Vec(avatar.transform.lossyScale),rootPosition=Vec(avatar.transform.position),support=Vec(core.CurrentVmdPlayer.LastSupportRootCorrection),hips=Vec(avatar.transform.InverseTransformPoint(helper.CurrentAvatarHips.position)),leftFoot=Vec(avatar.transform.InverseTransformPoint(helper.CurrentAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position)),human=core.CurrentVmdPlayer.TargetHumanScale});yield return Capture("neutral-"+Path.GetFileNameWithoutExtension(model));
   core.StopPlay();
   // Real camera and motion from the user's authoring test.
   ui.RefreshAndPlayPackage(realPackage);until=Time.realtimeSinceStartup+25;while(core.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return null;if(!core.Paused)core.TogglePause();
   foreach(float seconds in new[]{0f,10f,30f}){core.Seek(seconds/core.Duration);yield return new WaitForEndOfFrame();camera=core.CurrentVmdPlayer.TargetCamera;Record("real-"+model+"-"+seconds,true,new{eye,ground,localEyes=Vec(avatar.transform.InverseTransformPoint(Camera26Eyes())),localCamera=Vec(avatar.transform.InverseTransformPoint(camera.transform.position)),eyeView=Vec(camera.WorldToScreenPoint(Camera26Eyes())),effective=core.GetEffectiveCameraScale(),native=core.CurrentVmdPlayer.CameraDistanceScale});}
  }
  core.StopPlay();File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("CAMERA26_COMPLETE");
 }
}
