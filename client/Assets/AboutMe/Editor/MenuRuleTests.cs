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
                foreach(var asset in new[]{"background","title","mascot-open","mascot-blink","arm-camera","arm-controller","arm-book","arm-cat","arm-pizza","arm-stylus","arm-clover","arm-photo"})
                    Assert(Resources.Load<Sprite>("StartMenu2D/"+asset)!=null,"missing start-menu sprite "+asset);
            });
            Test("start-menu expansion and reduced motion are stable",()=>
            {
                var root=new GameObject("start-menu-test",typeof(RectTransform));
                RectTransform Rect(string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);return (RectTransform)go.transform;}
                var title=Rect("title");title.anchoredPosition=new Vector2(12,34);var hero=Rect("hero");var doodles=Rect("doodles");
                var open=Rect("open").gameObject.AddComponent<Image>();var blink=Rect("blink").gameObject.AddComponent<Image>();
                var sheet=Rect("sheet");var sheetGroup=sheet.gameObject.AddComponent<CanvasGroup>();var hint=Rect("hint").gameObject.AddComponent<CanvasGroup>();
                var dimmer=Rect("dimmer").gameObject.AddComponent<Image>();var buttons=Enumerable.Range(0,4).Select(i=>Rect("button-"+i)).ToArray();
                var stage=root.AddComponent<StartMenuStage2D>();stage.Configure(title,hero,open,blink,doodles,System.Array.Empty<RectTransform>(),sheet,sheetGroup,hint,dimmer,buttons);
                stage.SetExpanded(true);stage.SetExpanded(true);Assert(stage.Expanded,"expanded state did not remain idempotent");
                title.anchoredPosition+=new Vector2(20,10);stage.SetMotionEnabled(false);
                Assert(!stage.MotionEnabled&&title.anchoredPosition==new Vector2(12,34),"reduced motion did not restore the rest pose");
                UnityEngine.Object.DestroyImmediate(root);
            });
            Test("standard schedule and protected reveal",()=>{var m=Make();Assert(m.View("a").handAnimation==0,"hand at reveal");Tick(m,5.02f);Assert(m.Phase==MatchPhase.Battle,"reveal");m.Time=m.BattleStarted+149.9f;Tick(m,.12f);Assert(m.HandEventId==1,"first hand");m.Time=m.BattleStarted+299.9f;Tick(m,.12f);Assert(m.HandEventId==2,"second hand");m.Time=m.BattleStarted+449.9f;Tick(m,.12f);Assert(m.HandEventId==2&&m.Phase==MatchPhase.Finished,"no third hand");Assert(m.Winners.Length==2,"tie");float frozen=m.Time;Tick(m,2);Assert(m.Time==frozen,"frozen result");});
            Test("score before remaining, eliminated excluded",()=>{var m=new Match("SCORE",new MatchRules());m.Add(P("a",1));m.Add(P("b",5));m.Add(P("c",3));m.Players["c"].eliminated=true;for(int i=0;i<9;i++)m.Eaten.Add(new EatRecord{eaterId="c",instanceId="c"+i});m.Eaten.Add(new EatRecord{eaterId="a",instanceId="a0"});m.Finish("timeout");Assert(m.Winner=="a"&&m.Rankings[0].remaining==1&&m.Rankings[1].playerId=="b","score priority and eligibility");});
            Test("remaining breaks score tie including dropped life",()=>{var m=Make();m.Items.First(x=>x.ownerId=="b").state=ItemState.Consumed;m.Drop(m.Players["a"],99);m.Finish("timeout");Assert(m.Winner=="a"&&m.Rankings[0].remaining==3,"ground count");});
            Test("deadline cancels unfinished interaction",()=>{var m=Make();Tick(m,5.02f);m.Drop(m.Players["b"],1);var item=m.Items.First(x=>x.ownerId=="b"&&x.state==ItemState.Ground);item.position=m.Players["a"].position;m.Time=m.BattleStarted+449.98f;m.Interact(m.Players["a"]);Tick(m,.04f);Assert(m.Eaten.Count==0&&item.state==ItemState.Ground&&m.Players["a"].interaction=="","deadline consumption");});
            Test("solo clones assets and recreates instances",()=>{var p=P("human");var a=new LocalMatchSession(p);var b=new LocalMatchSession(p);Assert(a.Match.Players.Count==2&&a.Match.Items.Count==6,"solo counts");Assert(!a.Match.Items.Select(x=>x.id).Intersect(b.Match.Items.Select(x=>x.id)).Any(),"instance reuse");a.Match.Drop(a.Match.Players[p.playerId],99);Assert(p.personas.Length==3,"persistent mutation");});
            Test("NPC approaches and attacks an idle player",()=>{var solo=new LocalMatchSession(P("human",4));for(int i=0;i<2500&&solo.Match.Phase!=MatchPhase.Finished;i++)solo.Tick(.02f);Assert(solo.Match.Players["human"].lastHit>0||solo.Match.Eaten.Count>0,"NPC never attacked");});
            Test("NPC recovers its last dropped life before consuming enemy",()=>{var m=Make();Tick(m,5.02f);var npc=m.Players["b"];foreach(var item in m.Items.Where(x=>x.ownerId=="b").Skip(1))item.state=ItemState.Consumed;m.Drop(npc,99);m.Drop(m.Players["a"],1);foreach(var item in m.Items.Where(x=>x.state==ItemState.Ground))item.position=npc.position;var brain=new NpcController("b");var command=brain.Decide(m.View("b"));Assert(command.action=="interact","NPC did not interact");m.Receive("b",command);Assert(m.Item(npc.carrying).ownerId=="b","last life was not prioritized");Tick(m,3);Assert(m.Remaining("b")==1&&m.Items.Any(x=>x.ownerId=="b"&&x.state==ItemState.Equipped),"NPC recovery incomplete");});
            Test("NPC walks upstairs to reach a player",()=>{var solo=new LocalMatchSession(P("human",4));Tick(solo.Match,5.02f);solo.Match.Players["human"].position=new Vector3(27,5.5f,3);var npc=solo.Match.Players.Values.First(x=>x.profile.playerId!="human");npc.position=new Vector3(18,1,-22);bool reached=false;for(int i=0;i<3500;i++){solo.Tick(.02f);if(npc.position.y>5&&Vector3.Distance(npc.position,solo.Match.Players["human"].position)<3){reached=true;break;}}Assert(reached,"NPC could not traverse the stairs");});
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
