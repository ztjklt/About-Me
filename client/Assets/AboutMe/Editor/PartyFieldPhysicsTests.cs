using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace AboutMe.Editor
{
    public static class PartyFieldPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Place(Match m,Player p,Vector3 position){p.position=position;p.motorHeight=position.y;p.verticalSpeed=0;p.airborne=false;p.impulse=Vector3.zero;m.BodyWorld.Reset(p);}
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","fields");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);Cache.SetValue(null,new[]{new CampusSolid{name="Ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}});
            int passed=0;var failures=new System.Collections.Generic.List<string>();
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="field"))using(var m=PartyFieldTests.Make(d.id))
                {
                    try
                    {
                        m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];Place(m,a,new Vector3(0,1,0));Place(m,b,new Vector3(10,1,0));Place(m,c,new Vector3(-10,1,0));PartyFieldTests.Tick(m,.6f);
                        if(d.id=="polarity-push")m.Props.Add(new PublicProp{id="crate",visual="box",position=new Vector3(0,.6f,4)});
                        EffectState glue=null,sticker=null;
                        if(d.id=="clean-rain")
                        {
                            Place(m,b,new Vector3(0,1,2));string source=c.equipped;
                            glue=new EffectState{id="glue",kind="PartyMechanic",mechanicId="sticky-step",sourceInstance=source,owner="p2",position=new Vector3(0,.15f,2),deadline=m.Time+5,radius=2};
                            sticker=new EffectState{id="sticker",kind="PartyMechanic",mechanicId="sticky-burst",sourceInstance=source,owner="p2",stage="attached",targetId="p1",position=b.position,deadline=m.Time+5};m.Effects.Add(glue);m.Effects.Add(sticker);
                        }
                        if(d.id=="flame-fan")Place(m,b,new Vector3(0,1,2.3f));
                        var e=PartyFieldTests.Deploy(m);PersonaInstance item=null;PublicProp prop=null;
                        if(d.id=="wet-trail"){Place(m,a,new Vector3(-4,1,0));Place(m,b,new Vector3(0,1,0));}
                        if(d.id=="sand-sink"||d.id=="sticky-step")Place(m,b,new Vector3(e.position.x,1,e.position.z));
                        if(d.id=="fan-volume")Place(m,b,new Vector3(e.position.x,1,e.position.z+1));
                        if(d.id=="fan-volume"||d.id=="loose-whirlpool"||d.id=="tractor-loose"||d.id=="item-push-wave")
                        {
                            var point=d.id=="item-push-wave"?new Vector3(2,.35f,0):e.position+Vector3.up*.2f+(d.id=="fan-volume"?Vector3.forward*2:Vector3.right*2.5f);item=PartyFieldTests.Ground(m,point);
                        }
                        if(d.id=="polarity-push")prop=m.Props.First();
                        var before=b.position;var itemBefore=item?.position??Vector3.zero;float slide=0;
                        if(d.id=="wet-trail"){PartyFieldTests.Tick(m,.24f,"p1",0,1);before=b.position;PartyFieldTests.Tick(m,.2f);slide=FlatDistance(before,b.position);Check(slide>.04f&&b.fieldStatus.Contains("湿滑"),"real body did not slide");}
                        else if(d.id=="sand-sink"){PartyFieldTests.Tick(m,1.25f);before=b.position;PartyFieldTests.Tick(m,.15f,"p1",1,0);Check(FlatDistance(before,b.position)>.02f&&FlatDistance(before,b.position)<.65f,"real sand slow absent");}
                        else if(d.id=="sticky-step"){PartyFieldTests.Tick(m,.5f,"p1",0,1);Check(e.touchTimes.ContainsKey("p1next"),"real footsteps did not trigger glue");}
                        else PartyFieldTests.Tick(m,d.id=="tractor-loose"?1.5f:.8f);
                        float peak=m.Players.Values.SelectMany(x=>m.BodyWorld.Get(x).Bodies).Max(x=>x.velocity.magnitude);
                        if(d.id=="fan-volume")Check(FlatDistance(before,b.position)>.3f&&item.position.z>itemBefore.z+.1f&&b.stability==100,"real wind did not move ground bodies/objects");
                        if(d.id=="loose-whirlpool")Check(Mathf.Abs(item.position.z-itemBefore.z)>.2f&&b.stability==100,"whirlpool did not rotate loose item");
                        if(d.id=="tractor-loose")Check(FlatDistance(item.position,e.position)<1.1f&&item.state==ItemState.Ground,"magnet failed or recovered asset");
                        if(d.id=="polarity-push")Check(prop.position.z>4.3f&&e.stage=="repelled","public prop not repelled");
                        if(d.id=="item-push-wave")Check(item.position.x>itemBefore.x+.3f&&m.Players.Values.All(x=>x.stability==100),"low wave pushed wrong objects");
                        if(d.id=="clean-rain")Check(!m.Effects.Contains(glue)&&!m.Effects.Contains(sticker)&&m.Remaining("p2")==4,"real rain did not clean safely");
                        if(d.id=="flame-fan")Check(b.stability==76&&FlatDistance(before,b.position)>.05f,"real fire damage/knockback wrong");
                        Check(peak<25&&m.View("p0").players.All(x=>x.bodyPose.positions.Length==15)&&m.Players.Keys.All(id=>m.Remaining(id)==4),"body velocity/pose/life invalid");
                        Debug.Log("FIELD_REAL_PHYSICS "+d.id+" targetTravel="+FlatDistance(before,b.position).ToString("F3")+" looseTravel="+(item!=null?FlatDistance(itemBefore,item.position):0).ToString("F3")+" slide="+slide.ToString("F3")+" peak="+peak.ToString("F3")+" stability="+b.stability);
                        m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic"),"finish retained field");passed++;
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("FIELD_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("FIELD_REAL_PHYSICS_PASSED "+passed+" mechanisms / real 15-body rigs / field counters and conserved lives");
        }
    }
}
