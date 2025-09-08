using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;


public class DanceScreenCompensator : MonoBehaviour
{
    [Header("References")]
    private Camera targetCamera;                  // 使用 Camera.main
    public DanceAvatarHelper avatarHelper;       // 必须：用于获取 CurrentAvatar
    public DancePlayerCore dancePlayerCore; // 可选：用于检测是否在播放

    [Header("Detection")]
    public float screenMarginPx = 200f;          // 距离屏幕边缘多少像素时触发补偿
    public float checkInterval = 0.06f;          // 检测周期（秒）
    public float minShiftPx = 2f;                // 小于此像素的不触发（防抖）

    [Header("Movement / smoothing")]
    public float smoothTime = 0.10f;             // 平滑时长（秒），你要求 0.1s，默认即 0.1
    [Tooltip("放大或缩小相机响应量（若相机移动量看起来不够大，可把它调 >1）")]
    public float cameraMovementMultiplier = 1.0f;
    [Tooltip("放大或缩小窗口响应量（通常为 1）")]
    public float windowMovementMultiplier = 1.0f;

    [Header("Sign adjustments (flip if axis is reversed)")]
    public bool invertCameraX = false;            // 若相机 X 方向反了（角色左移相机 X 反向），切换此项
    public bool invertCameraY = false;           // 若相机 Y 方向反了，切换此项
    public bool invertWindowX = true;            // 若窗口 X 方向反了，切换此项
    public bool invertWindowY = false;           // 若窗口 Y 方向反了，切换此项

    [Header("Safety")]
    public float maxCumulativeWindowShiftPx = 10000f; // 防止窗口累积移动过大
    public bool enableVerticalCompensation = true;    // 是否处理 Y 方向

    // internal state
    private Vector3 cameraOriginalPos;
    private Vector2 windowOriginalPos;
    private bool haveOriginals = false;

    private Vector2 cumulativeWindowShift = Vector2.zero;
    private float lastShiftTime = -10f;
    private Coroutine activeShiftCoroutine = null;
    private Coroutine restoreCoroutine = null;
    private bool wasPlaying = false;

    private IntPtr hWnd = IntPtr.Zero;

    // Win32 constants
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOZORDER = 0x0004;

