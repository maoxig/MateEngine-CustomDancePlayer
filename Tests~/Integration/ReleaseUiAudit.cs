using System.Collections;
using System.IO;
using CustomDancePlayer;
using UnityEngine;
public partial class DanceAudit
{
 private IEnumerator ReleaseUiOnly()
 {
  output=Path.Combine(OutputDirectory(),"release-ui-current");Directory.CreateDirectory(output);
  while(DanceBootstrap.Root==null)yield return null;
  ui=DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);
  yield return new WaitForSecondsRealtime(3);yield return WaitForLibrary();ui.playerCore.InitPlayer();
  ui.Window.SetTab("library");yield return ExtraChecks();Save();Logger.LogInfo("RELEASE_UI_COMPLETE");
 }
}
