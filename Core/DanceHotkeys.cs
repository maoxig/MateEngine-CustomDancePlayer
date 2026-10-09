using UnityEngine;

namespace CustomDancePlayer
{
    public static class DanceHotkeys
    {
        public static int Revision { get; private set; }
        private static bool capturing;
        private static int suppressThroughFrame=-1;
        public static bool IsCapturing { get => capturing; internal set { if(capturing&&!value)suppressThroughFrame=Time.frameCount+1;capturing=value; } }
        public static bool IsSuppressed => capturing || Time.frameCount<=suppressThroughFrame;
        public static bool Control => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        public static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        public static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool PanelPressed()
        {
            var data = DanceSettingsHandler.Instance.data;
            return !IsSuppressed && data.panelHotkeyEnabled && data.toggleKey != KeyCode.None && Input.GetKeyDown(data.toggleKey) &&
                Control == data.toggleControl && Alt == data.toggleAlt && Shift == data.toggleShift;
        }
        public static int VirtualKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return 65 + (int)key - (int)KeyCode.A;
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return 48 + (int)key - (int)KeyCode.Alpha0;
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return 112 + (int)key - (int)KeyCode.F1;
            switch(key) {
                case KeyCode.Space:return 32; case KeyCode.Return:return 13;
                case KeyCode.Period:return 190; case KeyCode.Comma:return 188;
                case KeyCode.Minus:return 189; case KeyCode.Equals:return 187;
                case KeyCode.Slash:return 191; case KeyCode.Semicolon:return 186;
                case KeyCode.LeftBracket:return 219; case KeyCode.RightBracket:return 221;
                case KeyCode.Backslash:return 220; case KeyCode.Quote:return 222;
                default:return 0;
            }
        }
        public static string Label(KeyCode key, bool control, bool alt, bool shift) =>
            key == KeyCode.None ? DanceLocale.T("settings.unboundKey") : (control ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") +
            (key == KeyCode.Period ? "." : key == KeyCode.Comma ? "," : key.ToString().Replace("Alpha", ""));
        public enum Action { Panel, PlayPause, PlayStop, Stop, Previous, Next }
        public static DanceSettingsHandler.DanceHotkeyBinding Get(Action action)
        {
            var d = DanceSettingsHandler.Instance.data;
            switch(action) {
                case Action.Panel: return new DanceSettingsHandler.DanceHotkeyBinding { key=d.toggleKey,enabled=d.panelHotkeyEnabled,control=d.toggleControl,alt=d.toggleAlt,shift=d.toggleShift };
                case Action.PlayPause: return new DanceSettingsHandler.DanceHotkeyBinding { key=d.globalPlaybackKey,enabled=d.enableGlobalHotkey,control=d.globalControl,alt=d.globalAlt,shift=d.globalShift };
                case Action.PlayStop:return d.playStopKey;
                case Action.Stop:return d.stopKey;
                case Action.Previous:return d.previousKey;
                default:return d.nextKey;
            }
        }
        public static void Set(Action action, KeyCode key, bool ctrl, bool alt, bool shift)
        {
            Revision++;
            var d=DanceSettingsHandler.Instance.data;
            if(key==KeyCode.None)ctrl=alt=shift=false;
            if(action==Action.Panel){d.toggleKey=key;d.toggleControl=ctrl;d.toggleAlt=alt;d.toggleShift=shift;}
            else if(action==Action.PlayPause){d.globalPlaybackKey=key;d.globalControl=ctrl;d.globalAlt=alt;d.globalShift=shift;d.enableGlobalHotkey=key!=KeyCode.None;}
            else { var b=Get(action);b.enabled=key!=KeyCode.None;b.key=key;b.control=ctrl;b.alt=alt;b.shift=shift; }
        }
        public static void SetEnabled(Action action,bool enabled) {
            Revision++;
            var d=DanceSettingsHandler.Instance.data;
            if(action==Action.Panel)d.panelHotkeyEnabled=enabled;
            else if(action==Action.PlayPause)d.enableGlobalHotkey=enabled;
            else Get(action).enabled=enabled;
            RefreshListeners();DanceSettingsHandler.OnSettingChanged();
        }
        public static bool HasGlobalBinding {
            get { foreach(Action a in System.Enum.GetValues(typeof(Action)))if(a!=Action.Panel && Get(a).enabled && VirtualKey(Get(a).key)!=0)return true;return false; }
        }
        public static string BindingLabel(Action action) {var b=Get(action);return Label(b.key,b.control,b.alt,b.shift);}
        public static void RefreshListeners() {
            if(DanceBootstrap.Root==null)return;
            foreach(var listener in DanceBootstrap.Root.GetComponentsInChildren<GlobalHotkeyListener>(true))listener.enabled=HasGlobalBinding;
        }
        public static string PanelLabel() { var d=DanceSettingsHandler.Instance.data;return Label(d.toggleKey,d.toggleControl,d.toggleAlt,d.toggleShift); }
        public static string GlobalLabel() { var d=DanceSettingsHandler.Instance.data;return Label(d.globalPlaybackKey,d.globalControl,d.globalAlt,d.globalShift); }
    }
}
