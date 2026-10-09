using System;
using System.Collections;
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
 private IEnumerator Review25Only() {
  output=Path.Combine(OutputDirectory(),"review25-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;
  var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);var helper=ui.avatarHelper;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var data=DanceSettingsHandler.Instance.data;data.autoMmdCameraScale=false;core.SetCameraScale(1.7f);
  string root="F:/Program Files/ME/.audit/v0.2/donut-hole-20261009/tree/ドーナツホール配布用モーション";
  var draft=VmdWorkspaceScanner.SuggestFolder(root);draft.Id="review25-reference";draft.Title="Reference review";composer.RestoreDraft(draft);
  float eye=helper.MeasureAvatarEyeHeight();var autoDraft=composer.CaptureDraft();
  Record("new-reference-auto-eye",eye>0&&Math.Abs(autoDraft.CameraReferenceEyeHeight.Value-eye)<.00001f,new{eye,autoDraft.CameraReferenceEyeHeight});
  Record("body-field-removed",!composer.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="CameraReferenceBody")&&!autoDraft.CameraReferenceBodyHeight.HasValue);
  composer.RestoreDraft(new VmdDanceDraft{Id="legacy-body-reference",CameraReferenceBodyHeight=1.5f});var legacyDraft=composer.CaptureDraft();Record("legacy-body-converted-to-eye",Math.Abs(legacyDraft.CameraReferenceEyeHeight.Value-1.5f*1.6f/1.65f)<.00001f&&!legacyDraft.CameraReferenceBodyHeight.HasValue);composer.RestoreDraft(draft);
  Record("reference-readonly",composer.GetComponentsInChildren<InputField>(true).First(t=>t.name=="CameraReferenceEye").readOnly);
  typeof(DanceComposerPanel).GetMethod("BuildAndPreview",flags).Invoke(composer,null);
  float deadline=Time.realtimeSinceStartup+30;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;yield return null;
  Record("preview-started",core.IsPlaying&&core.CurrentVmdPlayer!=null,core.LastError);
  Record("preview-independent-personal-scale",Math.Abs(core.GetEffectiveCameraScale()-1)<.00001&&data.mmdCameraScale==1.7f&&!data.autoMmdCameraScale,new{effective=core.GetEffectiveCameraScale(),data.mmdCameraScale});
  if(!core.Paused)core.TogglePause();core.Seek(10/core.Duration);ui.SetCameraEnabled(true);yield return new WaitForEndOfFrame();
  var slider=(Slider)typeof(DanceComposerPanel).GetField("cameraAuthorScaleSlider",flags).GetValue(composer);
  var originalPlayer=core.CurrentVmdPlayer;var cameraPosition=core.CurrentVmdPlayer.TargetCamera.transform.position;float time=core.PlaybackTime;
  slider.value=.5f;yield return new WaitForEndOfFrame();
  Record("live-slider-half",Math.Abs(core.GetEffectiveCameraScale()-.5f)<.00001&&Math.Abs(core.CurrentVmdPlayer.CameraDistanceScale-.5f)<.00001,new{effective=core.GetEffectiveCameraScale(),native=core.CurrentVmdPlayer.CameraDistanceScale});
  Record("live-camera-moved",Vector3.Distance(cameraPosition,core.CurrentVmdPlayer.TargetCamera.transform.position)>.01f);
  Record("live-slider-no-restart",core.CurrentVmdPlayer==originalPlayer&&core.Paused&&Math.Abs(core.PlaybackTime-time)<.001f);
  var tuned=composer.CaptureDraft();Record("draft-direct-multiplier",tuned.CameraAuthoringScale==.5f&&tuned.CameraReferenceEyeHeight==autoDraft.CameraReferenceEyeHeight);
  slider.value=.01f;Record("small-slider-value",Math.Abs(core.GetEffectiveCameraScale()-.01f)<.00001f);slider.value=.5f;
  Record("reference-other-playback-rejected",!core.SetAuthoringPreviewCameraReference("other-package",1.4f,.5f));
  Record("reference-nonfinite-rejected",!core.SetAuthoringPreviewCameraReference(core.CurrentResourceId,float.NaN,.5f));
  // Target eye / recorded eye * multiplier, using real target geometry.
  core.SetAuthoringPreviewCameraReference(core.CurrentResourceId,eye*2,.5f);
  Record("half-height-quarter-scale",Math.Abs(core.GetEffectiveCameraScale()-.25f)<.00001f,new{eye,reference=eye*2,effective=core.GetEffectiveCameraScale()});
  typeof(DanceComposerPanel).GetMethod("CaptureCameraReference",flags).Invoke(composer,null);
  Record("capture-retains-package-multiplier",composer.CaptureDraft().CameraAuthoringScale==.5f&&Math.Abs(core.GetEffectiveCameraScale()-.5f)<.00001f);
  string package=Path.Combine(ui.resourceManager.LibraryFolder,"Review25","reference.vmdance");VmdDancePackageBuilder.Build(composer.CaptureDraft(),package);
  VmdDancePackageDescriptor descriptor;string error;VmdDancePackage.TryOpenArchive(package,Path.Combine(output,"read-cache"),out descriptor,out error);
  var manifest=File.ReadAllText(descriptor.ManifestPath);Record("manifest-only-eye-and-multiplier",descriptor.CameraReferenceEyeHeight==eye&&descriptor.CameraAuthoringScale==.5f&&!manifest.Contains("cameraReferenceBodyHeight"),manifest);
  ui.SetCameraEnabled(false);core.StopPlay();
  var loader=UnityEngine.Object.FindFirstObjectByType<VRMLoader>();loader.LoadVRM("E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me");yield return new WaitForSecondsRealtime(4);
  Record("open-existing",composer.OpenPackage(package));var reopened=composer.CaptureDraft();
  Record("existing-reference-preserved",reopened.CameraReferenceEyeHeight==eye&&reopened.CameraAuthoringScale==.5f,new{eye,currentEye=helper.MeasureAvatarEyeHeight(),reopened.CameraReferenceEyeHeight});
  typeof(DanceComposerPanel).GetMethod("BuildAndPreview",flags).Invoke(composer,null);deadline=Time.realtimeSinceStartup+30;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;yield return null;
  float secondEye=helper.MeasureAvatarEyeHeight();Record("other-model-auto-adapt",Math.Abs(core.GetEffectiveCameraScale()-secondEye/eye*.5f)<.00001f,new{secondEye,referenceEye=eye,effective=core.GetEffectiveCameraScale()});
  var modelRoot=helper.CurrentAvatar.transform;var originalScale=modelRoot.localScale;modelRoot.localScale=originalScale*2.5f;yield return null;
  Record("display-scale-independent",Math.Abs(helper.MeasureAvatarEyeHeight()-secondEye)<.00001f&&Math.Abs(core.GetEffectiveCameraScale()-secondEye/eye*.5f)<.00001f);modelRoot.localScale=originalScale;
  core.StopPlay();ui.RefreshAndPlayPackage(package);deadline=Time.realtimeSinceStartup+30;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
  Record("normal-playback-personal-setting-restored",Math.Abs(core.GetEffectiveCameraScale()-.5f*1.7f)<.00001f&&data.mmdCameraScale==1.7f,new{effective=core.GetEffectiveCameraScale()});core.StopPlay();
  foreach(string lang in new[]{"zh-CN","en"}) {
   DanceLocale.Set(lang);ui.Window.SetTab("import");yield return null;yield return null;
   composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);var advanced=composer.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<Text>().text==DanceLocale.T("composer.advanced"));advanced.onClick.Invoke();yield return null;
   var scroll=composer.GetComponentsInChildren<ScrollRect>(true).First();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;yield return null;
   yield return Capture("composer-reference-"+lang);
   var referenceRow=composer.GetComponentsInChildren<Transform>(true).First(t=>t.name=="CameraReference");
   Record("reference-controls-single-line-"+lang,referenceRow.GetComponentsInChildren<Text>(true).All(t=>t.preferredWidth<=t.rectTransform.rect.width+1));
  }
  string locales=Path.Combine(Path.GetDirectoryName(typeof(DanceLocale).Assembly.Location),"Locales");string testPath=Path.Combine(locales,"ja.json"),invalidPath=Path.Combine(locales,"bad.json");
  File.WriteAllText(testPath,"{\"language.name\":\"日本語\",\"composer.title\":\"タイトル\",\"status.ready\":\"Invalid {9}\"}");File.WriteAllText(invalidPath,"not json");
  DanceLocale.Set("ja");Record("new-language-discovered",DanceLocale.AvailableLanguages.Contains("ja")&&DanceLocale.LanguageName("ja")=="日本語"&&DanceLocale.T("composer.title")=="タイトル");
  Record("missing-translation-fallback",DanceLocale.T("composer.author")=="Author");Record("invalid-format-fallback",DanceLocale.T("status.ready",4)=="Ready · 4 dances",DanceLocale.T("status.ready",4));Record("invalid-file-skipped",!DanceLocale.AvailableLanguages.Contains("bad"));
  ui.Window.SetTab("settings");yield return null;yield return null;var languageButton=ui.Window.GetComponentsInChildren<Button>(true).First(b=>b.name=="LanguageSelector");
  Record("language-selector-discovered",languageButton.GetComponentInChildren<Text>().text.Contains(DanceLocale.LanguageName(data.language))||data.language=="auto");
  for(int i=0;i<DanceLocale.AvailableLanguages.Length+1&&data.language!="ja";i++){languageButton.onClick.Invoke();yield return null;yield return null;languageButton=ui.Window.GetComponentsInChildren<Button>(true).First(b=>b.name=="LanguageSelector");}
  Record("new-language-ui-selected",data.language=="ja"&&DanceLocale.Language=="ja"&&languageButton.GetComponentInChildren<Text>().text.Contains("日本語"));yield return Capture("new-language-selector");
  File.Delete(testPath);File.Delete(invalidPath);DanceLocale.Set("zh-CN");ui.Window.SetTab("settings");yield return null;yield return Capture("settings-language");
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("REVIEW25_AUDIT_COMPLETE "+results.Count);
 }
}

