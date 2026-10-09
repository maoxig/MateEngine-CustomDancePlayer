using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomDancePlayer;
using Newtonsoft.Json;
using UnityEngine;

public partial class DanceAudit
{
 private IEnumerator PreviewGifOnly()
 {
  output=Path.Combine(OutputDirectory(),"preview-gif-"+DateTime.Now.ToString("HHmmss"));Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();core.StopPlay();ui.SetCameraEnabled(false);
  string model="C:/Users/xp/AppData/LocalLow/Shinymoon/MateEngineX/Steam Workshop/YYBMikuV2.me";
  UnityEngine.Object.FindFirstObjectByType<VRMLoader>().LoadVRM(model);
  yield return new WaitForSecondsRealtime(4);
  var data=DanceSettingsHandler.Instance.data;
  data.language="zh-CN";data.enableDanceUIFollow=true;data.enableWindowFollow=false;
  data.showAvatarShadow=false;data.danceVolume=0;data.autoPlayOnStart=false;
  data.uiBasePosition=new Vector2(-320,0);data.uiRawPosition=data.uiBasePosition;
  DanceLocale.Set("zh-CN");ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);ui.Window.SetTab("library");
  var playlist=core.playlistManager;playlist.Search="";playlist.Format="";
  playlist.SetPlaylistType(DancePlaylistManager.PlaylistType.Folder,"Xenophon");playlist.ApplyFilters();
  var dance=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("MMD-CatchTheWave.unity3d"));
  core.PlayDanceByIndex(playlist.GetIndexByFile(dance.Id));
  yield return new WaitForSecondsRealtime(4);
  if(!core.IsPlaying||core.Duration<=0)throw new InvalidOperationException("Preview dance failed: "+core.LastError);
  core.Seek(30f/core.Duration);
  // Position the front view beside the UI; MMD camera playback remains disabled.
  var camera=Camera.main;var position=camera.transform.position;position.x-=0.6f;camera.transform.position=position;
  camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.12f,1);
  var follower=ui.Window.GetComponentInChildren<HipsFollower>();
  follower.enabled=true;follower.GetComponent<RectTransform>().anchoredPosition=data.uiBasePosition;follower.UpdateBaseAndInitial();
  ui.Window.RefreshLibrary();yield return new WaitForSecondsRealtime(.5f);
  const int frames=96;const float fps=12;
  float start=Time.realtimeSinceStartup;var positions=new System.Collections.Generic.List<object>();
  for(int i=0;i<frames;i++)
  {
   while(Time.realtimeSinceStartup<start+i/fps)yield return null;
   yield return new WaitForEndOfFrame();
   var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
   texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
   File.WriteAllBytes(Path.Combine(output,"frame-"+i.ToString("D4")+".png"),texture.EncodeToPNG());Destroy(texture);
   positions.Add(new{frame=i,time=core.PlaybackTime,panel=follower.GetComponent<RectTransform>().anchoredPosition.ToString()});
  }
  File.WriteAllText(Path.Combine(output,"capture.json"),JsonConvert.SerializeObject(new{model,workshopId=3582662597,dance=dance.Id,frames,fps,mmdCamera=data.enableMMDCamera,panelFollows=data.enableDanceUIFollow,positions},Formatting.Indented));
  Logger.LogInfo("PREVIEW_GIF_OUTPUT "+output);Logger.LogInfo("PREVIEW_GIF_COMPLETE");
 }
}