    #region Win32 P/Invoke
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }
    #endregion

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Debug.LogError("[DanceScreenCompensator_Final] No camera assigned and Camera.main is null. Disabled.");
            enabled = false;
            return;
        }

        if (avatarHelper == null)
        {
            Debug.LogError("[DanceScreenCompensator_Final] avatarHelper is not assigned. Disabled.");
            enabled = false;
            return;
        }

        // cache originals
        cameraOriginalPos = targetCamera.transform.position;
        hWnd = GetForegroundWindow();
        windowOriginalPos = GetWindowPosition(hWnd);
        haveOriginals = true;

        StartCoroutine(CheckLoop());
    }

    private IEnumerator CheckLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (!avatarHelper.IsAvatarAvailable() || avatarHelper.CurrentAvatar == null)
            {
                // if we were compensating and avatar disappears, restore
                if (wasPlaying)
                {
                    wasPlaying = false;
                    StartRestore();
                }
                continue;
            }



            if (dancePlayerCore.IsPlaying)
            {
                if (!wasPlaying)
                {
                    wasPlaying = true;
                    // capture baseline for restore
                    cameraOriginalPos = targetCamera.transform.position;
                    windowOriginalPos = GetWindowPosition(hWnd);
                    cumulativeWindowShift = Vector2.zero;
                    if (restoreCoroutine != null) { StopCoroutine(restoreCoroutine); restoreCoroutine = null; }
                }

                // get hips

                Transform hips = null;
                try
                {
                    hips = avatarHelper.CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips);
                }
                catch
                {
                    hips = null;
                }
                if (hips == null) continue;

                Vector3 screenPos = targetCamera.WorldToScreenPoint(hips.position);

                Vector2 deltaPixels = Vector2.zero;

                // X
                if (screenPos.x < screenMarginPx)
                {
                    deltaPixels.x = screenMarginPx - screenPos.x; // positive value: push right
                }
                else if (screenPos.x > Screen.width - screenMarginPx)
                {
                    deltaPixels.x = (Screen.width - screenMarginPx) - screenPos.x; // negative value: push left
                }

                // Y (Unity screen Y origin is bottom)
                if (enableVerticalCompensation)
                {
                    if (screenPos.y < screenMarginPx)
                    {
                        deltaPixels.y = screenMarginPx - screenPos.y; // positive: push up
                    }
                    else if (screenPos.y > Screen.height - screenMarginPx)
                    {
                        deltaPixels.y = (Screen.height - screenMarginPx) - screenPos.y; // negative: push down
                    }
                }

                // consider min shift threshold
                if (Mathf.Abs(deltaPixels.x) < minShiftPx) deltaPixels.x = 0f;
                if (Mathf.Abs(deltaPixels.y) < minShiftPx) deltaPixels.y = 0f;

                if (deltaPixels.sqrMagnitude > 0f && Time.unscaledTime - lastShiftTime > 0f)
                {
                    // compute effective camera pixel delta accounting for camera invert flags
                    Vector2 effectiveCamPixelDelta = new Vector2(
                        deltaPixels.x * (invertCameraX ? -1f : 1f),
                        deltaPixels.y * (invertCameraY ? -1f : 1f)
                    );

                    // compute window pixel delta accounting for invert flags and multiplier
                    Vector2 effectiveWindowDelta = new Vector2(
                        effectiveCamPixelDelta.x * (invertWindowX ? -1f : 1f) * windowMovementMultiplier,
                        effectiveCamPixelDelta.y * (invertWindowY ? -1f : 1f) * windowMovementMultiplier
                    );

                    // clamp cumulative
                    Vector2 potentialTotal = cumulativeWindowShift + effectiveWindowDelta;
                    if (potentialTotal.magnitude > maxCumulativeWindowShiftPx)
                    {
                        // scale down to remain capacity
                        float remain = Mathf.Max(0f, maxCumulativeWindowShiftPx - cumulativeWindowShift.magnitude);
                        if (remain <= 0f) continue;
                        float factor = remain / effectiveWindowDelta.magnitude;
                        effectiveWindowDelta *= factor;
                        effectiveCamPixelDelta *= factor;
                    }

                    // start shift coroutine (smooth camera + window)
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
        if (targetCamera == null) yield break;

        // compute camera world shift (accurate) based on hips projection
        // pick hips again (guard)
        var avatar = avatarHelper.CurrentAvatar;
        if (avatar == null || avatarHelper.CurrentAnimator == null) yield break;
        Transform hips = null;
        try { hips = avatarHelper.CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips); } catch { hips = null; }
        if (hips == null) yield break;

        Vector3 screenP = targetCamera.WorldToScreenPoint(hips.position);
        Vector3 screenPprime = new Vector3(screenP.x + camPixelDelta.x, screenP.y + camPixelDelta.y, screenP.z);

        Vector3 worldAtP = targetCamera.ScreenToWorldPoint(screenP);
        Vector3 worldAtPprime = targetCamera.ScreenToWorldPoint(screenPprime);

        // camera should move by (worldAtP - worldAtPprime) to make the hips appear at screenPprime
        Vector3 cameraShift = (worldAtP - worldAtPprime) * cameraMovementMultiplier;

        // apply smoothing: lerp camera.position and window pos in unscaled time
        Vector3 camStart = targetCamera.transform.position;
        Vector3 camTarget = camStart + cameraShift;

        Vector2 winStart = GetWindowPosition(hWnd);
        Vector2 winTarget = winStart + windowPixelDelta;

        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            // camera
            targetCamera.transform.position = Vector3.Lerp(camStart, camTarget, easeT);

            // window
            SetWindowPosition(hWnd, Vector2.Lerp(winStart, winTarget, easeT));

            yield return null;
        }

        // finalize
        targetCamera.transform.position = camTarget;
        SetWindowPosition(hWnd, winTarget);

        // accumulate
        cumulativeWindowShift += windowPixelDelta;

        activeShiftCoroutine = null;
        yield break;
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
        Vector2 winStart = GetWindowPosition(hWnd);
        Vector2 winTarget = windowOriginalPos;

        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            targetCamera.transform.position = Vector3.Lerp(camStart, camTarget, easeT);
            SetWindowPosition(hWnd, Vector2.Lerp(winStart, winTarget, easeT));

            yield return null;
        }

        targetCamera.transform.position = camTarget;
        SetWindowPosition(hWnd, winTarget);

        cumulativeWindowShift = Vector2.zero;
        restoreCoroutine = null;
    }

    private Vector2 GetWindowPosition(IntPtr window)
    {
        if (window == IntPtr.Zero) return Vector2.zero;
        if (GetWindowRect(window, out RECT r))
        {
            return new Vector2(r.left, r.top);
        }
        return Vector2.zero;
    }

    private void SetWindowPosition(IntPtr window, Vector2 pos)
    {
        if (window == IntPtr.Zero) return;
        // fetch current size
        if (!GetWindowRect(window, out RECT r)) return;
        int w = r.right - r.left;
        int h = r.bottom - r.top;
        SetWindowPos(window, IntPtr.Zero, (int)pos.x, (int)pos.y, w, h, SWP_NOZORDER);
    }

    private void OnDisable()
    {
        // restore on disable to avoid leaving window moved
        if (haveOriginals && hWnd != IntPtr.Zero)
        {
            SetWindowPosition(hWnd, windowOriginalPos);
            if (targetCamera != null)
                targetCamera.transform.position = cameraOriginalPos;
        }
    }
}
