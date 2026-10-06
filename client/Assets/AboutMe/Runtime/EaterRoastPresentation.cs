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
                string text=fresh?bubble.line:"";
                bool visible=!string.IsNullOrEmpty(text)&&actors.TryGetValue(player.id,out var actor);
                if(!visible){bubble.root.gameObject.SetActive(false);continue;}
                var point=camera.WorldToScreenPoint(actors[player.id].head.position+Vector3.up*.65f);
                // Prevent a remote/offscreen speaker from appearing to speak for the local player.
                visible=point.z>0&&point.x>=0&&point.x<=Screen.width&&point.y>=0&&point.y<=Screen.height;
                bubble.root.gameObject.SetActive(visible);if(!visible)continue;
                bubble.text.text=text;
                var size=canvas.rect.size;
                var desired=new Vector2(point.x/Screen.width*size.x-size.x*.5f,point.y/Screen.height*size.y-size.y*.5f+94);
                bool onRight=desired.x+146<size.x*.5f-132;
                desired.x+=onRight?146:-146;
                bubble.root.GetComponent<HollowSpeechBubble>().SetTailSide(!onRight);
                bubble.root.anchoredPosition=PlaceBubble(desired,size,occupied);
                occupied.Add(new Rect(bubble.root.anchoredPosition-new Vector2(132,55),new Vector2(264,110)));
            }
            foreach(var id in bubbles.Keys.Where(id=>!state.players.Any(p=>p.id==id)).ToArray()){Destroy(bubbles[id].root.gameObject);bubbles.Remove(id);}
        }
        public static Vector2 PlaceBubble(Vector2 desired,Vector2 size,IList<Rect> occupied)
        {
            float minX=-size.x*.5f+132,maxX=size.x*.5f-132,minY=-size.y*.5f+220,maxY=size.y*.5f-280;
            desired=new Vector2(Mathf.Clamp(desired.x,minX,maxX),Mathf.Clamp(desired.y,minY,maxY));
            var xs=new List<float>{desired.x,minX,maxX};var ys=new List<float>{desired.y,minY,maxY};
            foreach(var rect in occupied){xs.Add(rect.xMin-132);xs.Add(rect.xMax+132);ys.Add(rect.yMin-55);ys.Add(rect.yMax+55);}
            var result=desired;float best=float.PositiveInfinity;
            foreach(float x in xs)foreach(float y in ys)
            {
                if(x<minX||x>maxX||y<minY||y>maxY)continue;
                var candidate=new Vector2(x,y);float distance=(candidate-desired).sqrMagnitude+(y<desired.y?100000:0);
                if(distance>=best||occupied.Any(rect=>rect.Overlaps(new Rect(candidate-new Vector2(132,55),new Vector2(264,110)))))continue;
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
            var panel=new GameObject("Speech · "+id,typeof(RectTransform),typeof(HollowSpeechBubble));panel.transform.SetParent(canvas,false);
            var root=panel.GetComponent<RectTransform>();root.sizeDelta=new Vector2(250,94);
            panel.GetComponent<HollowSpeechBubble>().raycastTarget=false;
            var label=new GameObject("Line",typeof(RectTransform),typeof(Text));label.transform.SetParent(root,false);
            var rect=label.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,8);rect.offsetMax=new Vector2(-12,-8);
            var text=label.GetComponent<Text>();text.font=Resources.Load<Font>("AboutMeFont");text.fontSize=18;text.resizeTextForBestFit=true;text.resizeTextMinSize=16;text.resizeTextMaxSize=18;text.alignment=TextAnchor.MiddleCenter;text.fontStyle=FontStyle.Bold;text.color=ToyFactory.ColorOf("#263248");text.supportRichText=false;text.raycastTarget=false;
            var textOutline=label.AddComponent<Outline>();textOutline.effectColor=ToyFactory.ColorOf("#FFFDF5");textOutline.effectDistance=new Vector2(1,-1);
            bubble=new Bubble{root=root,text=text};bubbles[id]=bubble;return bubble;
        }
    }
    // Only the rounded contour and speech tail have geometry. The centre is transparent.
    public sealed class HollowSpeechBubble : MaskableGraphic
    {
        bool mirrorTail;
        public void SetTailSide(bool mirror){if(mirrorTail==mirror)return;mirrorTail=mirror;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var points=new List<Vector2>();var half=rectTransform.rect.size*.5f;float radius=17;
            var centres=new[]{new Vector2(half.x-radius,-half.y+radius),new Vector2(half.x-radius,half.y-radius),
                new Vector2(-half.x+radius,half.y-radius),new Vector2(-half.x+radius,-half.y+radius)};
            for(int corner=0;corner<4;corner++)for(int i=0;i<=8;i++)
            {float angle=(-90+corner*90+i*90f/8)*Mathf.Deg2Rad;points.Add(centres[corner]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);}
            points.Add(new Vector2(-92,-half.y));points.Add(new Vector2(-103,-half.y-14));points.Add(new Vector2(-76,-half.y));
            if(mirrorTail)for(int i=0;i<points.Count;i++)points[i]=new Vector2(-points[i].x,points[i].y);
            Stroke(vh,points,4,ToyFactory.ColorOf("#263248"));
            Stroke(vh,points,2.3f,ToyFactory.ColorOf("#B0F0D7"));
        }
        static void Stroke(VertexHelper vh,List<Vector2> points,float width,Color tint)
        {
            for(int i=0;i<points.Count;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Count];var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;int index=vh.currentVertCount;
                vh.AddVert(a-n,tint,Vector2.zero);vh.AddVert(a+n,tint,Vector2.zero);vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
            }
        }
    }

}
