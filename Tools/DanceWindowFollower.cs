using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomDancePlayer
{
    public class DanceWindowFollower : MonoBehaviour
    {
        [Header("References")]
        private Camera targetCamera;
        public DanceAvatarHelper avatarHelper;
        public DancePlayerCore dancePlayerCore;

        [Header("Control")]
        [Tooltip("Enable or disable the screen window follow functionality")]
        public bool isEnabled = true;

        [Header("Detection")]
        public float screenMarginFraction = 0.15f;
        public float checkInterval = 0.06f;
        public float minShiftPx = 2f;

        [Header("Movement / smoothing")]
        public float smoothTime = 0.10f;
        public float cameraMovementMultiplier = 1.0f;
        public float windowMovementMultiplier = 1.0f;

        [Header("Sign adjustments")]
        public bool invertCameraX = false;
        public bool invertCameraY = false;
        public bool invertWindowX = true;
        public bool invertWindowY = false;

        [Header("Safety")]
        public float maxCumulativeWindowShiftPx = 10000f;
        public bool enableVerticalCompensation = false;

        [Header("Additional Objects to Move")]
        [Tooltip("Names of other objects that must move with the main camera")]
        public string[] additionalObjectNames;

        // Internal state
        private Vector3 cameraOriginalPos;
        private Vector2 windowOriginalPos;
        private bool haveOriginals = false;

        private List<Transform> additionalTransforms = new List<Transform>();
        private List<Vector3> additionalOriginalPos = new List<Vector3>();

        private Vector2 cumulativeWindowShift = Vector2.zero;
        private float lastShiftTime = -10f;
        private Coroutine activeShiftCoroutine = null;
        private Coroutine restoreCoroutine = null;
        private bool wasPlaying = false;


        private Kirurobo.UniWindowController windowController;
        private Kirurobo.UniWindowMoveHandle windowMoveHandle;
        private bool lastIsDragging = false;

        private void Start()
        {

            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null)
            {
                Debug.LogError("[DanceWindowFollower] No camera assigned and Camera.main is null. Disabled.");
                enabled = false;
                return;
            }


            if (avatarHelper == null)
            {
                Debug.LogError("[DanceWindowFollower] avatarHelper is not assigned. Disabled.");
                enabled = false;
                return;
            }


            windowController = Kirurobo.UniWindowController.current;
            if (windowController == null)
            {
                Debug.LogError("[DanceWindowFollower] UniWindowController not found in scene. Disabled.");
                enabled = false;
                return;
            }


            windowMoveHandle = FindFirstObjectByType<Kirurobo.UniWindowMoveHandle>();
            if (windowMoveHandle == null)
            {
                Debug.LogWarning("[DanceWindowFollower] UniWindowMoveHandle not found. Drag detection may not work.");
            }


            foreach (var name in additionalObjectNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    additionalTransforms.Add(go.transform);
                }
                else
                {
                    Debug.LogWarning($"[DanceWindowFollower] Additional object '{name}' not found.");
                }
            }


            cameraOriginalPos = targetCamera.transform.position;
            windowOriginalPos = windowController.windowPosition;
            additionalOriginalPos = additionalTransforms.ConvertAll(t => t ? t.position : Vector3.zero);
            haveOriginals = true;

            StartCoroutine(CheckLoop());
        }

        public void SetEnabled(bool enable)
        {
            if (isEnabled != enable)
            {
                isEnabled = enable;
                if (!isEnabled && wasPlaying)
                {
                    wasPlaying = false;
                    StartRestore();
                }
            }
        }

        private IEnumerator CheckLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);

                if (!isEnabled)
                {
                    if (wasPlaying)
                    {
                        wasPlaying = false;
                        StartRestore();
                    }
                    continue;
                }

                if (!avatarHelper.IsAvatarAvailable() || avatarHelper.CurrentAvatar == null)
                {
                    if (wasPlaying)
                    {
                        wasPlaying = false;
                        StartRestore();
                    }
                    continue;
                }

                bool isPlaying = (dancePlayerCore != null && dancePlayerCore.IsPlaying);

                if (isPlaying)
                {
                    if (!wasPlaying)
                    {
                        wasPlaying = true;
                        cameraOriginalPos = targetCamera.transform.position;
                        windowOriginalPos = windowController.windowPosition;
                        additionalOriginalPos = additionalTransforms.ConvertAll(t => t ? t.position : Vector3.zero);
                        cumulativeWindowShift = Vector2.zero;
                        if (restoreCoroutine != null) { StopCoroutine(restoreCoroutine); restoreCoroutine = null; }
                    }


                    bool isDragging = (windowMoveHandle != null && windowMoveHandle.IsDragging);

                    if (isDragging)
                    {
                        lastIsDragging = true;
                        continue;
                    }

                    if (lastIsDragging && !isDragging)
                    {
                        Vector2 currentWinPos = windowController.windowPosition;
                        windowOriginalPos = currentWinPos - cumulativeWindowShift;
                        Debug.Log("[DanceWindowFollower] Updated original window position after drag.");
                        lastIsDragging = false;
                    }


                    if (avatarHelper.CurrentAvatarHips == null) continue;

                    Vector3 screenPos = targetCamera.WorldToScreenPoint(avatarHelper.CurrentAvatarHips.position);

                    float marginX = Screen.width * screenMarginFraction;
                    float marginY = Screen.height * screenMarginFraction;

                    Vector2 deltaPixels = Vector2.zero;


                    if (screenPos.x < marginX)
                    {
                        deltaPixels.x = marginX - screenPos.x;
                    }
                    else if (screenPos.x > Screen.width - marginX)
                    {
                        deltaPixels.x = (Screen.width - marginX) - screenPos.x;
                    }


                    if (enableVerticalCompensation)
                    {
                        if (screenPos.y < marginY)
                        {
                            deltaPixels.y = marginY - screenPos.y;
                        }
                        else if (screenPos.y > Screen.height - marginY)
                        {
                            deltaPixels.y = (Screen.height - marginY) - screenPos.y;
                        }
                    }


                    if (Mathf.Abs(deltaPixels.x) < minShiftPx) deltaPixels.x = 0f;
                    if (Mathf.Abs(deltaPixels.y) < minShiftPx) deltaPixels.y = 0f;

                    if (deltaPixels.sqrMagnitude > 0f && Time.unscaledTime - lastShiftTime > 0f)
                    {
                        Vector2 effectiveCamPixelDelta = new Vector2(
                            deltaPixels.x * (invertCameraX ? -1f : 1f),
                            deltaPixels.y * (invertCameraY ? -1f : 1f)
                        );

                        Vector2 effectiveWindowDelta = new Vector2(
                            effectiveCamPixelDelta.x * (invertWindowX ? -1f : 1f) * windowMovementMultiplier,
                            effectiveCamPixelDelta.y * (invertWindowY ? -1f : 1f) * windowMovementMultiplier
                        );


                        Vector2 potentialTotal = cumulativeWindowShift + effectiveWindowDelta;
                        if (potentialTotal.magnitude > maxCumulativeWindowShiftPx)
                        {
                            float remain = Mathf.Max(0f, maxCumulativeWindowShiftPx - cumulativeWindowShift.magnitude);
                            if (remain <= 0f) continue;
                            float factor = remain / effectiveWindowDelta.magnitude;
                            effectiveWindowDelta *= factor;
                            effectiveCamPixelDelta *= factor;
                        }

                        if (activeShiftCoroutine != null) StopCoroutine(activeShiftCoroutine);
                        activeShiftCoroutine = StartCoroutine(DoCameraAndWindowShift(effectiveCamPixelDelta, effectiveWindowDelta, smoothTime));
                        lastShiftTime = Time.unscaledTime;
                    }
                }
                else
                {
                    if (wasPlaying)
                    {
                        wasPlaying = false;
                        StartRestore();
                    }
                }
            }
        }

        private IEnumerator DoCameraAndWindowShift(Vector2 camPixelDelta, Vector2 windowPixelDelta, float duration)
        {
            if (targetCamera == null || !isEnabled) yield break;

            var avatar = avatarHelper.CurrentAvatar;
            if (avatar == null || avatarHelper.CurrentAnimator == null) yield break;

            Transform hips = null;
            try { hips = avatarHelper.CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips); } catch { hips = null; }
            if (hips == null) yield break;

            Vector3 screenP = targetCamera.WorldToScreenPoint(hips.position);
            Vector3 screenPprime = new Vector3(screenP.x + camPixelDelta.x, screenP.y + camPixelDelta.y, screenP.z);

            Vector3 worldAtP = targetCamera.ScreenToWorldPoint(screenP);
            Vector3 worldAtPprime = targetCamera.ScreenToWorldPoint(screenPprime);

            Vector3 cameraShift = (worldAtP - worldAtPprime) * cameraMovementMultiplier;

            Vector3 camStart = targetCamera.transform.position;
            Vector3 camTarget = camStart + cameraShift;

            List<Vector3> addStarts = additionalTransforms.ConvertAll(t => t ? t.position : Vector3.zero);


            Vector2 winStart = windowController.windowPosition;
            Vector2 winTarget = winStart + windowPixelDelta;

            float elapsed = 0f;
            duration = Mathf.Max(0.0001f, duration);
            while (elapsed < duration)
            {
                if (!isEnabled) yield break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                targetCamera.transform.position = Vector3.Lerp(camStart, camTarget, easeT);

                for (int i = 0; i < additionalTransforms.Count; i++)
                {
                    var trans = additionalTransforms[i];
                    if (trans)
                    {
                        Vector3 addTarget = addStarts[i] + cameraShift;
                        trans.position = Vector3.Lerp(addStarts[i], addTarget, easeT);
                    }
                }


                windowController.windowPosition = Vector2.Lerp(winStart, winTarget, easeT);

                yield return null;
            }

            targetCamera.transform.position = camTarget;
            for (int i = 0; i < additionalTransforms.Count; i++)
            {
                var trans = additionalTransforms[i];
                if (trans)
                {
                    Vector3 addTarget = addStarts[i] + cameraShift;
                    trans.position = addTarget;
                }
            }
            windowController.windowPosition = winTarget;

            cumulativeWindowShift += windowPixelDelta;

            activeShiftCoroutine = null;
        }

        private void StartRestore()
        {
            if (activeShiftCoroutine != null) { StopCoroutine(activeShiftCoroutine); activeShiftCoroutine = null; }
            if (restoreCoroutine != null) { StopCoroutine(restoreCoroutine); restoreCoroutine = null; }
            restoreCoroutine = StartCoroutine(RestoreCoroutine(smoothTime * 2f));
        }

        private IEnumerator RestoreCoroutine(float duration)
        {
            if (!haveOriginals) yield break;

            Vector3 camStart = targetCamera.transform.position;
            Vector3 camTarget = cameraOriginalPos;

            List<Vector3> addStarts = additionalTransforms.ConvertAll(t => t ? t.position : Vector3.zero);

            Vector2 winStart = windowController.windowPosition;
            Vector2 winTarget = windowOriginalPos;

            float elapsed = 0f;
            duration = Mathf.Max(0.0001f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                targetCamera.transform.position = Vector3.Lerp(camStart, camTarget, easeT);

                for (int i = 0; i < additionalTransforms.Count; i++)
                {
                    var trans = additionalTransforms[i];
                    if (trans && i < additionalOriginalPos.Count)
                    {
                        trans.position = Vector3.Lerp(addStarts[i], additionalOriginalPos[i], easeT);
                    }
                }

                windowController.windowPosition = Vector2.Lerp(winStart, winTarget, easeT);

                yield return null;
            }

            targetCamera.transform.position = camTarget;
            for (int i = 0; i < additionalTransforms.Count; i++)
            {
                var trans = additionalTransforms[i];
                if (trans && i < additionalOriginalPos.Count)
                {
                    trans.position = additionalOriginalPos[i];
                }
            }
            windowController.windowPosition = winTarget;

            cumulativeWindowShift = Vector2.zero;
            restoreCoroutine = null;
        }

        private void OnDisable()
        {
            if (haveOriginals && windowController != null)
            {
                windowController.windowPosition = windowOriginalPos;
                if (targetCamera != null)
                    targetCamera.transform.position = cameraOriginalPos;
                for (int i = 0; i < additionalTransforms.Count; i++)
                {
                    var trans = additionalTransforms[i];
                    if (trans && i < additionalOriginalPos.Count)
                    {
                        trans.position = additionalOriginalPos[i];
                    }
                }
                cumulativeWindowShift = Vector2.zero;
            }
        }
    }
}