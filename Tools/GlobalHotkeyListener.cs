using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CustomDancePlayer
{
    /// <summary>
    /// Standalone global hotkey listener component (Ctrl+Alt+H), supports enabling/disabling hook, depends on DancePlayerCore for playback
    /// </summary>
    [RequireComponent(typeof(DancePlayerCore))] // Auto-associate PlayerCore (can also be assigned manually)
    public class GlobalHotkeyListener : MonoBehaviour
    {
        // ================================ Hook Basic Config ================================
        // Windows API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101; // Key up message (fix state residue)
                                             // Hotkey virtual codes (Ctrl+Alt+H)
        private const int VK_CONTROL = 162;
        private const int VK_ALT = 164;
        private const int VK_H = 72;

        // Hook core variables
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardCallback;
        // Key states (avoid single key mis-trigger)
        private bool _isCtrlPressed;
        private bool _isAltPressed;
        // Main thread sync flag (prevent cross-thread Unity API calls)
        private bool _needTriggerPlay;

        // ================================ External Config & References ================================
        [Header("Hotkey Control Switch")]
        [Tooltip("Enable global hotkey listener (Enable=mount hook, Disable=unmount hook)")]
        public bool isHotkeyEnabled = true;

        [Header("Dependency Reference")]
        [Tooltip("Associated player core (if null, will auto-find DancePlayerCore on the same GameObject)")]
        public DancePlayerCore playerCore;

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
            if (playerCore == null)
            {
                playerCore = GetComponent<DancePlayerCore>();
            }

            // Initialize hook callback (prevent GC collection causing hook failure)
            _keyboardCallback = OnKeyboardEvent;
        }

        private void OnEnable()
        {
            // Mount hook according to switch state when enabled
            if (isHotkeyEnabled)
            {
                MountGlobalHook();
            }
        }

        private void OnDisable()
        {
            // Force unmount hook when disabled (prevent memory leak)
            UnmountGlobalHook();
        }

        private void OnDestroy()
        {
            // Double check unmount on destroy (extra safety)
            UnmountGlobalHook();
        }

        private void Update()
        {
            // Execute playback on main thread (Unity API cannot be called from system thread)
            if (_needTriggerPlay && playerCore != null)
            {
                TriggerPlayerPlay();
                _needTriggerPlay = false; // Reset flag
            }
        }

        // ================================ Hook Core Logic ================================
        /// <summary>
        /// Mount global keyboard hook
        /// </summary>
        private void MountGlobalHook()
        {
            // Check: skip if already mounted, show error if PlayerCore is null
            if (_hookId != IntPtr.Zero) return;
            if (playerCore == null)
            {
                Debug.LogError("GlobalHotkeyListener: DancePlayerCore reference not found, cannot mount hook!");
                return;
            }

            // Get current program module handle (required by Windows API)
            IntPtr moduleHandle = GetModuleHandle(Assembly.GetExecutingAssembly().GetName().Name);
            if (moduleHandle == IntPtr.Zero)
            {
                Debug.LogError("GlobalHotkeyListener: Failed to get program module handle, hook mount failed!");
                return;
            }

            // Mount hook
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardCallback, moduleHandle, 0);
            if (_hookId == IntPtr.Zero)
            {
                Debug.LogError("GlobalHotkeyListener: Hook mount failed! Please run the program as administrator.");
            }
            else
            {
                Debug.Log("GlobalHotkeyListener: Global hotkey hook mounted (Ctrl+Alt+H)");
            }
        }

        /// <summary>
        /// Unmount global keyboard hook
        /// </summary>
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

        /// <summary>
        /// Keyboard event callback (system thread, cannot call Unity API directly)
        /// </summary>
        private IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // nCode < 0: must pass to next hook (required by system)
            if (nCode < 0)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Parse keyboard event
            KBDLLHOOKSTRUCT keyEvent = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP;

            // 1. Update Ctrl/Alt state (down=true, up=false)
            switch (keyEvent.vkCode)
            {
                case VK_CONTROL:
                    _isCtrlPressed = isKeyDown;
                    break;
                case VK_ALT:
                    _isAltPressed = isKeyDown;
                    break;
            }

            // 2. Detect Ctrl+Alt+H pressed simultaneously (trigger only on key down)
            if (isKeyDown && keyEvent.vkCode == VK_H && _isCtrlPressed && _isAltPressed)
            {
                _needTriggerPlay = true;
            }

            // Pass event to next hook (ensure normal keyboard operation)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // ================================ Player Interaction Logic ================================
        /// <summary>
        /// Trigger PlayerCore playback (executed on main thread)
        /// </summary>
        private void TriggerPlayerPlay()
        {
            // Check player state: show warning if playlist is empty
            if (playerCore == null) return;
            if (!playerCore.IsPlaying)
            {
                // Not playing: start from track 0 (can be changed to "remember last index" as needed)
                bool playSuccess = playerCore.PlayDanceByIndex(0);
                if (!playSuccess)
                {
                    Debug.LogWarning("GlobalHotkeyListener: Playback failed (playlist empty or avatar not ready)");
                }
            }
            else
            {
                // Already playing: can be changed as needed (e.g. pause/switch track, default is no action)
                Debug.Log("GlobalHotkeyListener: Player is already playing, trigger skipped");
            }
        }

        // ================================ External Control Interface (Optional) ================================
        /// <summary>
        /// Manually toggle hotkey enabled state (e.g. called by UI button)
        /// </summary>
        public void ToggleHotkeyEnabled()
        {
            isHotkeyEnabled = !isHotkeyEnabled;
            if (isHotkeyEnabled)
            {
                MountGlobalHook();
            }
            else
            {
                UnmountGlobalHook();
            }
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