using System;
using System.IO;
using SFB;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace CustomDancePlayer
{
    // Use the same native dialogs as MateEngine; its trimmed WinForms build
    // does not support constructing ordinary Windows Forms controls.
    public static class DanceDialogs
    {
        public static bool Busy { get; private set; }
        private static readonly Queue<Action> callbacks=new Queue<Action>();
        private static Dispatcher dispatcher;
        public static void Open(string title, string[] extensions, Action<string[]> selected, Action<Exception> failed, bool multiple = false, string current = null)
        {
            string directory = !string.IsNullOrEmpty(current) && File.Exists(current) ? Path.GetDirectoryName(current) : "";
            Run(() => StandaloneFileBrowser.OpenFilePanel(title, directory, new[] { new ExtensionFilter(title, extensions) }, multiple),paths=>selected(paths??new string[0]),failed);
        }
        public static void Folder(string title,Action<string> selected,Action<Exception> failed)
        {
            Run(() => StandaloneFileBrowser.OpenFolderPanel(title,"",false),paths=>selected(paths==null||paths.Length==0?null:paths[0]),failed);
        }
        public static void Save(string title, string name,Action<string> selected,Action<Exception> failed)
        {
            Run(() => StandaloneFileBrowser.SaveFilePanel(title,"",name,new[] {new ExtensionFilter("VMD dance package","vmdance")}),selected,failed);
        }
        private static void Run<T>(Func<T> action,Action<T> selected,Action<Exception> failed)
        {
            // Initialize Unity's platform selection on its main thread. Windows
            // shell dialogs need STA and must not inherit the desktop pet's
            // transparent/non-activating window as their owner.
            RuntimeHelpers.RunClassConstructor(typeof(StandaloneFileBrowser).TypeHandle);
            if(Busy)throw new InvalidOperationException("A file dialog is already open.");
            if(dispatcher==null){var go=new GameObject("DanceFileDialogDispatcher");go.transform.SetParent(DanceBootstrap.Root.transform,false);dispatcher=go.AddComponent<Dispatcher>();}
            Busy=true;
            try
            {
                var thread=new Thread(()=>
                {
                    T result=default;Exception failure=null;
                    try{result=action();}catch(Exception error){failure=error;}
                    lock(callbacks)callbacks.Enqueue(()=>{Busy=false;try{if(failure!=null)failed(failure);else selected(result);}catch(Exception error){failed(error);}});
                }){IsBackground=true};
                thread.SetApartmentState(ApartmentState.STA);thread.Start();
            }
            catch{Busy=false;throw;}
        }
        public sealed class Dispatcher : MonoBehaviour
        {
            void Update(){Action callback=null;lock(callbacks){if(callbacks.Count>0)callback=callbacks.Dequeue();}callback?.Invoke();}
        }
    }
}
