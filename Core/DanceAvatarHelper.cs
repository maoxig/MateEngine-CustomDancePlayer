using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace CustomDancePlayer
{
    // Manages avatar-related components and audio setup
    public class DanceAvatarHelper : MonoBehaviour
    {
        private const string MODEL_PARENT_NAME = "Model"; private const string CUSTOM_DANCE_AUDIO_NAME = "CustomDanceAudio"; private const string BODY_NAME = "Body";
        private static readonly string[] MMDBlendshapeKeywords = { "まばたき", "あ", "い", "う", "え", "お" };

        public Mesh DummyBlendshapeMesh;
        public RuntimeAnimatorController CustomDanceAvatarController;
        public DancePlayerCore playerCore;

        public Camera RenderCamera;

        public GameObject CurrentAvatar { get; private set; }
        public Animator CurrentAnimator { get; private set; }

        public Transform CurrentAvatarHips { get; private set; }
        public AudioSource CurrentAudioSource { get; private set; }
        public SkinnedMeshRenderer TargetSMR { get; private set; }
        public AnimatorOverrideController CurrentOverrideController { get; private set; }

        public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

        private GameObject _modelParent;
        private DanceSettingsHandler _settingsHandler;
        private Transform _originalBodyTransform;
        private Transform _dummyBodyTransform;
        private string _oldBodyName;
        private Transform smrParent;
        private string smrName;
        private Vector3 smrPosition,smrScale;
        private Quaternion smrRotation;
        private bool smrLeased;
        private float currentAvatarHeight;
        private float currentAvatarEyeHeight;
        private bool eyeHeightMeasured;
        private float currentAvatarGroundHeight;
        private float nextVisibilityCheck;
        public void PrepareBodyForDance()
        {
            if(TargetSMR!=null&&TargetSMR.sharedMesh!=null)
            {
                var bindings=Maoxig.RuntimeVmd.VmdExpressionBindings.Create(CurrentAnimator,Enumerable.Range(0,TargetSMR.sharedMesh.blendShapeCount).Select(TargetSMR.sharedMesh.GetBlendShapeName));
                var expressions=CurrentAvatar.GetComponent<DanceLegacyExpressionDriver>()??CurrentAvatar.AddComponent<DanceLegacyExpressionDriver>();expressions.Bind(TargetSMR,bindings);
            }
            if (TargetSMR==null || smrLeased) return;
            var existing = CurrentAvatar.transform.Find(BODY_NAME);
            if (existing != null && existing != TargetSMR.transform) { _originalBodyTransform = existing; _oldBodyName = existing.name; existing.name = "Body_DanceBackup"; }
            var t=TargetSMR.transform;smrParent=t.parent;smrName=t.name;smrPosition=t.localPosition;smrRotation=t.localRotation;smrScale=t.localScale;
            t.SetParent(CurrentAvatar.transform,true);t.name=BODY_NAME;smrLeased=true;
        }


        void Start()
        {

            _modelParent = GameObject.Find(MODEL_PARENT_NAME);
            SetupAudioSource();

            _settingsHandler = DanceSettingsHandler.Instance;
            CheckAndUpdateCurrentAvatar();
            if(CurrentAvatar!=null&&Time.unscaledTime>=nextVisibilityCheck)
            {
                nextVisibilityCheck=Time.unscaledTime+1f;
                SMRHandler.SetUpdateWhenOffscreen(CurrentAvatar,true);
            }
            if (CurrentAnimator != null)
            {
                DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
            }
            UpdateAudioVolume();
        }

        void Update()
        {
            if (CurrentAudioSource == null) SetupAudioSource();
            CheckAndUpdateCurrentAvatar();
            if(CurrentAvatar!=null&&Time.unscaledTime>=nextVisibilityCheck)
            {
                nextVisibilityCheck=Time.unscaledTime+1f;
                SMRHandler.SetUpdateWhenOffscreen(CurrentAvatar,true);
            }
        }

        void OnDestroy()
        {
            ClearCurrentAvatar();
            CurrentAvatar = null;
            CurrentAvatarHips = null;
            CurrentAnimator = null;
            CurrentAudioSource = null;
        }


        private void SetupAudioSource()
        {
            GameObject soundFX = GameObject.Find("SoundFX");
            if (soundFX == null) return;

            Transform audioTrans = soundFX.transform.Find(CUSTOM_DANCE_AUDIO_NAME);
            GameObject audioObj;
            if (audioTrans != null)
            {
                audioObj = audioTrans.gameObject;
            }
            else
            {
                audioObj = new GameObject(CUSTOM_DANCE_AUDIO_NAME);
                audioObj.transform.SetParent(soundFX.transform, false);
            }

            CurrentAudioSource = audioObj.GetComponent<AudioSource>();
            if (CurrentAudioSource == null)
            {
                CurrentAudioSource = audioObj.AddComponent<AudioSource>();
            }
        }

        // Updates audio volume from settings
        public void UpdateAudioVolume()
        {
            if (CurrentAudioSource != null)
            {
                CurrentAudioSource.volume = DanceSettingsHandler.Instance.data.danceVolume;
            }
        }

        // Checks and updates the active avatar
        private void CheckAndUpdateCurrentAvatar()
        {
            if (_modelParent == null) _modelParent = GameObject.Find(MODEL_PARENT_NAME);
            if (_modelParent == null)
            {
                ClearCurrentAvatar();
                return;
            }
            GameObject newAvatar = null;

            foreach (Transform child in _modelParent.transform)
            {
                if (child.gameObject.activeSelf && child.GetComponent<Animator>() != null)
                {
                    newAvatar = child.gameObject;
                    break;
                }
            }

            if (newAvatar != CurrentAvatar)
            {
                UpdateAvatarComponents(newAvatar);
            }
        }

        // Updates avatar components and notifies core
        private void UpdateAvatarComponents(GameObject newAvatar)
        {
            if (playerCore != null) playerCore.StopPlay();
            ClearCurrentAvatar();

            CurrentAvatar = null; CurrentAnimator = null; CurrentAvatarHips = null; TargetSMR = null;
            currentAvatarHeight = 0f;currentAvatarEyeHeight=0f;eyeHeightMeasured=false;currentAvatarGroundHeight=0f;

            if (newAvatar == null) return;

            CurrentAvatar = newAvatar;
            CurrentAnimator = newAvatar.GetComponentInChildren<Animator>();
            if (CurrentAnimator == null)
            {
                CurrentAvatar = null;
                CurrentAvatarHips = null;
                return;
            }
            Maoxig.RuntimeVmd.VmdRigScaleSnapshot.Capture(CurrentAnimator);
            CurrentAvatarHips = CurrentAnimator.isHuman ? CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
            SetupMMDBlendshapeSMR();
            SetupMMDCameraHierarchy();
            currentAvatarHeight = CalculateAvatarHeight();currentAvatarEyeHeight=CalculateAvatarEyeHeight();eyeHeightMeasured=currentAvatarHeight>0f;
            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
            if (playerCore != null)
            {
                playerCore.StopPlay();
            }
            SMRHandler.SetUpdateWhenOffscreen(CurrentAvatar, true);

            var proxy = CurrentAvatar.GetComponent<DancePlayerAvatarProxy>() ?? CurrentAvatar.AddComponent<DancePlayerAvatarProxy>();
            proxy.playerCore = playerCore;

        }

        // Sets up MMD Camera Hierarchy
        public void SetupMMDCameraHierarchy()
        {
            if (CurrentAvatar == null) return;

            Transform camRootInAvatar = CurrentAvatar.transform.Find("Camera_root");
            if (camRootInAvatar) Destroy(camRootInAvatar.gameObject);


            Transform root = new GameObject("Camera_root").transform;
            root.SetParent(CurrentAvatar.transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(0, 180, 0);

            Transform child1 = new GameObject("Camera_root_1").transform;
            child1.SetParent(root, false);
            child1.localPosition = Vector3.zero;
            child1.localRotation = Quaternion.identity;

            Transform cameraNode = new GameObject("Camera").transform;
            cameraNode.SetParent(child1, false);
            cameraNode.localPosition = Vector3.zero;
            cameraNode.localRotation = Quaternion.identity;

            Camera cameraComponent = cameraNode.gameObject.AddComponent<Camera>();
            cameraComponent.enabled = false; // Animation will enable it if needed
        }

        public void ResetMMDCameraHierarchy()
        {
            if (CurrentAvatar == null) return;

            Transform camRootInAvatar = CurrentAvatar.transform.Find("Camera_root");
            if (camRootInAvatar)
            {
                camRootInAvatar.localPosition = Vector3.zero;
                camRootInAvatar.localRotation = Quaternion.Euler(0, 180, 0);

                Transform cameraRoot1 = camRootInAvatar.Find("Camera_root_1");
                if (cameraRoot1)
                {
                    cameraRoot1.localPosition = Vector3.zero;
                    cameraRoot1.localRotation = Quaternion.identity;

                    Transform cameraNode = cameraRoot1.Find("Camera");
                    if (cameraNode)
                    {
                        cameraNode.localPosition = Vector3.zero;
                        cameraNode.localRotation = Quaternion.identity;
                    }
                }
            }
        }
        public void ClearMMDCameraHierarchy()
        {
            if (CurrentAvatar == null) return;

            Transform camRootInAvatar = CurrentAvatar.transform.Find("Camera_root");
            if (camRootInAvatar) Destroy(camRootInAvatar.gameObject);
        }

        // Sets up SkinnedMeshRenderer for MMD blendshapes
        private void SetupMMDBlendshapeSMR()
        {
            if (CurrentAvatar == null) return;

            SkinnedMeshRenderer[] smrs = CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();
            TargetSMR = smrs.FirstOrDefault(smr => smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                !smr.sharedMesh.GetBlendShapeName(0).ToLower().Contains("dummy") &&
                MMDBlendshapeKeywords.All(keyword => Enumerable.Range(0, smr.sharedMesh.blendShapeCount)
                    .Any(i => smr.sharedMesh.GetBlendShapeName(i) == keyword)));

        }

        // Clears current avatar state
        private void ClearCurrentAvatar()
        {
            if (CurrentAnimator != null && DefaultAnimatorController != null)
            {
                CurrentAnimator.runtimeAnimatorController = DefaultAnimatorController;
                CurrentAnimator.SetBool("isDancing", false);
            }
        }

        // Checks if avatar is available
        public bool IsAvatarAvailable()
        {
            return CurrentAvatar != null && CurrentAnimator != null;
        }

        /// <summary>
        /// Measures intrinsic standing height from immutable Avatar skeleton data.
        /// Display scale, animations and renderer culling bounds are excluded.
        /// The result is cached separately for each selected avatar.
        /// </summary>
        public float MeasureAvatarHeight()
        {
            if (currentAvatarHeight <= 0f && CurrentAvatar != null) currentAvatarHeight = CalculateAvatarHeight();
            if(!eyeHeightMeasured && currentAvatarHeight>0f){currentAvatarEyeHeight=CalculateAvatarEyeHeight();eyeHeightMeasured=true;}
            return currentAvatarHeight;
        }

        public float MeasureAvatarGroundHeight(){MeasureAvatarHeight();return currentAvatarGroundHeight;}
        public float MeasureAvatarEyeHeight() {MeasureAvatarHeight();return currentAvatarEyeHeight;}
        private float CalculateAvatarEyeHeight()
        {
            if(CurrentAnimator==null||CurrentAnimator.avatar==null||!CurrentAnimator.isHuman)return 0f;
            var skeleton=new Dictionary<string,SkeletonBone>(StringComparer.Ordinal);
            foreach(var rest in CurrentAnimator.avatar.humanDescription.skeleton)if(!string.IsNullOrEmpty(rest.name)&&!skeleton.ContainsKey(rest.name))skeleton.Add(rest.name,rest);
            float sum=0f;int count=0;
            foreach(var role in new[]{HumanBodyBones.LeftEye,HumanBodyBones.RightEye}) {
                var bone=CurrentAnimator.GetBoneTransform(role);Vector3 position;
                if(bone!=null&&TryRestPosition(bone,skeleton,out position)){sum+=position.y;count++;}
            }
            float height=count>0?sum/count-currentAvatarGroundHeight:0f;
            return IsFinite(height)&&height>.1f?height:0f;
        }

        private float CalculateAvatarHeight()
        {
            if(CurrentAvatar==null||CurrentAnimator==null||!CurrentAnimator.isHuman||CurrentAnimator.avatar==null) return 0f;
            var skeleton=new Dictionary<string,SkeletonBone>(StringComparer.Ordinal);
            foreach(var rest in CurrentAnimator.avatar.humanDescription.skeleton)
                if(!string.IsNullOrEmpty(rest.name)&&!skeleton.ContainsKey(rest.name))skeleton.Add(rest.name,rest);
            var head=CurrentAnimator.GetBoneTransform(HumanBodyBones.Head);
            var left=CurrentAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right=CurrentAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 h,l,r;
            if(head==null||(left==null&&right==null)||!TryRestPosition(head,skeleton,out h)||
                !TryRestPosition(left??right,skeleton,out l)||!TryRestPosition(right??left,skeleton,out r))return 0f;
            float height=Mathf.Abs(h.y-(l.y+r.y)*0.5f);
            if(!IsFinite(height)||height<0.2f)return 0f;
            float min=float.PositiveInfinity,max=float.NegativeInfinity,faceTop=float.NegativeInfinity;
            var faceBindings=Maoxig.RuntimeVmd.VmdExpressionBindings.Create(CurrentAnimator,new[]{"あ","い","う","え","お","まばたき"});
            foreach(var renderer in CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if(!renderer.enabled||renderer.sharedMesh==null)continue;
                var mesh=renderer.sharedMesh;var bones=renderer.bones;var binds=mesh.bindposes;
                Matrix4x4 meshRest=Matrix4x4.identity;bool found=false;
                for(int i=0;i<Math.Min(bones.Length,binds.Length);i++)
                {
                    Matrix4x4 boneRest;
                    if(bones[i]!=null&&TryRestMatrix(bones[i],skeleton,out boneRest)){meshRest=boneRest*binds[i];found=true;break;}
                }
                if(!found)continue;
                var bounds=mesh.bounds;float meshMin=float.PositiveInfinity,meshMax=float.NegativeInfinity;
                for(int corner=0;corner<8;corner++)
                {
                    var p=meshRest.MultiplyPoint3x4(new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z));
                    meshMin=Mathf.Min(meshMin,p.y);meshMax=Mathf.Max(meshMax,p.y);
                }
                // Reject auxiliary or deliberately inflated culling extents.
                if(!IsFinite(meshMin)||!IsFinite(meshMax)||meshMax>h.y+height*0.5f||meshMin<Mathf.Min(l.y,r.y)-height*0.25f)continue;
                min=Mathf.Min(min,meshMin);max=Mathf.Max(max,meshMax);
                // The renderer actually bound to mouth/blink expressions gives
                // the anatomical crown, rather than hats and tall hair meshes.
                if(faceBindings.HasRenderer(renderer) && meshMax>h.y && meshMax-h.y<height*.5f)
                    faceTop=Mathf.Max(faceTop,meshMax);
            }
            currentAvatarGroundHeight=IsFinite(min)?min:Mathf.Min(l.y,r.y);
            if(IsFinite(faceTop))max=faceTop;
            float meshHeight=max-min;
            return IsFinite(meshHeight)&&meshHeight>=height&&meshHeight<=height*1.5f?meshHeight:height*1.12f;
        }

        private bool TryRestPosition(Transform bone,Dictionary<string,SkeletonBone> skeleton,out Vector3 position)
        {
            Matrix4x4 matrix;
            if(!TryRestMatrix(bone,skeleton,out matrix)){position=Vector3.zero;return false;}
            position=matrix.MultiplyPoint3x4(Vector3.zero);
            return IsFinite(position.x)&&IsFinite(position.y)&&IsFinite(position.z);
        }

        private bool TryRestMatrix(Transform bone,Dictionary<string,SkeletonBone> skeleton,out Matrix4x4 matrix)
        {
            matrix=Matrix4x4.identity;var chain=new Stack<Transform>();
            for(var node=bone;node!=null&&node!=CurrentAnimator.transform;node=node.parent)chain.Push(node);
            while(chain.Count>0)
            {
                var node=chain.Pop();SkeletonBone rest;
                // Current animated transforms cannot serve as a rest-pose fallback.
                if(!skeleton.TryGetValue(node.name,out rest))return false;
                matrix=matrix*Matrix4x4.TRS(rest.position,rest.rotation,Maoxig.RuntimeVmd.VmdRigScaleSnapshot.Capture(CurrentAnimator).ScaleFor(node,rest.scale));
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// Resolves the real active host camera for native VMD camera tracks.
        /// The serialized RenderCamera reference is absent in some installed
        /// MateEngine mod bundles, while Camera.main remains the authoritative
        /// camera used by the desktop renderer.
        /// </summary>
        public Camera ResolveRuntimeVmdCamera()
        {
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled) return main;
            if (RenderCamera != null && RenderCamera.isActiveAndEnabled) return RenderCamera;

            DanceCameraSync sync = FindFirstObjectByType<DanceCameraSync>();
            if (sync != null && sync.RenderCamera != null && sync.RenderCamera.isActiveAndEnabled)
                return sync.RenderCamera;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate != null && candidate.isActiveAndEnabled) return candidate;
            }
            return null;
        }

        // Sets up dummy mesh for dance if needed
        public void SetupDummyForDance()
        {
            if (TargetSMR != null) return;
            var bindings=Maoxig.RuntimeVmd.VmdExpressionBindings.Create(CurrentAnimator,Enumerable.Range(0,DummyBlendshapeMesh.blendShapeCount).Select(DummyBlendshapeMesh.GetBlendShapeName));

            Transform existingBody = CurrentAvatar.transform.Find(BODY_NAME);
            if (existingBody != null)
            {
                SkinnedMeshRenderer smr = existingBody.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                    Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Any(i => smr.sharedMesh.GetBlendShapeName(i).ToLower().Contains("dummy")))
                {
                    return;
                }
                _originalBodyTransform = existingBody;
                _oldBodyName = BODY_NAME + $"_Old_{UnityEngine.Random.Range(0, 10000)}";
                existingBody.name = _oldBodyName;
            }

            GameObject dummyObj = new GameObject(BODY_NAME);
            dummyObj.transform.SetParent(CurrentAvatar.transform, false);
            dummyObj.transform.localScale = Vector3.zero;
            var dummySmr = dummyObj.AddComponent<SkinnedMeshRenderer>();
            dummySmr.sharedMesh = DummyBlendshapeMesh;
            dummySmr.updateWhenOffscreen = true;
            _dummyBodyTransform = dummyObj.transform;

            if (!CurrentAvatar.TryGetComponent<DummyToUniversalSync>(out var dummySync))
            {
                dummySync = CurrentAvatar.AddComponent<DummyToUniversalSync>();
            }
            dummySync.dummySmr = dummySmr;
            dummySync.enabled = true;
            var expressions=CurrentAvatar.GetComponent<DanceLegacyExpressionDriver>()??CurrentAvatar.AddComponent<DanceLegacyExpressionDriver>();expressions.Bind(dummySmr,bindings);
        }

        // Restores original body if dummy was used
        public void RestoreOriginalBody()
        {
            if (CurrentAvatar == null) return;
            var expressions=CurrentAvatar.GetComponent<DanceLegacyExpressionDriver>();if(expressions!=null)expressions.enabled=false;
            if (TargetSMR != null) { if(smrLeased){var t=TargetSMR.transform;t.SetParent(smrParent,false);t.name=smrName;t.localPosition=smrPosition;t.localRotation=smrRotation;t.localScale=smrScale;smrLeased=false;} if(_originalBodyTransform!=null){_originalBodyTransform.name=_oldBodyName ?? BODY_NAME;_originalBodyTransform=null;_oldBodyName=null;} return; }

            if (_dummyBodyTransform != null)
            {
                _dummyBodyTransform.name = "Body_DanceRetired";
                _dummyBodyTransform.gameObject.SetActive(false); // Destroy is deferred; don't let the next clip bind to this node.
                Destroy(_dummyBodyTransform.gameObject);
                _dummyBodyTransform = null;
            }

            if (_originalBodyTransform != null)
            {
                _originalBodyTransform.name = BODY_NAME;
                _originalBodyTransform = null;
                _oldBodyName = null;
            }

            if (CurrentAvatar.TryGetComponent<DummyToUniversalSync>(out var sync))
            {
                sync.enabled = false;
            }
        }

        // Sets up animation override controller
        public void SetupAnimation(AnimationClip clip)
        {
            if (CurrentAnimator == null || clip == null) return;


            CurrentOverrideController = new AnimatorOverrideController(CustomDanceAvatarController);
            CurrentOverrideController["CUSTOM_DANCE"] = clip;
            CurrentAnimator.runtimeAnimatorController = CurrentOverrideController;
            var rebind=typeof(Animator).GetMethod("Rebind", Type.EmptyTypes);
            if(rebind!=null)rebind.Invoke(CurrentAnimator,null);
            CurrentAnimator.SetBool("isDancing", true);
            CurrentAnimator.Update(0f);
        }
    }
}
