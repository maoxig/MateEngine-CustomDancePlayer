using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomDancePlayer;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public partial class DanceAudit
{
 private IEnumerator FooterOnly()
 {
  output=Path.Combine(OutputDirectory(),"footer-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);
  var data=DanceSettingsHandler.Instance.data;data.enableDanceUIFollow=false;
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Title.Contains("California Gurls"));
  core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(3);
  string originalTitle=descriptor.Title;descriptor.Title="Catch The Wave — A very long dance title / 长标题布局检查 · Extended version";
  data.favorites.Remove(descriptor.Id);
  foreach(string language in new[]{"zh-CN","en"})
  foreach(bool mini in new[]{false,true})
  {
   DanceLocale.Set(language);ui.Window.SetMiniMode(mini);ui.Window.SetTab("library");yield return null;yield return new WaitForEndOfFrame();
   var favorite=ui.Window.GetComponentsInChildren<Button>(true).First(b=>b.name=="CurrentDanceFavorite");
   favorite.onClick.Invoke();yield return null;
   Record("favorite-current-"+language+"-"+mini,core.playlistManager.IsFavorite(descriptor.Id)&&favorite.GetComponentInChildren<Text>().text=="★");
   if(language=="zh-CN")yield return Capture("favorite-on-"+(mini?"mini":"full"));
   core.playlistManager.Search="no-match";ui.Window.RefreshLibrary();favorite.onClick.Invoke();yield return null;
   Record("favorite-independent-of-filter-"+language+"-"+mini,!core.playlistManager.IsFavorite(descriptor.Id));
   core.playlistManager.Search="";ui.Window.RefreshLibrary();
   var clip=ui.Window.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="CurrentDanceTitle");
   var a=new Vector3[4];var b=new Vector3[4];favorite.GetComponent<RectTransform>().GetWorldCorners(a);clip.GetWorldCorners(b);
   Record("information-row-layout-"+language+"-"+mini,a[2].x<=b[0].x+0.1f&&clip.rect.width>100&&clip.GetComponent<RectMask2D>()!=null,new{titleWidth=clip.rect.width,starWidth=favorite.GetComponent<RectTransform>().rect.width});
   yield return Capture("footer-"+language+"-"+(mini?"mini":"full"));
  }
  ui.Window.SetMiniMode(false);ui.Window.SetTab("settings");yield return null;
  Record("folder-button",ui.Window.GetComponentsInChildren<Button>(true).Any(b=>b.name=="OpenDanceFolder"));
  Record("bind-panel",ui.Window.TryBindKey(false,KeyCode.F8,false,false,false));yield return null;
  Record("bind-global",ui.Window.TryBindKey(true,KeyCode.F9,true,false,true));yield return null;
  ui.Window.SetTab("settings");yield return null;ui.Window.BeginKeyCapture(true);
  Record("capture-suppression",DanceHotkeys.IsCapturing&&!DanceHotkeys.PanelPressed());
  Record("binding-conflict",!ui.Window.TryBindKey(true,KeyCode.F8,false,false,false));
  ui.Window.TryBindKey(true,KeyCode.F9,true,false,true);Record("recording-key-does-not-trigger",DanceHotkeys.IsSuppressed);yield return null;
  var listener=DanceBootstrap.Root.GetComponentInChildren<GlobalHotkeyListener>(true);
  listener.enabled=true;yield return null;yield return null;yield return null;yield return null;
  Record("global-hook-mount",(IntPtr)typeof(GlobalHotkeyListener).GetField("_hookId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(listener)!=IntPtr.Zero);
  typeof(GlobalHotkeyListener).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(listener,null);
  var apply=typeof(GlobalHotkeyListener).GetMethod("ApplyKeyState",BindingFlags.Instance|BindingFlags.NonPublic);
  Func<int,bool,bool,bool> key=(vk,down,up)=>(bool)apply.Invoke(listener,new object[]{vk,down,up});
  key(0xA2,true,false);key(0xA0,true,false);
  Record("custom-global-chord",key(120,true,false));Record("global-no-autorepeat",!key(120,true,false));
  key(120,false,true);key(0xA0,false,true);Record("global-modifier-match",!key(120,true,false));
  key(120,false,true);key(0xA2,false,true);
  listener.enabled=false;
  DanceSettingsHandler.Instance.SaveToDisk();DanceSettingsHandler.Instance.LoadFromDisk();
  Record("binding-save-reload",DanceSettingsHandler.Instance.data.toggleKey==KeyCode.F8&&DanceSettingsHandler.Instance.data.globalPlaybackKey==KeyCode.F9&&DanceSettingsHandler.Instance.data.globalShift);
  foreach(string language in new[]{"en","zh-CN"})
  {
   DanceLocale.Set(language);ui.Window.SetTab("settings");yield return null;
   var scroll=ui.Window.GetComponentsInChildren<ScrollRect>(true).First(s=>s.gameObject.activeInHierarchy);scroll.verticalNormalizedPosition=0;yield return new WaitForEndOfFrame();
   yield return Capture("settings-bottom-"+language);
  }
  foreach(string language in new[]{"en","zh-CN"}) {
   DanceLocale.Set(language);ui.Window.SetMiniMode(false);
   foreach(string tab in new[]{"library","import","settings"}) {
    ui.Window.SetTab(tab);yield return null;
    var transport=(GameObject)typeof(DanceWindow).GetField("libraryTransport",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui.Window);
    Record("footer-tab-"+language+"-"+tab,transport.activeSelf==(tab!="settings"));
    yield return Capture("tab-"+language+"-"+tab);
   }
   var check=ui.Window.GetComponentsInChildren<Toggle>(true).First(t=>t.name=="Toggle");
   check.isOn=true;var on=((Image)check.targetGraphic).color;check.isOn=false;var off=((Image)check.targetGraphic).color;
   Record("toggle-contrast-"+language,on!=off && check.graphic is Text && ((Text)check.graphic).text=="✓");
  }
  foreach(DanceHotkeys.Action action in Enum.GetValues(typeof(DanceHotkeys.Action)))if(action!=DanceHotkeys.Action.Panel)ui.Window.TryBindKey(action,KeyCode.None,false,false,false);
  yield return null;yield return null;yield return null;
  Record("empty-bindings-no-hook",!listener.enabled && (IntPtr)typeof(GlobalHotkeyListener).GetField("_hookId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(listener)==IntPtr.Zero);
  Record("new-binding-defaults",new DanceSettingsHandler.DanceSettingsData().playStopKey.key==KeyCode.None && new DanceSettingsHandler.DanceSettingsData().nextKey.key==KeyCode.None);
  ui.Window.TryBindKey(DanceHotkeys.Action.PlayStop,KeyCode.F10,true,false,false);yield return null;yield return null;yield return null;
  Record("optional-binding-mounts",listener.enabled && (IntPtr)typeof(GlobalHotkeyListener).GetField("_hookId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(listener)!=IntPtr.Zero);
  core.StopPlay();core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(3);
  core.Seek(0);if(!core.Paused)core.TogglePause();listener.Dispatch(DanceHotkeys.Action.PlayStop);
  Record("play-stop-paused-zero-stops",!core.IsPlaying);
  listener.Dispatch(DanceHotkeys.Action.PlayStop);yield return new WaitForSecondsRealtime(3);
  Record("play-stop-restarts",core.IsPlaying && !core.Paused);listener.Dispatch(DanceHotkeys.Action.Stop);Record("explicit-stop",!core.IsPlaying);
  DanceHotkeys.SetEnabled(DanceHotkeys.Action.PlayStop,false);yield return null;
  Record("disabled-keeps-binding",DanceHotkeys.Get(DanceHotkeys.Action.PlayStop).key==KeyCode.F10 && !listener.enabled);
  DanceHotkeys.SetEnabled(DanceHotkeys.Action.PlayStop,true);yield return null;yield return null;yield return null;
  Record("reenable-keeps-binding",listener.enabled);
  ui.Window.TryBindKey(DanceHotkeys.Action.PlayStop,KeyCode.None,false,false,false);
  ui.SetPanelVisible(true);ui.Window.SetTab("library");
  foreach(bool mini in new[]{false,true}) {
   ui.Window.SetMiniMode(mini);yield return null;
   var panel=ui.Window.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="PlayerPanel");
   var corners=new Vector3[4];panel.GetWorldCorners(corners);Vector2 inside=(corners[0]+corners[2])*0.5f,outside=new Vector2(-1000,-1000);
   ui.UpdatePointerOwnership(inside,true,true);Record("panel-blocks-drag-"+mini,ui.PanelOwnsPointer && DancePlayerUIManager.SuppressHostDragAnimation());
   ui.UpdatePointerOwnership(outside,false,true);Record("panel-gesture-latched-"+mini,ui.PanelOwnsPointer);
   ui.UpdatePointerOwnership(outside,false,false);Record("avatar-area-unblocked-"+mini,!ui.PanelOwnsPointer && !MenuActions.IsMovementBlocked());
   ui.UpdatePointerOwnership(outside,true,true);ui.UpdatePointerOwnership(inside,false,true);Record("avatar-gesture-can-cross-panel-"+mini,!ui.PanelOwnsPointer);ui.UpdatePointerOwnership(outside,false,false);
   var drag=panel.GetComponent<UIDragHandler>();var before=ui.avatarHelper.CurrentAnimator.transform.position;
   var handle=panel.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(i=>i.gameObject.name=="Row");
   var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,pointerPressRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=handle.gameObject},delta=new Vector2(20,0) };
   drag.OnBeginDrag(pointer);drag.OnDrag(pointer);drag.OnEndDrag(pointer);
   Record("panel-drag-does-not-move-avatar-"+mini,ui.avatarHelper.CurrentAnimator.transform.position==before);
   var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();ui.Window.GetComponent<GraphicRaycaster>().Raycast(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(10,10)},hits);
   Record("panel-no-fullscreen-raycast-"+mini,hits.Count==0);

  }
  var plugin=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="CustomDancePlayer.BepInEx").GetType("DancePlugin");
  var controller=UnityEngine.Object.FindFirstObjectByType<AvatarAnimatorController>();
  var prefix=plugin.GetMethod("AvatarUpdatePrefix",BindingFlags.Static|BindingFlags.NonPublic);
  Record("native-drag-prefix-outside",controller!=null && (bool)prefix.Invoke(null,new object[]{controller}));
  ui.OnPlayPauseBtnClick();yield return new WaitForSecondsRealtime(3);
  Record("native-drag-animation-suppressed-dancing",!(bool)prefix.Invoke(null,new object[]{controller}));core.StopPlay();
  var random=new System.Random(23);bool geometry=true;int solutions=0;
  for(int i=0;i<1000;i++) {
   var desired=new Vector3(0,(float)random.NextDouble()*3f,0);
   var a=new Vector3((float)random.NextDouble()-.5f,0,(float)random.NextDouble()-.5f);
   var b=new Vector3((float)random.NextDouble()-.5f,.2f,(float)random.NextDouble()-.5f);
   Vector3 projected;
   if(Maoxig.RuntimeVmd.VmdSupportRetargeting.TryKeepHorizontalRoot(desired,a,1f,b,1.1f,true,out projected)) {
    solutions++;geometry &= projected.x==desired.x && projected.z==desired.z && (projected-a).sqrMagnitude<1.00001f && (projected-b).sqrMagnitude<1.21001f;
   }
  }
  Record("vertical-support-feasible-geometry",geometry && solutions==1000,new{solutions});
  Vector3 ignored;Record("vertical-support-infeasible-fallback",!Maoxig.RuntimeVmd.VmdSupportRetargeting.TryKeepHorizontalRoot(Vector3.zero,Vector3.right*2,1f,Vector3.zero,1f,false,out ignored));
  descriptor.Title=originalTitle;core.StopPlay();
  Record("favorite-after-stop",ui.Window.GetComponentsInChildren<Button>(true).First(b=>b.name=="CurrentDanceFavorite").interactable);
  File.WriteAllText(Path.Combine(output,"footer.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("FOOTER_AUDIT_COMPLETE");
 }
}
