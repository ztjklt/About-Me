using System;
using System.Linq;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class NpcPracticeTests
    {
        public static void Run()
        {
            int passed=0;
            void Check(bool ok,string label){if(!ok)throw new Exception("PRACTICE_NPC_FAILED "+label);passed++;Debug.Log("PRACTICE_NPC_PASS "+label);}
            Snapshot State(float time,float now)=>new Snapshot{phase=MatchPhase.Battle,time=time,simulationTime=now,inventory=Array.Empty<PublicPersonaInstance>(),ground=Array.Empty<PublicPersonaInstance>(),players=new[]{new PublicPlayer{id="npc",grabbing="",interaction="",publicProp="",position=new Vector3(0,1,1),remaining=4,equippedVisual="book"},new PublicPlayer{id="human",grabbing="",interaction="",publicProp="",position=new Vector3(0,1,0),remaining=4}}};
            var npc=new NpcController("npc",true);var state=State(0,10);npc.Decide(state);
            bool attacked=false;for(int i=1;i<40;i++){state=State(i*.1f,10+i*.1f);var c=npc.Decide(state);attacked|=c.action=="use_item"||c.action=="grab";}
            Check(!attacked,"four-second opening has no offensive action");
            state=State(4.1f,14.1f);Check(npc.Decide(state).action=="use_item","practice resumes attacks after opening");
            state=State(7,17);state.players[1].bodyProtectedUntil=18;Check(npc.Decide(state).action!="use_item","get-up protection prevents renewed attack");
            state=State(8,18);state.players[1].bodyMode=BodyMode.GettingUp;Check(npc.Decide(state).action!="use_item","getting-up animation prevents renewed attack");
            state=State(9,19);Check(npc.Decide(state).action=="use_item","practice remains interactive after protection ends");
            state=State(.5f,20);state.ground=new[]{new PublicPersonaInstance{id="drop",ownerId="npc",state=ItemState.Ground,position=state.players[0].position}};Check(npc.Decide(state).action=="interact","opening still permits recovering a dropped Persona");
            var normal=new NpcController("npc");normal.Decide(State(0,30));Check(normal.Decide(State(.4f,30.4f)).action=="use_item","ordinary NPC remains active without practice grace");
            var p=new Profile{playerId="human",nickname="human",personas=Enumerable.Range(0,4).Select(i=>new PersonaAsset{assetId="h"+i,name="书",visualId="book",templateId="Melee"}).ToArray()};
            var solo=new LocalMatchSession(p,new MatchRules{revealDuration=.01f});solo.Tick(.02f);Check(solo.Current.practiceOpening>3.9f,"latest solo snapshot exposes opening countdown");solo.Close();
            Debug.Log("PRACTICE_NPC_PASSED "+passed);
        }
    }
}
