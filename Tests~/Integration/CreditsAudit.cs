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
public partial class DanceAudit
{
 private IEnumerator CreditsOnly()
 {
  output=Path.Combine(OutputDirectory(),"credits28-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();ui.playerCore.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;
  var panel=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  var source="F:/Program Files/ME/.audit/v0.2/camera26-fixtures/neutral.vmd";
  const string credits="Motion: 作者\nCamera: \"测试\"\nhttps://example.org/motion\n自由备注：无需格式 <tag>\\path";
  panel.RestoreDraft(new VmdDanceDraft{Id="credits28",Title="Credits test",MotionVmd=source,Credits=credits});
  var input=panel.GetComponentsInChildren<InputField>(true).First(i=>i.name=="CreditsInput");
  Record("multiline-free-text",input.lineType==InputField.LineType.MultiLineNewline&&!input.textComponent.supportRichText&&panel.CaptureDraft().Credits==credits);
  string package=Path.Combine(output,"credits.vmdance");VmdDancePackageBuilder.Build(panel.CaptureDraft(),package);
  Record("open-restores-credits",panel.OpenPackage(package)&&panel.CaptureDraft().Credits==credits);
  ui.Window.SetMiniMode(true);yield return null;ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;panel=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  Record("rebuild-preserves-credits",panel.CaptureDraft().Credits==credits);
  foreach(var lang in new[]{"zh-CN","en"}){
   DanceSettingsHandler.Instance.data.language=lang;DanceLocale.Set(lang);ui.Window.SetTab("import");yield return null;
   panel=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
   Record("language-preserves-"+lang,panel.CaptureDraft().Credits==credits);
   var advanced=panel.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<Text>().text==DanceLocale.T("composer.advanced"));advanced.onClick.Invoke();yield return null;
   var scroll=panel.GetComponentsInChildren<ScrollRect>().First();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;yield return null;yield return Capture("credits-"+lang);
  }
  var draft=panel.CaptureDraft();draft.Credits="任意文字，不必有冒号\n\n末行";panel.RestoreDraft(draft);
  Record("save-edit",panel.SaveEditingPackage());Record("reopen-edit",panel.OpenPackage(package)&&panel.CaptureDraft().Credits==draft.Credits);
  string copy=Path.Combine(output,"copy.vmdance");VmdDancePackageBuilder.Build(panel.CaptureDraft(),copy);
  Record("export-copy",panel.OpenPackage(copy)&&panel.CaptureDraft().Credits==draft.Credits);
  draft=panel.CaptureDraft();draft.Credits="";panel.RestoreDraft(draft);Record("clear-save",panel.SaveEditingPackage());Record("clear-reopen",panel.OpenPackage(copy)&&string.IsNullOrEmpty(panel.CaptureDraft().Credits));
  string old=Path.Combine(output,"old.vmdance");VmdDancePackageBuilder.Build(new VmdDanceDraft{Id="old",MotionVmd=source},old);Record("old-package-empty",panel.OpenPackage(old)&&string.IsNullOrEmpty(panel.CaptureDraft().Credits));
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("CREDITS_COMPLETE");
 }
}
