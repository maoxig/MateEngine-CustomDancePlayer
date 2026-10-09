using Maoxig.VmdDanceStudio;
using Maoxig.RuntimeVmd;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    /// <summary>
    /// Runtime-built composer backed by the paired canvas/theme resources.
    /// </summary>
    public sealed class DanceComposerPanel : MonoBehaviour
    {
        private DancePlayerUIManager owner;
        private GameObject panel;
        private InputField idInput;
        private InputField titleInput;
        private InputField authorInput;
        private InputField creditsInput;
        private InputField motionInput;
        private InputField faceInput;
        private InputField lipInput;
        private InputField cameraInput;
        private InputField audioInput;
        private InputField pmxInput;
        private InputField audioOffsetInput;
        private InputField positionScaleInput;
        private InputField cameraEyeInput;
        private Slider cameraAuthorScaleSlider;
        private Text cameraAuthorScaleLabel;
        private Toggle loopToggle;
        private bool? packageFootIk;
        private Button[] footIkButtons;
        private Text layersText;
        private Text statusText;
        private GameObject statusRow;
        private Text statusDetails;
        private Text motionSummary;
        private readonly List<string> additionalLayers = new List<string>();
        private Font font;
        private GameObject syncSection;
        private Button syncStepButton;
        private Button previewPauseButton,previewStopButton;
        private int syncStepIndex;
        private readonly float[] syncSteps = { 0.1f, 0.01f, 1f };
        public string PreviewResourceId { get; set; }
        public string EditingPackagePath { get; private set; }
        private bool preserveSlotTracks;
        private bool applyingDraft;
        private string originalPackageRoot,originalManifestJson;
        private string originalFace,originalLip,originalCamera;
        private GameObject editingRow;
        private Text editingLabel;
        public void RestoreEditingPath(string path)
        {
            EditingPackagePath=path;
            editingRow.SetActive(!string.IsNullOrEmpty(path));
            editingLabel.text=string.IsNullOrEmpty(path)?"":DanceLocale.T("composer.editing",Path.GetFileName(path));
        }
        public bool OpenPackage(string path)
        {
            try
            {
                VmdDancePackageDescriptor package;string error;
                if(!VmdDancePackage.TryOpenArchive(path,Path.Combine(Application.temporaryCachePath,"VmdComposerEdit"),out package,out error))throw new InvalidDataException(error);
                var draft=new VmdDanceDraft{Id=package.Id,Title=package.Title,Author=package.Author,Credits=package.Credits,
                    MotionVmd=package.PrimaryVmdPath,FaceVmd=package.FaceVmdPath,LipVmd=package.LipVmdPath,CameraVmd=package.CameraVmdPath,
                    AudioFile=package.AudioPath,ReferencePmx=package.ReferencePmxPath,
                    AdditionalVmdFiles=new List<string>(package.AdditionalVmdPaths??new string[0]),AudioOffsetSeconds=package.AudioOffsetSeconds,
                    CameraReferenceEyeHeight=package.CameraReferenceEyeHeight,CameraReferenceBodyHeight=package.CameraReferenceBodyHeight,CameraAuthoringScale=package.CameraAuthoringScale,PositionScale=package.PositionScale??0.08f,Loop=package.Loop??false,FootIk=package.FootIk,PreserveSlotTracks=true,
                    OriginalPackageRoot=package.PackageRoot,OriginalManifestJson=File.ReadAllText(package.ManifestPath),
                    OriginalFaceVmd=package.FaceVmdPath,OriginalLipVmd=package.LipVmdPath,OriginalCameraVmd=package.CameraVmdPath};
                ApplyDraft(draft);PreviewResourceId=null;RestoreEditingPath(Path.GetFullPath(path));
                statusText.text=DanceLocale.T("composer.opened");return true;
            }
            catch(Exception exception){SetError(exception);return false;}
        }
        private void SelectPackage()
        {
            try{DanceDialogs.Open(DanceLocale.T("composer.open"),new[]{"vmdance"},paths=>{if(this!=null&&paths.Length>0)OpenPackage(paths[0]);},SetError,false);}
            catch(Exception exception){SetError(exception);}
        }
        public bool SaveEditingPackage()
        {
            try
            {
                if(string.IsNullOrEmpty(EditingPackagePath))return false;
                var result=VmdDancePackageBuilder.Build(ReadDraft(),EditingPackagePath);
                statusText.text=DanceLocale.T("composer.updated",Path.GetFileName(result.OutputPath));owner.RefreshDropdown();return true;
            }
            catch(Exception exception){SetError(exception);return false;}
        }

        public static DanceComposerPanel Create(DancePlayerUIManager owner, Canvas canvas)
        {
            if (owner == null || canvas == null) return null;
            GameObject overlayRoot = new GameObject("VmdDanceComposerCanvas", typeof(RectTransform),
                typeof(DanceComposerPanel));
            overlayRoot.transform.SetParent(owner.Window.ImportRoot, false);
            overlayRoot.AddComponent<LayoutElement>().flexibleHeight = 1;
            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            DanceComposerPanel result = overlayRoot.GetComponent<DanceComposerPanel>();
            result.owner = owner;
            result.Build(overlayRoot.transform);


            return result;
        }

        private readonly List<GameObject> advancedRows = new List<GameObject>();
        public void ShowEmbedded() { panel.SetActive(true); }
        public VmdDanceDraft CaptureDraft() { try { return ReadDraft(); } catch { return null; } }
        public void RestoreDraft(VmdDanceDraft draft) { ApplyDraft(draft); }
        public void LoadMotion(string path)
        {
            motionInput.text = path; titleInput.text = Path.GetFileNameWithoutExtension(path);
            idInput.text = VmdWorkspaceScanner.Slugify(titleInput.text);
            UpdateMotionSummary();
            statusText.text = DanceLocale.T("composer.loaded");
        }
        private void Build(Transform root)
        {
            font = DanceUi.Font;
            panel = DanceUi.Node("Composer", root); DanceUi.Stretch(panel.GetComponent<RectTransform>());
            var layout = panel.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var content = DanceUi.Scroll(panel.transform, out var scroll);
            content.offsetMax=new Vector2(-6,content.offsetMax.y);
            Section(content,"composer.step.files","composer.hint");
            var tools=CreateRow(content,34);
            DanceUi.Button(tools.transform,DanceLocale.T("composer.auto"),AutoFillFolder,170,true);
            var folderHelp=DanceUi.Button(tools.transform,"?",()=>{},26);DanceUi.AttachTooltip(folderHelp,DanceLocale.T("composer.auto.help"),300,96);
            var open=DanceUi.Button(tools.transform,DanceLocale.T("composer.open"),SelectPackage,150);open.name="OpenVmdance";
            DanceUi.AttachTooltip(open,DanceLocale.T("composer.open.help"),300,88);
            DanceUi.Button(tools.transform,DanceLocale.T("composer.new"),()=>{ApplyDraft(new VmdDanceDraft{Id="dance"});PreviewResourceId=null;RestoreEditingPath(null);},56).name="NewVmdance";
            editingRow=CreateRow(content,36);editingLabel=DanceUi.Label(editingRow.transform,"",12);DanceUi.Size(editingLabel.gameObject,-1,36,1);editingLabel.horizontalOverflow=HorizontalWrapMode.Wrap;
            var save=DanceUi.Button(editingRow.transform,DanceLocale.T("composer.saveChanges"),()=>SaveEditingPackage(),110);save.name="SaveVmdanceChanges";
            DanceUi.AttachTooltip(save,DanceLocale.T("composer.saveChanges.help"),300,78);editingRow.SetActive(false);
            motionInput = CreateTextRow(content, DanceLocale.T("composer.motion"), "VMD|*.vmd", null,"composer.motion.help");
            audioInput = CreateTextRow(content, DanceLocale.T("composer.audio"), "Audio|*.ogg;*.wav;*.mp3", null,"composer.audio.help");
            motionInput.gameObject.name="MotionInput";audioInput.gameObject.name="AudioInput";
            motionSummary=DanceUi.Label(content,"",12);motionSummary.color=new Color(0.66f,0.69f,0.78f);DanceUi.Size(motionSummary.gameObject,-1,44);motionSummary.gameObject.SetActive(false);
            motionInput.onEndEdit.AddListener(_=>UpdateMotionSummary());
            syncSection=DanceUi.Node("AudioSync",content);var syncLayout=syncSection.AddComponent<VerticalLayoutGroup>();syncLayout.spacing=6;syncLayout.childControlHeight=syncLayout.childControlWidth=true;syncLayout.childForceExpandHeight=false;
            Section(syncSection.transform,"composer.step.sync","composer.offset.help");
            var syncNumbers=CreateRow(syncSection.transform,34);CreateFixedLabel(syncNumbers.transform,DanceLocale.T("composer.offset"),92);
            audioOffsetInput=CreateInput(syncNumbers.transform,"0",74);audioOffsetInput.gameObject.name="AudioOffsetInput";
            audioOffsetInput.onEndEdit.AddListener(_=>ApplyAudioOffset());
            syncStepButton=DanceUi.Button(syncNumbers.transform,DanceLocale.T("composer.offset.step",0.1f),()=>{syncStepIndex=(syncStepIndex+1)%syncSteps.Length;syncStepButton.GetComponentInChildren<Text>().text=DanceLocale.T("composer.offset.step",syncSteps[syncStepIndex]);},100);
            var reset=DanceUi.Button(syncNumbers.transform,DanceLocale.T("composer.offset.reset"),()=>ChangeAudioOffset(0,true),64);reset.name="SyncReset";
            var syncActions=CreateRow(syncSection.transform,34);
            var earlier=DanceUi.Button(syncActions.transform,DanceLocale.T("composer.offset.earlier"),()=>ChangeAudioOffset(-syncSteps[syncStepIndex]),110);earlier.name="SyncEarlier";
            var later=DanceUi.Button(syncActions.transform,DanceLocale.T("composer.offset.later"),()=>ChangeAudioOffset(syncSteps[syncStepIndex]),110);later.name="SyncLater";
            previewPauseButton=DanceUi.Button(syncActions.transform,"▶",()=>{if(IsOwnPreviewReady())owner.playerCore.TogglePause();},36);previewPauseButton.name="PreviewPause";
            previewStopButton=DanceUi.Button(syncActions.transform,"■",()=>{if(IsOwnPreviewReady()){owner.playerCore.StopPlay();PreviewResourceId=null;}},36);previewStopButton.name="PreviewStop";
            DanceUi.AttachTooltip(previewPauseButton,DanceLocale.T("composer.preview.pause"),180);DanceUi.AttachTooltip(previewStopButton,DanceLocale.T("composer.preview.stop"),180);
            DanceUi.AttachTooltip(earlier,DanceLocale.T("composer.offset.earlier.help"),290,70);DanceUi.AttachTooltip(later,DanceLocale.T("composer.offset.later.help"),290,70);
            Section(content,"composer.step.details",null);
            titleInput = CreateTextRow(content, DanceLocale.T("composer.title"), null, null);
            authorInput = CreateTextRow(content, DanceLocale.T("composer.author"), null, null);
            var advanced=DanceUi.Button(content, DanceLocale.T("composer.advanced"), () => { bool show = !advancedRows[0].activeSelf; foreach (var row in advancedRows) row.SetActive(show); });
            DanceUi.AttachTooltip(advanced,DanceLocale.T("composer.advanced.help"),300,82);
            idInput = CreateTextRow(content,DanceLocale.T("composer.id"),null,"dance","composer.id.help");advancedRows.Add(idInput.transform.parent.gameObject);
            faceInput = CreateTextRow(content, DanceLocale.T("composer.face"), "VMD|*.vmd", null,"composer.face.help");
            lipInput = CreateTextRow(content, DanceLocale.T("composer.lip"), "VMD|*.vmd", null,"composer.lip.help");
            foreach(var input in new[]{faceInput,lipInput}){var selected=input;selected.onValueChanged.AddListener(_=>PromoteEmbeddedBody(selected));}
            cameraInput = CreateTextRow(content, DanceLocale.T("composer.camera"), "VMD|*.vmd", null,"composer.camera.help");
            pmxInput = CreateTextRow(content, DanceLocale.T("composer.pmx"), "PMX|*.pmx", null,"composer.pmx.help");
            foreach (var field in new[] { faceInput, lipInput, cameraInput, pmxInput }) advancedRows.Add(field.transform.parent.gameObject);
            var cameraReference=CreateRow(content,36);advancedRows.Add(cameraReference);cameraReference.name="CameraReference";
            CreateFixedLabel(cameraReference.transform,DanceLocale.T("composer.camera.eye"),116);
            cameraEyeInput=CreateInput(cameraReference.transform,"1.650",76);cameraEyeInput.gameObject.name="CameraReferenceEye";cameraEyeInput.readOnly=true;
            var capture=DanceUi.Button(cameraReference.transform,DanceLocale.T("composer.camera.capture"),CaptureCameraReference,120);capture.name="CaptureCameraReference";
            var referenceHelp=DanceUi.Button(cameraReference.transform,"?",()=>{},24);DanceUi.AttachTooltip(referenceHelp,DanceLocale.T("composer.camera.reference.help"),320,130);
            var cameraNumbers=CreateRow(content,36);advancedRows.Add(cameraNumbers);cameraNumbers.name="CameraPackageScale";
            CreateFixedLabel(cameraNumbers.transform,DanceLocale.T("composer.camera.authorScale"),116);
            cameraAuthorScaleSlider=DanceUi.Slider(cameraNumbers.transform,.01f,4f,1f,OnPackageCameraScaleChanged);cameraAuthorScaleSlider.name="CameraAuthoringScale";
            cameraAuthorScaleLabel=DanceUi.Label(cameraNumbers.transform,"1.000",13,56);
            DanceUi.Button(cameraNumbers.transform,"↺",()=>cameraAuthorScaleSlider.value=1f,28);

            var numbers = CreateRow(content, 36); advancedRows.Add(numbers);
            CreateFixedLabel(numbers.transform, DanceLocale.T("composer.scale"), 92); positionScaleInput = CreateInput(numbers.transform, "0.08", 72);
            var scaleHelp=DanceUi.Button(numbers.transform,"?",()=>{},26);DanceUi.AttachTooltip(scaleHelp,DanceLocale.T("composer.scale.help"),290,82);
            loopToggle = CreateToggle(numbers.transform, DanceLocale.T("composer.loop"));
            var footIk = CreateRow(content, 36); advancedRows.Add(footIk);
            CreateFixedLabel(footIk.transform, DanceLocale.T("composer.footIk"), 80);
            footIkButtons = new[] {
                DanceUi.Button(footIk.transform,DanceLocale.T("composer.footIk.default"),()=>SetPackageFootIk(null),82),
                DanceUi.Button(footIk.transform,DanceLocale.T("composer.footIk.on"),()=>SetPackageFootIk(true),86),
                DanceUi.Button(footIk.transform,DanceLocale.T("composer.footIk.off"),()=>SetPackageFootIk(false),82)
            };
            foreach(var button in footIkButtons)DanceUi.AttachTooltip(button,DanceLocale.T("composer.footIk.help"),300,96);
            SetPackageFootIk(null);
            var layers = CreateRow(content, 38); advancedRows.Add(layers);
            layersText = CreateText(layers.transform, "", 13, TextAnchor.MiddleLeft); layersText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var layerAdd=CreateButton(layers.transform, DanceLocale.T("composer.layers.add"), DanceUi.Surface, AddLayers);DanceUi.AttachTooltip(layerAdd,DanceLocale.T("composer.layers.help"),300,96);
            CreateButton(layers.transform, DanceLocale.T("composer.clear"), DanceUi.Surface, ClearLayers); UpdateLayersText();
            var credits = DanceUi.Node("CreditsSection", content); advancedRows.Add(credits);
            var creditsLayout=credits.AddComponent<VerticalLayoutGroup>();creditsLayout.spacing=6;
            creditsLayout.childControlWidth=creditsLayout.childControlHeight=true;creditsLayout.childForceExpandHeight=false;
            var creditsHeader=CreateRow(credits.transform,26);
            var creditsLabel=CreateText(creditsHeader.transform,DanceLocale.T("composer.credits"),13,TextAnchor.MiddleLeft);
            creditsLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            var creditsHelp=DanceUi.Button(creditsHeader.transform,"?",()=>{},26);
            DanceUi.AttachTooltip(creditsHelp,DanceLocale.T("composer.credits.help"),300,96);
            creditsInput=CreateInput(credits.transform,string.Empty,0);
            creditsInput.name="CreditsInput";creditsInput.lineType=InputField.LineType.MultiLineNewline;
            creditsInput.textComponent.alignment=TextAnchor.UpperLeft;
            creditsInput.textComponent.horizontalOverflow=HorizontalWrapMode.Wrap;
            creditsInput.textComponent.supportRichText=false;
            var creditsSize=creditsInput.GetComponent<LayoutElement>();creditsSize.minHeight=creditsSize.preferredHeight=112;creditsSize.flexibleWidth=1;
            var creditsHint=CreateText(creditsInput.transform,DanceLocale.T("composer.credits.placeholder"),12,TextAnchor.UpperLeft);
            creditsHint.color=new Color(.53f,.58f,.70f);creditsHint.supportRichText=false;
            DanceUi.Stretch(creditsHint.rectTransform);creditsHint.rectTransform.offsetMin=new Vector2(8,2);creditsHint.rectTransform.offsetMax=new Vector2(-8,-2);
            creditsInput.placeholder=creditsHint;
            foreach (var row in advancedRows) row.SetActive(false);
            statusRow=DanceUi.Row(panel.transform,24);statusRow.name="ComposerStatus";
            var statusClip=DanceUi.Node("StatusClip",statusRow.transform);DanceUi.Size(statusClip,-1,24,1);statusClip.AddComponent<RectMask2D>();
            statusText=DanceUi.Label(statusClip.transform,DanceLocale.T("composer.ready"),11);DanceUi.Stretch(statusText.rectTransform);statusText.horizontalOverflow=HorizontalWrapMode.Overflow;
            var info=DanceUi.Button(statusRow.transform,"?",()=>{},24);DanceUi.AttachTooltip(info,statusText.text,320,140);statusDetails=info.GetComponent<DanceTooltip>().Hint.GetComponentInChildren<Text>();info.GetComponent<LayoutElement>().minHeight=info.GetComponent<LayoutElement>().preferredHeight=24;statusRow.SetActive(false);
            var actions = DanceUi.Row(panel.transform,30);actions.name="ComposerActions";
            DanceUi.Button(actions.transform, DanceLocale.T("composer.validate"), ValidateDraft,72);
            var preview=DanceUi.Button(actions.transform, DanceLocale.T("composer.preview"), BuildAndPreview,128,true);DanceUi.AttachTooltip(preview,DanceLocale.T("composer.preview.help"),300,82);
            var export=DanceUi.Button(actions.transform, DanceLocale.T("composer.export"), ExportPackage,110);DanceUi.AttachTooltip(export,DanceLocale.T("composer.export.help"),300,82);
            foreach(Transform child in actions.transform){var element=child.GetComponent<LayoutElement>();if(element!=null){element.minHeight=element.preferredHeight=30;var label=child.GetComponentInChildren<Text>();if(label!=null)label.fontSize=13;}}
            motionInput.onValueChanged.AddListener(_=>PreviewResourceId=null);
            audioInput.onValueChanged.AddListener(_=>{PreviewResourceId=null;syncSection.SetActive(!string.IsNullOrWhiteSpace(audioInput.text));});
            syncSection.SetActive(false);
            CaptureCameraReference();
        }

        private void Section(Transform parent,string title,string description)
        {
            var header=DanceUi.Label(parent,DanceLocale.T(title),13);header.fontStyle=FontStyle.Bold;DanceUi.Size(header.gameObject,-1,25);
            if(description==null)return;
            var help=DanceUi.Label(parent,DanceLocale.T(description),12);help.color=new Color(0.66f,0.69f,0.78f);DanceUi.Size(help.gameObject,-1,46);
        }

        private bool IsOwnPreviewReady() { return !string.IsNullOrEmpty(PreviewResourceId)&&owner!=null&&owner.playerCore.IsPlaying&&!owner.playerCore.IsLoading&&owner.playerCore.CurrentResourceId==PreviewResourceId; }
        private void Update()
        {
            if(statusRow!=null){statusRow.SetActive(statusText.text!=DanceLocale.T("composer.ready"));if(statusDetails!=null)statusDetails.text=statusText.text;}
            if(previewPauseButton==null)return;
            bool ready=IsOwnPreviewReady();previewPauseButton.interactable=previewStopButton.interactable=ready;
            previewPauseButton.GetComponentInChildren<Text>().text=ready&&!owner.playerCore.Paused?"Ⅱ":"▶";
        }

        private void ChangeAudioOffset(float delta,bool reset=false)
        {
            float current=0;
            if(!reset&&(!float.TryParse(audioOffsetInput.text,NumberStyles.Float,CultureInfo.InvariantCulture,out current)||float.IsNaN(current)||float.IsInfinity(current))){SetError(new InvalidDataException(DanceLocale.T("composer.number")));return;}
            current=reset?0f:current+delta;
            audioOffsetInput.text=Mathf.Clamp(current,-3600,3600).ToString("0.###",CultureInfo.InvariantCulture);ApplyAudioOffset();
        }

        private void ApplyAudioOffset()
        {
            float value;
            if(!float.TryParse(audioOffsetInput.text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||Mathf.Abs(value)>3600f){SetError(new InvalidDataException(DanceLocale.T("composer.number")));return;}
            bool applied=!string.IsNullOrEmpty(PreviewResourceId)&&owner.playerCore.AdjustPreviewAudioOffset(PreviewResourceId,value);
            statusText.text=DanceLocale.T(applied?"composer.offset.live":"composer.offset.saved",value);
        }

        private void UpdateMotionSummary()
        {
            if(motionSummary==null)return;
            motionSummary.gameObject.SetActive(File.Exists(motionInput.text));
            if(!motionSummary.gameObject.activeSelf)return;
            var info=VmdInspector.Inspect(motionInput.text);
            if(!info.IsValid||info.BoneKeys==0){motionSummary.text=DanceLocale.T("composer.error.body",DanceLocale.T("composer.motion"));return;}
            var kinds=new List<string>{DanceLocale.T("composer.track.body")};
            if(info.HasExpressionMorph)kinds.Add(DanceLocale.T("composer.track.expression"));
            if(info.HasLipMorph)kinds.Add(DanceLocale.T("composer.track.lip"));
            if(info.MorphKeys>0 && !info.HasExpressionMorph && !info.HasLipMorph)kinds.Add(DanceLocale.T("composer.track.morph"));
            if(info.CameraKeys>0)kinds.Add(DanceLocale.T("composer.track.camera"));
            if(info.HasLegIk||info.IkFrames>0)kinds.Add("IK");
            motionSummary.text=DanceLocale.T("composer.motion.summary",string.Join(" · ",kinds.ToArray()));
        }

        private InputField CreateTextRow(Transform parent, string label, string filter, string defaultValue,string helpKey=null)
        {
            GameObject row = CreateRow(parent, 36f);
            CreateFixedLabel(row.transform, label, 92f);
            InputField input = CreateInput(row.transform, defaultValue ?? string.Empty, 0f);
            input.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            if (!string.IsNullOrEmpty(filter))
            {
                var browse=DanceUi.Button(row.transform, DanceLocale.T("composer.browse"), delegate
                {
                    try { SelectFile(filter, input); }
                    catch (Exception exception) { SetError(exception); }
                },80);browse.GetComponent<LayoutElement>().minWidth=80;
            }
            if(helpKey!=null){var help=DanceUi.Button(row.transform,"?",()=>{},24);help.GetComponent<LayoutElement>().minWidth=24;DanceUi.AttachTooltip(help,DanceLocale.T(helpKey),300,108);}
            return input;
        }

        private void AutoFillFolder()
        {
            try { DanceDialogs.Folder(DanceLocale.T("composer.folder"), selected => {
                if(string.IsNullOrEmpty(selected) || this==null)return;
                var draft=VmdWorkspaceScanner.SuggestFolder(selected);
                if(!string.IsNullOrEmpty(EditingPackagePath)){draft.OriginalPackageRoot=originalPackageRoot;draft.OriginalManifestJson=originalManifestJson;}
                ApplyDraft(draft);
                statusText.text=DanceLocale.T("composer.filled",draft.Title)+"\n"+DanceLocale.T("composer.auto.review");
            },SetError); } catch(Exception error){SetError(error);}
        }

        private void ApplyDraft(VmdDanceDraft draft)
        {
            applyingDraft=true;
            preserveSlotTracks=draft.PreserveSlotTracks;
            originalPackageRoot=draft.OriginalPackageRoot;originalManifestJson=draft.OriginalManifestJson;
            originalFace=draft.OriginalFaceVmd;originalLip=draft.OriginalLipVmd;originalCamera=draft.OriginalCameraVmd;
            idInput.text = draft.Id ?? string.Empty;
            titleInput.text = draft.Title ?? string.Empty;
            authorInput.text = draft.Author ?? string.Empty;
            creditsInput.text = draft.Credits ?? string.Empty;
            motionInput.text = draft.MotionVmd ?? string.Empty;
            faceInput.text = draft.FaceVmd ?? string.Empty;
            lipInput.text = draft.LipVmd ?? string.Empty;
            cameraInput.text = draft.CameraVmd ?? string.Empty;
            audioInput.text = draft.AudioFile ?? string.Empty;
            pmxInput.text = draft.ReferencePmx ?? string.Empty;
            audioOffsetInput.text = draft.AudioOffsetSeconds.ToString("G9", CultureInfo.InvariantCulture);
            float referenceEye=draft.CameraReferenceEyeHeight ?? (draft.CameraReferenceBodyHeight.HasValue?draft.CameraReferenceBodyHeight.Value*1.6f/1.65f:CurrentReferenceEyeHeight());
            cameraEyeInput.text=referenceEye.ToString("G9",CultureInfo.InvariantCulture);
            float packageScale=draft.CameraAuthoringScale??1f;
            cameraAuthorScaleSlider.maxValue=Mathf.Max(4f,packageScale);
            cameraAuthorScaleSlider.SetValueWithoutNotify(packageScale);cameraAuthorScaleLabel.text=packageScale.ToString("0.000",CultureInfo.InvariantCulture);
            positionScaleInput.text = draft.PositionScale.ToString("G9", CultureInfo.InvariantCulture);
            loopToggle.isOn = draft.Loop;
            SetPackageFootIk(draft.FootIk);
            additionalLayers.Clear();
            additionalLayers.AddRange(draft.AdditionalVmdFiles ?? new List<string>());
            UpdateLayersText();
            UpdateMotionSummary();
            statusText.text=DanceLocale.T("composer.ready");applyingDraft=false;
        }

        private static float? OptionalCameraNumber(InputField input)
        {
            if(string.IsNullOrWhiteSpace(input.text))return null;
            float value;if(!float.TryParse(input.text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||value<=0)throw new InvalidDataException(DanceLocale.T("composer.number"));
            return value;
        }
        private float CurrentReferenceEyeHeight()
        {
            float eye=owner.avatarHelper.MeasureAvatarEyeHeight();
            if(eye>0)return eye;
            float body=owner.avatarHelper.MeasureAvatarHeight();
            return body>0?body*1.6f/1.65f:DancePlayerCore.CameraReferenceEyeHeight;
        }
        private void CaptureCameraReference()
        {
            cameraEyeInput.text=CurrentReferenceEyeHeight().ToString("G9",CultureInfo.InvariantCulture);
            ApplyPreviewCameraReference();
        }
        private void OnPackageCameraScaleChanged(float value)
        {
            cameraAuthorScaleLabel.text=value.ToString("0.000",CultureInfo.InvariantCulture);
            if(!applyingDraft)ApplyPreviewCameraReference();
        }
        private void ApplyPreviewCameraReference()
        {
            if(IsOwnPreviewReady())owner.playerCore.SetAuthoringPreviewCameraReference(PreviewResourceId,OptionalCameraNumber(cameraEyeInput).Value,cameraAuthorScaleSlider.value);
        }
        private void PromoteEmbeddedBody(InputField input)
        {
            if(applyingDraft || !string.IsNullOrWhiteSpace(motionInput.text) || !File.Exists(input.text))return;
            var track=VmdInspector.Inspect(input.text);
            if(!track.IsValid || track.BoneKeys==0)return;
            motionInput.text=input.text;UpdateMotionSummary();
            statusText.text=DanceLocale.T("composer.embeddedBody");
        }
        private void AddLayers()
        {
            try { DanceDialogs.Open(DanceLocale.T("composer.layers.add"),new[]{"vmd"},paths=>{
                if(this==null)return;
                    foreach(var path in paths)if(!additionalLayers.Contains(path,StringComparer.OrdinalIgnoreCase))additionalLayers.Add(path);
                UpdateLayersText();
            },SetError,true); } catch(Exception error){SetError(error);}
        }

        private void ClearLayers() { additionalLayers.Clear(); UpdateLayersText(); }

        private void UpdateLayersText()
        {
            layersText.text = additionalLayers.Count == 0 ? DanceLocale.T("composer.layers.none")
                : DanceLocale.T("composer.layers.count", additionalLayers.Count) + " "
                    + string.Join(", ", additionalLayers.Select(Path.GetFileName).ToArray());
        }

        private void ValidateDraft()
        {
            try
            {
                VmdDanceDraft draft = ReadDraft();
                List<string> errors = VmdDancePackageBuilder.Validate(draft);
                statusText.text = errors.Count == 0
                    ? DanceLocale.T("composer.valid")+" · "+DanceLocale.T("composer.valid.next")
                    : DanceLocale.T("composer.invalid") + "\n" + string.Join("\n", errors.Select(FriendlyValidation).ToArray());
            }
            catch (Exception exception) { SetError(exception); }
        }

        private void BuildAndPreview()
        {
            try
            {
                VmdDanceDraft draft = ReadDraft();
                var errors=VmdDancePackageBuilder.Validate(draft);
                if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors.Select(FriendlyValidation).ToArray()));
                string directory = Path.Combine(owner.resourceManager.LibraryFolder, "VmdComposer");
                string output = Path.Combine(directory, VmdWorkspaceScanner.Slugify(draft.Id) + ".vmdance");
                for (int suffix = 2; File.Exists(output); suffix++) output = Path.Combine(directory, VmdWorkspaceScanner.Slugify(draft.Id) + "-" + suffix + ".vmdance");
                VmdDanceBuildResult result = VmdDancePackageBuilder.Build(draft, output);
                bool started = owner.RefreshAndPlayPackage(result.OutputPath);
                PreviewResourceId=started?owner.playerCore.CurrentResourceId:null;
                statusText.text = (started ? DanceLocale.T("composer.preview.started") + "\n" : DanceLocale.T("composer.preview.failed") + "\n")
                    + result.OutputPath;
                if(started){statusText.text=DanceLocale.T("composer.preview.loading");StartCoroutine(WatchPreview(PreviewResourceId));}
            }
            catch (Exception exception) { SetError(exception); }
        }

        private void ExportPackage()
        {
            try {
                var draft=ReadDraft();var errors=VmdDancePackageBuilder.Validate(draft);
                if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors.Select(FriendlyValidation).ToArray()));
                DanceDialogs.Save(DanceLocale.T("composer.export"),VmdWorkspaceScanner.Slugify(draft.Id)+".vmdance",selected=>{
                    if(string.IsNullOrEmpty(selected)||this==null)return;
                    var result=VmdDancePackageBuilder.Build(draft,selected);statusText.text=DanceLocale.T("composer.exported")+"\n"+result.OutputPath;owner.RefreshDropdown();
                },SetError);
            } catch(Exception error){SetError(error);}
        }

        private IEnumerator WatchPreview(string resourceId)
        {
            while(owner!=null&&owner.playerCore.IsLoading&&PreviewResourceId==resourceId)yield return null;
            if(owner==null||PreviewResourceId!=resourceId)yield break;
            statusText.text=owner.playerCore.IsPlaying&&owner.playerCore.CurrentResourceId==resourceId
                ?DanceLocale.T("composer.preview.started")+"\n"+DanceLocale.T("composer.offset.live",audioOffsetInput.text)
                :DanceLocale.T("composer.preview.failed")+"\n"+owner.playerCore.LastError;
            ApplyPreviewCameraReference();
            float offset;
            if(float.TryParse(audioOffsetInput.text,NumberStyles.Float,CultureInfo.InvariantCulture,out offset))owner.playerCore.AdjustPreviewAudioOffset(resourceId,offset);
        }

        private VmdDanceDraft ReadDraft()
        {
            float offset;
            float scale;
            if (!float.TryParse(audioOffsetInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out offset) || float.IsNaN(offset) || float.IsInfinity(offset)) throw new InvalidDataException(DanceLocale.T("composer.number"));
            if (!float.TryParse(positionScaleInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale)) throw new InvalidDataException(DanceLocale.T("composer.number"));
            return new VmdDanceDraft
            {
                Id = idInput.text.Trim(),
                Title = titleInput.text.Trim(),
                Author = authorInput.text.Trim(),
                Credits = creditsInput.text,
                MotionVmd = EmptyToNull(motionInput.text),
                FaceVmd = EmptyToNull(faceInput.text),
                LipVmd = EmptyToNull(lipInput.text),
                CameraVmd = EmptyToNull(cameraInput.text),
                AudioFile = EmptyToNull(audioInput.text),
                ReferencePmx = EmptyToNull(pmxInput.text),
                AdditionalVmdFiles = new List<string>(additionalLayers),
                AudioOffsetSeconds = offset,
                CameraReferenceEyeHeight=OptionalCameraNumber(cameraEyeInput),
                CameraReferenceBodyHeight=null,
                CameraAuthoringScale=cameraAuthorScaleSlider.value,
                PositionScale = scale,
                Loop = loopToggle.isOn,
                FootIk = packageFootIk,
                PreserveSlotTracks=preserveSlotTracks,
                OriginalPackageRoot=originalPackageRoot,OriginalManifestJson=originalManifestJson,
                OriginalFaceVmd=originalFace,OriginalLipVmd=originalLip,OriginalCameraVmd=originalCamera
            };
        }

        private void SetPackageFootIk(bool? value)
        {
            packageFootIk=value;
            if(footIkButtons==null)return;
            for(int index=0;index<footIkButtons.Length;index++)
            {
                bool selected=index==0&&!value.HasValue||index==1&&value==true||index==2&&value==false;
                footIkButtons[index].GetComponent<Image>().color=selected?DanceUi.Accent:DanceUi.Surface;
            }
        }

        private void ToggleVisible() { panel.SetActive(!panel.activeSelf); }
        private void SetError(Exception exception) { statusText.text = DanceLocale.T("error.detail", exception.Message); Debug.LogError("[CustomDancePlayer] VMD composer: " + exception); }

        private static string FriendlyValidation(string error)
        {
            var labels=new Dictionary<string,string>{{"Body motion","composer.motion"},{"Face VMD","composer.face"},{"Lip VMD","composer.lip"},{"Camera VMD","composer.camera"},{"Audio","composer.audio"},{"Reference PMX","composer.pmx"},{"Additional VMD","composer.layers.add"}};
            foreach(var pair in labels)
            {
                if(!error.StartsWith(pair.Key,StringComparison.Ordinal))continue;
                string label=DanceLocale.T(pair.Value);
                if(error.EndsWith(": file has no compatible track.",StringComparison.Ordinal))return DanceLocale.T(pair.Key=="Body motion"?"composer.error.body":"composer.error.track",label);
                if(error.EndsWith(" is required.",StringComparison.Ordinal))return DanceLocale.T("composer.error.required",label);
                string marker=" was not found: ";int missing=error.IndexOf(marker,StringComparison.Ordinal);if(missing>=0)return DanceLocale.T("composer.error.missing",label,Path.GetFileName(error.Substring(missing+marker.Length)));
                if(error.Contains("unsupported extension"))return DanceLocale.T("composer.error.extension",label);
                if(error.Contains("invalid VMD"))return DanceLocale.T("composer.error.invalid",label);
                return label+error.Substring(pair.Key.Length);
            }
            if(error=="Package id is required.")return DanceLocale.T("composer.error.required",DanceLocale.T("composer.id"));
            if(error.StartsWith("Audio offset",StringComparison.Ordinal)||error.StartsWith("Position scale",StringComparison.Ordinal))return DanceLocale.T("composer.number");
            return error;
        }

        private void SelectFile(string filter, InputField input)
        {
            var extensions=filter.StartsWith("Audio",StringComparison.Ordinal)?new[]{"ogg","wav","mp3"}:filter.StartsWith("PMX",StringComparison.Ordinal)?new[]{"pmx"}:new[]{"vmd"};
            DanceDialogs.Open(DanceLocale.T("composer.browse"),extensions,paths=>{
                if(paths.Length==0||input==null)return;
                if(input==motionInput)LoadMotion(paths[0]);else input.text=paths[0];
            },SetError,false,input.text);
        }

        private static string EmptyToNull(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

        private GameObject CreateRow(Transform parent, float height)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            DanceUi.Size(row, -1, height);
            return row;
        }

        private void CreateFixedLabel(Transform parent, string text, float width)
        {
            Text label = CreateText(parent, text, 12, TextAnchor.MiddleLeft);
            var layout=label.gameObject.AddComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=width;
        }

        private InputField CreateInput(Transform parent, string value, float width)
        {
            GameObject root = new GameObject("Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = DanceUi.Surface;
            LayoutElement size = root.GetComponent<LayoutElement>();
            size.minWidth = width > 0f ? width : 80f;
            size.preferredWidth = width > 0f ? width : 120f;
            Text text = CreateText(root.transform, value, 12, TextAnchor.MiddleLeft);
            text.horizontalOverflow=HorizontalWrapMode.Overflow;
            root.AddComponent<RectMask2D>();
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);
            InputField input = root.GetComponent<InputField>();
            input.lineType=InputField.LineType.SingleLine;
            input.textComponent = text;
            input.text = value;
            return input;
        }

        private Toggle CreateToggle(Transform parent, string label)
        {
            GameObject root = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredWidth = 120f;
            GameObject box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(root.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f); boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.sizeDelta = new Vector2(20f, 20f); boxRect.anchoredPosition = new Vector2(12f, 0f);
            box.GetComponent<Image>().color = new Color(0.25f, 0.31f, 0.43f, 1f);
            GameObject mark = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            mark.transform.SetParent(box.transform, false);
            RectTransform markRect = mark.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0.2f, 0.2f); markRect.anchorMax = new Vector2(0.8f, 0.8f);
            markRect.offsetMin = Vector2.zero; markRect.offsetMax = Vector2.zero;
            mark.GetComponent<Image>().color = new Color(0.28f, 0.82f, 0.62f, 1f);
            Text text = CreateText(root.transform, label, 12, TextAnchor.MiddleLeft);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 0f); textRect.offsetMax = Vector2.zero;
            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = mark.GetComponent<Image>();
            return toggle;
        }

        private Button CreateButton(Transform parent, string label, Color color, Action action)
        {
            GameObject root = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = color;
            LayoutElement size = root.GetComponent<LayoutElement>();
            size.minWidth = Mathf.Max(72f, label.Length * 9f + 20f);
            size.preferredHeight = 34f;
            Text text = CreateText(root.transform, label, 12, TextAnchor.MiddleCenter);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero;
            Button button = root.GetComponent<Button>();
            button.onClick.AddListener(delegate { action(); });
            return button;
        }

        private Text CreateText(Transform parent, string value, int size, TextAnchor anchor)
        {
            GameObject root = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            Text text = root.GetComponent<Text>();
            text.font = font;
            text.fontSize = Math.Max(11, size);
            text.raycastTarget = false;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static Font LoadFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        }
    }

}
