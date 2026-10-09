using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomDancePlayer;
using UnityEngine;
using UnityEngine.UI;

public partial class DanceAudit
{
 private IEnumerator DocumentationOnly()
 {
  output=Path.Combine(OutputDirectory(),"documentation-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.playerCore.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);
  var data=DanceSettingsHandler.Instance.data;data.enableDanceUIFollow=false;data.uiRawPosition=Vector2.zero;
  // Keep the translucent panel readable against the isolated game's empty background.
  foreach(var renderer in ui.avatarHelper.CurrentAvatar.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
  ui.Window.SetMiniMode(false);
  var manager=ui.playerCore.playlistManager;manager.Search="";manager.Format="";
  manager.SetPlaylistType(DancePlaylistManager.PlaylistType.Folder,"Xenophon");
  foreach(var language in new[]{"zh-CN","en"})
  {
   data.language=language;DanceLocale.Set(language);ui.Window.SetTab("library");ui.Window.RefreshLibrary();yield return null;
   yield return DocumentationPanel("library-"+language);
   ui.Window.SetMiniMode(true);yield return null;yield return DocumentationPanel("mini-"+language);
   ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;
   var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
   composer.RestoreDraft(new Maoxig.VmdDanceStudio.VmdDanceDraft{Id="catch-the-wave",Title="Catch The Wave",Author="",MotionVmd="motion.vmd",AudioFile="music.ogg"});
   yield return null;yield return DocumentationPanel("composer-"+language);
   ui.Window.SetTab("settings");yield return null;
   var scroll=ui.Window.GetComponentsInChildren<ScrollRect>(true).First(s=>s.gameObject.activeInHierarchy);
   Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;yield return null;
   yield return DocumentationPanel("settings-"+language);
  }
  Logger.LogInfo("DOCUMENTATION_COMPLETE");
 }
 private IEnumerator DocumentationPanel(string name)
 {
  // Capture the actual panel only; sample draft fields are illustrative and are never exported.
  var panel=(RectTransform)typeof(DanceWindow).GetField("panel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui.Window);
  panel.anchoredPosition=Vector2.zero;
  Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
  float scale=ui.Window.Canvas.scaleFactor;int width=Mathf.RoundToInt(panel.rect.width*scale),height=Mathf.RoundToInt(panel.rect.height*scale);
  int x=Mathf.RoundToInt(panel.position.x-panel.rect.width*panel.pivot.x*scale);
  int y=Mathf.RoundToInt(panel.position.y-panel.rect.height*panel.pivot.y*scale);
  var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
  texture.ReadPixels(new Rect(x,y,width,height),0,0);texture.Apply();
  File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
 }
}
