using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomDancePlayer;
using Maoxig.RuntimeVmd;
using Maoxig.VmdDanceStudio;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
public partial class DanceAudit {
 private IEnumerator Scale26Only(){
  output=Path.Combine(OutputDirectory(),"scale26-current");Directory.CreateDirectory(output);while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();var helper=ui.avatarHelper;var data=DanceSettingsHandler.Instance.data;data.autoMmdCameraScale=true;core.SetCameraScale(1);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);ui.SetCameraEnabled(true);ui.Window.SetTab("import");
  var loader=UnityEngine.Object.FindFirstObjectByType<VRMLoader>();loader.LoadVRM("E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me");yield return new WaitForSecondsRealtime(3);
  var parent=helper.CurrentAvatar.GetComponentsInChildren<Transform>(true).First(t=>t.name=="全ての親");var rest=helper.CurrentAnimator.avatar.humanDescription.skeleton.First(b=>b.name==parent.name);
  Record("imported-internal-scale",Math.Abs(parent.localScale.y-.8f)<.0001&&Math.Abs(rest.scale.y-1)<.0001,new{actual=parent.localScale.y,avatar=rest.scale.y});
  float referenceEye=helper.MeasureAvatarEyeHeight();Record("eye-measure-internal-scale-once",Math.Abs(referenceEye-1.48891115f*.8f)<.0001,new{referenceEye,height=helper.MeasureAvatarHeight()});
  string root="F:/Program Files/ME/.audit/v0.2/donut-hole-20261009/tree/ドーナツホール配布用モーション";var draft=VmdWorkspaceScanner.SuggestFolder(root);draft.Id="scale26-reference";draft.Title="Scale reference";draft.CameraReferenceEyeHeight=referenceEye;draft.CameraAuthoringScale=.796948195f;
  string package=Path.Combine(ui.resourceManager.LibraryFolder,"Scale26","reference.vmdance");VmdDancePackageBuilder.Build(draft,package);ui.RefreshAndPlayPackage(package);float until=Time.realtimeSinceStartup+30;while(core.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForSecondsRealtime(.5f);if(!core.Paused)core.TogglePause();
  Record("reference-package-scale-once",Math.Abs(core.GetEffectiveCameraScale()-draft.CameraAuthoringScale.Value)<.00001);var flags=BindingFlags.Instance|BindingFlags.NonPublic;var restBones=(Dictionary<Transform,Transform>)typeof(VmdNativePmxPlayer).GetField("directRestBones",flags).GetValue(core.CurrentVmdPlayer);
  Record("calibration-internal-scale-once",Math.Abs(restBones[parent].localScale.y-.8f)<.00001);
  var times=new[]{0f,5f,10f,15f,20f,30f,40f,60f,90f,120f,150f,180f};var original=new Dictionary<float,Vector3>();foreach(float t in times){core.Seek(t/core.Duration);yield return new WaitForEndOfFrame();original[t]=core.CurrentVmdPlayer.TargetCamera.WorldToScreenPoint(Camera26Eyes());}
  var beforeScale=helper.CurrentAvatar.transform.localScale;
  foreach(float multiplier in new[]{.5f,2.5f}){helper.CurrentAvatar.transform.localScale=beforeScale*multiplier;core.Seek(10/core.Duration);yield return new WaitForEndOfFrame();Record("display-scale-keeps-eye-"+multiplier,Math.Abs(helper.MeasureAvatarEyeHeight()-referenceEye)<.00001);Record("display-scale-keeps-framing-"+multiplier,Math.Abs(core.CurrentVmdPlayer.TargetCamera.WorldToScreenPoint(Camera26Eyes()).y-original[10].y)<3);}helper.CurrentAvatar.transform.localScale=beforeScale;
  core.StopPlay();loader.LoadVRM("Ayrina");yield return new WaitForSecondsRealtime(2);float smallEye=helper.MeasureAvatarEyeHeight();Record("official-small-eye",Math.Abs(smallEye-1.06502962f)<.0001,smallEye);ui.RefreshAndPlayPackage(package);until=Time.realtimeSinceStartup+30;while(core.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForSecondsRealtime(.3f);if(!core.Paused)core.TogglePause();
  float expected=smallEye/referenceEye*draft.CameraAuthoringScale.Value;Record("small-scale-single-ratio",Math.Abs(core.GetEffectiveCameraScale()-expected)<.00001&&Math.Abs(core.CurrentVmdPlayer.CameraDistanceScale-expected)<.00001,new{expected,actual=core.GetEffectiveCameraScale()});
  var framing=new List<object>();foreach(float t in times){core.Seek(t/core.Duration);yield return new WaitForEndOfFrame();var screen=core.CurrentVmdPlayer.TargetCamera.WorldToScreenPoint(Camera26Eyes());float delta=Math.Abs(screen.y-original[t].y);framing.Add(new{t,delta,before=Vec(original[t]),after=Vec(screen)});Record("cross-model-framing-"+t,delta<60,new{delta,before=Vec(original[t]),after=Vec(screen)});}
  File.WriteAllText(Path.Combine(output,"framing.json"),JsonConvert.SerializeObject(framing,Formatting.Indented));yield return Capture("small-model-camera");
  core.StopPlay();Record("default-reference-165",core.GetCameraReferenceEyeHeight()==1.65f);VmdDancePackageDescriptor descriptor;string error;VmdDancePackage.TryOpenArchive(package,Path.Combine(output,"roundtrip"),out descriptor,out error);string json=File.ReadAllText(descriptor.ManifestPath);Record("two-field-package-only",descriptor.CameraReferenceEyeHeight==referenceEye&&descriptor.CameraAuthoringScale==draft.CameraAuthoringScale&&!json.Contains("cameraReferenceEyeTrack")&&!json.Contains("cameraReferenceBodyHeight"),json);
  foreach(string language in new[]{"zh-CN","en"}){DanceLocale.Set(language);ui.Window.SetTab("settings");yield return null;yield return null;var scroll=ui.Window.GetComponentsInChildren<ScrollRect>(true).First(s=>s.gameObject.activeInHierarchy);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=.6f;yield return null;yield return Capture("settings-global-"+language);Record("global-labels-"+language,DanceLocale.T("camera.scale").StartsWith(language=="en"?"Global":"全局")&&DanceLocale.T("settings.vmdFootIk").StartsWith(language=="en"?"Global":"全局"));}
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("SCALE26_COMPLETE");
 }
}
