using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AboutMe.Editor
{
    public static class PartyMechanicPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Input(Match m,Player p,string action="")=>m.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,action=action,yaw=p.yaw});
        static void Tick(Match m,int frames){for(int i=0;i<frames;i++){foreach(var p in m.Players.Values)Input(m,p);m.Tick(.02f);}}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","party");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);Cache.SetValue(null,new[]{new CampusSolid{name="Ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}});
            int passed=0;
            try
            {
                foreach(bool depleted in new[]{false,true})foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="blast"))using(var m=new Match("PARTY-PHYSICS",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999}))
                {
                    for(int i=0;i<3;i++)m.Add(new Profile{playerId="p"+i,nickname="p"+i,appearance=AppearanceData.Demo(i),personas=Enumerable.Range(0,4).Select(n=>new PersonaAsset{assetId="p"+i+n,name=d.label,templateId="SpecialAbility",visualId=i==0?d.visualId:"book",color="#F49A6B"}).ToArray()});
                    m.Props.Clear();m.EnableBodyPhysics();m.Phase=MatchPhase.Reveal;m.Tick(.02f);var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];
                    a.position=new Vector3(0,1,0);b.position=new Vector3(10,1,0);c.position=new Vector3(-10,1,0);a.yaw=0;b.yaw=180;c.yaw=0;m.BodyWorld.Reset(a);m.BodyWorld.Reset(b);m.BodyWorld.Reset(c);Tick(m,30);
                    bool direct=d.id=="impact-burst"||d.id=="sticky-burst"||d.id=="cone-burst"||d.id=="ring-burst";
                    if(direct){b.position=new Vector3(0,1,d.id=="ring-burst"?3:2.3f);m.BodyWorld.Reset(b);}
                    b.stability=depleted?1:100;b.lastHit=m.Time;var before=b.position;int initialImpact=b.impactId;Input(m,a,"use_item");Tick(m,Mathf.CeilToInt(d.windup/.02f)+1);
                    var e=m.Effects.Last(x=>x.kind=="PartyMechanic");
                    if(!direct)
                    {
                        int guard=0;while(e.stage=="flying"&&guard++<100)Tick(m,1);
                        if(d.id=="fuse-burst")Tick(m,25);
                        var prop=m.Props.Find(x=>x.id==e.targetId);var center=prop!=null?prop.position:e.position;
                        b.position=center+Vector3.right; b.position.y=1;b.motorHeight=1;m.BodyWorld.Reset(b);before=b.position;
                    }
                    if(d.id=="remote-burst"){Tick(m,25);Input(m,a,"use_item");}
                    float peak=0;for(int f=0;f<210;f++){Tick(m,1);peak=Mathf.Max(peak,m.BodyWorld.Get(b).Bodies.Max(x=>x.velocity.magnitude));}
                    float distance=Vector3.Distance(before,b.position);int hits=b.impactId-initialImpact;
                    Debug.Log("PARTY_REAL_PHYSICS "+d.id+" depleted="+depleted+" impacts="+hits+" bodyTravel="+distance.ToString("F3")+" peakSpeed="+peak.ToString("F3")+" dropped="+m.Items.Count(x=>x.ownerId=="p1"&&x.state==ItemState.Ground));
                    Check(hits>0&&distance>.12f,d.id+" produced no actual body knockback");Check(peak<25&&m.View("p1").players.All(x=>x.bodyPose.positions.Length==15),d.id+" body velocity or pose invalid");
                    Check(m.Items.Count(x=>x.ownerId=="p1"&&x.state==ItemState.Ground)==(depleted?1:0)&&m.Remaining("p1")==4,d.id+" duplicated Persona drop/life");passed++;
                }
            }
            finally{Cache.SetValue(null,old);}
            Debug.Log("PARTY_REAL_PHYSICS_PASSED "+passed+" cases / 10 mechanisms / real 15 body rigs / finite kick and single life drop");
        }
    }
}
