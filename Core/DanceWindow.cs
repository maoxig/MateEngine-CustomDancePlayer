using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    public class DanceWindow : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public Transform ImportRoot { get; private set; }
        private DancePlayerUIManager owner;
        private RectTransform panel, list;
        private DanceVirtualLibrary virtualList;
        private GraphicRaycaster interaction;
        private GameObject library, settings, import, libraryTransport;
        private Text current, status, time, cameraScaleInfo;
        private Button play, playModeButton, favoriteButton, panelKeyButton, globalKeyButton;
        private Text favoriteHint;
        private int captureKey;
        private readonly Dictionary<DanceHotkeys.Action,Button> keyButtons=new Dictionary<DanceHotkeys.Action,Button>();
        private Slider progress;
        private DanceComposerPanel composer;
        private bool updatingProgress;
        private readonly System.Collections.Generic.Dictionary<string,Button> tabs = new System.Collections.Generic.Dictionary<string,Button>();
        private readonly System.Collections.Generic.Dictionary<DancePlaylistManager.PlaylistType,Button> scopeButtons = new System.Collections.Generic.Dictionary<DancePlaylistManager.PlaylistType,Button>();
        private readonly System.Collections.Generic.Dictionary<string,Button> formatButtons = new System.Collections.Generic.Dictionary<string,Button>();
        private Button folderButton, formatCycleButton;
        private string tab = "library";
        private float nextRefresh;
        private int folderIndex = -1;
        private Maoxig.VmdDanceStudio.VmdDanceDraft savedDraft;
        private string savedPreviewResourceId;
        private string savedEditingPackagePath;
        private bool settingsBuilt, importBuilt;
        private ScrollRect settingsScroll;
        private float settingsScrollPosition=1f;
        private bool settingsScrollRestorePending;
        public bool IsMini => DanceSettingsHandler.Instance.data.miniMode;
        private static string BuildBadge()
        {
            var values=typeof(DanceWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false);
            if(values.Length==0)return "v0.2.0";
            string version=((System.Reflection.AssemblyInformationalVersionAttribute)values[0]).InformationalVersion;
            int suffix=version.IndexOf("rc.",StringComparison.Ordinal);
            return suffix>=0?version.Substring(suffix):"v"+version;
        }
        public void SetMiniMode(bool value)
        {
            if (IsMini == value) return;
            DanceSettingsHandler.Instance.data.miniMode = value;
            DanceSettingsHandler.OnSettingChanged(); Rebuild();
        }
        public static DanceWindow Create(DancePlayerUIManager owner)
        {
            var image = owner.MainPanelRoot == null ? null : owner.MainPanelRoot.GetComponent<Image>();
            DanceUi.Init(owner.CurrentPlayText == null ? null : owner.CurrentPlayText.font, image == null ? null : image.sprite);
            var go = DanceUi.CreateCanvas(); var window = go.AddComponent<DanceWindow>();window.owner=owner;owner.Window=window;
            window.Canvas = go.GetComponent<Canvas>(); window.Canvas.sortingOrder = 150;
            var scale = go.AddComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280,720); scale.matchWidthOrHeight = 0.5f;
            window.interaction=go.AddComponent<GraphicRaycaster>(); window.Build(); DanceLocale.Changed += window.Rebuild; return window;
        }
        private void Rebuild()
        {
            if(settingsScroll!=null && !settingsScrollRestorePending)settingsScrollPosition=settingsScroll.verticalNormalizedPosition;
            settingsScroll=null;
            if (composer != null) { savedDraft = composer.CaptureDraft() ?? savedDraft; savedPreviewResourceId=composer.PreviewResourceId; savedEditingPackagePath=composer.EditingPackagePath; }
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            if (composer != null) { composer.gameObject.SetActive(false); Destroy(composer.gameObject); }
            composer = null; list = null; virtualList = null; library = settings = import = libraryTransport = null;
            settingsBuilt = importBuilt = false;
            CancelKeyCapture();
            current = status = time = favoriteHint = cameraScaleInfo = null; play = playModeButton = favoriteButton = panelKeyButton = globalKeyButton = null; progress = null;
            Build(); if (savedDraft != null && composer != null) composer.RestoreDraft(savedDraft); SetTab(tab);
        }
        public void RefreshSettingsState()
        {
            if (!IsMini && tab == "settings") Rebuild();
        }
        private void Build()
        {
            if (IsMini) { BuildMini(); return; }
            var go = DanceUi.Node("PlayerPanel", transform); panel = go.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f,0.5f); panel.sizeDelta = new Vector2(520,640);
            DanceUi.Image(go, DanceUi.Background);
            var inner = DanceUi.Node("Padding", go.transform); DanceUi.Stretch(inner.GetComponent<RectTransform>());
            inner.GetComponent<RectTransform>().offsetMin = new Vector2(20,16); inner.GetComponent<RectTransform>().offsetMax = new Vector2(-20,-16);
            var layout = inner.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var follow = go.AddComponent<HipsFollower>(); follow.avatarHelper=owner.avatarHelper;
            var data=DanceSettingsHandler.Instance.data; follow.enabled=data.enableDanceUIFollow;
            panel.anchoredPosition=data.enableDanceUIFollow ? data.uiBasePosition : data.uiRawPosition;
            DanceSettingsHandler.Instance.hipsFollower=follow;
            var header = DanceUi.Row(inner.transform, 54); DanceUi.Image(header,new Color(0,0,0,0));
            var brand=DanceUi.Node("Brand",header.transform); DanceUi.Size(brand,-1,54,1);
            var brandLayout=brand.AddComponent<VerticalLayoutGroup>();brandLayout.childControlWidth=brandLayout.childControlHeight=true;brandLayout.childForceExpandHeight=false;
            DanceUi.Label(brand.transform, "Custom Dance Player", 21);
            var hint=DanceUi.Label(brand.transform,DanceLocale.T("window.drag",DanceHotkeys.PanelLabel()),12); hint.color=new Color(0.53f,0.58f,0.70f);
            var drag=go.AddComponent<UIDragHandler>();drag.hipsFollower=follow;drag.dragHandles=new[] {header.GetComponent<RectTransform>()};
            status=DanceUi.Label(header.transform,"",10,108);status.alignment=TextAnchor.MiddleRight;
            var versionBadge=DanceUi.Node("VersionBadge",header.transform);DanceUi.Size(versionBadge,54,28);DanceUi.Image(versionBadge,new Color(0.12f,0.14f,0.21f,1f));
            var versionText=DanceUi.Label(versionBadge.transform,BuildBadge(),11);versionText.alignment=TextAnchor.MiddleCenter;DanceUi.Stretch(versionText.rectTransform);
            DanceUi.ModeButton(header.transform, false, () => SetMiniMode(true));
            DanceUi.Button(header.transform, "×", () => owner.SetPanelVisible(false), 34);
            var tabRow = DanceUi.Row(inner.transform,38); tabs.Clear();
            foreach (var key in new[] { "library", "import", "settings" }) {var captured=key;tabs[key]=DanceUi.Button(tabRow.transform,DanceLocale.T("tab."+key),()=>SetTab(captured));}
            var body = DanceUi.Node("Body", inner.transform); body.AddComponent<LayoutElement>().flexibleHeight = 1;
            library = Pane(body.transform, "Library"); settings = Pane(body.transform, "Settings"); import = Pane(body.transform, "Import"); ImportRoot = import.transform;
            BuildLibrary();
            BuildFullTransport(inner.transform);
            RefreshLibrary(); SetTab(tab);
        }
        private void BuildFullTransport(Transform parent)
        {
            // Keep the pre-rc.4 transport's light, open layout.  The only scope
            // container is also available on the Import tab in a compact layout.
            libraryTransport=DanceUi.Node("LibraryTransport",parent);DanceUi.Size(libraryTransport,-1,98);
            var layout=libraryTransport.AddComponent<VerticalLayoutGroup>();layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var title=DanceUi.Row(libraryTransport.transform,28);title.name="TransportTitle";
            BuildTrackInfo(title.transform, false);
            var seek=DanceUi.Row(libraryTransport.transform,20);seek.name="TransportSeek";
            progress=DanceUi.Slider(seek.transform,0,1,0,value=>{if(!updatingProgress)owner.playerCore.Seek(value);});
            var controls=DanceUi.Row(libraryTransport.transform,38);controls.name="TransportControls";
            var previous=DanceUi.Button(controls.transform,"◀",owner.playerCore.PlayPrev,40);DanceUi.AttachTooltip(previous,DanceLocale.T("player.prev"));
            play=DanceUi.Button(controls.transform,"▶",owner.OnPlayPauseBtnClick,44,true);DanceUi.AttachTooltip(play,DanceLocale.T("player.play"));
            var stop=DanceUi.Button(controls.transform,"■",owner.playerCore.StopPlay,40);DanceUi.AttachTooltip(stop,DanceLocale.T("player.stop"));
            var next=DanceUi.Button(controls.transform,"▶",owner.playerCore.PlayNext,40);DanceUi.AttachTooltip(next,DanceLocale.T("player.next"));
            playModeButton=DanceUi.Button(controls.transform,ModeIcon(DanceSettingsHandler.Instance.data.currentPlayMode),CyclePlayMode,40);
            playModeButton.name="PlayMode";DanceUi.AttachTooltip(playModeButton,ModeLabel(),170);
            var volumeIcon=DanceUi.Label(controls.transform,"♪",16,22);volumeIcon.alignment=TextAnchor.MiddleCenter;
            DanceUi.Slider(controls.transform,0,1,DanceSettingsHandler.Instance.data.danceVolume,value=>{DanceSettingsHandler.Instance.data.danceVolume=value;owner.avatarHelper.UpdateAudioVolume();DanceSettingsHandler.OnSettingChanged();});
        }
        private static GameObject Pane(Transform parent, string name)
        {
            var go = DanceUi.Node(name, parent); DanceUi.Stretch(go.GetComponent<RectTransform>());
            var layout = go.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            return go;
        }
        private void BuildLibrary()
        {
            var row = DanceUi.Row(library.transform,34);
            var input = DanceUi.Input(row.transform, DanceLocale.T("library.search"), value => { owner.playerCore.playlistManager.Search = value; RefreshLibrary(); });
            input.text = owner.playerCore.playlistManager.Search;
            DanceUi.Button(row.transform, DanceLocale.T("library.refresh"), owner.RefreshDropdownAsync, 68);
            DanceUi.Button(row.transform, DanceLocale.T("library.add"), ImportDance, 84, true);
            scopeButtons.Clear(); formatButtons.Clear();
            var filters = DanceUi.Row(library.transform,34);
            scopeButtons[DancePlaylistManager.PlaylistType.All] = CompactFilter(DanceUi.Button(filters.transform, DanceLocale.T("library.all"), () => SelectScope(DancePlaylistManager.PlaylistType.All), 58));
            scopeButtons[DancePlaylistManager.PlaylistType.Favorites] = CompactFilter(DanceUi.Button(filters.transform, DanceLocale.T("library.favorites"), () => SelectScope(DancePlaylistManager.PlaylistType.Favorites), 76));
            scopeButtons[DancePlaylistManager.PlaylistType.Queue] = CompactFilter(DanceUi.Button(filters.transform,DanceLocale.T("library.queue"),()=>SelectScope(DancePlaylistManager.PlaylistType.Queue),58));
            formatCycleButton=CompactFilter(DanceUi.Button(filters.transform,FormatFilterLabel(),CycleFormat,122));
            folderButton=CompactFilter(DanceUi.Button(filters.transform,FolderFilterLabel(),CycleFolder));
            list = DanceUi.Scroll(library.transform, out var scroll);
            virtualList = list.gameObject.AddComponent<DanceVirtualLibrary>(); virtualList.Initialize(owner,scroll);
        }
        public void RefreshLibrary()
        {
            if(virtualList==null)return;
            owner.playerCore.playlistManager.ApplyFilters();virtualList.Refresh();UpdateLibraryFilterStyles();
        }
        private void SelectScope(DancePlaylistManager.PlaylistType type)
        {
            folderIndex=-1;
            owner.playerCore.playlistManager.SetPlaylistType(type);RefreshLibrary();
        }
        private void CycleFormat()
        {
            string[] formats={"","unity3d","me","vmdance","vmd"};
            string current=owner.playerCore.playlistManager.Format??"";int index=Array.IndexOf(formats,current);
            owner.playerCore.playlistManager.Format=formats[(index+1+formats.Length)%formats.Length];RefreshLibrary();
        }
        private void CycleFolder()
        {
            var folders=owner.playerCore.playlistManager.GetAvailableFolders();folderIndex++;
            if(folderIndex>=folders.Count)folderIndex=-1;string folder=folderIndex<0?"":folders[folderIndex];
            owner.playerCore.playlistManager.SetPlaylistType(folderIndex<0?DancePlaylistManager.PlaylistType.All:DancePlaylistManager.PlaylistType.Folder,folder);RefreshLibrary();
        }
        private string FormatFilterLabel()
        {
            string format=owner.playerCore.playlistManager.Format??"";
            string value=format.Length==0?DanceLocale.T("library.filter.allValue"):format=="unity3d"?"Unity3D":format=="vmdance"?"VMDance":format.ToUpperInvariant();
            return DanceLocale.T("library.format")+": "+value+"  ›";
        }
        private string FolderFilterLabel()
        {
            var manager=owner.playerCore.playlistManager;
            string value=manager.CurrentType==DancePlaylistManager.PlaylistType.Folder&&!string.IsNullOrEmpty(manager.CurrentFolder)?manager.CurrentFolder:DanceLocale.T("library.filter.allValue");
            return DanceLocale.T("library.folder.label")+": "+value+"  ›";
        }
        private static Button CompactFilter(Button button)
        {
            var label=button.GetComponentInChildren<Text>();label.fontSize=13;
            label.rectTransform.offsetMin=new Vector2(4,0);label.rectTransform.offsetMax=new Vector2(-4,0);
            return button;
        }
        private void UpdateLibraryFilterStyles()
        {
            var manager=owner.playerCore.playlistManager;
            foreach(var pair in scopeButtons)pair.Value.GetComponent<Image>().color=manager.CurrentType==pair.Key?DanceUi.Accent:DanceUi.Surface;
            if(formatCycleButton!=null){formatCycleButton.GetComponent<Image>().color=string.IsNullOrEmpty(manager.Format)?DanceUi.Surface:DanceUi.Accent;formatCycleButton.GetComponentInChildren<Text>().text=FormatFilterLabel();}
            foreach(var pair in formatButtons)pair.Value.GetComponent<Image>().color=manager.Format==pair.Key?DanceUi.Accent:DanceUi.Surface;
            if(folderButton!=null)folderButton.GetComponentInChildren<Text>().text=FolderFilterLabel();
            if(folderButton!=null)folderButton.GetComponent<Image>().color=manager.CurrentType==DancePlaylistManager.PlaylistType.Folder?DanceUi.Accent:DanceUi.Surface;
        }
        private void SettingsSection(Transform parent,string key)
        {
            var label=DanceUi.Label(parent,DanceLocale.T(key),14);label.fontStyle=FontStyle.Bold;
            label.color=new Color(.70f,.72f,.86f);DanceUi.Size(label.gameObject,-1,24);
        }
        private void BuildSettings()
        {
            var content=DanceUi.Scroll(settings.transform,out var scroll);settingsScroll=scroll;settingsScrollRestorePending=true;StartCoroutine(RestoreSettingsScroll(scroll,settingsScrollPosition));
            var data=DanceSettingsHandler.Instance.data;
            SettingsSection(content,"settings.section.interface");
            var row=DanceUi.Row(content);DanceUi.Label(row.transform,DanceLocale.T("settings.language"),16,110);
            var languages=new List<string>{"auto"};languages.AddRange(DanceLocale.AvailableLanguages);
            string selectedLanguage=data.language;
            var languageButton=DanceUi.Button(row.transform,(selectedLanguage=="auto"?DanceLocale.T("settings.language.auto"):DanceLocale.LanguageName(selectedLanguage))+"  ›",()=>{
                int index=languages.IndexOf(selectedLanguage);data.language=languages[(index+1)%languages.Count];
                DanceSettingsHandler.OnSettingChanged();DanceLocale.Set(data.language);
            });languageButton.name="LanguageSelector";
            DanceUi.AttachTooltip(languageButton,string.Join(" / ",languages.Select(code=>code=="auto"?DanceLocale.T("settings.language.auto"):DanceLocale.LanguageName(code))),320,70);
            DanceUi.Toggle(content, DanceLocale.T("settings.uiFollow"), data.enableDanceUIFollow, v => { data.enableDanceUIFollow=v; DanceSettingsHandler.Instance.hipsFollower.enabled=v; DanceSettingsHandler.Instance.hipsFollower.UpdateBaseAndInitial(); DanceSettingsHandler.OnSettingChanged(); });
            DanceUi.Toggle(content, DanceLocale.T("settings.hide"), data.hidePanelOnStart, v => { data.hidePanelOnStart = v; DanceSettingsHandler.OnSettingChanged(); });
            SettingsSection(content,"settings.section.playback");
            DanceUi.Toggle(content, DanceLocale.T("settings.autoplay"), data.autoPlayOnStart, v => { data.autoPlayOnStart = v; DanceSettingsHandler.OnSettingChanged(); });
            DanceUi.Toggle(content,DanceLocale.T("settings.vmdFootIk"),data.enableVmdFootIk,v=>{owner.playerCore.SetVmdFootIk(v);DanceSettingsHandler.OnSettingChanged();});
            DanceUi.Toggle(content,DanceLocale.T("settings.vmdUpright"),data.keepVmdRootUpright,v=>{owner.playerCore.SetVmdRootOptions(v,data.lockVmdFacingForward);DanceSettingsHandler.OnSettingChanged();});
            DanceUi.Toggle(content,DanceLocale.T("settings.vmdFacing"),data.lockVmdFacingForward,v=>{owner.playerCore.SetVmdRootOptions(data.keepVmdRootUpright,v);DanceSettingsHandler.OnSettingChanged();});
            var delay=DanceUi.Row(content);DanceUi.Label(delay.transform,DanceLocale.T("settings.delay"),15,200);
            var delayText=DanceUi.Label(delay.transform,data.animationStartDelay.ToString("0.00")+" s",15,80);
            DanceUi.Slider(delay.transform,0,1,data.animationStartDelay,v=>{data.animationStartDelay=v;delayText.text=v.ToString("0.00")+" s";DanceSettingsHandler.OnSettingChanged();});
            SettingsSection(content,"settings.section.camera");
            DanceUi.Toggle(content, DanceLocale.T("camera.enabled"), data.enableMMDCamera, owner.SetCameraEnabled);
            DanceUi.Toggle(content,DanceLocale.T("camera.autoScale"),data.autoMmdCameraScale,v=>{data.autoMmdCameraScale=v;owner.playerCore.SetCameraScale(data.mmdCameraScale);RefreshCameraScaleInfo();DanceSettingsHandler.OnSettingChanged();});
            var scale=DanceUi.Row(content);DanceUi.Label(scale.transform,DanceLocale.T("camera.scale"),15,190);
            var valueLabel=DanceUi.Label(scale.transform,data.mmdCameraScale.ToString("0.000")+"x",15,78);
            DanceUi.Slider(scale.transform,.25f,4,data.mmdCameraScale,v=>{data.mmdCameraScale=v;owner.playerCore.SetCameraScale(v);valueLabel.text=v.ToString("0.000")+"x";RefreshCameraScaleInfo();DanceSettingsHandler.OnSettingChanged();});
            cameraScaleInfo=DanceUi.Label(content,CameraScaleInfo(),12);cameraScaleInfo.name="CameraScaleInfo";cameraScaleInfo.color=new Color(.60f,.65f,.73f);DanceUi.Size(cameraScaleInfo.gameObject,-1,32);
            DanceUi.Toggle(content, DanceLocale.T("settings.window"), data.enableWindowFollow, owner.SetWindowFollowEnabled);
            DanceUi.Toggle(content, DanceLocale.T("settings.distance"), data.enableCameraDistanceKeep, v => { data.enableCameraDistanceKeep = v; foreach (var c in DanceBootstrap.Root.GetComponentsInChildren<DanceCameraDistKeeper>(true)) c.enabled = v; DanceSettingsHandler.OnSettingChanged(); });
            SettingsSection(content,"settings.section.shadow");
            DanceUi.Toggle(content, DanceLocale.T("settings.shadowVisible"), data.showAvatarShadow, v => { data.showAvatarShadow=v; foreach(var c in DanceBootstrap.Root.GetComponentsInChildren<DanceShadowFollower>(true)) c.ApplyVisibility(); DanceSettingsHandler.OnSettingChanged(); });
            DanceUi.Toggle(content, DanceLocale.T("settings.shadow"), data.enableShadowFollow, v => { data.enableShadowFollow=v; DanceSettingsHandler.OnSettingChanged(); });
            keyButtons.Clear();SettingsSection(content,"settings.hotkeys");
            foreach(DanceHotkeys.Action action in Enum.GetValues(typeof(DanceHotkeys.Action)))BuildKeyRow(content,action);
            SettingsSection(content,"settings.section.library");
            var pathRow=DanceUi.Row(content,36);
            var pathClip=DanceUi.Node("DanceFolderPath",pathRow.transform);DanceUi.Size(pathClip,-1,36,1);pathClip.AddComponent<RectMask2D>();
            var pathText=DanceUi.Label(pathClip.transform,DanceLocale.T("settings.paths",owner.resourceManager.LibraryFolder).Replace("\n"," "),12);DanceUi.Stretch(pathText.rectTransform);pathText.horizontalOverflow=HorizontalWrapMode.Overflow;
            var openFolder=DanceUi.Button(pathRow.transform,DanceLocale.T("settings.openFolderShort"),OpenDanceFolder,56);openFolder.name="OpenDanceFolder";
            DanceUi.AttachTooltip(openFolder,owner.resourceManager.LibraryFolder,300,72);
        }
        private System.Collections.IEnumerator RestoreSettingsScroll(ScrollRect scroll,float position)
        {
            yield return null;
            if(scroll==null || scroll!=settingsScroll)yield break;
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=position;settingsScrollRestorePending=false;
        }
        public void RefreshCameraScaleInfo() {if(cameraScaleInfo!=null)cameraScaleInfo.text=CameraScaleInfo();}
        private string CameraScaleInfo()
        {
            float eye=owner.avatarHelper.MeasureAvatarEyeHeight();bool eyes=eye>0f;
            float target=eyes?eye:owner.avatarHelper.MeasureAvatarHeight();
            float reference=eyes?owner.playerCore.GetCameraReferenceEyeHeight():owner.playerCore.GetCameraReferenceBodyHeight();
            return DanceLocale.T("camera.scaleInfo",DanceLocale.T(eyes?"camera.basis.eye":"camera.basis.body"),target.ToString("0.000"),reference.ToString("0.000"),owner.playerCore.GetAvatarCameraScale().ToString("0.000"),owner.playerCore.GetCameraAuthoringScale().ToString("0.000"),owner.playerCore.GetEffectiveCameraScale().ToString("0.000"));
        }
        private void ImportDance()
        {
            try { DanceDialogs.Open(DanceLocale.T("library.add"),new[]{"unity3d","me","vmdance","vmd"},files=>{
                if(this==null||files.Length==0)return;
                Directory.CreateDirectory(owner.resourceManager.LibraryFolder);
                foreach(var source in files){
                    if(Path.GetExtension(source).Equals(".vmd",StringComparison.OrdinalIgnoreCase)){SetTab("import");composer.LoadMotion(source);continue;}
                    string destination=Path.Combine(owner.resourceManager.LibraryFolder,Path.GetFileName(source));
                    if(!Path.GetFullPath(source).Equals(Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))File.Copy(source,destination,false);
                }
                owner.RefreshDropdown();
            },error=>status.text=DanceLocale.T("error.detail",error.Message),true); } catch(Exception error){status.text=DanceLocale.T("error.detail",error.Message);}
        }
        private void SetTransportCompact(bool compact)
        {
            DanceUi.Size(libraryTransport,-1,compact?78:98);
            foreach(var row in new[]{new[]{"TransportTitle","24","28"},new[]{"TransportSeek","16","20"},new[]{"TransportControls","30","38"}}) {
                var transform=libraryTransport.transform.Find(row[0]);float height=float.Parse(row[compact?1:2]);DanceUi.Size(transform.gameObject,-1,height);
                foreach(Transform child in transform) {
                    var element=child.GetComponent<LayoutElement>();if(element!=null && element.preferredHeight>=0){element.minHeight=element.preferredHeight=compact?Mathf.Min(element.preferredHeight,height):row[0]=="TransportSeek"?20:row[0]=="TransportTitle"?28:34;}
                }
            }
        }
        public bool ContainsPointer(Vector2 position) {return gameObject.activeInHierarchy && panel!=null && RectTransformUtility.RectangleContainsScreenPoint(panel,position,Canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:Canvas.worldCamera);}
        public void SetTab(string value) { tab = value; if(IsMini)return; EnsureTabBuilt(value); library.SetActive(value == "library"); import.SetActive(value == "import"); settings.SetActive(value == "settings"); if(libraryTransport!=null){libraryTransport.SetActive(value!="settings");SetTransportCompact(value=="import");} foreach(var pair in tabs) pair.Value.GetComponent<Image>().color=pair.Key==value?new Color(0.23f,0.22f,0.37f):DanceUi.Surface; }
        private void EnsureTabBuilt(string value)
        {
            if(value=="settings"&&!settingsBuilt){settingsBuilt=true;BuildSettings();}
            if(value=="import"&&!importBuilt)
            {
                importBuilt=true;composer=DanceComposerPanel.Create(owner,Canvas);composer.ShowEmbedded();
                if(savedDraft!=null)composer.RestoreDraft(savedDraft);
                composer.PreviewResourceId=savedPreviewResourceId;
                composer.RestoreEditingPath(savedEditingPackagePath);
            }
        }
        private void BuildMini()
        {
            var data = DanceSettingsHandler.Instance.data;
            var go = DanceUi.Node("PlayerPanel", transform); panel = go.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f,0.5f); panel.sizeDelta = new Vector2(380,160);
            panel.anchoredPosition = data.enableDanceUIFollow ? data.miniBasePosition : data.miniRawPosition;
            DanceUi.Image(go, DanceUi.Background);
            var inner = DanceUi.Node("Padding", go.transform); DanceUi.Stretch(inner.GetComponent<RectTransform>());
            inner.GetComponent<RectTransform>().offsetMin = new Vector2(14,12); inner.GetComponent<RectTransform>().offsetMax = new Vector2(-14,-12);
            var layout = inner.AddComponent<VerticalLayoutGroup>();layout.spacing=6;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var header = DanceUi.Row(inner.transform,24);DanceUi.Image(header,new Color(0,0,0,0));
            DanceUi.Label(header.transform,"Custom Dance Player",14);
            DanceUi.ModeButton(header.transform,true,()=>SetMiniMode(false));
            DanceUi.Button(header.transform,"×",()=>owner.SetPanelVisible(false),30);
            var follow = go.AddComponent<HipsFollower>();follow.avatarHelper=owner.avatarHelper;follow.enabled=data.enableDanceUIFollow;
            DanceSettingsHandler.Instance.hipsFollower=follow;
            var drag=go.AddComponent<UIDragHandler>();drag.hipsFollower=follow;drag.dragHandles=new[]{header.GetComponent<RectTransform>()};
            var title = DanceUi.Row(inner.transform,26);BuildTrackInfo(title.transform,true);
            var seek = DanceUi.Row(inner.transform,14);progress=DanceUi.Slider(seek.transform,0,1,0,v=>{if(!updatingProgress)owner.playerCore.Seek(v);});
            var controls = DanceUi.Row(inner.transform,30);
            DanceUi.Button(controls.transform,"◀",owner.playerCore.PlayPrev,34);
            play=DanceUi.Button(controls.transform,DanceLocale.T("player.play"),owner.OnPlayPauseBtnClick,60,true);
            DanceUi.Button(controls.transform,"■",owner.playerCore.StopPlay,34);
            DanceUi.Button(controls.transform,"▶",owner.playerCore.PlayNext,34);
            playModeButton=DanceUi.Button(controls.transform,ModeIcon(data.currentPlayMode),CyclePlayMode,34);
            playModeButton.name="PlayMode";DanceUi.AttachTooltip(playModeButton,ModeLabel(),170);
            DanceUi.Slider(controls.transform,0,1,data.danceVolume,v=>{data.danceVolume=v;owner.avatarHelper.UpdateAudioVolume();DanceSettingsHandler.OnSettingChanged();});
            status=DanceUi.Label(inner.transform,"",11);DanceUi.Size(status.gameObject,-1,18);
        }
        private string ModeLabel() => DanceLocale.T("mode." + DanceSettingsHandler.Instance.data.currentPlayMode.ToString().ToLowerInvariant());
        private static string ModeIcon(DancePlayerCore.PlayMode mode)
        {
            switch(mode){case DancePlayerCore.PlayMode.Loop:return "↻";case DancePlayerCore.PlayMode.Random:return "⇄";default:return "→";}
        }
        private void CyclePlayMode()
        {
            var data=DanceSettingsHandler.Instance.data;data.currentPlayMode=(DancePlayerCore.PlayMode)(((int)data.currentPlayMode+1)%3);
            DanceSettingsHandler.OnSettingChanged();Rebuild();
        }
        void Update()
        {
            if(panel==null)return;
            PollKeyCapture();
            interaction.enabled=!DanceDialogs.Busy;
            var bounds=Canvas.GetComponent<RectTransform>().rect.size;
            var position=panel.anchoredPosition;
            position.x=Mathf.Clamp(position.x,-Mathf.Max(0,(bounds.x-panel.rect.width)/2),Mathf.Max(0,(bounds.x-panel.rect.width)/2));
            position.y=Mathf.Clamp(position.y,-Mathf.Max(0,(bounds.y-panel.rect.height)/2),Mathf.Max(0,(bounds.y-panel.rect.height)/2));
            panel.anchoredPosition=position;
            var core = owner.playerCore;
            if(current!=null)current.text = CurrentTrackLabel();
            UpdateFavorite();
            if(play!=null)play.GetComponentInChildren<Text>().text = IsMini
                ? DanceLocale.T(core.IsPlaying && !core.Paused ? "player.pause" : "player.play")
                : (core.IsPlaying && !core.Paused ? "Ⅱ" : "▶");
            if(progress!=null){progress.interactable = core.IsPlaying && core.CanSeek; updatingProgress = true; progress.value = core.Duration <= 0 ? 0 : core.PlaybackTime / core.Duration; updatingProgress = false;}
            if(time!=null)time.text = FormatTime(core.PlaybackTime) + " / " + FormatTime(core.Duration);
            if (Time.unscaledTime > nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; RefreshCameraScaleInfo(); if(status!=null)status.text = core.LastError ?? (owner.resourceManager.IsRefreshing ? DanceLocale.T("status.scanning") : core.IsLoading ? DanceLocale.T("status.loading") : DanceLocale.T(owner.avatarHelper.IsAvatarAvailable() ? "status.ready" : "status.avatar", owner.resourceManager.DanceFileList.Count)); var scale=Canvas.GetComponent<CanvasScaler>();scale.matchWidthOrHeight=Screen.width/1280f<Screen.height/720f?0:1; }
        }
        private string CurrentTrackLabel()
        {
            string label=owner.playerCore.GetCurrentPlayFileName();bool? packageIk=owner.resourceManager.CurrentVmdFootIk;
            if(owner.playerCore.IsPlaying&&packageIk.HasValue)label+=" · "+DanceLocale.T(packageIk.Value?"player.packageIk.on":"player.packageIk.off");
            return label;
        }
        private static string FormatTime(float seconds) => ((int)seconds / 60).ToString() + ":" + ((int)seconds % 60).ToString("00");
        private void BuildTrackInfo(Transform parent, bool mini)
        {
            var row=parent.GetComponent<HorizontalLayoutGroup>();row.spacing=mini?5:6;
            favoriteButton=DanceUi.Button(parent,"☆",ToggleCurrentFavorite,mini?26:28);
            favoriteButton.name="CurrentDanceFavorite";
            DanceUi.Size(favoriteButton.gameObject,mini?26:28,mini?26:28);
            var icon=favoriteButton.GetComponentInChildren<Text>();icon.fontSize=19;icon.rectTransform.offsetMin=icon.rectTransform.offsetMax=Vector2.zero;
            favoriteButton.GetComponent<Image>().color=Color.clear;
            DanceUi.AttachTooltip(favoriteButton,DanceLocale.T("player.favorite"),190);
            favoriteHint=favoriteButton.GetComponent<DanceTooltip>().Hint.GetComponentInChildren<Text>();
            var clip=DanceUi.Node("CurrentDanceTitle",parent);DanceUi.Size(clip,-1,-1,1);clip.GetComponent<LayoutElement>().minWidth=0;clip.AddComponent<RectMask2D>();
            current=DanceUi.Label(clip.transform,CurrentTrackLabel(),mini?14:16);DanceUi.Stretch(current.rectTransform);current.horizontalOverflow=HorizontalWrapMode.Overflow;
            time=DanceUi.Label(parent,"0:00 / 0:00",mini?11:13,mini?94:106);time.alignment=TextAnchor.MiddleRight;time.horizontalOverflow=HorizontalWrapMode.Overflow;
            UpdateFavorite();
        }
        private string FavoriteResourceId()
        {
            string id=owner.playerCore.CurrentResourceId;
            return id!=null&&owner.resourceManager.Descriptors.ContainsKey(id)?id:null;
        }
        private void ToggleCurrentFavorite()
        {
            string id=FavoriteResourceId();if(id==null)return;
            owner.playerCore.playlistManager.ToggleFavorite(id);RefreshLibrary();UpdateFavorite();
        }
        private void UpdateFavorite()
        {
            if(favoriteButton==null)return;
            string id=FavoriteResourceId();bool selected=id!=null&&owner.playerCore.playlistManager.IsFavorite(id);
            favoriteButton.interactable=id!=null;
            var icon=favoriteButton.GetComponentInChildren<Text>();icon.text=selected?"★":"☆";icon.color=selected?DanceUi.Accent:new Color(0.65f,0.69f,0.78f);
            if(favoriteHint!=null)favoriteHint.text=DanceLocale.T(selected?"player.unfavorite":"player.favorite");
        }
        public void OpenDanceFolder()
        {
            try { string path=Path.GetFullPath(owner.resourceManager.LibraryFolder);Directory.CreateDirectory(path);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true}); }
            catch(Exception exception){Debug.LogWarning("[CustomDancePlayer] Open dance folder: "+exception.Message);}
        }
        private void BuildKeyRow(Transform parent, DanceHotkeys.Action action)
        {
            var row=DanceUi.Row(parent,36);
            var enabled=DanceUi.ToggleInRow(row.transform,DanceHotkeys.Get(action).enabled && DanceHotkeys.Get(action).key!=KeyCode.None,v=>DanceHotkeys.SetEnabled(action,v));
            enabled.interactable=DanceHotkeys.Get(action).key!=KeyCode.None;
            enabled.name=action+"HotkeyEnabled";
            DanceUi.Label(row.transform,DanceLocale.T("settings.key."+action),13);
            var key=DanceUi.Button(row.transform,DanceHotkeys.BindingLabel(action),()=>BeginKeyCapture(action),126);
            key.name=action==DanceHotkeys.Action.Panel?"PanelBinding":action==DanceHotkeys.Action.PlayPause?"GlobalPlaybackBinding":action+"Binding";
            key.GetComponentInChildren<Text>().fontSize=12;keyButtons[action]=key;
            if(action==DanceHotkeys.Action.Panel)panelKeyButton=key;
            if(action==DanceHotkeys.Action.PlayPause)globalKeyButton=key;
            DanceUi.AttachTooltip(key,DanceLocale.T("settings.keyHelp"),300,70);
            var clear=DanceUi.Button(row.transform,"×",()=>TryBindKey(action,KeyCode.None,false,false,false),28);
            DanceUi.AttachTooltip(clear,DanceLocale.T("settings.clearKey"));
            var reset=DanceUi.Button(row.transform,"↺",()=>ResetKey(action),28);
            DanceUi.AttachTooltip(reset,DanceLocale.T("settings.resetKey"));
        }
        public void BeginKeyCapture(bool global) { BeginKeyCapture(global?DanceHotkeys.Action.PlayPause:DanceHotkeys.Action.Panel); }
        public void BeginKeyCapture(DanceHotkeys.Action action)
        {
            CancelKeyCapture();captureKey=(int)action+1;DanceHotkeys.IsCapturing=true;
            if(keyButtons.TryGetValue(action,out var button))button.GetComponentInChildren<Text>().text=DanceLocale.T("settings.pressKey");
        }
        private void CancelKeyCapture()
        {
            captureKey=0;DanceHotkeys.IsCapturing=false;
            foreach(var pair in keyButtons)if(pair.Value!=null)pair.Value.GetComponentInChildren<Text>().text=DanceHotkeys.BindingLabel(pair.Key);
        }
        private void PollKeyCapture()
        {
            if(captureKey==0)return;
            if(Input.GetKeyDown(KeyCode.Escape)){CancelKeyCapture();return;}
            foreach(KeyCode key in Enum.GetValues(typeof(KeyCode))) {
                if(DanceHotkeys.VirtualKey(key)==0||!Input.GetKeyDown(key))continue;
                TryBindKey((DanceHotkeys.Action)(captureKey-1),key,DanceHotkeys.Control,DanceHotkeys.Alt,DanceHotkeys.Shift);return;
            }
        }
        public bool TryBindKey(bool global, KeyCode key, bool control, bool alt, bool shift) {return TryBindKey(global?DanceHotkeys.Action.PlayPause:DanceHotkeys.Action.Panel,key,control,alt,shift);}
        public bool TryBindKey(DanceHotkeys.Action action, KeyCode key, bool control, bool alt, bool shift)
        {
            bool conflict=false;
            if(key!=KeyCode.None)foreach(DanceHotkeys.Action other in Enum.GetValues(typeof(DanceHotkeys.Action))) {
                var binding=DanceHotkeys.Get(other);
                if(other!=action && key==binding.key && control==binding.control && alt==binding.alt && shift==binding.shift)conflict=true;
            }
            if((key!=KeyCode.None && DanceHotkeys.VirtualKey(key)==0)||conflict) {
                if(keyButtons.TryGetValue(action,out var button)&&button!=null)button.GetComponentInChildren<Text>().text=DanceLocale.T("settings.keyConflict");return false;
            }
            DanceHotkeys.Set(action,key,control,alt,shift);CancelKeyCapture();DanceHotkeys.RefreshListeners();DanceSettingsHandler.OnSettingChanged();Rebuild();return true;
        }
        private void ResetKey(DanceHotkeys.Action action) {TryBindKey(action,action==DanceHotkeys.Action.Panel?KeyCode.H:KeyCode.None,false,false,false);}
        void OnDisable(){CancelKeyCapture();}
        void OnDestroy() { CancelKeyCapture();DanceLocale.Changed -= Rebuild; }
    }
}
