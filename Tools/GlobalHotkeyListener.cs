using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CustomDancePlayer
{
    /// <summary>
    /// Standalone global hotkey listener component (Ctrl+Alt+H), supports enabling/disabling hook, depends on DancePlayerCore for playback
    /// </summary>
    public class GlobalHotkeyListener : MonoBehaviour
    {
        // ================================ Hook Basic Config ================================
        // Windows API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101; // Key up message (fix state residue)
                                             // Hotkey virtual codes (Ctrl+Alt+>)
                                             // TODO: make configurable if needed
        private const int VK_CONTROL = 162;
        private const int VK_ALT = 164;
        private const int VK_PERIOD = 190;

        // Hook core variables
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardCallback;
        // Key states (avoid single key mis-trigger)
        private bool _isCtrlPressed;
        private bool _isAltPressed;
        // Main thread sync flag (prevent cross-thread Unity API calls)
        private bool _needTriggerPlay;


        [Header("Dependency Reference")]
        public DancePlayerUIManager dancePlayerUIManager;

        // ================================ Hook Delegate & Struct ================================
        // Hook callback delegate (must match Windows API signature)
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Keyboard event info struct
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;          // Key virtual code
            public int scanCode;        // Scan code
            public int flags;           // Event flags (e.g. extended key)
            public int time;            // Timestamp
            public IntPtr dwExtraInfo;  // Extra info
        }

        // ================================ Lifecycle & Hook Management ================================
        private void Start()
        {
            if (dancePlayerUIManager == null)
            {
                dancePlayerUIManager = FindFirstObjectByType<DancePlayerUIManager>();
            }

            _keyboardCallback = OnKeyboardEvent;
        }

        private void OnEnable()
        {
            StartCoroutine(DelayedMountHook());
        }

        private IEnumerator DelayedMountHook()
        {
            // Delay a few frames to ensure other components are initialized
            yield return null;
            yield return null;

            MountGlobalHook();
        }

        private void OnDisable()
        {
            UnmountGlobalHook();
        }

        private void OnDestroy()
        {
            UnmountGlobalHook();
        }

        private void Update()
        {

            if (_needTriggerPlay && dancePlayerUIManager != null)
            {
                TriggerPlayerPlay();
                _needTriggerPlay = false; // Reset flag
            }
        }

        // ================================ Hook Core Logic ================================

        private void MountGlobalHook()
        {
            if (_hookId != IntPtr.Zero) return;
            if (dancePlayerUIManager == null)
            {
                Debug.LogError("GlobalHotkeyListener: DancePlayerCore reference not found, cannot mount hook!");
                return;
            }

            IntPtr moduleHandle = GetModuleHandle(null);
            if (moduleHandle == IntPtr.Zero)
            {
                Debug.LogError("GlobalHotkeyListener: Failed to get program module handle, hook mount failed!");
                return;
            }

            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardCallback, moduleHandle, 0);
            if (_hookId == IntPtr.Zero)
            {
                Debug.LogError("GlobalHotkeyListener: Hook mount failed! Please run the program as administrator.");
            }
            else
            {
                Debug.Log("GlobalHotkeyListener: Global hotkey hook mounted (Ctrl+Alt+>)");
            }
        }


        private void UnmountGlobalHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                Debug.Log("GlobalHotkeyListener: Global hotkey hook unmounted");
            }

            // Reset key states (avoid residue when re-enabled)
            _isCtrlPressed = false;
            _isAltPressed = false;
        }


        private IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode < 0)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }


            KBDLLHOOKSTRUCT keyEvent = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP;

            switch (keyEvent.vkCode)
            {
                case VK_CONTROL:
                    _isCtrlPressed = isKeyDown;
                    break;
                case VK_ALT:
                    _isAltPressed = isKeyDown;
                    break;
            }

            if (isKeyDown && keyEvent.vkCode == VK_PERIOD && _isCtrlPressed && _isAltPressed)
            {
                _needTriggerPlay = true;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void TriggerPlayerPlay()
        {
            if (dancePlayerUIManager == null) return;
            dancePlayerUIManager.OnPlayPauseBtnClick();
        }


        // ================================ Windows API Imports ================================
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}