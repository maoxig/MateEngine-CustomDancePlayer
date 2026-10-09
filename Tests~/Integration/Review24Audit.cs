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
 private IEnumerator Review24Only() {
  output=Path.Combine(OutputDirectory(),"review24-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);
  string root="F:/Program Files/ME/.audit/v0.2/donut-hole-20261009/tree/ドーナツホール配布用モーション";
  var groups=VmdWorkspaceScanner.Scan(root);var group=groups.First(g=>g.HasMotion);var suggested=VmdWorkspaceScanner.SuggestFolder(root);
  Record("donut-inspect-valid",groups.SelectMany(g=>g.Tracks).All(t=>t.IsValid),groups.SelectMany(g=>g.Tracks).ToArray());
  Record("donut-auto-camera",!string.IsNullOrEmpty(suggested.CameraVmd),new{suggested.MotionVmd,suggested.CameraVmd,suggested.AudioFile});
  string body=Directory.GetFiles(root,"*.vmd")[0],camera=Directory.GetFiles(root,"*.vmd",SearchOption.AllDirectories).First(p=>p!=body);
  var helper=ui.avatarHelper;var loader=UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
  loader.LoadVRM("C:/Users/xp/AppData/LocalLow/Shinymoon/MateEngineX/Steam Workshop/YYBMikuV2.me");yield return new WaitForSecondsRealtime(4);
  foreach(int slots in new[]{0,1,2,3}) {
   var draft=new VmdDanceDraft{Id="donut-review-"+slots,Title="Donut review "+slots,MotionVmd=body,FaceVmd=(slots&1)!=0?body:null,LipVmd=(slots&2)!=0?body:null,CameraVmd=camera,AudioFile=Directory.GetFiles(root,"*.wav")[0],PositionScale=.08f};
   var errors=VmdDancePackageBuilder.Validate(draft);Record("donut-validate-"+slots,errors.Count==0,errors);
   string package=Path.Combine(ui.resourceManager.LibraryFolder,"Review24","donut-"+slots+".vmdance");VmdDancePackageBuilder.Build(draft,package);
   ui.RefreshAndPlayPackage(package);float deadline=Time.realtimeSinceStartup+25;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
   yield return new WaitForSecondsRealtime(1);Record("donut-play-"+slots,core.IsPlaying && core.CurrentVmdPlayer!=null,new{core.LastError,core.Duration});
   if(core.CurrentVmdPlayer!=null) {
    if(!core.Paused)core.TogglePause();float lip=0,blink=0;
    var bindings=(VmdExpressionBindings)typeof(VmdNativePmxPlayer).GetField("expressionBindings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(core.CurrentVmdPlayer);
    var route=(System.Collections.IEnumerable)typeof(VmdExpressionBindings).GetField("bindings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bindings);
    var observations=new List<object>();
    for(int i=0;i<180;i++) {core.Seek((i*.25f)/core.Duration);yield return new WaitForEndOfFrame();foreach(var binding in route) {
     var type=binding.GetType();var smr=(SkinnedMeshRenderer)type.GetField("Renderer").GetValue(binding);int index=(int)type.GetField("Index").GetValue(binding);
     var terms=(System.Collections.IEnumerable)type.GetField("Terms").GetValue(binding);bool mouth=false,eye=false;var names=new List<string>();
     foreach(var term in terms){string morph=(string)term.GetType().GetField("Morph").GetValue(term);names.Add(morph);mouth|=morph=="あ";eye|=morph=="まばたき";}
     float weight=smr.GetBlendShapeWeight(index);if(mouth)lip=Math.Max(lip,weight);if(eye)blink=Math.Max(blink,weight);
     if(i==60)observations.Add(new{renderer=smr.name,shape=smr.sharedMesh.GetBlendShapeName(index),names,weight});
    }}
    File.WriteAllText(Path.Combine(output,"expressions-"+slots+".json"),JsonConvert.SerializeObject(observations,Formatting.Indented));
    Record("donut-expressions-"+slots,lip>5 && blink>5,new{lip,blink,core.CurrentVmdPlayer.BoundMorphCount});
   }
   core.StopPlay();
  }
  ui.Window.SetTab("settings");yield return null;
  var data=DanceSettingsHandler.Instance.data;data.autoMmdCameraScale=true;core.SetCameraScale(1f);
  ui.Window.SetTab("import");var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);var flags24=BindingFlags.Instance|BindingFlags.NonPublic;
  foreach(string slot in new[]{"faceInput","lipInput"}) {
   composer.RestoreDraft(new VmdDanceDraft{Id="promote-test"});var input=(InputField)typeof(DanceComposerPanel).GetField(slot,flags24).GetValue(composer);input.text=body;
   Record("promote-body-from-"+slot,composer.CaptureDraft().MotionVmd==body);
  }
  var referenceDraft=new VmdDanceDraft{Id="reference-review",Title="Reference review",MotionVmd=body,CameraVmd=camera,CameraReferenceEyeHeight=1.52f,CameraReferenceBodyHeight=1.65f,CameraAuthoringScale=.96f};
  var referencePath=Path.Combine(ui.resourceManager.LibraryFolder,"Review24","reference.vmdance");VmdDancePackageBuilder.Build(referenceDraft,referencePath);
  VmdDancePackageDescriptor descriptor;string packageError;VmdDancePackage.TryOpenArchive(referencePath,Path.Combine(output,"reference-cache"),out descriptor,out packageError);
  Record("camera-reference-roundtrip",descriptor!=null && descriptor.CameraReferenceEyeHeight==1.52f && descriptor.CameraReferenceBodyHeight==1.65f && descriptor.CameraAuthoringScale==.96f,packageError);
  referenceDraft.CameraReferenceEyeHeight=float.NaN;Record("camera-reference-validation",VmdDancePackageBuilder.Validate(referenceDraft).Count>0);referenceDraft.CameraReferenceEyeHeight=1.52f;
  ui.RefreshAndPlayPackage(referencePath);float referenceDeadline=Time.realtimeSinceStartup+25;while(core.IsLoading&&Time.realtimeSinceStartup<referenceDeadline)yield return null;yield return null;
  float targetEye=helper.MeasureAvatarEyeHeight();Record("camera-reference-runtime-ratio",Math.Abs(core.GetAvatarCameraScale()-targetEye/1.52f)<.00001f,new{targetEye,actual=core.GetAvatarCameraScale()});
  composer.OpenPackage(referencePath);composer.PreviewResourceId=core.CurrentResourceId;
  core.CapturePreviewCameraReference(core.CurrentResourceId,null,1.5f,1f);Record("reference-body-only-inference",Math.Abs(core.GetCameraReferenceEyeHeight()-1.5f*1.6f/1.65f)<.00001f);
  core.CapturePreviewCameraReference(core.CurrentResourceId,1.52f,null,1f);Record("reference-eye-only-inference",Math.Abs(core.GetCameraReferenceBodyHeight()-1.52f*1.65f/1.6f)<.00001f);
  core.CapturePreviewCameraReference(core.CurrentResourceId,1.52f,1.65f,.05f);Record("small-camera-multiplier-not-silently-clamped",Math.Abs(core.CurrentVmdPlayer.CameraDistanceScale-core.GetEffectiveCameraScale())<.00001f);
  core.CapturePreviewCameraReference(core.CurrentResourceId,1.52f,1.65f,.96f);
  if(!core.Paused)core.TogglePause();ui.SetCameraEnabled(true);core.SetCameraScale(1.2f);core.Seek(10/core.Duration);yield return new WaitForEndOfFrame();
  var mainCamera=Camera.main;var cameraPosition=mainCamera.transform.position;var cameraRotation=mainCamera.transform.rotation;
  float beforeCapture=core.GetEffectiveCameraScale();
  typeof(DanceComposerPanel).GetMethod("CaptureCameraReference",flags24).Invoke(composer,null);var captured=composer.CaptureDraft();
  Record("camera-reference-capture-preserves-framing",Math.Abs(beforeCapture-core.GetEffectiveCameraScale())<.00001f && data.mmdCameraScale==1f,new{beforeCapture,after=core.GetEffectiveCameraScale(),captured.CameraAuthoringScale,captured.CameraReferenceEyeHeight});
  yield return new WaitForEndOfFrame();Record("camera-reference-capture-live-pose",Vector3.Distance(mainCamera.transform.position,cameraPosition)<.0001f && QuaternionDegrees(mainCamera.transform.rotation,cameraRotation)<.01f);
  ui.SetCameraEnabled(false);
  Record("camera-reference-edit-save",composer.SaveEditingPackage());VmdDancePackage.TryOpenArchive(referencePath,Path.Combine(output,"updated-reference"),out descriptor,out packageError);
  Record("camera-reference-edit-keeps-metadata",descriptor!=null && Math.Abs(descriptor.CameraAuthoringScale.Value-beforeCapture)<.00001f && Math.Abs(descriptor.CameraReferenceEyeHeight.Value-targetEye)<.00001f);
  core.StopPlay();Record("old-package-reference-defaults",Math.Abs(core.GetCameraReferenceEyeHeight()-1.6f)<.00001f && Math.Abs(core.GetCameraReferenceBodyHeight()-1.65f)<.00001f && core.GetCameraAuthoringScale()==1f);composer.RestoreDraft(new VmdDanceDraft{Id="compact-layout"});yield return null;
  foreach(string lang in new[]{"zh-CN","en"}) {
   DanceLocale.Set(lang);ui.Window.SetTab("import");yield return null;yield return new WaitForEndOfFrame();
   var transport=(GameObject)typeof(DanceWindow).GetField("libraryTransport",flags24).GetValue(ui.Window);var actionRow=ui.Window.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="ComposerActions");
   Record("composer-compact-"+lang,transport.GetComponent<RectTransform>().rect.height<=80 && actionRow.rect.height<=32 && !ui.Window.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="ComposerStatus").gameObject.activeSelf,new{transportHeight=transport.GetComponent<RectTransform>().rect.height,actionHeight=actionRow.rect.height});
   composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
   var primaryButtons=actionRow.GetComponentsInChildren<Button>(true);Record("composer-actions-single-line-"+lang,primaryButtons.All(b=>b.GetComponentInChildren<Text>().preferredWidth<=b.GetComponentInChildren<Text>().rectTransform.rect.width+1));
   var advanced=composer.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<Text>().text==DanceLocale.T("composer.advanced"));
   yield return Capture("composer-compact-"+lang);advanced.onClick.Invoke();yield return null;
   var composeScroll=ui.Window.GetComponentsInChildren<ScrollRect>(true).First(t=>t.gameObject.activeInHierarchy);Canvas.ForceUpdateCanvases();composeScroll.verticalNormalizedPosition=0;yield return null;yield return new WaitForEndOfFrame();yield return Capture("composer-reference-"+lang);

   ui.Window.SetTab("settings");yield return null;yield return null;
   var settingsScroll=(ScrollRect)typeof(DanceWindow).GetField("settingsScroll",flags24).GetValue(ui.Window);
   var boxes=settingsScroll.GetComponentsInChildren<Toggle>(true).Select(t=>t.targetGraphic.rectTransform.rect.size).ToArray();Record("checkbox-uniform-"+lang,boxes.All(v=>Math.Abs(v.x-28)<.01 && Math.Abs(v.y-28)<.01),boxes.Select(v=>new[]{v.x,v.y}).ToArray());
   settingsScroll.verticalNormalizedPosition=.35f;ui.SetWindowFollowEnabled(!data.enableWindowFollow);yield return null;yield return null;yield return new WaitForEndOfFrame();
   settingsScroll=(ScrollRect)typeof(DanceWindow).GetField("settingsScroll",flags24).GetValue(ui.Window);Record("settings-scroll-preserved-"+lang,Math.Abs(settingsScroll.verticalNormalizedPosition-.35f)<.03f,new{position=settingsScroll.verticalNormalizedPosition});
   ui.SetCameraEnabled(true);yield return null;yield return null;
   settingsScroll=(ScrollRect)typeof(DanceWindow).GetField("settingsScroll",flags24).GetValue(ui.Window);settingsScroll.verticalNormalizedPosition=.45f;
   ui.SetWindowFollowEnabled(true);yield return null;yield return null;yield return new WaitForEndOfFrame();
   settingsScroll=(ScrollRect)typeof(DanceWindow).GetField("settingsScroll",flags24).GetValue(ui.Window);Record("settings-scroll-mutual-rebuild-"+lang,Math.Abs(settingsScroll.verticalNormalizedPosition-.45f)<.03f,new{position=settingsScroll.verticalNormalizedPosition});
   settingsScroll.verticalNormalizedPosition=0;yield return new WaitForEndOfFrame();yield return Capture("settings-compact-"+lang);
  }
  var diagnostics=new List<object>();
  foreach(string model in new[]{"E:/SteamLibrary/steamapps/common/MateEngine/Models/橘雪莉.vrm","E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me"}) {
   var before=helper.CurrentAvatar;loader.LoadVRM(model);float deadline=Time.realtimeSinceStartup+25;while((helper.CurrentAvatar==null||helper.CurrentAvatar==before)&&Time.realtimeSinceStartup<deadline)yield return null;yield return new WaitForSecondsRealtime(1);
   var animator=helper.CurrentAnimator;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var skeleton=new Dictionary<string,SkeletonBone>();foreach(var bone in animator.avatar.humanDescription.skeleton)if(!skeleton.ContainsKey(bone.name))skeleton.Add(bone.name,bone);
   var method=typeof(DanceAvatarHelper).GetMethod("TryRestMatrix",flags);var meshes=new List<object>();
   foreach(var smr in helper.CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
    if(smr.sharedMesh==null)continue;Matrix4x4 matrix=Matrix4x4.identity;for(int i=0;i<Math.Min(smr.bones.Length,smr.sharedMesh.bindposes.Length);i++){var args=new object[]{smr.bones[i],skeleton,null};if(smr.bones[i]!=null && (bool)method.Invoke(helper,args)){matrix=(Matrix4x4)args[2]*smr.sharedMesh.bindposes[i];break;}}
    var bound=smr.sharedMesh.bounds;float low=float.PositiveInfinity,high=float.NegativeInfinity;for(int i=0;i<8;i++){var p=matrix.MultiplyPoint3x4(new Vector3((i&1)==0?bound.min.x:bound.max.x,(i&2)==0?bound.min.y:bound.max.y,(i&4)==0?bound.min.z:bound.max.z));low=Math.Min(low,p.y);high=Math.Max(high,p.y);}
    meshes.Add(new{smr.name,smr.enabled,low,high,shapes=Enumerable.Range(0,smr.sharedMesh.blendShapeCount).Select(smr.sharedMesh.GetBlendShapeName).ToArray()});
   }
   diagnostics.Add(new{model,avatar=helper.CurrentAvatar.name,height=helper.MeasureAvatarHeight(),eyeHeight=helper.MeasureAvatarEyeHeight(),autoScale=core.GetAvatarCameraScale(),rootScale=new[]{helper.CurrentAvatar.transform.localScale.x,helper.CurrentAvatar.transform.localScale.y,helper.CurrentAvatar.transform.localScale.z},meshes,skeleton=skeleton.Values.Take(4).Select(v=>new{v.name,position=new[]{v.position.x,v.position.y,v.position.z},scale=new[]{v.scale.x,v.scale.y,v.scale.z}}).ToArray(),text=ui.Window.GetComponentsInChildren<Text>(true).Where(t=>t.name=="CameraScaleInfo").Select(t=>t.text).ToArray()});
   float measuredEye=helper.MeasureAvatarEyeHeight(),measuredBody=helper.MeasureAvatarHeight();
   Record("bare-height-"+Path.GetFileNameWithoutExtension(model),measuredBody<1.67f && measuredBody>1.4f,new{measuredEye,measuredBody});
   var infoText=ui.Window.GetComponentsInChildren<Text>(true).First(t=>t.name=="CameraScaleInfo");Record("height-tip-auto-refresh-"+Path.GetFileNameWithoutExtension(model),infoText.text.Contains((measuredEye>0?measuredEye:measuredBody).ToString("0.000")),infoText.text);
   var rootTransform=helper.CurrentAvatar.transform;var originalScale=rootTransform.localScale;var eyeMethod=typeof(DanceAvatarHelper).GetMethod("CalculateAvatarEyeHeight",flags);
   foreach(float scale in new[]{.5f,2.5f,1.2f}) {rootTransform.localScale=originalScale*scale;Record("eye-scale-invariant-"+Path.GetFileNameWithoutExtension(model)+"-"+scale,Math.Abs((float)eyeMethod.Invoke(helper,null)-measuredEye)<.00001f);}
   rootTransform.localScale=originalScale;
   yield return Capture("height-"+Path.GetFileNameWithoutExtension(model));
  }
  File.WriteAllText(Path.Combine(output,"height.json"),JsonConvert.SerializeObject(diagnostics,Formatting.Indented));File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("REVIEW24_AUDIT_COMPLETE");
 }
}
