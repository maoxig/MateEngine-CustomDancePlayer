using UnityEngine;
using UnityEditor;
using System.IO;
public static class DanceThemeBuilder
{
 public static void Run()
 {
  foreach (var path in new[] { "Assets/DanceTheme/panel.png", "Assets/DanceTheme/control.png" })
  {
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
   importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(18,18,18,18);importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
  }
  var canvas=new GameObject("DancePlayerCanvas",typeof(RectTransform),typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
  PrefabUtility.SaveAsPrefabAsset(canvas,"Assets/DanceTheme/PlayerCanvas.prefab");Object.DestroyImmediate(canvas);
  var args=System.Environment.GetCommandLineArgs();int index=System.Array.IndexOf(args,"--theme-output");if(index<0||index+1>=args.Length)throw new System.ArgumentException("--theme-output is required");var output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
  BuildPipeline.BuildAssetBundles(output,new[] { new AssetBundleBuild {assetBundleName="dance-theme.bundle",assetNames=new[] {"Assets/DanceTheme/panel.png","Assets/DanceTheme/control.png","Assets/DanceTheme/PlayerCanvas.prefab"} } },BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
  EditorApplication.Exit(0);
 }
}
