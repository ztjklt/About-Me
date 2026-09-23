using UnityEngine;

namespace AboutMe
{
    public sealed class HandPresentation : MonoBehaviour
    {
        LineRenderer warning;
        Snapshot lastSnapshot;
        string matchId;
        float clock, received, displayed;
        public static float Retreat(float age)
        {
            if(age<.2f)return 1;
            if(age<.65f){float t=Mathf.Clamp01((age-.2f)/.45f);return 1-t*t*(3-2*t);}
            if(age<.8f)return 0;
            float back=Mathf.Clamp01((age-.8f)/.7f);return back*back*(3-2*back);
        }
        public void Draw(Snapshot state,Transform hand,GameObject open,GameObject fist,Camera camera)
        {
            if(state==null||state.phase!=MatchPhase.Battle||state.handEventId==0){hand.gameObject.SetActive(false);if(warning)warning.gameObject.SetActive(false);return;}
            float now=Time.unscaledTime;
            if(state!=lastSnapshot)
            {
                if(matchId!=state.matchId){clock=displayed=state.simulationTime;matchId=state.matchId;}
                else clock=Mathf.Max(clock,state.simulationTime);
                received=now;lastSnapshot=state;
            }
            displayed=Mathf.Max(displayed,clock+Mathf.Min(now-received,.15f));
            float age=displayed-state.handStartedAt;
            bool active=age>=0&&age<1.5f;hand.gameObject.SetActive(active);
            if(!warning)
            {
                var go=new GameObject("Hand warning ring");warning=go.AddComponent<LineRenderer>();warning.loop=true;warning.useWorldSpace=true;warning.positionCount=64;warning.widthMultiplier=.12f;
                warning.material=new Material(Shader.Find("Sprites/Default"));warning.startColor=warning.endColor=new Color(1,.64f,.15f,.85f);
            }
            warning.gameObject.SetActive(active);if(!active)return;
            bool reduced=PlayerPrefs.GetInt("reduceMotion",0)==1;
            open.SetActive(!state.handFist);fist.SetActive(state.handFist);
            float retreat=Retreat(age);
            var away=state.handFist?new Vector3(Mathf.Sign(state.handPosition.x)*14,8,0):new Vector3(0,16,0);
            hand.position=state.handPosition+Vector3.up*.65f+away*retreat;
            float tilt=reduced?0:Mathf.Sin(Mathf.Clamp01(age/.65f)*Mathf.PI)*-13;
            hand.rotation=state.handFist?Quaternion.Euler(0,state.handPosition.x>0?90:-90,tilt):Quaternion.Euler(tilt,0,0);
            float contact=Mathf.Clamp01((age-.65f)/.25f);
            float radius=age<.65f?3.2f:3.2f+contact*1.4f;
            float floor=CampusGeometry.Floor(state.handPosition)+.035f;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;warning.SetPosition(i,new Vector3(state.handPosition.x+Mathf.Cos(a)*radius,floor,state.handPosition.z+Mathf.Sin(a)*radius));}
            float opacity=age<.65f?.8f:Mathf.Clamp01(1-contact)*.8f;warning.startColor=warning.endColor=new Color(1,.58f,.12f,opacity);
            if(!reduced&&age>=.65f&&age<.85f)camera.transform.position+=Vector3.up*(Mathf.Sin((age-.65f)*100)*.07f*(1-(age-.65f)/.2f));
        }
        void OnDestroy(){if(warning){Destroy(warning.material);Destroy(warning.gameObject);}}
    }
}
