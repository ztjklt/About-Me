using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    // Screen-space bubbles stay readable while their positions follow individual actors.
    public sealed class EaterRoastPresentation : MonoBehaviour
    {
        sealed class Bubble { public RectTransform root; public Text text; public string line,attempt,item; public float expires; }
        readonly Dictionary<string,Bubble> bubbles=new Dictionary<string,Bubble>();
        readonly HashSet<string> seen=new HashSet<string>();
        string matchId="";
        RectTransform canvas;
        public void Draw(Snapshot state,Dictionary<string,ToyActor> actors,Camera camera)
        {
            if(state==null||state.matchId!=matchId)
            {
                foreach(var bubble in bubbles.Values)Destroy(bubble.root.gameObject);
                bubbles.Clear();seen.Clear();matchId=state?.matchId??"";
            }
            if(state==null||camera==null)return;
            foreach(var entry in state.events??System.Array.Empty<MatchEvent>())
            {
                if(entry.kind!="eating-roast"||string.IsNullOrEmpty(entry.speakerId)||string.IsNullOrEmpty(entry.roastId)||!seen.Add(entry.roastId)||state.simulationTime-entry.spokenAt>8)continue;
                var bubble=Get(entry.speakerId);bubble.line=entry.line;bubble.attempt=entry.roastId;bubble.item=entry.itemId;bubble.expires=UnityEngine.Time.unscaledTime+8;
            }
            var occupied=new List<Rect>();
            foreach(var player in state.players)
            {
                bool eating=player.interaction=="eat";
                if(!bubbles.TryGetValue(player.id,out var bubble)&&!eating)continue;
                if(bubble==null)bubble=Get(player.id);
                bool fresh=UnityEngine.Time.unscaledTime<bubble.expires&&(!eating||bubble.item==player.carrying);
                string text=fresh?bubble.line:eating?"调侃酝酿中…":"";
                bool visible=!string.IsNullOrEmpty(text)&&actors.TryGetValue(player.id,out var actor);
                if(!visible){bubble.root.gameObject.SetActive(false);continue;}
                var point=camera.WorldToScreenPoint(actors[player.id].head.position+Vector3.up*.65f);
                // Prevent a remote/offscreen speaker from appearing to speak for the local player.
                visible=point.z>0&&point.x>=0&&point.x<=Screen.width&&point.y>=0&&point.y<=Screen.height;
                bubble.root.gameObject.SetActive(visible);if(!visible)continue;
                bubble.text.text=player.nickname+"\n"+text;
                var size=canvas.rect.size;
                var desired=new Vector2(point.x/Screen.width*size.x-size.x*.5f,point.y/Screen.height*size.y-size.y*.5f+60);
                bubble.root.anchoredPosition=PlaceBubble(desired,size,occupied);
                occupied.Add(new Rect(bubble.root.anchoredPosition-new Vector2(181,62),new Vector2(362,124)));
            }
            foreach(var id in bubbles.Keys.Where(id=>!state.players.Any(p=>p.id==id)).ToArray()){Destroy(bubbles[id].root.gameObject);bubbles.Remove(id);}
        }
        public static Vector2 PlaceBubble(Vector2 desired,Vector2 size,IList<Rect> occupied)
        {
            float minX=-size.x*.5f+182,maxX=size.x*.5f-182,minY=-size.y*.5f+220,maxY=size.y*.5f-260;
            desired=new Vector2(Mathf.Clamp(desired.x,minX,maxX),Mathf.Clamp(desired.y,minY,maxY));
            var xs=new List<float>{desired.x,minX,maxX};var ys=new List<float>{desired.y,minY,maxY};
            foreach(var rect in occupied){xs.Add(rect.xMin-181);xs.Add(rect.xMax+181);ys.Add(rect.yMin-62);ys.Add(rect.yMax+62);}
            var result=desired;float best=float.PositiveInfinity;
            foreach(float x in xs)foreach(float y in ys)
            {
                if(x<minX||x>maxX||y<minY||y>maxY)continue;
                var candidate=new Vector2(x,y);float distance=(candidate-desired).sqrMagnitude;
                if(distance>=best||occupied.Any(rect=>rect.Overlaps(new Rect(candidate-new Vector2(181,62),new Vector2(362,124)))))continue;
                result=candidate;best=distance;
            }
            return result;
        }
        Bubble Get(string id)
        {
            if(bubbles.TryGetValue(id,out var bubble))return bubble;
            if(canvas==null)
            {
                var go=new GameObject("Eater speech bubbles",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
                var component=go.GetComponent<Canvas>();component.renderMode=RenderMode.ScreenSpaceOverlay;component.sortingOrder=25;
                var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;canvas=go.GetComponent<RectTransform>();
            }
            var panel=new GameObject("Speech · "+id,typeof(RectTransform),typeof(Image),typeof(Outline));panel.transform.SetParent(canvas,false);
            var root=panel.GetComponent<RectTransform>();root.sizeDelta=new Vector2(350,112);
            var background=panel.GetComponent<Image>();background.color=ToyFactory.ColorOf("#FFF8E9");background.raycastTarget=false;
            var outline=panel.GetComponent<Outline>();outline.effectColor=ToyFactory.ColorOf("#263248");outline.effectDistance=new Vector2(2,-2);
            var label=new GameObject("Line",typeof(RectTransform),typeof(Text));label.transform.SetParent(root,false);
            var rect=label.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,8);rect.offsetMax=new Vector2(-12,-8);
            var text=label.GetComponent<Text>();text.font=Resources.Load<Font>("AboutMeFont");text.fontSize=22;text.alignment=TextAnchor.MiddleCenter;text.color=ToyFactory.ColorOf("#263248");text.supportRichText=false;text.raycastTarget=false;
            bubble=new Bubble{root=root,text=text};bubbles[id]=bubble;return bubble;
        }
    }
}
