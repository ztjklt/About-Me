using System;
using System.Collections.Generic;
using UnityEngine;
namespace AboutMe
{
    public sealed class ImpactFeedback : MonoBehaviour
    {
        sealed class Particle {public Transform t;public Vector3 origin,velocity,scale;public float born,life;}
        readonly List<Particle> particles=new List<Particle>();
        readonly Queue<GameObject> pool=new Queue<GameObject>();
        readonly Dictionary<string,int> actions=new Dictionary<string,int>();
        AudioClip lightClip,heavyClip,landClip;AudioSource audioSource;
        int lastEvent;string matchId="";float shake,shakeEnd,clock;
        public static float ShakeStrength=>PlayerPrefs.GetFloat("hitShake",.3f)*(PlayerPrefs.GetInt("reduceMotion",0)==1?0:1);
        void Awake()
        {
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.volume=.35f;
            lightClip=Sound("Soft punch",.11f,190);heavyClip=Sound("Heavy punch",.20f,85);landClip=Sound("Body landing",.22f,60);
        }
        static AudioClip Sound(string name,float seconds,float frequency)
        {
            int rate=22050;var data=new float[(int)(rate*seconds)];uint rng=117;
            for(int i=0;i<data.Length;i++){rng=1664525*rng+1013904223;float t=i/(float)rate;float envelope=Mathf.Exp(-t/seconds*7);data[i]=(.55f*Mathf.Sin(2*Mathf.PI*frequency*t*(1-t/seconds*.4f))+.2f*((rng&65535)/32768f-1))*envelope;}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void Bit(Vector3 position,Vector3 velocity,string color,float size,float life)
        {
            var go=pool.Count>0?pool.Dequeue():ToyFactory.Shape("Impact geometry",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,color);
            var collider=go.GetComponent<Collider>();if(collider)collider.enabled=false;
            go.GetComponent<Renderer>().sharedMaterial=ToyFactory.Material(color);go.SetActive(true);go.transform.position=position;go.transform.localScale=Vector3.one*size;
            particles.Add(new Particle{t=go.transform,origin=position,velocity=velocity,scale=Vector3.one*size,born=clock,life=life});
        }
        void Burst(CombatEvent e,Camera camera)
        {
            bool landed=e.kind=="landing";float strength=e.severity==0?.65f:e.severity==1?1:1.4f;
            int count=landed?10:e.severity==0?7:12;
            for(int i=0;i<count;i++)
            {
                float a=i*2*Mathf.PI/count;var radial=landed?new Vector3(Mathf.Cos(a),.20f,Mathf.Sin(a)):new Vector3(Mathf.Cos(a),Mathf.Sin(a),.3f);
                if(!landed&&e.impulse.sqrMagnitude>.01f)radial=Quaternion.LookRotation(e.impulse.normalized)*radial;
                Bit(e.point,radial*strength*(landed?2:3),landed?"#E6D7BE":i%2==0?"#FFE08D":"#FFF9EF",landed?.18f:.10f,landed?.38f:.18f+strength*.06f);
            }
            if(e.severity>0||landed)for(int i=0;i<16;i++){float a=i*Mathf.PI/8;Bit(e.point,new Vector3(Mathf.Cos(a),landed?.06f:.2f,Mathf.Sin(a))*3*strength,"#FFF9EF",.07f,.22f);}
            float distance=camera?Vector3.Distance(camera.transform.position,e.point):8;float volume=Mathf.Clamp01(12/Mathf.Max(6,distance));
            audioSource.PlayOneShot(landed?landClip:e.severity==0?lightClip:heavyClip,volume);
        }
        public void Draw(Snapshot snapshot,Dictionary<string,ToyActor> actors,Camera camera,float previewTime=-1)
        {
            clock=previewTime>=0?previewTime:Time.unscaledTime;
            if(matchId!=snapshot.matchId){matchId=snapshot.matchId;lastEvent=0;actions.Clear();foreach(var particle in particles){particle.t.gameObject.SetActive(false);pool.Enqueue(particle.t.gameObject);}particles.Clear();}
            if(snapshot.combatEvents!=null)foreach(var e in snapshot.combatEvents)
            {
                if(e.id<=lastEvent)continue;lastEvent=e.id;
                if(snapshot.simulationTime-e.time>.3f||e.kind!="hit"&&e.kind!="landing")continue;
                Burst(e,camera);
                float stop=e.severity==0?.035f:.065f;
                if(actors.TryGetValue(e.sourceId??"",out var attacker))attacker.FreezeUntil=clock+stop;
                if(actors.TryGetValue(e.targetId??"",out var victim))victim.FreezeUntil=clock+stop;
                if(e.sourceId==snapshot.you||e.targetId==snapshot.you){shake=(e.severity==0?.04f:.11f)*ShakeStrength;shakeEnd=clock+.16f;}
            }
            foreach(var p in snapshot.players)
            {
                if(actions.TryGetValue(p.id,out int id)&&id==p.actionId)continue;actions[p.id]=p.actionId;
                if((p.actionKind=="light"||p.actionKind=="heavy")&&snapshot.simulationTime-p.actionStartedAt<.15f&&actors.TryGetValue(p.id,out var a))
                    for(int i=0;i<4;i++)Bit(a.RightHandAttachment.position,a.root.transform.forward*(i+2),"#DCE8E2",.04f,.1f+i*.02f);
            }
            for(int i=particles.Count-1;i>=0;i--)
            {
                var p=particles[i];float age=clock-p.born;if(age>=p.life){p.t.gameObject.SetActive(false);pool.Enqueue(p.t.gameObject);particles.RemoveAt(i);continue;}
                p.t.position=p.origin+p.velocity*age+Vector3.down*age*age;p.t.localScale=p.scale*(1-age/p.life);p.t.Rotate(Vector3.one,Time.unscaledDeltaTime*220);
            }
            if(camera&&clock<shakeEnd)camera.transform.position+=camera.transform.right*Mathf.Sin(clock*110)*shake*(shakeEnd-clock)/.16f;
        }
    }
}
