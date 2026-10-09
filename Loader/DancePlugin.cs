using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("maoxig.customdanceplayer", "CustomDancePlayer", "0.2.0")]
public sealed class DancePlugin : BaseUnityPlugin
{
    private object root;
    private static MethodInfo suppressDrag;
    private static MethodInfo clearDragging;
    private static FieldInfo mouseHeld,dragLockTimer;
    private Harmony inputHarmony;
    void Awake()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        bool audit = arguments.Contains("--cdp-audit");
        bool hostBaseline = arguments.Contains("--cdp-host-baseline");
        if (audit || hostBaseline)
        {
            var host = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            var startup = host?.GetType("SystemStartHandler");
            var registry = startup?.GetMethod("TryApplyRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            if (registry != null) new Harmony("maoxig.customdanceplayer.audit").Patch(registry, new HarmonyMethod(typeof(DancePlugin), nameof(SkipRegistry)));
        }
        // Internal performance probe: keep BepInEx and the loader present while
        // omitting all CustomDancePlayer asset/UI work. This separates a Steam
        // host/model hitch from the plugin's own initialization frame.
        if (hostBaseline) return;
        StartCoroutine(Begin());
    }
    private static bool SkipRegistry() { return false; }
    private IEnumerator Begin()
    {
        string directory = Path.GetDirectoryName(Info.Location);
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "CustomDancePlayer");
        if (loaded == null) loaded = Assembly.LoadFrom(Path.Combine(directory, "CustomDancePlayer.dll"));
        var bootstrap = loaded.GetType("CustomDancePlayer.DanceBootstrap");
        if (bootstrap == null)
        {
            Logger.LogError("An older CustomDancePlayer is loaded from Managed. Back it up and remove its DLL registration/UI mod before installing 0.2.");
            yield break;
        }
        var prepare = bootstrap.GetMethod("Prepare")?.Invoke(null, new object[] { directory }) as IEnumerator;
        if (prepare != null) yield return prepare;
        float timeout = Time.realtimeSinceStartup + 45;
        while ((Camera.main == null || GameObject.Find("Model") == null) && Time.realtimeSinceStartup < timeout) yield return null;
        if (Camera.main == null) { Logger.LogError("MateEngine scene did not become ready."); yield break; }
        // Let the host finish and present its first model frame before building
        // the code-driven player window. This keeps the remaining UI work out of
        // the same frame as avatar initialization and physics activation.
        yield return null;
        yield return null;
        root = bootstrap.GetMethod("Create").Invoke(null, new object[] { directory });
        suppressDrag=loaded.GetType("CustomDancePlayer.DancePlayerUIManager")?.GetMethod("SuppressHostDragAnimation");
        var avatarController=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Assembly-CSharp")?.GetType("AvatarAnimatorController");
        var update=avatarController?.GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        clearDragging=avatarController?.GetMethod("SetDragging",BindingFlags.Instance|BindingFlags.NonPublic);
        mouseHeld=avatarController?.GetField("mouseHeld",BindingFlags.Instance|BindingFlags.NonPublic);
        dragLockTimer=avatarController?.GetField("dragLockTimer",BindingFlags.Instance|BindingFlags.NonPublic);
        if(update!=null && suppressDrag!=null){inputHarmony=new Harmony("maoxig.customdanceplayer.input");inputHarmony.Patch(update,new HarmonyMethod(typeof(DancePlugin),nameof(AvatarUpdatePrefix)));}
        var build = loaded.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
            .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.2";
        Logger.LogInfo("CustomDancePlayer " + build + " ready; loaded via BepInEx. Assembly=" + loaded.Location);
    }
    private static bool AvatarUpdatePrefix(object __instance)
    {
        if(suppressDrag==null || !(bool)suppressDrag.Invoke(null,null))return true;
        clearDragging?.Invoke(__instance,new object[]{false});mouseHeld?.SetValue(__instance,false);dragLockTimer?.SetValue(__instance,0f);
        return false;
    }
    void OnDestroy() { inputHarmony?.UnpatchSelf(); if (root is GameObject go && go != null) UnityEngine.Object.Destroy(go); }
}
