using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomDancePlayer;
using Newtonsoft.Json;
using UnityEngine;
public partial class DanceAudit {
 private IEnumerator Far27Only(){
  output=Path.Combine(OutputDirectory(),"far27-current");Directory.CreateDirectory(output);while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();var core=ui.playerCore;core.InitPlayer();ui.SetPanelVisible(false);var loader=UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
  string source=Directory.GetFiles("E:/SteamLibrary/steamapps/common/MateEngine/MateEngineX_Data/StreamingAssets/CustomDances","MMD-SOS.unity3d",SearchOption.AllDirectories)[0];string folder=Path.Combine(ui.resourceManager.LibraryFolder,"Far27");Directory.CreateDirectory(folder);string dance=Path.Combine(folder,"MMD-SOS.unity3d");if(!File.Exists(dance))File.Copy(source,dance);
  foreach(string model in new[]{"C:/Users/xp/AppData/LocalLow/Shinymoon/MateEngineX/Steam Workshop/YYBMikuV2.me","E:/SteamLibrary/steamapps/common/MateEngine/Models/Kokoro_Amamiya.me"}){
   core.StopPlay();ui.SetCameraEnabled(false);loader.LoadVRM(model);yield return new WaitForSecondsRealtime(3);var front=Camera.main;float beforeNear=front.nearClipPlane,beforeFar=front.farClipPlane;var beforeMatrix=front.projectionMatrix;
   ui.SetCameraEnabled(true);ui.RefreshAndPlayPackage(dance);yield return new WaitForSecondsRealtime(3);if(!core.Paused)core.TogglePause();
   foreach(float t in new[]{17f,19f,20f,23f}){core.Seek(t/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var cam=ui.CameraSync.RenderCamera;var authored=ui.avatarHelper.CurrentAvatar.transform.Find("Camera_root/Camera_root_1/Camera").GetComponent<Camera>();var bounds=new List<object>();float farthest=0;
    foreach(var renderer in ui.avatarHelper.CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.sharedMesh==null)continue;float distance=Vector3.Distance(cam.transform.position,renderer.bounds.center);float radius=renderer.bounds.extents.magnitude;farthest=Math.Max(farthest,distance+radius);bounds.Add(new{renderer.name,distance,radius,layer=renderer.gameObject.layer});}
    Record("projection-clips-"+Path.GetFileNameWithoutExtension(model)+"-"+t,Math.Abs(cam.projectionMatrix.m22+(cam.farClipPlane+cam.nearClipPlane)/(cam.farClipPlane-cam.nearClipPlane))<.00001&&Math.Abs(cam.projectionMatrix.m23+2f*cam.farClipPlane*cam.nearClipPlane/(cam.farClipPlane-cam.nearClipPlane))<.00001);
    Record("far-model-"+Path.GetFileNameWithoutExtension(model)+"-"+t,cam.farClipPlane>farthest,new{cam.nearClipPlane,cam.farClipPlane,authoredNear=authored.nearClipPlane,authoredFar=authored.farClipPlane,farthest,mainMatrix22=cam.projectionMatrix.m22,mainMatrix23=cam.projectionMatrix.m23,bounds});yield return Capture(Path.GetFileNameWithoutExtension(model)+"-"+t);
   }
   ui.SetCameraEnabled(false);yield return new WaitForEndOfFrame();Record("restore-clips-"+Path.GetFileNameWithoutExtension(model),Math.Abs(front.nearClipPlane-beforeNear)<.00001&&Math.Abs(front.farClipPlane-beforeFar)<.00001&&MatrixDifference(front.projectionMatrix,beforeMatrix)<.00001);core.StopPlay();
  }
  ui.SetCameraEnabled(true);
  var draft=new Maoxig.VmdDanceStudio.VmdDanceDraft{Id="far27-long-pivot",MotionVmd="F:/Program Files/ME/.audit/v0.2/camera26-fixtures/neutral.vmd",CameraVmd="F:/Program Files/ME/.audit/v0.2/camera26-fixtures/long-pivot.vmd",CameraReferenceEyeHeight=ui.avatarHelper.MeasureAvatarEyeHeight(),CameraAuthoringScale=1,PositionScale=.08f};
  string package=Path.Combine(ui.resourceManager.LibraryFolder,"Far27","long-pivot.vmdance");Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(draft,package);ui.RefreshAndPlayPackage(package);float until=Time.realtimeSinceStartup+25;while(core.IsLoading&&Time.realtimeSinceStartup<until)yield return null;yield return new WaitForEndOfFrame();
  var native=core.CurrentVmdPlayer;float actualDistance=Vector3.Distance(native.TargetCamera.transform.position,ui.avatarHelper.CurrentAvatar.transform.position);Record("native-pivot-distance-far",actualDistance>50&&native.TargetCamera.farClipPlane>actualDistance+1,new{actualDistance,native.LastAppliedCameraDistanceWorld,native.TargetCamera.farClipPlane});core.StopPlay();
  File.WriteAllText(Path.Combine(output,"review.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("FAR27_COMPLETE");
 }
}
