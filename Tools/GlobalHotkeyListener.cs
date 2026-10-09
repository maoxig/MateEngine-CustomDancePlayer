using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CustomDancePlayer
{
    /// <summary>
    /// Shared, non-exclusive global hook; mounted only while a playback shortcut is assigned.
    /// </summary>
    public class GlobalHotkeyListener : MonoBehaviour
    {
        // ================================ Hook Basic Config ================================
        // Windows API constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101; // Key up message (fix state residue)
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LALT = 0xA4;
        private const int VK_RALT = 0xA5;


        // Hook core variables
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardCallback;
        // Key states (avoid single key mis-trigger)
        private bool _isLeftCtrlPressed;
        private bool _isRightCtrlPressed;
        private bool _isLeftAltPressed;
        private bool _isRightAltPressed;
        private bool _isLeftShiftPressed, _isRightShiftPressed;
        private readonly System.Collections.Generic.HashSet<int> pressed = new System.Collections.Generic.HashSet<int>();

        // Main thread sync flag (prevent cross-thread Unity API calls)
        private bool _needTriggerPlay;
        private int pendingAction=-1;
        private readonly System.Collections.Generic.List<Chord> bindings=new System.Collections.Generic.List<Chord>();
        private int bindingRevision=-1;
        private DanceSettingsHandler.DanceSettingsData bindingSettings;
        private struct Chord { public DanceHotkeys.Action action; public int key; public bool ctrl,alt,shift; }


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
        private void Awake()
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

            if(isActiveAndEnabled && DanceHotkeys.HasGlobalBinding) { RefreshBindings(); MountGlobalHook(); }
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
            RefreshBindings();
            if(bindings.Count==0){UnmountGlobalHook();return;}
            if(_hookId==IntPtr.Zero)MountGlobalHook();
            if(DanceHotkeys.IsSuppressed){_needTriggerPlay=false;pendingAction=-1;return;}
            if(_needTriggerPlay && dancePlayerUIManager!=null) {
                var action=pendingAction<0?DanceHotkeys.Action.PlayPause:(DanceHotkeys.Action)pendingAction;
                _needTriggerPlay=false;pendingAction=-1;
                Dispatch(action);
            }
        }
        private void RefreshBindings()
        {
            var current=DanceSettingsHandler.Instance.data;
            if(bindingRevision==DanceHotkeys.Revision && ReferenceEquals(bindingSettings,current))return;
            bindingRevision=DanceHotkeys.Revision;bindingSettings=current;bindings.Clear();
            foreach(DanceHotkeys.Action action in Enum.GetValues(typeof(DanceHotkeys.Action))) {
                if(action==DanceHotkeys.Action.Panel)continue;
                var b=DanceHotkeys.Get(action);int key=DanceHotkeys.VirtualKey(b.key);if(key==0||!b.enabled)continue;
                bindings.Add(new Chord{action=action,key=key,ctrl=b.control,alt=b.alt,shift=b.shift});
            }
        }
        public void Dispatch(DanceHotkeys.Action action)
        {
            if(dancePlayerUIManager==null)return;
            var core=dancePlayerUIManager.playerCore;
            switch(action) {
                case DanceHotkeys.Action.PlayPause:dancePlayerUIManager.OnPlayPauseBtnClick();break;
                case DanceHotkeys.Action.PlayStop:dancePlayerUIManager.OnPlayStopBtnClick();break;
                case DanceHotkeys.Action.Stop:core.StopPlay();break;
                case DanceHotkeys.Action.Previous:core.PlayPrev();break;
                case DanceHotkeys.Action.Next:core.PlayNext();break;
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
                Debug.Log("GlobalHotkeyListener: Global hotkey hook mounted ("+DanceHotkeys.GlobalLabel()+")");
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
            _isLeftCtrlPressed = false;
            _isRightCtrlPressed = false;
            _isLeftAltPressed = false;
            _isRightAltPressed = false;
            _isLeftShiftPressed = _isRightShiftPressed = false;
            pressed.Clear();
            _needTriggerPlay = false;pendingAction=-1;
        }


        private IntPtr OnKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode < 0)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }


            KBDLLHOOKSTRUCT keyEvent = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

            if (ApplyKeyState(keyEvent.vkCode, isKeyDown, isKeyUp))
            {
                _needTriggerPlay = true;
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // Kept separate from the native callback so the one-shot chord logic can
        // be exercised by the in-game audit without synthesizing OS input.
        private bool ApplyKeyState(int vkCode, bool isKeyDown, bool isKeyUp)
        {
            switch (vkCode)
            {
                case VK_LCONTROL: if (isKeyDown || isKeyUp) _isLeftCtrlPressed = isKeyDown; break;
                case VK_RCONTROL: if (isKeyDown || isKeyUp) _isRightCtrlPressed = isKeyDown; break;
                case VK_LALT: if (isKeyDown || isKeyUp) _isLeftAltPressed = isKeyDown; break;
                case VK_RALT: if (isKeyDown || isKeyUp) _isRightAltPressed = isKeyDown; break;
                case 0xA0: if (isKeyDown || isKeyUp) _isLeftShiftPressed = isKeyDown; break;
                case 0xA1: if (isKeyDown || isKeyUp) _isRightShiftPressed = isKeyDown; break;
            }
            if(isKeyUp){pressed.Remove(vkCode);return false;}
            if(!isKeyDown||!pressed.Add(vkCode))return false;
            bool ctrl = _isLeftCtrlPressed || _isRightCtrlPressed;
            bool alt = _isLeftAltPressed || _isRightAltPressed;
            bool shift = _isLeftShiftPressed || _isRightShiftPressed;
            foreach(var binding in bindings)if(vkCode==binding.key && ctrl==binding.ctrl && alt==binding.alt && shift==binding.shift) {
                pendingAction=(int)binding.action;return true;
            }
            return false;
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
