using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using CustomDancePlayer;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using Maoxig.RuntimeVmd;

[BepInPlugin("maoxig.customdanceplayer.auditrunner", "Dance audit runner", "0.2.0")]
[BepInDependency("maoxig.customdanceplayer")]
public partial class DanceAudit : BaseUnityPlugin
{
 private readonly List<object> results = new List<object>();
 private DancePlayerUIManager ui;
 private string output;
 void Start()
 {
  if (!Environment.GetCommandLineArgs().Contains("--cdp-audit")) return;
  StartCoroutine(Environment.GetCommandLineArgs().Contains("--cdp-dialog-only") ? DialogOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-release-ui-audit") ? ReleaseUiOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-credits-audit") ? CreditsOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-gif-audit") ? PreviewGifOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-docs-audit") ? DocumentationOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-far27-audit") ? Far27Only() :
   Environment.GetCommandLineArgs().Contains("--cdp-scale26-audit") ? Scale26Only() :
   Environment.GetCommandLineArgs().Contains("--cdp-camera26-diagnostic") ? Camera26Diagnostic() :
   Environment.GetCommandLineArgs().Contains("--cdp-review25-audit") ? Review25Only() :
   Environment.GetCommandLineArgs().Contains("--cdp-review24-audit") ? Review24Only() :
   Environment.GetCommandLineArgs().Contains("--cdp-footer-audit") ? FooterOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-morph-audit") ? MorphOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-expression-audit") ? LegacyMorphOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-motion-pair-audit") ? MotionPairOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-menus-audit") ? NativeMenusOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-continuity-audit") ? ContinuityOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-refresh-audit") ? RefreshOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-shadow-audit") ? ShadowOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-composer-audit") ? ComposerOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-support-audit") ? SupportOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-drag-audit") ? DragOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-retarget-audit") ? RetargetOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-repro-audit") ? UserReproOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-frame-audit") ? FrameOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-quality-audit") ? QualityOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-startup-audit") ? StartupOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-sweep-audit") ? SweepOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-legacy-seek-audit") ? LegacySeekOnly() :
   Environment.GetCommandLineArgs().Contains("--cdp-posture-audit") ? PostureOnly() : Run());
 }
 private static float QuaternionDegrees(Quaternion a,Quaternion b)
 {double dot=Math.Abs((double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w);double norm=Math.Sqrt(((double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w)*((double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w));return (float)(2*Math.Acos(Math.Min(1,dot/Math.Max(norm,1e-12)))*180/Math.PI);}
 private IEnumerator NativeMenusOnly()
 {
  output=Path.Combine(OutputDirectory(),"menus-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  var core=ui.playerCore;core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var menus=UnityEngine.Object.FindFirstObjectByType<MenuActions>();menus.CloseAllMenus();
  ui.SetPanelVisible(true);ui.SetCameraEnabled(false);
  var camera=Camera.main;var baseline=camera.projectionMatrix;float near=camera.nearClipPlane,far=camera.farClipPlane;
  var dance=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("02-angelite"));
  ui.SetCameraEnabled(true);core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(dance.Id));
  float deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
  yield return new WaitForSecondsRealtime(1);core.TogglePause();core.Seek(15/core.Duration);yield return new WaitForEndOfFrame();
  Record("h-panel-authored-camera",core.CurrentVmdPlayer!=null&&ui.CameraSync.IsUsingDanceView&&MatrixDifference(baseline,camera.projectionMatrix)>0.01f);
  foreach(string name in new[]{"SettingsMenuCanvas","BlendshapeMenuCanvas"})
  {
   var entry=menus.menuEntries.First(e=>e.menu!=null&&e.menu.name==name);
   entry.menu.SetActive(true);yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
   Record("native-menu-front-camera-"+name,!ui.CameraSync.IsUsingDanceView&&MatrixDifference(baseline,camera.projectionMatrix)<0.001f&&Math.Abs(near-camera.nearClipPlane)<0.0001f&&Math.Abs(far-camera.farClipPlane)<0.001f,new{active=entry.menu.activeInHierarchy,near=camera.nearClipPlane,far=camera.farClipPlane});
   Record("native-menu-visible-"+name,entry.menu.activeInHierarchy&&entry.menu.GetComponentsInChildren<Canvas>(true).All(c=>c.enabled));
   yield return Capture("native-"+name);
   entry.menu.SetActive(false);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   Record("native-menu-resume-"+name,core.IsPlaying&&ui.CameraSync.IsUsingDanceView&&DanceSettingsHandler.Instance.data.enableMMDCamera&&MatrixDifference(baseline,camera.projectionMatrix)>0.01f);
  }
  // Open after pose evaluation, immediately before rendering, to exercise the
  // render-driver ownership check instead of only the Update path.
  var settings=menus.menuEntries.First(e=>e.menu!=null&&e.menu.name=="SettingsMenuCanvas").menu;
  core.CurrentVmdPlayer.ApplyAtTime(15);settings.SetActive(true);yield return new WaitForEndOfFrame();
  Record("native-menu-late-open",!core.CurrentVmdPlayer.HasAppliedCameraPose&&MatrixDifference(baseline,camera.projectionMatrix)<0.001f);
  settings.SetActive(false);core.Seek(16/core.Duration);settings.SetActive(true);core.Seek(17/core.Duration);yield return new WaitForEndOfFrame();
  Record("native-menu-close-seek-reopen",MatrixDifference(baseline,camera.projectionMatrix)<0.001f&&!core.CurrentVmdPlayer.HasAppliedCameraPose);
  core.StopPlay();yield return new WaitForEndOfFrame();
  Record("stop-from-native-menu",!core.IsPlaying&&settings.activeInHierarchy&&MatrixDifference(baseline,camera.projectionMatrix)<0.001f);
  menus.CloseAllMenus();ui.SetPanelVisible(true);ui.SetCameraEnabled(false);
  Record("native-menu-recovery",ui.Window!=null&&!core.IsPlaying);
  var fixes=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="MateEngineFixes")?.GetType("HostFixPlugin");
  if(fixes!=null)
  {
   var library=UnityEngine.Object.FindObjectsByType<AvatarLibraryMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
   int pending=(int)fixes.GetProperty("DeferredLibraryRefreshes").GetValue(null);
   settings.SetActive(true);
   library.OpenLibrary();yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
   Record("host-fixes-lazy-library-visible",pending>0&&library.contentParent.gameObject.activeInHierarchy&&library.contentParent.childCount>0&&(int)fixes.GetProperty("CompletedLibraryRefreshes").GetValue(null)>0,new{pending,items=library.contentParent.childCount,active=library.contentParent.gameObject.activeInHierarchy,completed=fixes.GetProperty("CompletedLibraryRefreshes").GetValue(null)});
   yield return Capture("native-avatar-library");library.CloseLibrary();settings.SetActive(false);
   Record("host-fixes-cleanup-path",(int)fixes.GetProperty("CleanupCalls").GetValue(null)>0);
   var mod=UnityEngine.Object.FindObjectsByType<MEModHandler>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
   var loadMe=typeof(MEModHandler).GetMethod("LoadME",BindingFlags.Instance|BindingFlags.NonPublic);
   string validMe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData/LocalLow/Shinymoon/MateEngineX/Mods/Kazotsky Kick.me");
   loadMe.Invoke(mod,new object[]{validMe});yield return null;
   var nativeEntries=(IEnumerable)typeof(MEModHandler).GetField("loadedMods",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mod);
   bool validFound=false;foreach(var nativeEntry in nativeEntries){var nt=nativeEntry.GetType();if((string)nt.GetField("name").GetValue(nativeEntry)=="Kazotsky Kick"&&nt.GetField("type").GetValue(nativeEntry).ToString()=="MEDance")validFound=true;}
   Record("host-fixes-valid-me-preserved",validFound);
  }
  File.WriteAllText(Path.Combine(output,"menus.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("MENUS_AUDIT_COMPLETE");
 }
 private IEnumerator ContinuityOnly()
 {
  output=Path.Combine(OutputDirectory(),"continuity-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();ui.SetCameraEnabled(false);ui.SetPanelVisible(false);
  var core=ui.playerCore;core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var reports=new List<object>();
  foreach(string fixture in new[]{"03-catch-the-wave","02-angelite","08-ik-no-morph-melt","12-light-camera-odds"})
  {
   var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains(fixture));core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
   var player=core.CurrentVmdPlayer;if(player==null){Record("continuous-load-"+fixture,false,core.LastError);continue;}core.TogglePause();
   var type=player.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic;var bindings=type.GetField("poseBindings",flags).GetValue(player) as IEnumerable;
   var roles=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
   var source=new Transform[roles.Length];var target=new Transform[roles.Length];foreach(object binding in bindings){var bt=binding.GetType();int i=Array.IndexOf(roles,(HumanBodyBones)bt.GetField("HumanBone").GetValue(binding));if(i>=0){source[i]=(Transform)bt.GetField("Source").GetValue(binding);target[i]=(Transform)bt.GetField("Target").GetValue(binding);}}
   var previousSource=new Quaternion[roles.Length];var previousTarget=new Quaternion[roles.Length];var anomalies=new List<object>();float maximumTargetStep=0,maximumSourceStep=0;int duplicated=0,moving=0;Vector3 previousHips=Vector3.zero,previousSourceHips=Vector3.zero;
   int samples=(int)(core.Duration*120);long ticks=0;
   for(int frame=0;frame<=samples;frame++)
   {
    var begin=System.Diagnostics.Stopwatch.GetTimestamp();player.ApplyAtTime(frame/120f);ticks+=System.Diagnostics.Stopwatch.GetTimestamp()-begin;
    if(frame>0){float sourceMovement=Vector3.Distance(source[0].position,previousSourceHips);float targetMovement=Vector3.Distance(target[0].position,previousHips);if(sourceMovement>0.00001f){moving++;if(targetMovement<0.0000001f)duplicated++;}}
    for(int i=0;i<roles.Length;i++){if(frame>0){float s=QuaternionDegrees(source[i].rotation,previousSource[i]),t=QuaternionDegrees(target[i].rotation,previousTarget[i]);maximumSourceStep=Math.Max(maximumSourceStep,s);maximumTargetStep=Math.Max(maximumTargetStep,t);if(t>20&&s<2&&anomalies.Count<100)anomalies.Add(new{seconds=frame/120f,bone=roles[i].ToString(),sourceStep=s,targetStep=t});}previousSource[i]=source[i].rotation;previousTarget[i]=target[i].rotation;}
    previousHips=target[0].position;previousSourceHips=source[0].position;if(frame%480==0)yield return null;
   }
   Record("continuous-pose-"+fixture,anomalies.Count==0&&duplicated==0,new{samples=samples+1,moving,duplicated,maximumSourceStep,maximumTargetStep,meanEvaluationMs=(double)ticks/System.Diagnostics.Stopwatch.Frequency*1000/(samples+1),anomalies});reports.Add(new{fixture,samples=samples+1,anomalies});
   var evaluated=new Quaternion[roles.Length];var evaluatedPositions=new Vector3[roles.Length];int poseFrame=-1;Action renderedPose=()=>{poseFrame=Time.frameCount;for(int i=0;i<roles.Length;i++){evaluated[i]=target[i].rotation;evaluatedPositions[i]=target[i].position;}};player.PoseApplied+=renderedPose;
   player.Seek(15,true);player.Play();float end=Time.realtimeSinceStartup+2;int renderFrames=0,missed=0;float driftAngle=0,driftPosition=0;var frameTimes=new List<double>();long stamp=System.Diagnostics.Stopwatch.GetTimestamp();
   while(Time.realtimeSinceStartup<end){yield return new WaitForEndOfFrame();long now=System.Diagnostics.Stopwatch.GetTimestamp();frameTimes.Add((double)(now-stamp)/System.Diagnostics.Stopwatch.Frequency*1000);stamp=now;renderFrames++;if(poseFrame!=Time.frameCount)missed++;for(int i=0;i<roles.Length;i++){driftAngle=Math.Max(driftAngle,QuaternionDegrees(evaluated[i],target[i].rotation));driftPosition=Math.Max(driftPosition,Vector3.Distance(evaluatedPositions[i],target[i].position));}}
   player.PoseApplied-=renderedPose;frameTimes.Sort();Record("rendered-pose-"+fixture,renderFrames>10&&missed==0&&driftAngle<0.05f&&driftPosition<0.0001f,new{renderFrames,missed,driftAngle,driftPosition,p95FrameMs=frameTimes[(int)(frameTimes.Count*0.95)],maxFrameMs=frameTimes.Last()});core.StopPlay();
  }
  File.WriteAllText(Path.Combine(output,"continuity.json"),JsonConvert.SerializeObject(new{results,reports},Formatting.Indented));Logger.LogInfo("CONTINUITY_AUDIT_COMPLETE");
 }
 private static bool SameSource(string a,string b) => a==null||b==null?a==b:File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
 private IEnumerator RefreshOnly()
 {
  output=Path.Combine(OutputDirectory(),"refresh-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetCameraEnabled(false);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;
  var helper=ui.avatarHelper;var core=ui.playerCore;core.InitPlayer();core.StopPlay();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var measure=typeof(DanceAvatarHelper).GetMethod("CalculateAvatarHeight",flags);float baseHeight=helper.MeasureAvatarHeight();
  Record("rest-height-available",baseHeight>0.5f&&baseHeight<2.5f,new{baseHeight,avatar=helper.CurrentAvatar.name});
  var savedScale=helper.CurrentAvatar.transform.localScale;var animator=helper.CurrentAnimator;var head=animator.GetBoneTransform(HumanBodyBones.Head);var headSaved=head.localPosition;
  foreach(float scale in new[]{0.4f,1f,3f,1.8f,0.6f,2.8f})
  {
   helper.CurrentAvatar.transform.localScale=savedScale*scale;head.localPosition=headSaved+new Vector3(0,4,0);float measured=(float)measure.Invoke(helper,null);
   Record("intrinsic-height-scale-"+scale,Math.Abs(measured-baseHeight)<0.0001f,new{measured,baseHeight,scale});
  }
  head.localPosition=headSaved;helper.CurrentAvatar.transform.localScale=savedScale;
  foreach(string modelPath in new[]{"E:/SteamLibrary/steamapps/common/MateEngine/Models/橘雪莉.vrm","E:/SteamLibrary/steamapps/common/MateEngine/Models/Koseki Bijou 黒逆バニー.vrm","Lazuli_VRM_Clothes"})
  {
   var before=helper.CurrentAvatar;UnityEngine.Object.FindFirstObjectByType<VRMLoader>().LoadVRM(modelPath);float deadline=Time.realtimeSinceStartup+25;
   while((helper.CurrentAvatar==null||helper.CurrentAvatar==before)&&Time.realtimeSinceStartup<deadline)yield return null;yield return new WaitForSecondsRealtime(1);
   bool loaded=helper.CurrentAvatar!=null&&helper.CurrentAvatar!=before;Record("height-model-switch-"+Path.GetFileName(modelPath),loaded);if(!loaded)continue;
   float expected=helper.MeasureAvatarHeight();var root=helper.CurrentAvatar.transform;var original=root.localScale;
   foreach(float scale in new[]{2.5f,0.5f,1.2f}){root.localScale=original*scale;float result=(float)measure.Invoke(helper,null);Record("height-switched-invariant-"+Path.GetFileName(modelPath)+"-"+scale,expected>0.5f&&expected<2.5f&&Math.Abs(result-expected)<0.0001f,new{expected,result});}
   root.localScale=original;
  }
  string sourcePackage=Directory.GetFiles(ui.resourceManager.LibraryFolder,"*02-angelite*.vmdance",SearchOption.AllDirectories).First();
  VmdDancePackageDescriptor sourceDescriptor;string sourceError;VmdDancePackage.TryOpenArchive(sourcePackage,Path.Combine(output,"source-cache"),out sourceDescriptor,out sourceError);
  var originalJson=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(sourceDescriptor.ManifestPath));originalJson["userNote"]="Local editing fixture";File.WriteAllText(Path.Combine(sourceDescriptor.PackageRoot,"notes.txt"),"Local editing fixture; keep with exported copy.");
  var sourceDraft=new Maoxig.VmdDanceStudio.VmdDanceDraft{Id=sourceDescriptor.Id,Title=sourceDescriptor.Title,Author=sourceDescriptor.Author,MotionVmd=sourceDescriptor.PrimaryVmdPath,AudioFile=sourceDescriptor.AudioPath,FaceVmd=sourceDescriptor.FaceVmdPath,LipVmd=sourceDescriptor.LipVmdPath,CameraVmd=sourceDescriptor.CameraVmdPath,ReferencePmx=sourceDescriptor.ReferencePmxPath,AdditionalVmdFiles=new List<string>(sourceDescriptor.AdditionalVmdPaths),AudioOffsetSeconds=sourceDescriptor.AudioOffsetSeconds,PositionScale=sourceDescriptor.PositionScale??0.08f,Loop=sourceDescriptor.Loop??false,FootIk=sourceDescriptor.FootIk,PreserveSlotTracks=true,OriginalPackageRoot=sourceDescriptor.PackageRoot,OriginalManifestJson=originalJson.ToString(),OriginalFaceVmd=sourceDescriptor.FaceVmdPath,OriginalLipVmd=sourceDescriptor.LipVmdPath,OriginalCameraVmd=sourceDescriptor.CameraVmdPath};
  sourceDraft.FaceVmd=sourceDescriptor.PrimaryVmdPath;sourceDraft.LipVmd=sourceDescriptor.PrimaryVmdPath;sourceDraft.OriginalFaceVmd=sourceDraft.FaceVmd;sourceDraft.OriginalLipVmd=sourceDraft.LipVmd;sourceDraft.ReferencePmx=Path.Combine(Path.GetDirectoryName(typeof(DancePlayerCore).Assembly.Location),"Native","_model.pmx");sourceDraft.FootIk=true;
  string copy=Path.Combine(output,"editable-"+DateTime.Now.ToString("HHmmss")+".vmdance");Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(sourceDraft,copy);ui.Window.SetTab("import");var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  bool opened=composer.OpenPackage(copy);VmdDancePackageDescriptor package;string error;VmdDancePackage.TryOpenArchive(copy,Path.Combine(output,"before-cache"),out package,out error);var draft=composer.CaptureDraft();
  Record("existing-package-all-slots",opened&&draft.Id==package.Id&&draft.Title==package.Title&&draft.Author==(package.Author??"")&&draft.FootIk==package.FootIk&&draft.Loop==(package.Loop??false)&&draft.AudioOffsetSeconds==package.AudioOffsetSeconds&&draft.PositionScale==(package.PositionScale??0.08f)&&SameSource(draft.MotionVmd,package.PrimaryVmdPath)&&SameSource(draft.FaceVmd,package.FaceVmdPath)&&SameSource(draft.LipVmd,package.LipVmdPath)&&SameSource(draft.CameraVmd,package.CameraVmdPath)&&SameSource(draft.AudioFile,package.AudioPath)&&SameSource(draft.ReferencePmx,package.ReferencePmxPath)&&draft.AdditionalVmdFiles.Count==package.AdditionalVmdPaths.Length&&draft.AdditionalVmdFiles.Zip(package.AdditionalVmdPaths,SameSource).All(value=>value),error);
  draft.Title="Edited 中文测试";draft.Author="Local test";draft.AudioOffsetSeconds=0.1234567f;draft.PositionScale=0.081234567f;draft.FootIk=false;draft.Loop=true;composer.RestoreDraft(draft);
  ui.Window.SetMiniMode(true);yield return null;ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  Record("edit-survives-mini",composer.EditingPackagePath==copy&&composer.CaptureDraft().Title==draft.Title&&Math.Abs(composer.CaptureDraft().AudioOffsetSeconds-draft.AudioOffsetSeconds)<0.0000001f);
  bool saved=composer.SaveEditingPackage();VmdDancePackageDescriptor edited;VmdDancePackage.TryOpenArchive(copy,Path.Combine(output,"after-cache"),out edited,out error);
  Record("existing-save-and-backup",saved&&Directory.GetFiles(output,Path.GetFileName(copy)+".bak-*").Length==1&&edited.Title==draft.Title&&edited.Author==draft.Author&&edited.FootIk==false&&edited.Loop==true&&Math.Abs(edited.AudioOffsetSeconds-draft.AudioOffsetSeconds)<0.000001f&&Math.Abs(edited.PositionScale.Value-draft.PositionScale)<0.000001f,error);
  Record("existing-tracks-preserved",File.ReadAllBytes(package.PrimaryVmdPath).SequenceEqual(File.ReadAllBytes(edited.PrimaryVmdPath))&&package.OverlayVmdPaths.Length==edited.OverlayVmdPaths.Length&&package.OverlayVmdPaths.Zip(edited.OverlayVmdPaths,(a,b)=>File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b))).All(value=>value));
  Record("existing-extra-metadata-assets",File.Exists(Path.Combine(edited.PackageRoot,"notes.txt"))&&Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(edited.ManifestPath))["userNote"].ToString()=="Local editing fixture");
  var replacement=VmdReader.Read(package.PrimaryVmdPath);replacement.BoneFrames[0].Position+=new Vector3(500,400,300);string replacementPath=Path.Combine(output,"replacement-face.vmd");File.WriteAllBytes(replacementPath,VmdWriter.Write(replacement));var replaceDraft=composer.CaptureDraft();replaceDraft.FaceVmd=replacementPath;
  var replaced=Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(replaceDraft,Path.Combine(output,"replaced-face-"+DateTime.Now.ToString("HHmmss")+".vmdance"));VmdDancePackageDescriptor replacementPackage;VmdDancePackage.TryOpenArchive(replaced.OutputPath,Path.Combine(output,"replacement-cache"),out replacementPackage,out error);Record("editing-new-slot-scoped",VmdReader.Read(replacementPackage.FaceVmdPath).BoneFrames.Count==0&&File.ReadAllBytes(package.PrimaryVmdPath).SequenceEqual(File.ReadAllBytes(replacementPackage.PrimaryVmdPath)));
  foreach(string language in new[]{"zh-CN","en"}){DanceLocale.Set(language);ui.Window.SetTab("import");yield return null;yield return new WaitForSecondsRealtime(0.2f);composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);Record("edit-language-context-"+language,composer.EditingPackagePath==copy&&composer.CaptureDraft().Title==draft.Title);yield return Capture(language+"-existing-package");}
  ui.SetPanelVisible(false);ui.SetCameraEnabled(true);var camera=Camera.main;float near=camera.nearClipPlane,far=camera.farClipPlane;var projection=camera.projectionMatrix;
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains("02-angelite"));core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float loadEnd=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<loadEnd)yield return null;
  Record("camera-vmd-loaded",core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded,core.LastError);
  if(core.CurrentVmdPlayer!=null){core.TogglePause();foreach(float seconds in new[]{0f,15f,39f,60f}){core.Seek(seconds/core.Duration);yield return new WaitForEndOfFrame();var player=core.CurrentVmdPlayer;float expected=1f/(float)Math.Tan(camera.fieldOfView*Math.PI/360);bool correct=core.RuntimeCameraPerspective?Math.Abs(camera.projectionMatrix.m11-expected)<0.001f&&camera.projectionMatrix.m32==-1f:Math.Abs(camera.projectionMatrix.m11-90f/(player.LastAppliedCameraDistanceWorld*25f))<0.001f&&camera.projectionMatrix.m33==1f;Record("camera-projection-clipping-"+seconds,correct&&camera.nearClipPlane<0.02f&&camera.farClipPlane>player.LastAppliedCameraDistanceWorld,new{seconds,fov=camera.fieldOfView,near=camera.nearClipPlane,far=camera.farClipPlane,distance=player.LastAppliedCameraDistanceWorld,perspective=core.RuntimeCameraPerspective});yield return Capture("camera-"+seconds);}}
  core.StopPlay();ui.SetCameraEnabled(false);yield return new WaitForEndOfFrame();Record("camera-clips-restored",Math.Abs(camera.nearClipPlane-near)<0.000001f&&Math.Abs(camera.farClipPlane-far)<0.0001f&&MatrixDifference(camera.projectionMatrix,projection)<0.001f);
  core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));loadEnd=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<loadEnd)yield return null;
  core.TogglePause();var frontRoot=helper.CurrentAvatar.transform;var frontScale=frontRoot.localScale;var keeper=DanceBootstrap.Root.GetComponentInChildren<DanceCameraDistKeeper>(true);bool keeperEnabled=keeper.enabled;keeper.enabled=true;
  foreach(float size in new[]{0.5f,1f,3f}){frontRoot.localScale=frontScale*size;core.Seek(15f/core.Duration);yield return new WaitForEndOfFrame();float expectedDistance=keeper.fixedZDistance*Math.Abs(frontRoot.lossyScale.z);Record("front-camera-display-size-"+size,Math.Abs((camera.transform.position.z-helper.CurrentAvatarHips.position.z)-expectedDistance)<0.001f);yield return Capture("front-scaled-"+size);}
  frontRoot.localScale=frontScale;keeper.enabled=keeperEnabled;core.StopPlay();
  var cameraMotion=VmdReader.Read(package.PrimaryVmdPath);cameraMotion.CameraFrames.Clear();
  foreach(var key in new[]{new VmdCameraKeyframe{FrameNumber=0,Distance=-25,Position=new Vector3(0,14,0),FieldOfView=30,Perspective=true},new VmdCameraKeyframe{FrameNumber=90,Distance=-25,Position=new Vector3(0,14,0),FieldOfView=60,Perspective=true},new VmdCameraKeyframe{FrameNumber=180,Distance=-25,Position=new Vector3(0,14,0),FieldOfView=30,Perspective=false},new VmdCameraKeyframe{FrameNumber=270,Distance=25,Position=new Vector3(0,14,0),FieldOfView=60,Perspective=false}})cameraMotion.CameraFrames.Add(key);
  string cameraPath=Path.Combine(output,"projection-test.vmd");File.WriteAllBytes(cameraPath,VmdWriter.Write(cameraMotion));var cameraDraft=new Maoxig.VmdDanceStudio.VmdDanceDraft{Id="rc18-projection-fixture",Title="Camera projection test",MotionVmd=cameraPath};ui.Window.SetTab("import");composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);composer.RestoreDraft(cameraDraft);ui.SetCameraEnabled(true);Button(composer.transform,"composer.preview").onClick.Invoke();loadEnd=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<loadEnd)yield return null;
  if(core.CurrentVmdPlayer!=null){core.TogglePause();float perspective30=0,perspective60=0,ortho30=0;foreach(float seconds in new[]{0f,3f,6f,9f}){core.Seek(seconds/core.Duration);yield return new WaitForEndOfFrame();float m11=camera.projectionMatrix.m11;if(seconds==0)perspective30=m11;if(seconds==3)perspective60=m11;if(seconds==6)ortho30=m11;Record("authored-projection-"+seconds,seconds<6?core.RuntimeCameraPerspective&&camera.projectionMatrix.m32==-1:!core.RuntimeCameraPerspective&&camera.projectionMatrix.m33==1);yield return Capture("projection-mode-"+seconds);if(seconds==9)Record("orthographic-distance-not-fov",Math.Abs(m11-ortho30)<0.00001f);}Record("authored-fov-changes-image",perspective30>perspective60*1.5f);core.StopPlay();}
  ui.SetCameraEnabled(false);ui.SetPanelVisible(false);
  var sampleRoot="F:/Program Files/ME/VmdDance-Import-Samples/rc18-20261007";
  ui.SetPanelVisible(true);ui.Window.SetTab("import");composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  foreach(string sample in Directory.GetFiles(sampleRoot,"*.vmdance",SearchOption.AllDirectories))
  {
   bool valid=composer.OpenPackage(sample);bool expectedValid=sample.IndexOf("07-Validation-Errors",StringComparison.OrdinalIgnoreCase)<0;
   Record("sample-open-"+Path.GetFileName(sample),valid==expectedValid);
   if(valid)Record("sample-form-valid-"+Path.GetFileName(sample),Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Validate(composer.CaptureDraft()).Count==0);
  }
  File.WriteAllText(Path.Combine(output,"refresh.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("REFRESH_AUDIT_COMPLETE");
 }
 private IEnumerator ShadowOnly()
 {
  output=Path.Combine(OutputDirectory(),"shadow-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);
  var settings=DanceSettingsHandler.Instance;bool savedVisible=settings.data.showAvatarShadow,savedFollow=settings.data.enableShadowFollow;
  var follower=DanceBootstrap.Root.GetComponentInChildren<DanceShadowFollower>(true);
  var shadow=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Shadow"&&t.GetComponent<Renderer>()!=null);
  Record("host-shadow-board-found",shadow!=null);if(shadow==null){Logger.LogInfo("SHADOW_AUDIT_COMPLETE");yield break;}
  bool active=shadow.gameObject.activeSelf;settings.data.showAvatarShadow=true;follower.ApplyVisibility();shadow.gameObject.SetActive(true);
  ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);DanceLocale.Set("zh-CN");ui.Window.SetTab("settings");yield return null;
  var toggle=ui.Window.GetComponentsInChildren<Toggle>(true).First(t=>t.transform.parent.GetComponentsInChildren<Text>(true).Any(label=>label.text==DanceLocale.T("settings.shadowVisible")));toggle.isOn=false;yield return new WaitForEndOfFrame();
  Record("ui-disables-board-gameobject",!shadow.gameObject.activeSelf&&!settings.data.showAvatarShadow);yield return new WaitForSecondsRealtime(0.3f);yield return Capture("zh-CN-shadow-disabled");
  settings.data.enableShadowFollow=false;shadow.gameObject.SetActive(true);yield return null;yield return new WaitForEndOfFrame();Record("host-reactivation-suppressed-without-follow",!shadow.gameObject.activeSelf);
  follower.ClearShadowCache();follower.ApplyVisibility();Record("inactive-board-rediscovered",!shadow.gameObject.activeSelf);
  settings.SaveToDisk();var roundtrip=JsonConvert.DeserializeObject<DanceSettingsHandler.DanceSettingsData>(File.ReadAllText(Path.Combine(Application.persistentDataPath,"danceSettings.json")));Record("global-shadow-setting-persisted",!roundtrip.showAvatarShadow);
  toggle.isOn=true;yield return null;Record("board-restored",shadow.gameObject.activeSelf);
  shadow.gameObject.SetActive(false);settings.data.showAvatarShadow=false;follower.ApplyVisibility();settings.data.showAvatarShadow=true;follower.ApplyVisibility();Record("host-inactive-state-preserved",!shadow.gameObject.activeSelf);
  DanceLocale.Set("en");ui.Window.SetTab("settings");yield return null;Record("english-global-control",ui.Window.GetComponentsInChildren<Text>(true).Any(t=>t.text=="Show shadow"));yield return Capture("en-shadow-settings");
  settings.data.showAvatarShadow=savedVisible;settings.data.enableShadowFollow=savedFollow;follower.ClearShadowCache();shadow.gameObject.SetActive(active);follower.ApplyVisibility();settings.SaveToDisk();
  yield return WaitForLibrary();var core=ui.playerCore;core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var avatar=ui.avatarHelper.CurrentAvatar.transform;var savedScale=avatar.localScale;
  foreach(string fixture in new[]{"03-catch-the-wave","02-angelite"})
  {
   var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.Contains(fixture));core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
   bool ready=core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded;Record("scale-load-"+fixture,ready,core.LastError);if(!ready)continue;
   core.TogglePause();core.Seek(15f/core.Duration);yield return new WaitForEndOfFrame();var animator=ui.avatarHelper.CurrentAnimator;
   var roles=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot};var before=roles.Select(role=>animator.GetBoneTransform(role).position-avatar.position).ToArray();float humanScale=core.CurrentVmdPlayer.TargetHumanScale;
   avatar.localScale*=1.4f;core.Seek(15f/core.Duration);yield return new WaitForEndOfFrame();float error=0;for(int i=0;i<roles.Length;i++)error=Math.Max(error,Vector3.Distance(animator.GetBoneTransform(roles[i]).position-avatar.position,before[i]*1.4f));
   Record("live-ik-scale-"+fixture,error<0.005f&&Math.Abs(core.CurrentVmdPlayer.TargetHumanScale/humanScale-1.4f)<0.005f,new{error,ratio=core.CurrentVmdPlayer.TargetHumanScale/humanScale});
   Vector3 latestScale=avatar.localScale;core.StopPlay();yield return new WaitForEndOfFrame();Record("stop-preserves-latest-scale-"+fixture,Vector3.Distance(avatar.localScale,latestScale)<0.0001f,new{latestScale=new[]{latestScale.x,latestScale.y,latestScale.z},actual=new[]{avatar.localScale.x,avatar.localScale.y,avatar.localScale.z}});
  }
  avatar.localScale=savedScale;
  File.WriteAllText(Path.Combine(output,"shadow.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("SHADOW_AUDIT_COMPLETE");
 }
 private IEnumerator ComposerOnly()
 {
  output=Path.Combine(OutputDirectory(),"composer-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();ui.SetCameraEnabled(false);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);
  var settings=DanceSettingsHandler.Instance;settings.data.enableDanceUIFollow=false;
  var migrated=JsonConvert.DeserializeObject<DanceSettingsHandler.DanceSettingsData>("{\"useNativeVmd\":false}");settings.SaveToDisk();Record("native-setting-migration",migrated.useNativeVmd&&!File.ReadAllText(Path.Combine(Application.persistentDataPath,"danceSettings.json")).Contains("useNativeVmd"));
  ui.Window.SetTab("settings");yield return null;Record("native-selector-removed",!ui.Window.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("compatibility mode")||t.text.Contains("近似兼容")));
  var core=ui.playerCore;core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();settings.data.useNativeVmd=false;
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.IndexOf("02-angelite",StringComparison.OrdinalIgnoreCase)>=0);core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
  Record("native-only-even-old-false",core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded,core.LastError);
  string motion=ui.resourceManager.CurrentVmdPath,audio=ui.resourceManager.CurrentVmdAudioPath;string camera=ui.resourceManager.CurrentVmdOverlayPaths.FirstOrDefault(path=>{var info=Maoxig.VmdDanceStudio.VmdInspector.Inspect(path);return info.CameraKeys>0&&info.BoneKeys==0;});core.StopPlay();
  var draft=new Maoxig.VmdDanceStudio.VmdDanceDraft{Id="composer-sync-audit",Title="同步测试 Sync preview",MotionVmd=motion,AudioFile=audio,PositionScale=0.08f};
  DanceLocale.Set("zh-CN");ui.Window.SetTab("import");yield return null;var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);composer.RestoreDraft(draft);Button(composer.transform,"composer.validate").onClick.Invoke();Record("composer-valid-tracks",composer.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith(DanceLocale.T("composer.valid"))));
  if(camera!=null){draft.MotionVmd=camera;composer.RestoreDraft(draft);Button(composer.transform,"composer.validate").onClick.Invoke();Record("camera-cannot-be-body",composer.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith(DanceLocale.T("composer.invalid"))));draft.MotionVmd=motion;composer.RestoreDraft(draft);}
  yield return Capture("zh-CN-composer-basic");
  var help=composer.transform.Find("Composer").GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text=="?");var tooltip=help.GetComponent<DanceTooltip>();var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(ui.Window.Canvas.worldCamera,help.transform.position)};tooltip.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(0.3f);Canvas.ForceUpdateCanvases();Record("tooltip-visible-unclipped",tooltip.Hint.activeInHierarchy&&tooltip.Hint.transform.parent==ui.Window.Canvas.transform);yield return Capture("zh-CN-composer-help");tooltip.OnPointerExit(pointer);
  Button(composer.transform,"composer.preview").onClick.Invoke();deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;Record("composer-preview-owned",core.IsPlaying&&composer.PreviewResourceId==core.CurrentResourceId&&core.CurrentVmdPlayer!=null,new{id=core.CurrentResourceId,composer.PreviewResourceId,error=core.LastError});
  composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="PreviewPause").onClick.Invoke();Record("preview-pause-button",core.Paused);core.Seek(5f/core.Duration);yield return new WaitForEndOfFrame();string previewId=core.CurrentResourceId;float poseTime=core.PlaybackTime;int packageCount=Directory.GetFiles(Path.Combine(ui.resourceManager.LibraryFolder,"VmdComposer"),"*.vmdance").Length;var offset=composer.GetComponentsInChildren<InputField>(true).First(i=>i.name=="AudioOffsetInput");
  composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="SyncEarlier").onClick.Invoke();yield return new WaitForEndOfFrame();Record("audio-earlier-live",Math.Abs(ui.resourceManager.CurrentVmdAudioOffsetSeconds+0.1f)<0.0001f&&Math.Abs(ui.avatarHelper.CurrentAudioSource.time-(poseTime+0.1f))<0.15f&&core.Paused&&core.CurrentResourceId==previewId,new{time=core.PlaybackTime,audioTime=ui.avatarHelper.CurrentAudioSource.time,offset=ui.resourceManager.CurrentVmdAudioOffsetSeconds});
  var later=composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="SyncLater");later.onClick.Invoke();later.onClick.Invoke();yield return new WaitForEndOfFrame();Record("audio-later-live",Math.Abs(ui.resourceManager.CurrentVmdAudioOffsetSeconds-0.1f)<0.0001f&&Math.Abs(ui.avatarHelper.CurrentAudioSource.time-(poseTime-0.1f))<0.15f&&core.Paused);
  composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="SyncReset").onClick.Invoke();Record("audio-reset",ui.resourceManager.CurrentVmdAudioOffsetSeconds==0f&&composer.CaptureDraft().AudioOffsetSeconds==0f);Record("sync-no-repacking",Directory.GetFiles(Path.Combine(ui.resourceManager.LibraryFolder,"VmdComposer"),"*.vmdance").Length==packageCount);
  offset.text="NaN";offset.onEndEdit.Invoke("NaN");Record("audio-invalid-no-change",ui.resourceManager.CurrentVmdAudioOffsetSeconds==0f);offset.text="0";
  ui.Window.SetMiniMode(true);yield return null;ui.Window.SetMiniMode(false);ui.Window.SetTab("import");yield return null;composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="SyncEarlier").onClick.Invoke();Record("sync-survives-mini",composer.PreviewResourceId==previewId&&Math.Abs(ui.resourceManager.CurrentVmdAudioOffsetSeconds+0.1f)<0.0001f);
  var exported=Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(composer.CaptureDraft(),Path.Combine(output,"sync-export.vmdance"));Maoxig.RuntimeVmd.VmdDancePackageDescriptor package;string error;bool opened=Maoxig.RuntimeVmd.VmdDancePackage.TryOpenArchive(exported.OutputPath,Path.Combine(output,"sync-export-cache"),out package,out error);Record("audio-offset-export",opened&&Math.Abs(package.AudioOffsetSeconds+0.1f)<0.0001f,error);
  foreach(string language in new[]{"zh-CN","en"}){DanceLocale.Set(language);ui.Window.SetTab("import");yield return null;composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);yield return Capture(language+"-composer-sync");Button(composer.transform,"composer.advanced").onClick.Invoke();yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();var scroll=composer.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Record(language+"-advanced-expanded",composer.GetComponentsInChildren<InputField>().Length>=10,new{contentHeight=scroll.content.rect.height,viewportHeight=scroll.viewport.rect.height,activeInputs=composer.GetComponentsInChildren<InputField>().Length});yield return Capture(language+"-composer-advanced");ui.Window.SetTab("settings");yield return Capture(language+"-settings-native-only");}
  ui.Window.SetTab("import");composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);composer.GetComponentsInChildren<Button>(true).First(b=>b.name=="PreviewStop").onClick.Invoke();Record("preview-stop-button",!core.IsPlaying);DanceLocale.Set("zh-CN");ui.Window.SetTab("import");yield return null;yield return MixedComposerChecks(motion);File.WriteAllText(Path.Combine(output,"composer.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("COMPOSER_AUDIT_COMPLETE");
 }
 private IEnumerator MixedComposerChecks(string bodyPath)
 {
  string fixtures=Path.Combine(output,"mixed-fixtures");Directory.CreateDirectory(fixtures);
  var main=VmdReader.Read(bodyPath);main.MorphFrames.Add(new VmdMorphKeyframe{MorphName="あ",FrameNumber=0,Weight=0.25f});main.MorphFrames.Add(new VmdMorphKeyframe{MorphName="笑い",FrameNumber=0,Weight=0.6f});main.MorphFrames.Add(new VmdMorphKeyframe{MorphName="独自モーフ",FrameNumber=0,Weight=0.9f});main.CameraFrames.Add(new VmdCameraKeyframe{FrameNumber=0,Distance=30,FieldOfView=45});main.LightFrames.Add(new VmdLightKeyframe{FrameNumber=0,Color=Color.white,Position=Vector3.up});main.SelfShadowFrames.Add(new VmdSelfShadowKeyframe{FrameNumber=0,Mode=1,Distance=0.5f});
  string mainPath=Path.Combine(fixtures,"combined.vmd");File.WriteAllBytes(mainPath,VmdWriter.Write(main));var draft=new Maoxig.VmdDanceStudio.VmdDanceDraft{Id="mixed-body-audit",Title="Combined VMD",MotionVmd=mainPath,FaceVmd=mainPath,LipVmd=mainPath,CameraVmd=mainPath,PositionScale=0.08f};
  var built=Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(draft,Path.Combine(fixtures,"combined.vmdance"));VmdDancePackageDescriptor package;string error;bool opened=VmdDancePackage.TryOpenArchive(built.OutputPath,Path.Combine(fixtures,"combined-cache"),out package,out error);Record("mixed-main-no-duplicate",opened&&package.OverlayVmdPaths.Length==0&&built.Entries.Count(e=>e.EndsWith(".vmd"))==1,error);Record("mixed-main-byte-preservation",opened&&File.ReadAllBytes(mainPath).SequenceEqual(File.ReadAllBytes(package.PrimaryVmdPath)));
  var overlay=VmdReader.Read(mainPath);overlay.BoneFrames[0].Position+=new Vector3(99,88,77);overlay.MorphFrames.First(m=>m.MorphName=="あ").Weight=0.9f;overlay.CameraFrames[0].Distance=50;
  string overlayPath=Path.Combine(fixtures,"mixed-overlay.vmd");File.WriteAllBytes(overlayPath,VmdWriter.Write(overlay));draft.FaceVmd=overlayPath;draft.LipVmd=overlayPath;draft.CameraVmd=overlayPath;
  built=Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(draft,Path.Combine(fixtures,"scoped.vmdance"));opened=VmdDancePackage.TryOpenArchive(built.OutputPath,Path.Combine(fixtures,"scoped-cache"),out package,out error);var layers=new List<VmdMotion>{VmdReader.Read(package.PrimaryVmdPath)};layers.AddRange(package.OverlayVmdPaths.Select(VmdReader.Read));VmdMotionMergeReport report;var merged=VmdMotionLayers.Merge(layers,out report);
  Record("mixed-slot-channel-safety",opened&&package.OverlayVmdPaths.Length==2&&merged.BoneFrames.Count==main.BoneFrames.Count&&merged.BoneFrames[0].Position==main.BoneFrames[0].Position&&report.BoneTrackOverrides==0&&report.IkSectionOverrides==0,new{overlays=package.OverlayVmdPaths.Length,report.BoneTrackOverrides,report.MorphTrackOverrides,report.IkSectionOverrides});
  Record("mixed-overrides-and-unknown",merged.MorphFrames.Any(m=>m.MorphName=="あ"&&Math.Abs(m.Weight-0.9f)<0.001f)&&merged.MorphFrames.Any(m=>m.MorphName=="独自モーフ")&&merged.CameraFrames[0].Distance==50&&merged.LightFrames.Count==main.LightFrames.Count&&merged.SelfShadowFrames.Count==main.SelfShadowFrames.Count);
  var suggested=Maoxig.VmdDanceStudio.VmdWorkspaceScanner.Suggest(Maoxig.VmdDanceStudio.VmdWorkspaceScanner.Scan(fixtures).First(g=>g.HasMotion));Record("mixed-auto-no-duplicate-tracks",suggested.FaceVmd==null&&suggested.LipVmd==null&&suggested.CameraVmd==null&&suggested.AdditionalVmdFiles.Count==0);
  var inspected=Maoxig.VmdDanceStudio.VmdInspector.Inspect(mainPath);Record("inspector-japanese-names",inspected.MorphNames.Contains("あ")&&inspected.BoneNames.Any(n=>n=="センター"||n=="上半身"||n=="下半身"));int morphCount=inspected.MorphKeys>int.MaxValue?0:(int)inspected.MorphKeys;inspected.BoneNames.Clear();Record("inspector-cache-isolation",Maoxig.VmdDanceStudio.VmdInspector.Inspect(mainPath).BoneNames.Count>0);main.MorphFrames.Add(new VmdMorphKeyframe{MorphName="追加モーフ",FrameNumber=0,Weight=0.3f});File.WriteAllBytes(mainPath,VmdWriter.Write(main));Record("inspector-cache-invalidation",Maoxig.VmdDanceStudio.VmdInspector.Inspect(mainPath).MorphKeys==morphCount+1);
  var core=ui.playerCore;var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);draft.FaceVmd=draft.LipVmd=draft.CameraVmd=null;composer.RestoreDraft(draft);Record("mixed-ui-recognizes-embedded",composer.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(DanceLocale.T("composer.track.morph"))&&t.text.Contains(DanceLocale.T("composer.track.camera"))));yield return Capture("zh-CN-mixed-main-tracks");Button(composer.transform,"composer.preview").onClick.Invoke();float deadline=Time.realtimeSinceStartup+15;while(core.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
  bool ready=core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded;Record("mixed-main-native-track-playback",ready&&core.CurrentVmdPlayer.Motion.MorphFrames.Any(m=>m.MorphName=="あ")&&core.CurrentVmdPlayer.Motion.CameraFrames.Count>0&&core.CurrentVmdPlayer.BoundMorphCount>0,new{ready,bound=core.CurrentVmdPlayer?.BoundMorphCount,error=core.LastError});
  if(ready){core.TogglePause();core.Seek(0);yield return new WaitForEndOfFrame();var bindings=core.CurrentVmdPlayer.GetType().GetField("morphBindings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(core.CurrentVmdPlayer) as IEnumerable;float peak=0;int count=0;foreach(object binding in bindings){var type=binding.GetType();var names=(List<string>)type.GetField("MorphNames").GetValue(binding);if(!names.Contains("あ"))continue;var renderer=(SkinnedMeshRenderer)type.GetField("Renderer").GetValue(binding);int index=(int)type.GetField("BlendShapeIndex").GetValue(binding);peak=Math.Max(peak,renderer.GetBlendShapeWeight(index));count++;}Record("mixed-main-lip-rendered",count>0&&peak>1,new{count,peak});yield return Capture("mixed-main-expression-rendered");}
  core.StopPlay();
 }
 private IEnumerator SupportOnly()
 {
  output=Path.Combine(OutputDirectory(),"support-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);ui.SetCameraEnabled(false);var core=ui.playerCore;core.StopPlay();core.InitPlayer();
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var dances=new List<object>();
  foreach(string fixture in new[]{"03-catch-the-wave","02-angelite","08-ik-no-morph-melt","12-light-camera-odds"})
  {
   var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.IndexOf(fixture,StringComparison.OrdinalIgnoreCase)>=0);
   bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;
   while(started&&(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded||core.IsLoading)&&Time.realtimeSinceStartup<deadline)yield return null;
   if(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded){Record("support-load-"+fixture,false,core.LastError);continue;}
   core.TogglePause();var player=core.CurrentVmdPlayer;player.LockBodyYaw=!Environment.GetCommandLineArgs().Contains("--cdp-support-free");var type=player.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var bindings=type.GetField("poseBindings",flags).GetValue(player) as IEnumerable;var source=new Dictionary<HumanBodyBones,Transform>();var target=new Dictionary<HumanBodyBones,Transform>();var sourceRest=new Dictionary<HumanBodyBones,Vector3>();var targetRest=new Dictionary<HumanBodyBones,Vector3>();
   foreach(object binding in bindings){var t=binding.GetType();var role=(HumanBodyBones)t.GetField("HumanBone").GetValue(binding);source[role]=(Transform)t.GetField("Source").GetValue(binding);target[role]=(Transform)t.GetField("Target").GetValue(binding);sourceRest[role]=(Vector3)t.GetField("SourceBindWorldPosition").GetValue(binding);targetRest[role]=(Vector3)t.GetField("TargetBindWorldPosition").GetValue(binding);}
   var basis=(Quaternion)type.GetField("directRetargetBasis",flags).GetValue(player);var correct=type.GetMethod("CorrectSourcePosition",flags);
   var ikCheck=type.GetMethod("IsSourceLegIkEnabled",flags);float sourceScale=(float)type.GetField("sourceHumanScale",flags).GetValue(player);
   float scale=(float)type.GetField("targetHumanScale",flags).GetValue(player)/(float)type.GetField("sourceHumanScale",flags).GetValue(player);
   var common=(Transform)type.GetField("directBodyCommonBone",flags).GetValue(player);var root=(Transform)type.GetField("retargetRootBone",flags).GetValue(player);var rootRest=(Quaternion)type.GetField("retargetRootBindWorldRotation",flags).GetValue(player);
   var samples=new List<object>();float maxSlip=0,maxRootExtraStep=0,maxCenterTravelError=0;int plantedPairs=0;Vector3[] lastExpected=null,lastActual=null;Vector3 lastRootExtra=Vector3.zero;
   int frames=(int)(core.Duration*30);
   for(int frame=0;frame<=frames;frame++)
   {
    float seconds=frame/30f;player.ApplyAtTime(seconds);
    if(player.LockBodyYaw&&common!=null&&root!=null){var pivot=(Vector3)type.GetField("retargetRootPivot",flags).GetValue(player);float initialFacing=(float)type.GetField("directFacingBaseline",flags).GetValue(player);var expectedCenter=pivot+Quaternion.AngleAxis(-initialFacing,Vector3.up)*rootRest*Quaternion.Inverse(root.rotation)*(common.position-pivot);var actualCenter=(Vector3)correct.Invoke(player,new object[]{common.position});maxCenterTravelError=Math.Max(maxCenterTravelError,Vector3.Distance(expectedCenter,actualCenter));}
    var expected=new Vector3[2];var actual=new Vector3[2];var knees=new float[2];
    for(int side=0;side<2;side++){var foot=side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;var upper=side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg;var lower=side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg;var sourcePosition=(Vector3)correct.Invoke(player,new object[]{source[foot].position});expected[side]=targetRest[foot]+basis*(sourcePosition-sourceRest[foot])*scale;actual[side]=target[foot].position;knees[side]=VectorAngle(target[upper].position-target[lower].position,target[foot].position-target[lower].position);bool supporting=sourcePosition.y-sourceRest[foot].y<=sourceScale*0.015f;bool enabled=ikCheck==null||(bool)ikCheck.Invoke(player,new object[]{foot});if(lastExpected!=null&&supporting&&enabled&&Vector3.Distance(expected[side],lastExpected[side])<0.0005f){maxSlip=Math.Max(maxSlip,Vector3.Distance(actual[side]-lastActual[side],expected[side]-lastExpected[side]));plantedPairs++;}}
    var hip=HumanBodyBones.Hips;var rawHip=targetRest[hip]+basis*((Vector3)correct.Invoke(player,new object[]{source[hip].position})-sourceRest[hip])*scale;var rootExtra=target[hip].position-rawHip;if(frame>0)maxRootExtraStep=Math.Max(maxRootExtraStep,Vector3.Distance(rootExtra,lastRootExtra));
    samples.Add(new{frame,seconds,hip=Vec(target[hip].position),rootExtra=Vec(rootExtra),expectedLeft=Vec(expected[0]),actualLeft=Vec(actual[0]),expectedRight=Vec(expected[1]),actualRight=Vec(actual[1]),leftKnee=knees[0],rightKnee=knees[1]});lastExpected=expected;lastActual=actual;lastRootExtra=rootExtra;
    if(frame%60==0)yield return null;
   }
   core.Seek(39f/core.Duration);yield return new WaitForEndOfFrame();yield return Capture(fixture+"-support-39");
   Record("support-"+fixture,plantedPairs>0&&maxSlip<0.002f&&maxCenterTravelError<0.00001f,new{frames=frames+1,plantedPairs,maxPlantedSlipPerFrame=maxSlip,maxRootExtraStep,maxCenterTravelError,lockFacing=player.LockBodyYaw});dances.Add(new{fixture,scale,frames=frames+1,plantedPairs,maxPlantedSlipPerFrame=maxSlip,maxRootExtraStep,maxCenterTravelError,lockFacing=player.LockBodyYaw,samples});core.StopPlay();yield return null;
  }
  File.WriteAllText(Path.Combine(output,"support.json"),JsonConvert.SerializeObject(new{results,dances},Formatting.Indented));Logger.LogInfo("SUPPORT_AUDIT_COMPLETE");
 }
 private IEnumerator UserReproOnly()
 {
  output=Path.Combine(OutputDirectory(),"repro-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);ui.SetCameraEnabled(false);var core=ui.playerCore;
  core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";
  core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.IndexOf("09-duet-female-ik",StringComparison.OrdinalIgnoreCase)>=0);
  bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;
  while(started&&(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded||core.IsLoading)&&Time.realtimeSinceStartup<deadline)yield return null;
  if(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded){Record("repro-load",false,core.LastError);yield break;}
  core.TogglePause();var animator=ui.avatarHelper.CurrentAnimator;var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
  Record("repro-model",true,new{avatar=ui.avatarHelper.CurrentAvatar.name,animator=animator.name,skeletonCount=animator.avatar.humanDescription.skeleton.Length,backend=core.CurrentVmdPlayer.LastDiagnostics});
  foreach(float seconds in new[]{0f,5f,15f,30f,45f,60f,90f,120f,core.Duration*0.5f})
  {
   core.Seek(Math.Min(seconds,core.Duration)/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   var bindings=core.CurrentVmdPlayer.GetType().GetField("poseBindings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(core.CurrentVmdPlayer) as IEnumerable;
   var details=new List<object>();
   foreach(object binding in bindings){var t=binding.GetType();var role=(HumanBodyBones)t.GetField("HumanBone").GetValue(binding);if(!new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Contains(role))continue;var target=(Transform)t.GetField("Target").GetValue(binding);var source=(Transform)t.GetField("Source").GetValue(binding);details.Add(new{role=role.ToString(),targetPosition=Vec(target.position),targetLocalRotation=new{x=target.localRotation.x,y=target.localRotation.y,z=target.localRotation.z,w=target.localRotation.w},sourceWorldRotation=new{x=source.rotation.x,y=source.rotation.y,z=source.rotation.z,w=source.rotation.w}});}
   Record("repro-"+seconds,true,new{seconds,spineOffset=Vec(spine.localPosition),hipsUp=Vec(hips.up),details});yield return Capture("duet-"+seconds.ToString("0.00"));
  }
  core.StopPlay();File.WriteAllText(Path.Combine(output,"repro.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("REPRO_COMPLETE");
 }
 private IEnumerator RetargetOnly()
 {
  output=Path.Combine(OutputDirectory(),"retarget-current");Directory.CreateDirectory(output);
  CompleteFrameChecks();
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);ui.SetCameraEnabled(false);var core=ui.playerCore;
  core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";
  core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var animator=ui.avatarHelper.CurrentAnimator;
  foreach(string fixture in new[]{"02-angelite","08-ik-no-morph-melt","12-light-camera-odds","23-marine-bloomin","32-iii-marine"})
  {
   var descriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Id.IndexOf(fixture,StringComparison.OrdinalIgnoreCase)>=0);
   if(descriptor==null){Record("retarget-fixture-"+fixture,false);continue;}
   bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));
   float deadline=Time.realtimeSinceStartup+15;
   while(started&&(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded||core.IsLoading)&&Time.realtimeSinceStartup<deadline)yield return null;
   if(!started||core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded){Record("retarget-load-"+fixture,false,core.LastError);core.StopPlay();continue;}
   core.TogglePause();
   foreach(float seconds in new[]{0f,5f,15f,39f,core.Duration*0.5f})
   {
    core.Seek(Math.Min(seconds,core.Duration)/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
    var metrics=MeasureRetarget(core.CurrentVmdPlayer,animator);
    Record("retarget-"+fixture+"-"+seconds,metrics.maxArmDirectionError<1f&&metrics.maxKneeAngleError<2f&&metrics.maxFootGoalError<0.002f&&metrics.maxPalmTwistError<1f&&metrics.maxLocalPositionError<0.00001f,metrics);
    yield return Capture(fixture+"-"+seconds.ToString("0.00"));
    core.CurrentVmdPlayer.ApplyIk=false;core.Seek(Math.Min(seconds,core.Duration)/core.Duration);
    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
    metrics=MeasureRetarget(core.CurrentVmdPlayer,animator);
    Record("retarget-fk-"+fixture+"-"+seconds,metrics.maxArmDirectionError<1f&&metrics.maxKneeAngleError<2f&&metrics.maxPalmTwistError<1f&&metrics.maxLocalPositionError<0.00001f,metrics);
    core.CurrentVmdPlayer.ApplyIk=true;
   }
   core.StopPlay();yield return null;
  }
  // Loading at different idle arm/pelvis rotations must produce identical
  // motion. Freeze the host Animator while injecting a reproducible idle pose.
  var invariant=ui.resourceManager.Descriptors.Values.First(d=>d.Id.IndexOf("02-angelite",StringComparison.OrdinalIgnoreCase)>=0);
  core.StopPlay();bool animatorEnabled=animator.enabled;animator.enabled=false;
  var bones=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.Spine};
  var transforms=bones.Select(animator.GetBoneTransform).ToArray();var saved=transforms.Select(t=>t.localRotation).ToArray();
  Vector3[] referencePositions=null;Quaternion[] referenceRotations=null;
  for(int pass=0;pass<2;pass++)
  {
   for(int i=0;i<transforms.Length;i++)transforms[i].localRotation=saved[i]*(pass==0?Quaternion.identity:Quaternion.Euler(31f+i*4f,-27f,49f));
   var beforePlay=transforms.Select(t=>t.localRotation).ToArray();
   core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(invariant.Id));float deadline=Time.realtimeSinceStartup+15;
   while((core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded||core.IsLoading)&&Time.realtimeSinceStartup<deadline)yield return null;
   if(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded){Record("idle-invariance-load",false);break;}
   core.TogglePause();core.Seek(15f/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   if(pass==0){referencePositions=transforms.Select(t=>t.position).ToArray();referenceRotations=transforms.Select(t=>t.rotation).ToArray();}
   else
   {
    float positionError=0,rotationError=0;for(int i=0;i<transforms.Length;i++){positionError=Math.Max(positionError,Vector3.Distance(referencePositions[i],transforms[i].position));rotationError=Math.Max(rotationError,RotationDifference(referenceRotations[i],transforms[i].rotation));}
    Record("idle-pose-invariance",positionError<0.00001f&&rotationError<0.01f,new{positionError,rotationError});
   }
   core.StopPlay();yield return null;
   float restoreError=0;for(int i=0;i<transforms.Length;i++)restoreError=Math.Max(restoreError,RotationDifference(beforePlay[i],transforms[i].localRotation));
   Record("idle-restore-"+pass,restoreError<0.01f,new{restoreError});
  }
  for(int i=0;i<transforms.Length;i++)transforms[i].localRotation=saved[i];animator.enabled=animatorEnabled;
  File.WriteAllText(Path.Combine(output,"retarget.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("RETARGET_AUDIT_COMPLETE");
 }
 private sealed class RetargetMetrics
 {
  public float maxLimbDirectionError,maxArmDirectionError,maxKneeAngleError,maxRecordedKneeAngleError,maxFootGoalError,maxUnreachableFootDistance,maxPalmNormalError,maxPalmTwistError,maxLocalPositionError;
  public int constrainedLegs;
  public int measuredPalms;
  public readonly Dictionary<string,float> directions=new Dictionary<string,float>();
 }
 private void CompleteFrameChecks()
 {
  // Same segment aim, different axial roll: a direction-only check would pass
  // all these cases, including a hand or knee turned upside down.
  foreach(float roll in new[]{-180f,-90f,-45f,0f,60f,90f,180f})
  {
   Quaternion sourceRest=Quaternion.Euler(-23f,37f,11f);
   Quaternion targetRest=sourceRest*Quaternion.AngleAxis(roll,Vector3.forward);
   Quaternion aligned;
   bool valid=VmdBoneFrameRetargeting.TryAlignRest(targetRest*Vector3.forward,targetRest*Vector3.up,
    sourceRest*Vector3.forward,sourceRest*Vector3.up,targetRest,out aligned);
   float primary=VectorAngle(aligned*Vector3.forward,sourceRest*Vector3.forward);
   float secondary=VectorAngle(aligned*Vector3.up,sourceRest*Vector3.up);
   Quaternion motion=Quaternion.Euler(47f,-83f,29f);
   float moved=VectorAngle(motion*aligned*Vector3.up,motion*sourceRest*Vector3.up);
   Record("frame-roll-"+roll,valid&&primary<0.1f&&secondary<0.1f&&moved<0.1f,new{primary,secondary,moved});
  }
  Quaternion ignored;
  Record("frame-degenerate",!VmdBoneFrameRetargeting.TryFrame(Vector3.up,Vector3.up,out ignored));
 }
 private RetargetMetrics MeasureRetarget(object player,Animator animator)
 {
  var result=new RetargetMetrics();var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=player.GetType();
  var bindings=type.GetField("poseBindings",flags).GetValue(player) as IEnumerable;
  var source=new Dictionary<HumanBodyBones,Transform>();var target=new Dictionary<HumanBodyBones,Transform>();var sourceRest=new Dictionary<HumanBodyBones,Vector3>();var targetRest=new Dictionary<HumanBodyBones,Vector3>();
  foreach(object binding in bindings){var t=binding.GetType();var role=(HumanBodyBones)t.GetField("HumanBone").GetValue(binding);source[role]=(Transform)t.GetField("Source").GetValue(binding);target[role]=(Transform)t.GetField("Target").GetValue(binding);sourceRest[role]=(Vector3)t.GetField("SourceBindWorldPosition").GetValue(binding);targetRest[role]=(Vector3)t.GetField("TargetBindWorldPosition").GetValue(binding);}
  var basis=(Quaternion)type.GetField("directRetargetBasis",flags).GetValue(player);var correct=type.GetMethod("CorrectSourcePosition",flags);
  var pairs=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
  for(int i=0;i<pairs.Length;i++){if(i%3==2)continue;var a=pairs[i];var b=pairs[i+1];if(!source.ContainsKey(a)||!source.ContainsKey(b))continue;var sa=(Vector3)correct.Invoke(player,new object[]{source[a].position});var sb=(Vector3)correct.Invoke(player,new object[]{source[b].position});float angle=VectorAngle(basis*(sb-sa),target[b].position-target[a].position);result.directions[a.ToString()]=angle;result.maxLimbDirectionError=Math.Max(result.maxLimbDirectionError,angle);if(i<6)result.maxArmDirectionError=Math.Max(result.maxArmDirectionError,angle);}
  // Positional IK constrains the foot, so a differently proportioned target
  // must bend its knee differently. FK still preserves the authored angle.
  var legIk=type.GetMethod("IsSourceLegIkEnabled",flags);bool applyIk=(bool)type.GetProperty("ApplyIk").GetValue(player,null);bool rootMotion=(bool)type.GetProperty("ApplyRootMotion").GetValue(player,null);
  float scale=(float)type.GetField("targetHumanScale",flags).GetValue(player)/(float)type.GetField("sourceHumanScale",flags).GetValue(player);
  foreach(int i in new[]{6,9}){var a=pairs[i];var b=pairs[i+1];var c=pairs[i+2];float sourceAngle=VectorAngle(source[b].position-source[a].position,source[c].position-source[b].position);float targetAngle=VectorAngle(target[b].position-target[a].position,target[c].position-target[b].position);float angleError=Math.Abs(sourceAngle-targetAngle);result.maxRecordedKneeAngleError=Math.Max(result.maxRecordedKneeAngleError,angleError);if(applyIk&&rootMotion&&legIk!=null&&(bool)legIk.Invoke(player,new object[]{c})){var goal=targetRest[c]+basis*((Vector3)correct.Invoke(player,new object[]{source[c].position})-sourceRest[c])*scale;float reach=Vector3.Distance(target[a].position,target[b].position)+Vector3.Distance(target[b].position,target[c].position);var delta=goal-target[a].position;float distance=delta.magnitude;bool supporting=(bool)type.GetMethod("IsSourceFootSupporting",flags).Invoke(player,new object[]{c});result.maxUnreachableFootDistance=Math.Max(result.maxUnreachableFootDistance,Math.Max(0f,distance-reach));var expected=supporting?goal:target[a].position+delta.normalized*Mathf.Clamp(distance,Math.Abs(Vector3.Distance(target[a].position,target[b].position)-Vector3.Distance(target[b].position,target[c].position))+0.0001f,reach-0.0001f);result.maxFootGoalError=Math.Max(result.maxFootGoalError,Vector3.Distance(expected,target[c].position));result.constrainedLegs++;}else result.maxKneeAngleError=Math.Max(result.maxKneeAngleError,angleError);}
  foreach(bool left in new[]{true,false})
  {
   var wrist=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;var index=left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal;var little=left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal;
   if(!source.ContainsKey(index)||!source.ContainsKey(little)||!target.ContainsKey(index)||!target.ContainsKey(little))continue;
   var sw=(Vector3)correct.Invoke(player,new object[]{source[wrist].position});var si=(Vector3)correct.Invoke(player,new object[]{source[index].position});var sl=(Vector3)correct.Invoke(player,new object[]{source[little].position});
   var sourceNormal=basis*Vector3.Cross(si-sw,sl-sw);var targetNormal=Vector3.Cross(target[index].position-target[wrist].position,target[little].position-target[wrist].position);
   result.maxPalmNormalError=Math.Max(result.maxPalmNormalError,VectorAngle(sourceNormal.normalized,targetNormal.normalized));
   var middle=left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal;
   if(target.ContainsKey(middle))
   {
    var primary=(target[middle].position-target[wrist].position).normalized;
    var expected=sourceNormal-primary*Vector3.Dot(primary,sourceNormal);var actual=targetNormal-primary*Vector3.Dot(primary,targetNormal);
    result.maxPalmTwistError=Math.Max(result.maxPalmTwistError,VectorAngle(expected.normalized,actual.normalized));
   }
   result.measuredPalms++;
  }
  var rest=animator.avatar.humanDescription.skeleton.ToDictionary(s=>s.name,s=>s);
  foreach(var pair in target){if(pair.Key==HumanBodyBones.Hips||!rest.ContainsKey(pair.Value.name))continue;result.maxLocalPositionError=Math.Max(result.maxLocalPositionError,Vector3.Distance(pair.Value.localPosition,rest[pair.Value.name].position));}
  return result;
 }
 private IEnumerator FrameOnly()
 {
  output=Path.Combine(OutputDirectory(),"frame-current");Directory.CreateDirectory(output);
  var watch=System.Diagnostics.Stopwatch.StartNew();double previous=watch.Elapsed.TotalMilliseconds;bool modelSeen=false,rootSeen=false;double modelMs=-1,rootMs=-1;var gaps=new List<object>();double maxGap=0;int frames=0;
  while(watch.Elapsed.TotalSeconds<30)
  {
   yield return null;frames++;double now=watch.Elapsed.TotalMilliseconds;double gap=now-previous;previous=now;if(gap>maxGap)maxGap=gap;
   if(!modelSeen&&GameObject.Find("Model")!=null){modelSeen=true;modelMs=now;}
   if(!rootSeen&&DanceBootstrap.Root!=null){rootSeen=true;rootMs=now;}
   if(gap>=40){var manager=DanceBootstrap.Root==null?null:DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);gaps.Add(new{atMs=now,gapMs=gap,unityDeltaMs=Time.unscaledDeltaTime*1000f,modelSeen,rootSeen,refreshing=manager!=null&&manager.resourceManager.IsRefreshing,dances=manager==null?0:manager.resourceManager.DanceFileList.Count});}
  }
  var uiManager=DanceBootstrap.Root==null?null:DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  File.WriteAllText(Path.Combine(output,"frames.json"),JsonConvert.SerializeObject(new{frames,maxGapMs=maxGap,modelMs,rootMs,bootstrap=DanceBootstrap.LastCreateTiming,scanMs=uiManager?.resourceManager.LastRefreshMilliseconds,applyMs=uiManager?.resourceManager.LastApplyMilliseconds,gaps},Formatting.Indented));Logger.LogInfo("FRAME_AUDIT_COMPLETE");
 }
 private IEnumerator QualityOnly()
 {
  output=Path.Combine(OutputDirectory(),"quality-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return WaitForLibrary();ui.SetPanelVisible(false);ui.SetCameraEnabled(false);
  var core=ui.playerCore;core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var animator=ui.avatarHelper.CurrentAnimator;var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var head=animator.GetBoneTransform(HumanBodyBones.Head);var leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var leftLower=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var leftToe=animator.GetBoneTransform(HumanBodyBones.LeftToes);var rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);var rightLower=animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);var rightToe=animator.GetBoneTransform(HumanBodyBones.RightToes);
  var packages=ui.resourceManager.Descriptors.Values.Where(d=>(d.Format=="vmdance"||d.Format=="vmd")&&d.Id.IndexOf("audit-preview",StringComparison.OrdinalIgnoreCase)<0&&d.Id.IndexOf("composed",StringComparison.OrdinalIgnoreCase)<0&&char.IsDigit(Path.GetFileName(d.Id)[0])).OrderBy(d=>d.Id).Take(40).ToArray();var dances=new List<object>();
  foreach(var descriptor in packages)
  {
   Vector3 restTorso=(head.position-hips.position).normalized,restHipsUp=hips.rotation*Vector3.up;Quaternion restAvatar=ui.avatarHelper.CurrentAvatar.transform.localRotation,restLeftFootRelative=Quaternion.Inverse(hips.rotation)*leftFoot.rotation,restRightFootRelative=Quaternion.Inverse(hips.rotation)*rightFoot.rotation;Vector3 restLeftToeDirection=leftToe==null?Vector3.zero:Quaternion.Inverse(hips.rotation)*(leftToe.position-leftFoot.position).normalized,restRightToeDirection=rightToe==null?Vector3.zero:Quaternion.Inverse(hips.rotation)*(rightToe.position-rightFoot.position).normalized;
   bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;while(started&&(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded||core.IsLoading)&&Time.realtimeSinceStartup<deadline)yield return null;var player=core.CurrentVmdPlayer;
   if(!started||player==null||!player.IsLoaded||core.IsLoading){dances.Add(new{id=descriptor.Id,started=false,error=core.LastError,loading=core.IsLoading});core.StopPlay();continue;}core.TogglePause();float duration=core.Duration;var samples=new List<object>();
   foreach(float fraction in new[]{0f,0.1f,0.25f,0.5f,0.75f,0.95f})
   {
    core.Seek(fraction);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Vector3 leftThigh=leftLower.position-leftUpper.position,leftShin=leftFoot.position-leftLower.position,rightThigh=rightLower.position-rightUpper.position,rightShin=rightFoot.position-rightLower.position;
    Quaternion leftFootRelative=Quaternion.Inverse(hips.rotation)*leftFoot.rotation,rightFootRelative=Quaternion.Inverse(hips.rotation)*rightFoot.rotation;Vector3 leftToeDirection=leftToe==null?Vector3.zero:Quaternion.Inverse(hips.rotation)*(leftToe.position-leftFoot.position).normalized,rightToeDirection=rightToe==null?Vector3.zero:Quaternion.Inverse(hips.rotation)*(rightToe.position-rightFoot.position).normalized;
    samples.Add(new{fraction,seconds=core.PlaybackTime,initialRootYawOffset=player.InitialRootYawOffset,initialBodyYawOffset=player.InitialBodyYawOffset,sourceYaw=player.LastSourceBodyYawFromRest,appliedYaw=player.LastAppliedBodyYawFromRest,bodyTilt=player.LastAppliedBodyTiltDegrees,rootTilt=player.LastSourceRootTiltDegrees,bodyPositionError=player.LastBodyPositionRetargetError,torsoDelta=VectorAngle(restTorso,(head.position-hips.position).normalized),hipsUpDelta=VectorAngle(restHipsUp,hips.rotation*Vector3.up),leftFootRelativeUpDelta=VectorAngle(restLeftFootRelative*Vector3.up,leftFootRelative*Vector3.up),leftFootRelativeForwardDelta=VectorAngle(restLeftFootRelative*Vector3.forward,leftFootRelative*Vector3.forward),rightFootRelativeUpDelta=VectorAngle(restRightFootRelative*Vector3.up,rightFootRelative*Vector3.up),rightFootRelativeForwardDelta=VectorAngle(restRightFootRelative*Vector3.forward,rightFootRelative*Vector3.forward),leftToeRelativeDelta=leftToe==null?0:VectorAngle(restLeftToeDirection,leftToeDirection),rightToeRelativeDelta=rightToe==null?0:VectorAngle(restRightToeDirection,rightToeDirection),leftKneeAngle=VectorAngle(-leftThigh,leftShin),rightKneeAngle=VectorAngle(-rightThigh,rightShin),targetLegs=player.LastTargetLegIkSolvedCount,nativeIk=player.LastNativeIkEnabledCount,finite=Finite(hips.position)&&Finite(leftFoot.position)&&Finite(rightFoot.position)});
    if(Math.Abs(fraction-0.5f)<0.001f)yield return Capture("quality-"+SafeName(descriptor.Id));
   }
   dances.Add(new{id=descriptor.Id,title=descriptor.Title,duration,packageIk=ui.resourceManager.CurrentVmdFootIk,diagnostics=player.LastDiagnostics,samples});core.StopPlay();yield return null;if(RotationDifference(restAvatar,ui.avatarHelper.CurrentAvatar.transform.localRotation)>0.001f)Logger.LogWarning("QUALITY root restore mismatch: "+descriptor.Id);
  }
  File.WriteAllText(Path.Combine(output,"quality.json"),JsonConvert.SerializeObject(new{count=dances.Count,dances},Formatting.Indented));Logger.LogInfo("QUALITY_AUDIT_COMPLETE");
 }
 private static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
 private static object DirectTopology(object player)
 {
  if(player==null)return null;var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=player.GetType();var hips=type.GetField("directHipsBinding",flags)?.GetValue(player);var spine=type.GetField("directSpineBinding",flags)?.GetValue(player);var head=type.GetField("directHeadBinding",flags)?.GetValue(player);var common=type.GetField("directBodyCommonBone",flags)?.GetValue(player) as Transform;if(hips==null||spine==null||head==null)return new{common=common?.name};
  var bindingType=hips.GetType();var hipsSource=bindingType.GetField("Source")?.GetValue(hips) as Transform;var hipsTarget=bindingType.GetField("Target")?.GetValue(hips) as Transform;var spineSource=bindingType.GetField("Source")?.GetValue(spine) as Transform;var spineTarget=bindingType.GetField("Target")?.GetValue(spine) as Transform;var headSource=bindingType.GetField("Source")?.GetValue(head) as Transform;var headTarget=bindingType.GetField("Target")?.GetValue(head) as Transform;
  return new{common=common?.name,commonPosition=common==null?null:Vec(common.position),hipsSource=hipsSource==null?null:Vec(hipsSource.position),spineSource=spineSource==null?null:Vec(spineSource.position),headSource=headSource==null?null:Vec(headSource.position),sourceSeparation=Vec(spineSource.position-hipsSource.position),sourceTorso=Vec(headSource.position-hipsSource.position),hipsTarget=hipsTarget==null?null:Vec(hipsTarget.position),spineTarget=spineTarget==null?null:Vec(spineTarget.position),headTarget=headTarget==null?null:Vec(headTarget.position),targetSeparation=Vec(spineTarget.position-hipsTarget.position),targetTorso=Vec(headTarget.position-hipsTarget.position)};
 }
 private static object Vec(Vector3 value)=>new{x=value.x,y=value.y,z=value.z};
 private static string SafeName(string value)=>new string(value.Select(character=>char.IsLetterOrDigit(character)?character:'-').ToArray()).Trim('-');
  private IEnumerator LegacySeekOnly()
  {
   output=Path.Combine(OutputDirectory(),"legacy-seek-current");Directory.CreateDirectory(output);
   while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
   var core=ui.playerCore;core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
   var descriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="unity3d"&&d.Title.IndexOf("camera ",StringComparison.OrdinalIgnoreCase)>=0)??ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="unity3d");
   bool started=descriptor!=null&&core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(3);core.TogglePause();float target=core.Duration*0.65f;core.Seek(0.65f);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
   var state=ui.avatarHelper.CurrentAnimator.GetCurrentAnimatorStateInfo(0);var normalizedField=typeof(AnimatorStateInfo).GetField("m_NormalizedTime",BindingFlags.Instance|BindingFlags.NonPublic);var fullPathField=typeof(AnimatorStateInfo).GetField("m_FullPath",BindingFlags.Instance|BindingFlags.NonPublic);float normalized=normalizedField==null?-1:(float)normalizedField.GetValue(state);normalized-=normalized<0?0:(float)Math.Floor(normalized);int fullPath=fullPathField==null?0:(int)fullPathField.GetValue(state);
   Record("unity3d-seek",started&&core.CanSeek&&core.Paused&&Math.Abs(core.PlaybackTime-target)<0.05f&&Math.Abs(normalized-0.65f)<0.08f,new{resource=descriptor?.Id,target,time=core.PlaybackTime,normalized,shortName=state.shortNameHash,fullPath,error=core.LastError});File.WriteAllText(Path.Combine(output,"legacy-seek.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("LEGACY_SEEK_COMPLETE");
  }
 private IEnumerator SweepOnly()
 {
  output=Path.Combine(OutputDirectory(),"sweep-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);ui.SetCameraEnabled(false);var core=ui.playerCore;core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Id.IndexOf("12-light-camera-odds",StringComparison.OrdinalIgnoreCase)>=0);if(descriptor==null){File.WriteAllText(Path.Combine(output,"sweep.json"),"{\"error\":\"fixture missing\"}");yield break;}
  var animator=ui.avatarHelper.CurrentAnimator;var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var head=animator.GetBoneTransform(HumanBodyBones.Head);var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);var restTorso=(head.position-hips.position).normalized;var restHipsUp=hips.rotation*Vector3.up;var restLeftFoot=leftFoot.rotation;var restRightFoot=rightFoot.rotation;
  bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(2);if(!started||core.CurrentVmdPlayer==null){File.WriteAllText(Path.Combine(output,"sweep.json"),JsonConvert.SerializeObject(new{error=core.LastError},Formatting.Indented));yield break;}core.TogglePause();var player=core.CurrentVmdPlayer;float duration=core.Duration;var modes=new List<object>();
  foreach(bool calibrated in new[]{true,false})
  {
   player.TargetCalibratedRetargeting=calibrated;var samples=new List<object>();float maxTorsoDelta=-1,maxHipsTilt=-1,maxBodyTilt=-1;float maxTorsoTime=0,maxHipsTime=0;
   for(float seconds=0;seconds<=duration;seconds+=2f){core.Seek(duration<=0?0:seconds/duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();float torsoDelta=VectorAngle(restTorso,(head.position-hips.position).normalized);float hipsTilt=VectorAngle(restHipsUp,hips.rotation*Vector3.up);if(torsoDelta>maxTorsoDelta){maxTorsoDelta=torsoDelta;maxTorsoTime=seconds;}if(hipsTilt>maxHipsTilt){maxHipsTilt=hipsTilt;maxHipsTime=seconds;}maxBodyTilt=Math.Max(maxBodyTilt,player.LastAppliedBodyTiltDegrees);samples.Add(new{seconds,torsoDelta,hipsTilt,bodyTilt=player.LastAppliedBodyTiltDegrees,sourceRootTilt=player.LastSourceRootTiltDegrees,leftFootUpDelta=VectorAngle(restLeftFoot*Vector3.up,leftFoot.rotation*Vector3.up),leftFootForwardDelta=VectorAngle(restLeftFoot*Vector3.forward,leftFoot.rotation*Vector3.forward),rightFootUpDelta=VectorAngle(restRightFoot*Vector3.up,rightFoot.rotation*Vector3.up),rightFootForwardDelta=VectorAngle(restRightFoot*Vector3.forward,rightFoot.rotation*Vector3.forward),footOrientations=player.LastTargetFootOrientationAppliedCount});}
   modes.Add(new{calibrated,duration,maxTorsoDelta,maxTorsoTime,maxHipsTilt,maxHipsTime,maxBodyTilt,samples});
  }
  core.StopPlay();File.WriteAllText(Path.Combine(output,"sweep.json"),JsonConvert.SerializeObject(new{resource=descriptor.Id,modes},Formatting.Indented));Logger.LogInfo("SWEEP_COMPLETE");
 }
 private IEnumerator StartupOnly()
 {
  output=Path.Combine(OutputDirectory(),"startup-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);var core=ui.playerCore;core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var wanted=new[]{"02-angelite","04-melt","03-catch-the-wave","05-ifuu-doudou","06-primary-star","19-pure-skirt-layered"};
  foreach(string title in wanted)
  {
   var descriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="vmdance"&&(d.Id.IndexOf(title,StringComparison.OrdinalIgnoreCase)>=0||d.Title.IndexOf(title,StringComparison.OrdinalIgnoreCase)>=0));
   if(descriptor==null){Record("startup-"+title,false,"missing");continue;}
   for(int pass=1;pass<=2;pass++)
   {
    GC.Collect();GC.WaitForPendingFinalizers();yield return null;
    var watch=System.Diagnostics.Stopwatch.StartNew();bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));long syncMilliseconds=watch.ElapsedMilliseconds;
    float deadline=Time.realtimeSinceStartup+8;while(started&&(core.CurrentVmdPlayer==null||!core.CurrentVmdPlayer.IsLoaded)&&Time.realtimeSinceStartup<deadline)yield return null;watch.Stop();
    var player=core.CurrentVmdPlayer;Record("startup-"+title+"-"+pass,started&&player!=null&&player.IsLoaded,new{syncMilliseconds,totalMilliseconds=watch.ElapsedMilliseconds,timing=player?.LastLoadTiming,diagnostics=player?.LastDiagnostics,error=core.LastError});
    yield return new WaitForSecondsRealtime(1);core.StopPlay();yield return null;
   }
  }
  var cancelDescriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="vmdance"&&d.Id.IndexOf("06-primary-star",StringComparison.OrdinalIgnoreCase)>=0);
  bool cancelStarted=cancelDescriptor!=null&&core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(cancelDescriptor.Id));core.StopPlay();yield return new WaitForSecondsRealtime(1);
  Record("startup-native-session-cancel",cancelStarted&&!core.IsPlaying&&core.CurrentVmdPlayer==null,core.LastError);
  SaveStartup();Logger.LogInfo("STARTUP_COMPLETE");
 }
 private IEnumerator PostureOnly()
 {
  output=Path.Combine(OutputDirectory(),"posture-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();
  ui.SetPanelVisible(false);ui.SetCameraEnabled(false);var core=ui.playerCore;core.StopPlay();core.InitPlayer();core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var descriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Id.IndexOf("08-ik-no-morph-melt",StringComparison.OrdinalIgnoreCase)>=0);if(descriptor==null){Record("posture-fixture",false,"08-ik-no-morph-melt missing");SavePosture();yield break;}
  var animator=ui.avatarHelper.CurrentAnimator;var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var head=animator.GetBoneTransform(HumanBodyBones.Head);var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);var restHipsUp=hips.rotation*Vector3.up;var restTorso=(head.position-hips.position).normalized;
  bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();Record("posture-play",started&&core.CurrentVmdPlayer!=null,core.LastError);
  foreach(float seconds in new[]{0f,2f,5f,10f,15f,30f,45f}){core.Seek(seconds/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();var torso=(head.position-hips.position).normalized;float torsoTilt=VectorAngle(Vector3.up,torso);float pelvisTilt=VectorAngle(restHipsUp,hips.rotation*Vector3.up);Record("posture-"+seconds,true,new{seconds,restTorsoTilt=VectorAngle(Vector3.up,restTorso),torsoTilt,torsoDelta=VectorAngle(restTorso,torso),pelvisTilt,leftFootY=leftFoot.position.y,rightFootY=rightFoot.position.y,footHeightDelta=Math.Abs(leftFoot.position.y-rightFoot.position.y),bodyTilt=core.CurrentVmdPlayer.LastAppliedBodyTiltDegrees,sourceRootTilt=core.CurrentVmdPlayer.LastSourceRootTiltDegrees,ik=core.CurrentVmdPlayer.LastIkSolveEnabled,topology=seconds==5f?DirectTopology(core.CurrentVmdPlayer):null});yield return Capture("melt-"+seconds+"-ik-on");}
  core.SetVmdFootIk(false);core.Seek(5/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Record("posture-5-ik-off",true,new{torsoTilt=VectorAngle(Vector3.up,(head.position-hips.position).normalized),pelvisTilt=VectorAngle(restHipsUp,hips.rotation*Vector3.up),leftFootY=leftFoot.position.y,rightFootY=rightFoot.position.y});yield return Capture("melt-5-ik-off");
  float meltFiveTilt=VectorAngle(Vector3.up,(head.position-hips.position).normalized);Record("melt-5-mmdtools-upright",meltFiveTilt<15f,new{torsoTilt=meltFiveTilt,blenderReferenceDegrees=5f,toleranceDegrees=10f});
  core.SetVmdFootIk(true);core.CurrentVmdPlayer.TargetCalibratedRetargeting=false;core.Seek(5/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Record("posture-5-canonical",true,new{torsoTilt=VectorAngle(Vector3.up,(head.position-hips.position).normalized),pelvisTilt=VectorAngle(restHipsUp,hips.rotation*Vector3.up),leftFootY=leftFoot.position.y,rightFootY=rightFoot.position.y});yield return Capture("melt-5-canonical");core.StopPlay();
  var odds=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Id.IndexOf("12-light-camera-odds",StringComparison.OrdinalIgnoreCase)>=0);if(odds==null){Record("odds-fixture",false,"12-light-camera-odds missing");SavePosture();yield break;}
  Vector3 leftFootUp=leftFoot.rotation*Vector3.up,rightFootUp=rightFoot.rotation*Vector3.up;var leftToe=animator.GetBoneTransform(HumanBodyBones.LeftToes);var rightToe=animator.GetBoneTransform(HumanBodyBones.RightToes);
  core.SetVmdFootIk(true);started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(odds.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();core.Seek(39/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  float oddsTorso=VectorAngle(Vector3.up,(head.position-hips.position).normalized);float leftRoll=VectorAngle(leftFootUp,leftFoot.rotation*Vector3.up);float rightRoll=VectorAngle(rightFootUp,rightFoot.rotation*Vector3.up);float leftToeVertical=leftToe==null?0:Math.Abs((leftToe.position-leftFoot.position).normalized.y);float rightToeVertical=rightToe==null?0:Math.Abs((rightToe.position-rightFoot.position).normalized.y);var oddsPlayer=core.CurrentVmdPlayer;
  Record("odds-39-upright",started&&oddsTorso<50&&oddsPlayer!=null&&oddsPlayer.LastAppliedBodyTiltDegrees<0.1f,new{torsoTilt=oddsTorso,bodyTilt=oddsPlayer?.LastAppliedBodyTiltDegrees,sourceRootTilt=oddsPlayer?.LastSourceRootTiltDegrees,yaw=oddsPlayer?.LastAppliedBodyYawFromRest});
  Record("odds-package-ik-on",oddsPlayer!=null&&oddsPlayer.LastIkSolveEnabled&&ui.resourceManager.CurrentVmdFootIk==true,new{package=ui.resourceManager.CurrentVmdFootIk,nativeEnabled=oddsPlayer?.LastNativeIkEnabledCount,targetLegs=oddsPlayer?.LastTargetLegIkSolvedCount});
  Record("odds-foot-orientation",oddsPlayer!=null&&oddsPlayer.LastTargetLegIkSolvedCount==2&&oddsPlayer.LastTargetFootOrientationAppliedCount==2&&leftRoll<60&&rightRoll<60,new{leftRoll,rightRoll,leftToeVertical,rightToeVertical,targetLegs=oddsPlayer?.LastTargetLegIkSolvedCount,directFootMappings=oddsPlayer?.LastTargetFootOrientationAppliedCount});yield return Capture("odds-39-package-ik-on");
  if(oddsPlayer!=null){oddsPlayer.ApplyIk=false;core.Seek(39/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Record("odds-39-forced-ik-off",!oddsPlayer.LastIkSolveEnabled&&oddsPlayer.LastTargetLegIkSolvedCount==0&&oddsPlayer.LastTargetFootOrientationAppliedCount==2,new{targetLegs=oddsPlayer.LastTargetLegIkSolvedCount,directFootMappings=oddsPlayer.LastTargetFootOrientationAppliedCount});yield return Capture("odds-39-forced-ik-off");}else Record("odds-39-forced-ik-off",false,"player missing");core.StopPlay();SavePosture();Logger.LogInfo("POSTURE_COMPLETE");
 }
 private void SavePosture(){File.WriteAllText(Path.Combine(output,"posture.json"),JsonConvert.SerializeObject(results,Formatting.Indented));}
 private void SaveStartup(){File.WriteAllText(Path.Combine(output,"startup.json"),JsonConvert.SerializeObject(results,Formatting.Indented));}
 private IEnumerator WaitForLibrary()
 {
  float deadline=Time.realtimeSinceStartup+90;
  while(ui!=null&&ui.resourceManager.IsRefreshing&&Time.realtimeSinceStartup<deadline)yield return null;
 }
 private IEnumerator DialogOnly()
 {
  output=OutputDirectory();
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(2);ui.Window.SetMiniMode(false);yield return DialogChecks();
 }
 private IEnumerator DragOnly()
 {
  output=Path.Combine(OutputDirectory(),"drag-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);yield return new WaitForSecondsRealtime(2);ui.SetPanelVisible(true);ui.Window.SetMiniMode(false);yield return null;
  var handler=ui.Window.GetComponentInChildren<UIDragHandler>(true);var method=typeof(UIDragHandler).GetMethod("IsValidHandle",BindingFlags.Instance|BindingFlags.NonPublic);var panel=handler.GetComponent<RectTransform>();var button=handler.GetComponentsInChildren<Button>(true).First();var scroll=handler.GetComponentsInChildren<ScrollRect>(true).First();
  Func<GameObject,bool> valid=hit=>{var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,pointerPressRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=hit}};return (bool)method.Invoke(handler,new object[]{data});};
  bool background=valid(panel.gameObject),interactiveButton=valid(button.gameObject),interactiveScroll=valid(scroll.gameObject);Record("drag-background",background);Record("drag-button-preserved",!interactiveButton);Record("drag-scroll-preserved",!interactiveScroll);File.WriteAllText(Path.Combine(output,"drag.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Logger.LogInfo("DRAG_AUDIT_COMPLETE");
 }
 private void Record(string name, bool pass, object detail = null) { results.Add(new { name, pass, detail }); Logger.LogInfo("AUDIT " + name + "=" + pass); }
 private IEnumerator Run()
 {
  output = OutputDirectory(); Directory.CreateDirectory(output);
  float deadline = Time.realtimeSinceStartup + 50;
  while (DanceBootstrap.Root == null && Time.realtimeSinceStartup < deadline) yield return null;
  ui = DanceBootstrap.Root?.GetComponentInChildren<DancePlayerUIManager>(true);
  if (ui == null) { Record("bootstrap",false); Save(); yield break; }
  yield return null;
  float libraryDeadline=Time.realtimeSinceStartup+30;
  while(ui.resourceManager.IsRefreshing&&Time.realtimeSinceStartup<libraryDeadline)yield return null;
  Record("startup-library-background",!ui.resourceManager.IsRefreshing&&ui.resourceManager.LastRefreshRanAsync,
   new{scanMs=ui.resourceManager.LastRefreshMilliseconds,applyMs=ui.resourceManager.LastApplyMilliseconds,count=ui.resourceManager.DanceFileList.Count});
  ui.Window.SetMiniMode(false);
  DanceSettingsHandler.Instance.data.currentPlayMode=DancePlayerCore.PlayMode.Loop;
  DanceSettingsHandler.Instance.data.autoPlayOnStart=false;
  ui.playerCore.StopPlay();ui.playerCore.InitPlayer();ui.playerCore.playlistManager.Search="";ui.playerCore.playlistManager.Format="";ui.playerCore.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);ui.playerCore.playlistManager.ApplyFilters();
  Record("bootstrap",ui.Window != null && ui.TargetCanvas != null);
  Record("single-root",UnityEngine.Object.FindObjectsByType<DancePlayerUIManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length == 1);
  foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
  {
   if (behaviour.GetType().FullName == "Kirurobo.UniWindowController")
   { foreach (var name in new[] { "isTransparent", "isTopmost", "isClickThrough" }) try { behaviour.GetType().GetProperty(name)?.SetValue(behaviour,false); } catch {} }
  }
  ui.SetPanelVisible(false);
  var menus = UnityEngine.Object.FindFirstObjectByType<MenuActions>();
  Logger.LogInfo("AUDIT menus=" + string.Join(",",menus.menuEntries.Where(m=>m.menu!=null).Select(m=>m.menu.name)));
  var official = menus.menuEntries.FirstOrDefault(m=>m.menu!=null && m.menu.name.IndexOf("setting",StringComparison.OrdinalIgnoreCase)>=0);
  if (official != null) File.WriteAllText(Path.Combine(output,"official-style.json"),JsonConvert.SerializeObject(official.menu.GetComponentsInChildren<Image>(true).Where(i=>i.sprite!=null).Select(i=>new {name=i.name,sprite=i.sprite.name,border=i.sprite.border.ToString(),type=i.type.ToString(),color=i.color.ToString()}),Formatting.Indented));
  if (official != null) official.menu.SetActive(true);
  yield return new WaitForSecondsRealtime(1); yield return Capture("official-reference");
  if (official != null) official.menu.SetActive(false);
  ui.SetPanelVisible(true);
  DanceSettingsHandler.Instance.data.enableDanceUIFollow=false;
  DanceSettingsHandler.Instance.hipsFollower.enabled=false;
  DanceSettingsHandler.Instance.hipsFollower.GetComponent<RectTransform>().anchoredPosition=Vector2.zero;
  Canvas.ForceUpdateCanvases();
  File.WriteAllText(Path.Combine(output,"layout.json"),JsonConvert.SerializeObject(ui.Window.GetComponentsInChildren<RectTransform>(true).Select(r=>new {name=r.name,parent=r.parent?.name,width=r.rect.width,height=r.rect.height,min=LayoutUtility.GetMinHeight(r),preferred=LayoutUtility.GetPreferredHeight(r),flexible=LayoutUtility.GetFlexibleHeight(r),element=r.GetComponent<LayoutElement>()==null?0:r.GetComponent<LayoutElement>().preferredHeight}),Formatting.Indented));
  ui.Window.SetTab("library");Canvas.ForceUpdateCanvases();
  var fullBody=ui.Window.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="Body");
  var fullTransport=ui.Window.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="LibraryTransport");
  Record("library-tab-has-transport",fullBody!=null&&fullTransport!=null&&fullTransport.gameObject.activeSelf&&fullBody.rect.height>=350&&fullBody.rect.height<470,new{body=fullBody==null?-1:fullBody.rect.height,transport=fullTransport==null?-1:fullTransport.rect.height});
  ui.Window.SetTab("import");Canvas.ForceUpdateCanvases();
  Record("import-tab-has-transport",fullBody!=null&&fullTransport!=null&&fullTransport.gameObject.activeSelf&&fullBody.rect.height>=350&&fullBody.rect.height<470,new{body=fullBody==null?-1:fullBody.rect.height});
  ui.Window.SetTab("settings");Canvas.ForceUpdateCanvases();
  Record("settings-tab-uses-footer-space",fullBody!=null&&fullTransport!=null&&!fullTransport.gameObject.activeSelf&&fullBody.rect.height>=470,new{body=fullBody==null?-1:fullBody.rect.height});
  var badge=ui.Window.GetComponentsInChildren<Transform>(true).First(t=>t.name=="VersionBadge").GetComponentInChildren<Text>();
  Record("release-version-badge",badge.text=="v0.2.0"&&badge.preferredWidth<=badge.rectTransform.rect.width,new{badge.text,width=badge.rectTransform.rect.width,preferred=badge.preferredWidth});
  foreach (var language in new[] { "zh-CN", "en" })
  {
   DanceSettingsHandler.Instance.data.language=language;DanceLocale.Set(language); yield return null;
   foreach (var tab in new[] { "library", "import", "settings" })
   { ui.Window.SetTab(tab); yield return new WaitForSecondsRealtime(0.3f); yield return Capture(language+"-"+tab); }
  }
  Record("Chinese-font",DanceUi.Font != null && DanceUi.Font.HasCharacter('舞') && DanceUi.Font.HasCharacter('蹈'),DanceUi.Font == null ? "null" : DanceUi.Font.name);
  DanceLocale.Set("zh-CN");ui.Window.SetTab("settings");
  Record("settings-play-mode-removed",!ui.Window.GetComponentsInChildren<Text>(true).Any(text=>text.text==DanceLocale.T("settings.playMode")));
  ui.Window.SetTab("library");
  var watch=System.Diagnostics.Stopwatch.StartNew();ui.RefreshDropdown();watch.Stop();var libraryRows=ui.Window.GetComponentInChildren<DanceVirtualLibrary>(true).RowCount;Record("stress-library",ui.resourceManager.Descriptors.Count>=400,new {count=ui.resourceManager.Descriptors.Count,refreshMs=watch.ElapsedMilliseconds,rows=libraryRows});Record("library-visible-density",libraryRows>=8,libraryRows);
  var core = ui.playerCore;
  Record("avatar",ui.avatarHelper.IsAvatarAvailable());
  foreach (var format in new[] { "unity3d", "me", "vmdance", "vmd" })
  {
   var descriptor = format=="vmdance" ? ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format==format&&d.Title.IndexOf("Angelite",StringComparison.OrdinalIgnoreCase)>=0) : ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format==format);
   if (descriptor == null) { Record(format,false,"no fixture");continue; }
   var controller = ui.avatarHelper.CurrentAnimator.runtimeAnimatorController;
   var cameraPosition=Camera.main.transform.position;var cameraRotation=Camera.main.transform.rotation;var cameraFov=Camera.main.fieldOfView;var avatarRotation=ui.avatarHelper.CurrentAvatar.transform.localRotation;
   bool started = core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));
   yield return new WaitForSecondsRealtime(4);
   float before = core.PlaybackTime; Record(format+"-play",started && core.IsPlaying && before > 0, new { resource = descriptor.Id,time = before,error = core.LastError });
   if(format=="vmdance")Record("native-retarget",core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsUsingDirectBoneRetargeting,core.CurrentVmdPlayer?.LastDiagnostics);
   yield return Capture(format+"-play");
   core.TogglePause(); yield return new WaitForSecondsRealtime(0.3f);
   Record(format+"-pause",core.Paused && Math.Abs(core.PlaybackTime-before)<0.15f);
   core.TogglePause();yield return new WaitForSecondsRealtime(0.3f);
   Record(format+"-resume",!core.Paused && core.PlaybackTime>before);
   core.StopPlay();
   float cameraPositionDelta=Vector3.Distance(cameraPosition,Camera.main.transform.position);float cameraRotationDelta=RotationDifference(cameraRotation,Camera.main.transform.rotation);float cameraFovDelta=Math.Abs(cameraFov-Camera.main.fieldOfView);
   Record(format+"-camera-restore",cameraPositionDelta<0.001f&&cameraRotationDelta<0.001f&&cameraFovDelta<0.001f,new{position=cameraPositionDelta,rotation=cameraRotationDelta,fov=cameraFovDelta});
   Record(format+"-root-restore",RotationDifference(avatarRotation,ui.avatarHelper.CurrentAvatar.transform.localRotation)<0.001f);
   yield return null;
   Record(format+"-restore",!core.IsPlaying && ui.avatarHelper.CurrentAnimator.runtimeAnimatorController==controller);
  }
  var idleCamera=Camera.main;var idleEnabled=idleCamera.enabled;ui.SetCameraEnabled(true);yield return null;
  Record("camera-switch-idle",ui.CameraSync!=null&&ui.CameraSync.RenderCamera==idleCamera&&idleCamera.enabled==idleEnabled&&ui.CameraSync.PreviewRoot==null);
  ui.SetCameraEnabled(false);
  ui.Window.SetTab("import");yield return null;var composer = UnityEngine.Object.FindFirstObjectByType<DanceComposerPanel>(FindObjectsInactive.Include);
  Record("composer",composer != null);
  var angelite=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="vmdance"&&d.Title.IndexOf("Angelite",StringComparison.OrdinalIgnoreCase)>=0);
  if(angelite!=null)
  {
   ui.SetPanelVisible(false);DanceSettingsHandler.Instance.data.useNativeVmd=true;
   core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(angelite.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();
   foreach(var seconds in new[] {2f,15f,40f,70f}){core.Seek(seconds/core.Duration);yield return null;yield return Capture("angelite-native-"+seconds);}
   core.StopPlay();DanceSettingsHandler.Instance.data.useNativeVmd=false;
   core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(angelite.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();core.Seek(15/core.Duration);yield return null;Record("retired-managed-selector-ignored",core.CurrentVmdPlayer!=null&&core.CurrentVmdPlayer.IsLoaded);yield return Capture("angelite-native-old-false-15");core.StopPlay();DanceSettingsHandler.Instance.data.useNativeVmd=true;
   ui.SetPanelVisible(true);
  }
  yield return CameraSwitchChecks();yield return ExtraChecks();
  Save();Logger.LogInfo("AUDIT_COMPLETE");
  if(Environment.GetCommandLineArgs().Contains("--cdp-dialog-audit")) yield return DialogChecks();
 }
 private float RotationDifference(Quaternion a, Quaternion b) => Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)+Math.Abs(a.z-b.z)+Math.Abs(a.w-b.w);
 private float VectorAngle(Vector3 a,Vector3 b){float denominator=(float)Math.Sqrt(a.sqrMagnitude*b.sqrMagnitude);if(denominator<0.000001f)return 0;return (float)(Math.Acos(Mathf.Clamp(Vector3.Dot(a,b)/denominator,-1,1))*180/Math.PI);}
 private float MatrixDifference(Matrix4x4 a,Matrix4x4 b){float total=0;for(int row=0;row<4;row++)for(int column=0;column<4;column++)total+=Math.Abs(a[row,column]-b[row,column]);return total;}
 private string OutputDirectory()=>Path.GetFullPath(Path.Combine(Paths.GameRootPath,"..",Paths.GameRootPath.IndexOf("steam-3.4",StringComparison.OrdinalIgnoreCase)>=0?"steam34-runtime":"runtime9"));
 private IEnumerator CameraSwitchChecks()
 {
  var core=ui.playerCore;var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;var fov=camera.fieldOfView;var projection=camera.projectionMatrix;var enabled=camera.enabled;
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  var window=DanceBootstrap.Root.GetComponentInChildren<DanceWindowFollower>(true);window.SetEnabled(false);
  var distance=DanceBootstrap.Root.GetComponentInChildren<DanceCameraDistKeeper>(true);distance.enabled=false;
  var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Format=="vmdance"&&d.Title.IndexOf("Angelite",StringComparison.OrdinalIgnoreCase)>=0);
  ui.SetWindowFollowEnabled(true);ui.SetCameraEnabled(true);
  Record("camera-disables-window-follow",DanceSettingsHandler.Instance.data.enableMMDCamera&&!DanceSettingsHandler.Instance.data.enableWindowFollow&&!window.isEnabled&&DanceSettingsHandler.Instance.data.restoreWindowFollowAfterMmdCamera);
  ui.SetCameraEnabled(false);
  Record("camera-restores-window-follow",!DanceSettingsHandler.Instance.data.enableMMDCamera&&DanceSettingsHandler.Instance.data.enableWindowFollow&&window.isEnabled&&!DanceSettingsHandler.Instance.data.restoreWindowFollowAfterMmdCamera);
  ui.SetWindowFollowEnabled(false);ui.SetCameraEnabled(true);ui.SetCameraEnabled(false);
  Record("camera-preserves-disabled-window-follow",!DanceSettingsHandler.Instance.data.enableWindowFollow&&!window.isEnabled);
  ui.SetWindowFollowEnabled(true);window.SetEnabled(false);
  ui.SetCameraEnabled(true);core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();core.Seek(15/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  Record("camera-switch-main-view",ui.CameraSync.IsUsingDanceView&&ui.CameraSync.RenderCamera==Camera.main&&camera.targetTexture==null&&MatrixDifference(projection,camera.projectionMatrix)>0.01f);
  yield return Capture("mmd-camera-main-view");
  ui.SetCameraEnabled(false);yield return new WaitForEndOfFrame();
  Record("camera-switch-toggle-restore",Vector3.Distance(position,camera.transform.position)<0.001f&&RotationDifference(rotation,camera.transform.rotation)<0.001f&&Math.Abs(fov-camera.fieldOfView)<0.001f&&MatrixDifference(projection,camera.projectionMatrix)<0.001f&&camera.enabled==enabled);
  var restoredProjection=camera.projectionMatrix;float originalOrthographicSize=camera.orthographicSize;
  if(camera.orthographic)camera.orthographicSize=Math.Max(0.01f,originalOrthographicSize*1.05f);else camera.fieldOfView=Math.Min(179f,fov+1f);
  bool automaticProjection=MatrixDifference(restoredProjection,camera.projectionMatrix)>0.0001f;
  camera.fieldOfView=fov;camera.orthographicSize=originalOrthographicSize;camera.ResetProjectionMatrix();
  Record("camera-restore-auto-projection",automaticProjection,new{camera.orthographic,fov,originalOrthographicSize});
  ui.SetCameraEnabled(true);core.Seek(15/core.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Record("camera-switch-reenable",ui.CameraSync.IsUsingDanceView&&MatrixDifference(projection,camera.projectionMatrix)>0.01f);
  core.StopPlay();yield return new WaitForEndOfFrame();
  Record("camera-switch-stop-restore",Vector3.Distance(position,camera.transform.position)<0.001f&&RotationDifference(rotation,camera.transform.rotation)<0.001f&&Math.Abs(fov-camera.fieldOfView)<0.001f&&MatrixDifference(projection,camera.projectionMatrix)<0.001f&&camera.enabled==enabled);
  ui.SetCameraEnabled(false);
  var legacy=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Format=="unity3d"&&d.Title.IndexOf("camera ",StringComparison.OrdinalIgnoreCase)>=0)??ui.resourceManager.Descriptors.Values.First(d=>d.Format=="unity3d");
  core.playlistManager.Search="";core.playlistManager.Format="";core.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);core.playlistManager.ApplyFilters();
  ui.SetCameraEnabled(true);bool legacyStarted=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(legacy.Id));yield return new WaitForSecondsRealtime(3);
  Record("camera-switch-legacy-main-view",legacyStarted&&ui.CameraSync.IsUsingDanceView&&ui.CameraSync.RenderCamera==Camera.main&&camera.targetTexture==null,new{legacyStarted,error=core.LastError});
  core.TogglePause();float legacyTarget=core.Duration*0.65f;core.Seek(0.65f);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  var legacyState=ui.avatarHelper.CurrentAnimator.GetCurrentAnimatorStateInfo(0);var normalizedField=typeof(AnimatorStateInfo).GetField("m_NormalizedTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);float legacyNormalized=normalizedField==null?-1:(float)normalizedField.GetValue(legacyState);legacyNormalized-=legacyNormalized<0?0:(float)Math.Floor(legacyNormalized);
  Record("unity3d-seek",core.CanSeek&&core.Paused&&Math.Abs(core.PlaybackTime-legacyTarget)<0.05f&&Math.Abs(legacyNormalized-0.65f)<0.08f,new{target=legacyTarget,time=core.PlaybackTime,normalized=legacyNormalized,state=legacyState.shortNameHash});
  core.Seek(0);ui.OnPlayPauseBtnClick();yield return new WaitForSecondsRealtime(0.2f);
  Record("global-command-resumes-from-zero",core.IsPlaying&&!core.Paused&&core.PlaybackTime>0.05f,new{time=core.PlaybackTime,paused=core.Paused,error=core.LastError});
  ui.OnPlayPauseBtnClick();
  yield return Capture("legacy-camera-main-view");ui.SetCameraEnabled(false);yield return new WaitForEndOfFrame();
  Record("camera-switch-legacy-toggle-restore",Vector3.Distance(position,camera.transform.position)<0.001f&&RotationDifference(rotation,camera.transform.rotation)<0.001f&&Math.Abs(fov-camera.fieldOfView)<0.001f&&MatrixDifference(projection,camera.projectionMatrix)<0.001f&&camera.enabled==enabled);
  core.StopPlay();window.SetEnabled(true);distance.enabled=true;
 }
 private Button Button(Transform parent,string key) => parent.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<Text>().text==DanceLocale.T(key));
 private IEnumerator ExtraChecks()
 {
  ui.SetPanelVisible(true);yield return null;
  ui.Window.SetMiniMode(false);yield return null;DanceSettingsHandler.Instance.data.enableDanceUIFollow=true;var follow=DanceSettingsHandler.Instance.hipsFollower;var rect=follow.GetComponent<RectTransform>();rect.anchoredPosition=Vector2.zero;follow.enabled=true;follow.UpdateBaseAndInitial();
  Record("full-panel-width",Math.Abs(rect.rect.width-520f)<0.01f,rect.rect.width);
  Canvas.ForceUpdateCanvases();var filterLabels=ui.Window.GetComponentsInChildren<Button>(true).Select(b=>b.GetComponentInChildren<Text>()).Where(t=>t!=null&&(t.text==DanceLocale.T("library.all")||t.text==DanceLocale.T("library.favorites")||t.text==DanceLocale.T("library.queue")||t.text.StartsWith(DanceLocale.T("library.format")+":")||t.text.StartsWith(DanceLocale.T("library.folder.label")+":"))).ToArray();
  Record("library-filters-single-line",filterLabels.Length==5&&filterLabels.All(t=>t.cachedTextGenerator.lineCount==1),filterLabels.Select(t=>new{text=t.text,lines=t.cachedTextGenerator.lineCount}).ToArray());
  var transport=ui.Window.transform.Find("PlayerPanel/Padding/LibraryTransport");var transportButtons=transport==null?new Button[0]:transport.GetComponentsInChildren<Button>(true);
  Record("full-transport-icons",transportButtons.Count(b=>new[]{"◀","▶","■","→","↻","⇄","Ⅱ"}.Contains(b.GetComponentInChildren<Text>().text))>=5,transportButtons.Select(b=>b.GetComponentInChildren<Text>().text).ToArray());
  var listener=DanceBootstrap.Root.GetComponentInChildren<GlobalHotkeyListener>(true);var apply=typeof(GlobalHotkeyListener).GetMethod("ApplyKeyState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  var hotkeySettings=DanceSettingsHandler.Instance.data;
  var savedKey=hotkeySettings.globalPlaybackKey;bool savedEnabled=hotkeySettings.enableGlobalHotkey;
  hotkeySettings.globalPlaybackKey=KeyCode.Period;hotkeySettings.enableGlobalHotkey=true;hotkeySettings.globalControl=true;hotkeySettings.globalAlt=true;hotkeySettings.globalShift=false;
  typeof(GlobalHotkeyListener).GetField("bindingRevision",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(listener,-1);
  typeof(GlobalHotkeyListener).GetMethod("RefreshBindings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(listener,null);
  bool first=false,repeat=false,second=false;if(listener!=null&&apply!=null){apply.Invoke(listener,new object[]{0xA2,true,false});apply.Invoke(listener,new object[]{0xA4,true,false});first=(bool)apply.Invoke(listener,new object[]{190,true,false});repeat=(bool)apply.Invoke(listener,new object[]{190,true,false});apply.Invoke(listener,new object[]{190,false,true});second=(bool)apply.Invoke(listener,new object[]{190,true,false});apply.Invoke(listener,new object[]{190,false,true});apply.Invoke(listener,new object[]{0xA4,false,true});apply.Invoke(listener,new object[]{0xA2,false,true});}
  Record("global-hotkey-one-shot",first&&!repeat&&second,new{first,repeat,second});
  typeof(GlobalHotkeyListener).GetField("pendingAction",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(listener,-1);
  hotkeySettings.globalPlaybackKey=savedKey;hotkeySettings.enableGlobalHotkey=savedEnabled;
  typeof(GlobalHotkeyListener).GetField("bindingRevision",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(listener,-1);
  typeof(GlobalHotkeyListener).GetMethod("RefreshBindings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(listener,null);
  var drag=rect.GetComponent<UIDragHandler>();var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerEnter=drag.dragHandles[0].gameObject,delta=new Vector2(20,10)};
  drag.OnBeginDrag(data);Record("drag-disables-follow",!follow.enabled);drag.OnDrag(data);var shifted=rect.anchoredPosition;drag.OnEndDrag(data);Record("drag-restores-follow",follow.enabled&&shifted.sqrMagnitude>1);Record("drag-persist",Vector2.Distance(DanceSettingsHandler.Instance.data.uiBasePosition,shifted)<0.01f);
  follow.enabled=false;rect.anchoredPosition=Vector2.zero;
  ui.Window.SetTab("settings");yield return null;
  foreach(var key in new[]{"settings.distance","settings.shadow","settings.window"})
  {
   Func<Toggle> find=()=>ui.Window.GetComponentsInChildren<Toggle>(true).First(t=>t.transform.parent.parent.GetComponentsInChildren<Text>(true).Any(text=>text.text==DanceLocale.T(key)));
   find().isOn=false;yield return null;find().isOn=true;yield return null;
   bool enabled=key=="settings.distance"?DanceBootstrap.Root.GetComponentInChildren<DanceCameraDistKeeper>(true).enabled:key=="settings.shadow"?DanceSettingsHandler.Instance.data.enableShadowFollow:DanceBootstrap.Root.GetComponentInChildren<DanceWindowFollower>(true).isEnabled;
   Record(key+"-reenable",enabled);
  }
  float avatarHeight=ui.avatarHelper.MeasureAvatarEyeHeight();var cameraSettings=DanceSettingsHandler.Instance.data;cameraSettings.autoMmdCameraScale=true;ui.playerCore.SetCameraScale(1.037f);
  float expectedCameraScale=Mathf.Clamp(avatarHeight/ui.playerCore.GetCameraReferenceEyeHeight()*ui.playerCore.GetCameraAuthoringScale()*1.037f,.01f,100f);
  Record("camera-auto-height",avatarHeight>0.25f&&Math.Abs(ui.playerCore.GetEffectiveCameraScale()-expectedCameraScale)<0.001f,new{avatarHeight,expectedCameraScale,effective=ui.playerCore.GetEffectiveCameraScale()});
  Record("camera-scale-precision",Math.Abs(cameraSettings.mmdCameraScale-1.037f)<0.0001f,cameraSettings.mmdCameraScale);ui.playerCore.SetCameraScale(1f);
  yield return VmdRootAndIkChecks();
  ui.Window.SetTab("import");var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  Button(composer.transform,"composer.advanced").onClick.Invoke();Record("composer-advanced",new[]{"composer.id","composer.face","composer.lip","composer.camera","composer.pmx","composer.scale"}.All(key=>composer.GetComponentsInChildren<Text>().Any(t=>t.text==DanceLocale.T(key))));Button(composer.transform,"composer.advanced").onClick.Invoke();
  var draft=new Maoxig.VmdDanceStudio.VmdDanceDraft {Id="audit-preview",Title="Audit Preview",MotionVmd=ui.resourceManager.Descriptors.Values.First(d=>d.Format=="vmd").Path,PositionScale=0.08f,FootIk=false};composer.RestoreDraft(draft);
  Button(composer.transform,"composer.validate").onClick.Invoke();Record("composer-valid",composer.GetComponentsInChildren<Text>(true).Any(t=>t.text.StartsWith(DanceLocale.T("composer.valid"))));
  var bad=Path.Combine(output,"invalid.vmd");File.WriteAllText(bad,"not a VMD");draft.MotionVmd=bad;composer.RestoreDraft(draft);Button(composer.transform,"composer.validate").onClick.Invoke();Record("composer-invalid",composer.GetComponentsInChildren<Text>(true).Any(t=>t.text.StartsWith(DanceLocale.T("composer.invalid"))));
  draft.MotionVmd=ui.resourceManager.Descriptors.Values.First(d=>d.Format=="vmd").Path;composer.RestoreDraft(draft);Button(composer.transform,"composer.preview").onClick.Invoke();yield return new WaitForSecondsRealtime(3);Record("composer-build-preview",ui.playerCore.IsPlaying&&ui.playerCore.CurrentResourceId.Contains("audit-preview"),ui.playerCore.LastError);var previewDescriptor=ui.resourceManager.Descriptors.Values.FirstOrDefault(d=>d.Id.StartsWith("VmdComposer/",StringComparison.OrdinalIgnoreCase)&&d.Id.Contains("audit-preview"));Record("composer-preview-in-custom-dances",previewDescriptor!=null&&Path.GetFullPath(previewDescriptor.Path).StartsWith(Path.Combine(Path.GetFullPath(ui.resourceManager.LibraryFolder),"VmdComposer")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),previewDescriptor?.Path);Record("vmdance-package-foot-ik",ui.resourceManager.CurrentVmdFootIk==false&&ui.playerCore.CurrentVmdPlayer!=null&&!ui.playerCore.CurrentVmdPlayer.LastIkSolveEnabled);ui.playerCore.StopPlay();
  var export=Maoxig.VmdDanceStudio.VmdDancePackageBuilder.Build(draft,Path.Combine(output,"export.vmdance"));Maoxig.RuntimeVmd.VmdDancePackageDescriptor exportedPackage;string packageError;bool packageOpened=Maoxig.RuntimeVmd.VmdDancePackage.TryOpenArchive(export.OutputPath,Path.Combine(output,"export-cache"),out exportedPackage,out packageError);Record("composer-export-roundtrip",export.Entries.Contains("dance.json")&&export.Entries.Contains("motion.vmd")&&packageOpened&&exportedPackage.FootIk==false,packageError);
  ui.Window.SetMiniMode(true);yield return null;yield return Capture("zh-CN-mini");Record("mini-size",ui.Window.GetComponentInChildren<UIDragHandler>().GetComponent<RectTransform>().rect.width==380);var modeButton=ui.Window.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="PlayMode");var firstMode=DanceSettingsHandler.Instance.data.currentPlayMode;string firstIcon=modeButton==null?"":modeButton.GetComponentInChildren<Text>().text;if(modeButton!=null)modeButton.onClick.Invoke();yield return null;modeButton=ui.Window.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="PlayMode");string secondIcon=modeButton==null?"":modeButton.GetComponentInChildren<Text>().text;Record("mini-play-mode",modeButton!=null&&DanceSettingsHandler.Instance.data.currentPlayMode!=(firstMode)&&firstIcon!=secondIcon,new{firstMode,mode=DanceSettingsHandler.Instance.data.currentPlayMode,firstIcon,secondIcon});ui.Window.SetMiniMode(false);yield return null;Record("mini-keeps-draft",ui.Window.GetComponentInChildren<DanceComposerPanel>(true).CaptureDraft()?.Id==draft.Id);
  yield return NativeFollowing();yield return ScrollChecks();yield return Expressions();
  string pluginDanceFolder=Path.Combine(Path.GetDirectoryName(typeof(DanceResourceManager).Assembly.Location),"Dances");Directory.CreateDirectory(pluginDanceFolder);string ignoredPluginDance=Path.Combine(pluginDanceFolder,"audit-must-not-load.vmd");File.WriteAllText(ignoredPluginDance,"ignored");ui.RefreshDropdown();Record("plugin-dances-not-scanned",!ui.resourceManager.Descriptors.Values.Any(d=>Path.GetFullPath(d.Path).Equals(Path.GetFullPath(ignoredPluginDance),StringComparison.OrdinalIgnoreCase)));File.Delete(ignoredPluginDance);
  ui.Window.SetTab("library");DanceSettingsHandler.Instance.data.enableDanceUIFollow=false;DanceSettingsHandler.Instance.data.uiRawPosition=Vector2.zero;DanceSettingsHandler.Instance.data.uiBasePosition=Vector2.zero;DanceSettingsHandler.OnSettingChanged();yield return new WaitForSecondsRealtime(0.6f);Record("settings-written",File.Exists(Path.Combine(Application.persistentDataPath,"danceSettings.json")));
 }
 private IEnumerator VmdRootAndIkChecks()
 {
  var core=ui.playerCore;var data=DanceSettingsHandler.Instance.data;var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Format=="vmdance"&&d.Title.IndexOf("Angelite",StringComparison.OrdinalIgnoreCase)>=0);
  data.useNativeVmd=true;core.SetVmdFootIk(false);core.SetVmdRootOptions(true,false);core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(2);core.TogglePause();core.Seek(15/core.Duration);yield return new WaitForEndOfFrame();
  var player=core.CurrentVmdPlayer;Record("vmd-foot-ik-off",player!=null&&!player.LastIkSolveEnabled);Record("vmd-root-upright",player!=null&&player.LastAppliedBodyTiltDegrees<0.1f,player==null?-1:player.LastAppliedBodyTiltDegrees);
  core.SetVmdFootIk(true);core.Seek(16/core.Duration);yield return new WaitForEndOfFrame();Record("vmd-foot-ik-on",player!=null&&player.LastIkSolveEnabled&&player.LastNativeIkEnabledCount>0&&player.LastTargetLegIkSolvedCount==2,new{enabled=player?.LastNativeIkEnabledCount,targetLegs=player?.LastTargetLegIkSolvedCount});
  core.SetVmdRootOptions(true,true);core.Seek(20/core.Duration);yield return new WaitForEndOfFrame();Record("vmd-facing-lock",player!=null&&Math.Abs(player.LastAppliedBodyYawFromRest)<0.1f,player==null?-999:player.LastAppliedBodyYawFromRest);
  core.StopPlay();core.SetVmdFootIk(true);core.SetVmdRootOptions(true,false);
 }
 private IEnumerator Expressions()
 {
  var core=ui.playerCore;var original=ui.avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
  var records=new List<object>();
  foreach(var descriptor in ui.resourceManager.Descriptors.Values.Where(d=>d.Format=="unity3d"&&(d.Title.Contains("Glow")||d.Title.Contains("KING")||d.Title.Contains("Gimme"))).Take(4).ToArray())
  {
   bool started=core.PlayDanceByIndex(core.playlistManager.GetIndexByFile(descriptor.Id));yield return new WaitForSecondsRealtime(2);
   var dummy=ui.avatarHelper.CurrentAvatar.GetComponent<DummyToUniversalSync>();bool driverConfigured=ui.avatarHelper.TargetSMR!=null||original!=null&&original.enabled&&dummy!=null&&dummy.enabled;
   var smr=dummy==null?null:dummy.dummySmr;float max=0;
   float facePeak=0;
   for(int i=0;i<540;i++){yield return null;if(smr!=null)for(int j=0;j<smr.sharedMesh.blendShapeCount;j++)max=Mathf.Max(max,smr.GetBlendShapeWeight(j));foreach(var face in ui.avatarHelper.CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>())if(face!=smr&&face.sharedMesh!=null)for(int j=0;j<face.sharedMesh.blendShapeCount;j++)facePeak=Mathf.Max(facePeak,face.GetBlendShapeWeight(j));}
   records.Add(new {resource=descriptor.Id,started,driverConfigured,dummyPeak=max,facePeak,universalA=original?.A,universalBlink=original?.Blink,error=core.LastError});Record("expression-driver-"+descriptor.Title,driverConfigured||facePeak>0,new{driverConfigured,facePeak});Record("expression-curves-"+descriptor.Title,facePeak>0,new{max,facePeak});core.StopPlay();yield return null;
  }
  File.WriteAllText(Path.Combine(output,"expressions.json"),JsonConvert.SerializeObject(records,Formatting.Indented));
 }
 private IEnumerator NativeFollowing()
 {
  var settings=DanceSettingsHandler.Instance;settings.data.enableDanceUIFollow=true;ui.Window.SetMiniMode(true);yield return null;
  var follow=settings.hipsFollower;follow.enabled=true;follow.smoothness=0;var rect=follow.GetComponent<RectTransform>();rect.anchoredPosition=Vector2.zero;
  var window=DanceBootstrap.Root.GetComponentInChildren<DanceWindowFollower>(true);window.SetEnabled(false);var distance=DanceBootstrap.Root.GetComponentInChildren<DanceCameraDistKeeper>(true);distance.enabled=false;
  ui.SetCameraEnabled(false);ui.playerCore.playlistManager.Search="";ui.playerCore.playlistManager.Format="";ui.playerCore.playlistManager.SetPlaylistType(DancePlaylistManager.PlaylistType.All);ui.playerCore.playlistManager.ApplyFilters();var descriptor=ui.resourceManager.Descriptors.Values.First(d=>d.Id.IndexOf("03-catch-the-wave",StringComparison.OrdinalIgnoreCase)>=0);
  bool started=ui.playerCore.PlayDanceByIndex(ui.playerCore.playlistManager.GetIndexByFile(descriptor.Id));float deadline=Time.realtimeSinceStartup+15;while(ui.playerCore.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;Record("native-follow-load",started&&ui.playerCore.CurrentVmdPlayer!=null&&ui.playerCore.CurrentVmdPlayer.IsLoaded,ui.playerCore.LastError);ui.playerCore.TogglePause();ui.playerCore.Seek(0);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();follow.UpdateBaseAndInitial();
  var panelOrigin=rect.anchoredPosition;var baselineWorld=ui.avatarHelper.CurrentAvatarHips.position;var origin=Camera.main.WorldToScreenPoint(baselineWorld);ui.playerCore.Seek(15/ui.playerCore.Duration);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  var current=Camera.main.WorldToScreenPoint(ui.avatarHelper.CurrentAvatarHips.position);var expected=(Vector2)(current-Camera.main.WorldToScreenPoint(baselineWorld))/ui.TargetCanvas.scaleFactor;var actual=rect.anchoredPosition-panelOrigin;Record("native-panel-follow",expected.sqrMagnitude>0.1f&&Vector2.Distance(actual,expected)<0.5f,new{panelDelta=actual.ToString(),expected=expected.ToString(),time=ui.playerCore.PlaybackTime,baseline=Vec(baselineWorld),hips=Vec(ui.avatarHelper.CurrentAvatarHips.position),resource=descriptor.Id});yield return Capture("native-mini-follow");
  ui.playerCore.StopPlay();window.SetEnabled(true);distance.enabled=true;settings.data.enableDanceUIFollow=false;follow.enabled=false;ui.Window.SetMiniMode(false);yield return null;
 }
 private IEnumerator ScrollChecks()
 {
  foreach(var tab in new[]{"library","import","settings"})
  {
   ui.Window.SetTab(tab);yield return null;Canvas.ForceUpdateCanvases();var scroll=ui.Window.GetComponentsInChildren<ScrollRect>().First();scroll.verticalNormalizedPosition=1;yield return null;
   float contentHeight=LayoutUtility.GetPreferredHeight(scroll.content);float viewportHeight=scroll.viewport.rect.height;var before=scroll.content.anchoredPosition.y;scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){scrollDelta=new Vector2(0,-1)});yield return null;float delta=scroll.content.anchoredPosition.y-before;Record("scroll-"+tab,contentHeight<=viewportHeight+1||delta>20,new{delta,scroll.scrollSensitivity,contentHeight,viewportHeight});
  }
  ui.Window.SetTab("library");
 }
 private IEnumerator DialogChecks()
 {
  ui.Window.SetTab("import");var composer=ui.Window.GetComponentInChildren<DanceComposerPanel>(true);
  foreach(var key in new[]{"composer.auto","composer.browse","composer.layers.add","composer.export"})
  {Logger.LogInfo("DIALOG_WAIT "+key);yield return new WaitForSecondsRealtime(1);Button(composer.transform,key).onClick.Invoke();while(DanceDialogs.Busy)yield return null;Logger.LogInfo("DIALOG_RETURN "+key);yield return new WaitForSecondsRealtime(2);}
  File.WriteAllText(Path.Combine(output,"dialogs-status.txt"),string.Join("\n",composer.GetComponentsInChildren<Text>(true).Select(t=>t.text)));Logger.LogInfo("DIALOG_COMPLETE");
 }
 private IEnumerator Capture(string name)
 {
  Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
  var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
  texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
 }
 private void Save() { File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new { scope=Paths.GameRootPath.IndexOf("steam-3.4",StringComparison.OrdinalIgnoreCase)>=0?"Steam 3.4 isolated game; automated API and rendered-frame checks":"GitHub 3.3 isolated game; automated API and rendered-frame checks",results },Formatting.Indented)); }
}
