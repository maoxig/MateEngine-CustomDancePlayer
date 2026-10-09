using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CustomDancePlayer
{
    // Keep a small row pool even when the local library contains hundreds of dances.
    public sealed class DanceVirtualLibrary : MonoBehaviour
    {
        private const float RowStride = 40f;
        private const float RowHeight = 36f;
        private sealed class Row {public RectTransform rect;public Text title,format;public Button favorite,queue;public string id;}
        private readonly List<Row> rows=new List<Row>();
        private DancePlayerUIManager owner;private ScrollRect scroll;private RectTransform content;private Text empty;
        private int first=-1,count=-1;private string selected;private float viewportHeight;
        public int RowCount => rows.Count;
        public void Initialize(DancePlayerUIManager ui,ScrollRect view)
        {
            owner=ui;scroll=view;content=view.content;content.GetComponent<VerticalLayoutGroup>().enabled=false;content.GetComponent<ContentSizeFitter>().enabled=false;
            empty=DanceUi.Label(content,DanceLocale.T("library.empty"),15);DanceUi.Stretch(empty.rectTransform);
            scroll.onValueChanged.AddListener(_=>Render());
        }
        public void Refresh()
        {
            count=owner.playerCore.playlistManager.CurrentPlaylistData.Count;
            content.sizeDelta=new Vector2(0,Mathf.Max(64,count*RowStride));
            var pos=content.anchoredPosition;pos.y=Mathf.Clamp(pos.y,0,Mathf.Max(0,content.sizeDelta.y-scroll.viewport.rect.height));content.anchoredPosition=pos;
            first=-1;Render();
        }
        private void LateUpdate() {if(scroll!=null)Render();}
        private void Render()
        {
            if(owner==null || count<0)return;
            int start=Mathf.Clamp(Mathf.FloorToInt(content.anchoredPosition.y/RowStride),0,Mathf.Max(0,count-1));
            int poolTarget=Mathf.Max(0,Mathf.CeilToInt(scroll.viewport.rect.height/RowStride)+2);
            if(first==start&&selected==owner.playerCore.CurrentResourceId&&Mathf.Approximately(viewportHeight,scroll.viewport.rect.height)&&rows.Count>=poolTarget)return;
            first=start;selected=owner.playerCore.CurrentResourceId;viewportHeight=scroll.viewport.rect.height;
            int visible=Mathf.Min(count-start,Mathf.CeilToInt(viewportHeight/RowStride)+2);
            int created=0;while(rows.Count<poolTarget&&created<2){var row=CreateRow();row.rect.gameObject.SetActive(false);rows.Add(row);created++;}
            empty.gameObject.SetActive(count==0);
            var playlist=owner.playerCore.playlistManager;
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];row.rect.gameObject.SetActive(i<visible);if(i>=visible)continue;
                row.id=playlist.CurrentPlaylistData[start+i];var item=owner.resourceManager.Descriptors[row.id];
                row.rect.anchoredPosition=new Vector2(0,-(start+i)*RowStride);row.title.text="  "+item.Title;row.format.text="."+item.Format;
                row.rect.GetComponent<Image>().color=row.id==selected?new Color(0.23f,0.22f,0.37f):DanceUi.Surface;
                row.favorite.GetComponentInChildren<Text>().text=playlist.IsFavorite(row.id)?"★":"☆";
                row.queue.GetComponentInChildren<Text>().text=playlist.CurrentType==DancePlaylistManager.PlaylistType.Queue?"−":"+";
            }
        }
        private Row CreateRow()
        {
            var go=DanceUi.Row(content,RowHeight);DanceUi.Image(go,DanceUi.Surface);var row=new Row{rect=go.GetComponent<RectTransform>()};
            row.rect.anchorMin=new Vector2(0,1);row.rect.anchorMax=Vector2.one;row.rect.pivot=new Vector2(0.5f,1);row.rect.sizeDelta=new Vector2(0,RowHeight);
            row.title=DanceUi.Label(go.transform,"",14);row.format=DanceUi.Label(go.transform,"",11,70);
            row.favorite=DanceUi.Button(go.transform,"☆",()=>{owner.playerCore.playlistManager.ToggleFavorite(row.id);owner.Window.RefreshLibrary();},36);
            row.queue=DanceUi.Button(go.transform,"+",()=>{var queue=DanceSettingsHandler.Instance.data.queue;if(owner.playerCore.playlistManager.CurrentType==DancePlaylistManager.PlaylistType.Queue)queue.Remove(row.id);else if(!queue.Contains(row.id))queue.Add(row.id);DanceSettingsHandler.OnSettingChanged();owner.Window.RefreshLibrary();},32);
            DanceUi.Button(go.transform,DanceLocale.T("player.play"),()=>{owner.playerCore.PlayDanceByIndex(owner.playerCore.playlistManager.GetIndexByFile(row.id));owner.Window.RefreshLibrary();},72,true);
            return row;
        }
    }
}
