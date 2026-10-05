using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe.Editor
{
    public static class MenuRuleTests
    {
        public static void Run()
        {
            int passed=0;
            void Assert(bool value,string label){if(!value)throw new Exception("MENU_RULE_FAILED "+label);}
            void Test(string label,Action action){action();passed++;Debug.Log("MENU_RULE_PASS "+label);}
            Profile P(string id,int count=3)=>new Profile{playerId=id,nickname=id,personas=Enumerable.Range(0,count).Select(i=>new PersonaAsset{assetId=id+i,name="球",templateId="Projectile",visualId="basketball",color="#FFA555"}).ToArray()};
            Match Make(){var m=new Match("RULE",new MatchRules());m.Add(P("a"));m.Add(P("b"));m.Receive("a",new Command{sequence=0,action="start"});return m;}
            void Tick(Match m,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)m.Tick(.02f);}
            Test("2.5D start-menu sprites are imported",()=>
            {
                foreach(var asset in new[]{"background","title","mascot-open","mascot-blink","arm-camera","arm-controller","arm-book","arm-cat","arm-pizza","arm-stylus","arm-clover","arm-photo","button-solo","button-multi","button-intro","button-settings"})
                    Assert(Resources.Load<Sprite>("StartMenu2D/"+asset)!=null,"missing start-menu sprite "+asset);
            });
            Test("upper classroom facade seams are sealed",()=>
            {
                foreach(int side in new[]{-1,1})
                {
                    for(float z=-18;z<=22;z+=2)
                    {
                        Assert(CampusGeometry.Floor(new Vector3(side*30.8f,5.5f,z))>=4.59f,"wing floor gap "+side+":"+z);
                        Assert(CampusGeometry.Blocked(new Vector3(side*31.45f,5.5f,z)),"wing sill gap "+side+":"+z);
                    }
                    foreach(float z in new[]{-18f,-10f,-2f,6f,14f,22f})
                        Assert(CampusGeometry.Blocked(new Vector3(side*33.1f,5.5f,z)),"wing side gap "+side+":"+z);
                }
                for(float x=-32;x<=32;x+=2)
                    Assert(CampusGeometry.Floor(new Vector3(x,5.5f,24.65f))>=4.59f,"rear floor gap "+x);
                foreach(int side in new[]{-1,1})
                {
                    for(float x=8;x<=32;x+=2)
                        Assert(CampusGeometry.Blocked(new Vector3(side*x,5.5f,24.9f)),"rear sill gap "+side+":"+x);
                    foreach(float x in new[]{8f,16f,24f,32f})
                        Assert(CampusGeometry.Blocked(new Vector3(side*x,5.5f,26.3f)),"rear side gap "+side+":"+x);
                }
            });
            Test("battle music is imported and phase gated",()=>
            {
                var clip=Resources.Load<AudioClip>("Audio/BattleMusic");
                Assert(clip!=null&&clip.length>120,"missing or truncated battle music");
                Assert(!BattleMusic.PlaysIn(MatchPhase.Lobby)&&!BattleMusic.PlaysIn(MatchPhase.Reveal)
                    &&BattleMusic.PlaysIn(MatchPhase.Battle)&&BattleMusic.PlaysIn(MatchPhase.FinalBurst)
                    &&!BattleMusic.PlaysIn(MatchPhase.Finished),"music phase gate");
            });
            Test("direct start-menu entries and reduced motion are stable",()=>
            {
                var root=new GameObject("start-menu-test",typeof(RectTransform));
                RectTransform Rect(string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);return (RectTransform)go.transform;}
                var title=Rect("title");title.anchoredPosition=new Vector2(12,34);var hero=Rect("hero");var doodles=Rect("doodles");
                var open=Rect("open").gameObject.AddComponent<Image>();var blink=Rect("blink").gameObject.AddComponent<Image>();
                var buttons=Enumerable.Range(0,4).Select(i=>Rect("button-"+i)).ToArray();
                var stage=root.AddComponent<StartMenuStage2D>();stage.Configure(title,hero,open,blink,doodles,System.Array.Empty<RectTransform>(),buttons);
                stage.SetExpanded(true);stage.SetExpanded(true);Assert(stage.Expanded,"expanded state did not remain idempotent");
                title.anchoredPosition+=new Vector2(20,10);stage.SetMotionEnabled(false);
                Assert(!stage.MotionEnabled&&title.anchoredPosition==new Vector2(12,34),"reduced motion did not restore the rest pose");
                Assert(Mathf.Abs(Mathf.DeltaAngle(buttons[0].localEulerAngles.z,6.5f))<.01f&&
                    Mathf.Abs(Mathf.DeltaAngle(buttons[2].localEulerAngles.z,6.5f))<.01f&&
                    Mathf.Abs(Mathf.DeltaAngle(buttons[1].localEulerAngles.z,-6.5f))<.01f&&
                    Mathf.Abs(Mathf.DeltaAngle(buttons[3].localEulerAngles.z,-6.5f))<.01f,
                    "menu labels lost their inward tilt in reduced motion");
                UnityEngine.Object.DestroyImmediate(root);
            });
            Test("standard schedule and protected reveal",()=>{var m=Make();Assert(m.View("a").handAnimation==0,"hand at reveal");Tick(m,5.02f);Assert(m.Phase==MatchPhase.Battle,"reveal");m.Time=m.BattleStarted+149.9f;Tick(m,.12f);Assert(m.HandEventId==1,"first hand");m.Time=m.BattleStarted+299.9f;Tick(m,.12f);Assert(m.HandEventId==2,"second hand");m.Time=m.BattleStarted+449.9f;Tick(m,.12f);Assert(m.HandEventId==2&&m.Phase==MatchPhase.Finished,"no third hand");Assert(m.Winners.Length==2,"tie");float frozen=m.Time;Tick(m,2);Assert(m.Time==frozen,"frozen result");});
            Test("score before remaining, eliminated excluded",()=>{var m=new Match("SCORE",new MatchRules());m.Add(P("a",1));m.Add(P("b",5));m.Add(P("c",3));m.Players["c"].eliminated=true;for(int i=0;i<9;i++)m.Eaten.Add(new EatRecord{eaterId="c",instanceId="c"+i});m.Eaten.Add(new EatRecord{eaterId="a",instanceId="a0"});m.Finish("timeout");Assert(m.Winner=="a"&&m.Rankings[0].remaining==1&&m.Rankings[1].playerId=="b","score priority and eligibility");});
            Test("remaining breaks score tie including dropped life",()=>{var m=Make();m.Items.First(x=>x.ownerId=="b").state=ItemState.Consumed;m.Drop(m.Players["a"],99);m.Finish("timeout");Assert(m.Winner=="a"&&m.Rankings[0].remaining==3,"ground count");});
            Test("deadline cancels unfinished interaction",()=>{var m=Make();Tick(m,5.02f);m.Drop(m.Players["b"],1);var item=m.Items.First(x=>x.ownerId=="b"&&x.state==ItemState.Ground);item.position=m.Players["a"].position;m.Time=m.BattleStarted+449.98f;m.Interact(m.Players["a"]);Tick(m,.04f);Assert(m.Eaten.Count==0&&item.state==ItemState.Ground&&m.Players["a"].interaction=="","deadline consumption");});
            Test("solo clones assets and recreates instances",()=>{var p=P("human");var a=new LocalMatchSession(p);var b=new LocalMatchSession(p);Assert(a.Match.Players.Count==4&&a.Match.Items.Count==12,"solo counts");Assert(!a.Match.Items.Select(x=>x.id).Intersect(b.Match.Items.Select(x=>x.id)).Any(),"instance reuse");a.Match.Drop(a.Match.Players[p.playerId],99);Assert(p.personas.Length==3,"persistent mutation");});
            Test("solo supports one through six unique NPCs and preserves online cap",()=>{
                foreach(int count in new[]{1,3,4,5,6})
                {var solo=new LocalMatchSession(P("human"),npcCount:count);Assert(solo.Match.Players.Count==count+1,"missing NPC "+count);Assert(solo.Match.Items.Count==3*(count+1),"missing NPC assets");Assert(solo.Match.Items.Select(x=>x.asset.assetId).Distinct().Count()==solo.Match.Items.Count,"duplicate source asset IDs");Assert(solo.Match.Players.Values.Select(x=>x.position).Distinct().Count()==count+1,"overlapping spawn");solo.Close();}
                var rules=new MatchRules();var max=new LocalMatchSession(P("human"),rules,99);Assert(max.NpcCount==6&&rules.maxPlayers==6&&new MatchRules().maxPlayers==6,"online cap changed");max.Close();
                var min=new LocalMatchSession(P("human"),npcCount:0);Assert(min.NpcCount==1,"minimum bots");min.Close();
            });
            Test("practice NPC delays reactions and spaces item use",()=>{
                var m=Make();Tick(m,10);var a=m.Players["a"];var b=m.Players["b"];a.position=new Vector3(0,1,0);b.position=new Vector3(0,1,1.5f);
                var bot=new NpcController("b",true);float start=m.Time;
                Assert(bot.Decide(m.View("b")).action=="","instant first attack");m.Time=start+.4f;Assert(bot.Decide(m.View("b")).action=="","reaction too quick");
                m.Time=start+.56f;Assert(bot.Decide(m.View("b")).action=="use_item","bot lost attack");
                m.Time=start+2.8f;Assert(bot.Decide(m.View("b")).action!="use_item","attack spacing too short");
                m.Time=start+3.08f;Assert(bot.Decide(m.View("b")).action=="use_item","bot stopped attacking");m.Dispose();
            });
            Test("NPC approaches and attacks an idle player",()=>{var solo=new LocalMatchSession(P("human",4),npcCount:1);for(int i=0;i<2500&&solo.Match.Phase!=MatchPhase.Finished;i++)solo.Tick(.02f);Assert(solo.Match.Players["human"].lastHit>0||solo.Match.Eaten.Count>0,"NPC never attacked");});
            Test("NPC recovers its last dropped life before consuming enemy",()=>{var m=Make();Tick(m,5.02f);var npc=m.Players["b"];foreach(var item in m.Items.Where(x=>x.ownerId=="b").Skip(1))item.state=ItemState.Consumed;m.Drop(npc,99);m.Drop(m.Players["a"],1);foreach(var item in m.Items.Where(x=>x.state==ItemState.Ground))item.position=npc.position;var brain=new NpcController("b");var command=brain.Decide(m.View("b"));Assert(command.action=="interact","NPC did not interact");m.Receive("b",command);Assert(m.Item(npc.carrying).ownerId=="b","last life was not prioritized");Tick(m,3);Assert(m.Remaining("b")==1&&m.Items.Any(x=>x.ownerId=="b"&&x.state==ItemState.Equipped),"NPC recovery incomplete");});
            Test("NPC walks upstairs to reach a player",()=>{var solo=new LocalMatchSession(P("human",4),npcCount:1);Tick(solo.Match,5.02f);solo.Match.Players["human"].position=new Vector3(27,5.5f,3);var npc=solo.Match.Players.Values.First(x=>x.profile.playerId!="human");npc.position=new Vector3(18,1,-22);bool reached=false;for(int i=0;i<3500;i++){solo.Tick(.02f);if(npc.position.y>5&&Vector3.Distance(npc.position,solo.Match.Players["human"].position)<3){reached=true;break;}}Assert(reached,"NPC could not traverse the stairs");});
            Test("navigation reaches both upper stairs",()=>{foreach(int sign in new[]{-1,1}){var path=CampusNavigation.Route(new Vector3(sign*18,1,-22),new Vector3(sign*27,5.5f,3));Assert(path.Length>0&&path.Last().y>5,"upper route "+sign);}});
            Test("hand pose continuous at contact and retreat",()=>{Assert(Mathf.Abs(HandPresentation.Retreat(.6499f)-HandPresentation.Retreat(.6501f))<.001f,"contact snap");Assert(Mathf.Abs(HandPresentation.Retreat(.7999f)-HandPresentation.Retreat(.8001f))<.001f,"retreat snap");});
            Test("concurrent WebSocket handshakes produce independent hashes",()=>{
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Mirror.SimpleWeb.ServerHandshake")).First(t=>t!=null);
                var handshake=Activator.CreateInstance(type,new object[]{null,3000});
                var hash=type.GetMethod("CreateHash",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                System.Threading.Tasks.Parallel.For(0,512,i=>{var data=System.Text.Encoding.ASCII.GetBytes(i.ToString("D60"));byte[] expected;using(var sha=System.Security.Cryptography.SHA1.Create())expected=sha.ComputeHash(data);var actual=(byte[])hash.Invoke(handshake,new object[]{data});Assert(expected.SequenceEqual(actual),"concurrent handshake key corruption");});
            });
            Debug.Log("ABOUTME_MENU_TESTS_PASSED "+passed);
        }
    }
}
