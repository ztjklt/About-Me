#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public static class PersonaFourRuntimeAudit
    {
        static readonly (string template,string visual,string effect)[] Cases =
        {
            ("Ricochet","football-cc0","Ricochet"),
            ("Utility","gamepad-cc0","Pulse"),
            ("Melee","ukulele-cc0","Pulse"),
            ("Disc","notebook-cc0","Disc")
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RunFromCommandLine()
        {
            if(!Application.isPlaying||!Environment.GetCommandLineArgs().Contains("--persona-four-audit"))return;
            try { Run(); Application.Quit(0); }
            catch(Exception error) { Debug.LogException(error); Application.Quit(1); }
        }

        public static void Run()
        {
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            if(catalog.items.Length<60||GameNetwork.SampleProfile("audit","audit").personas.Length!=8)
                throw new Exception("PERSONA_FOUR_FAILED catalog or fixed sample count");
            foreach(var item in Cases)
            {
                var entry=catalog.items.Single(x=>x.templateId==item.template&&x.visualId==item.visual);
                if(item.visual=="notebook-cc0"&&entry.label!="纸质笔记本")
                    throw new Exception("PERSONA_FOUR_FAILED notebook display name");
                var model=PersonaVisualCatalog.Create(item.visual, null);
                try
                {
                    var meta=model?.GetComponent<PersonaVisualMeta>();
                    if(meta==null||meta.visualId!=item.visual||meta.templateId!=item.template||!meta.Grip)
                        throw new Exception("PERSONA_FOUR_FAILED visual "+item.visual);
                }
                finally { if(model){if(Application.isPlaying)UnityEngine.Object.Destroy(model);else UnityEngine.Object.DestroyImmediate(model);} }

                var asset=new PersonaAsset{assetId="new-"+item.visual,name=entry.label,templateId=item.template,visualId=item.visual,color=entry.color,source="审计用介绍事实"};
                var match=CreateMatch(asset);
                try
                {
                    var owner=match.Players["owner"];
                    var instance=match.Item(owner.equipped);
                    if(instance.asset.visualId!=item.visual||instance.state!=ItemState.Equipped)
                        throw new Exception("PERSONA_FOUR_FAILED equip "+item.visual);
                    match.Drop(owner,1);
                    if(instance.state!=ItemState.Ground||owner.equipped!="")
                        throw new Exception("PERSONA_FOUR_FAILED drop "+item.visual);
                    instance.position=owner.position;
                    match.Receive("owner",new Command{sequence=1,action="interact",itemId=instance.id});
                    Tick(match,.16f);
                    if(instance.state!=ItemState.Equipped||owner.equipped!=instance.id||match.Remaining("owner")!=1)
                        throw new Exception("PERSONA_FOUR_FAILED recover "+item.visual);
                    match.Drop(owner,1);
                    instance.position=match.Players["other"].position;
                    match.Receive("other",new Command{sequence=0,action="interact",itemId=instance.id});
                    Tick(match,.16f);
                    if(instance.state!=ItemState.Consumed||match.Eaten.Count!=1||match.Eaten[0].visual!=item.visual||match.Eaten[0].persona!=entry.label||!owner.eliminated)
                        throw new Exception("PERSONA_FOUR_FAILED consume "+item.visual);
                }
                finally { match.Dispose(); }

                var combat=CreateMatch(asset);
                try
                {
                    var attacker=combat.Players["owner"];
                    var target=combat.Players["other"];
                    attacker.position=new Vector3(0,1,0);attacker.yaw=0;
                    target.position=new Vector3(0,1,item.template=="Melee"?2.2f:4f);
                    bool HasEffect()=>combat.Effects.Any(x=>x.kind==item.effect&&(item.effect=="Pulse"||x.visual==item.visual));
                    combat.Receive("owner",new Command{sequence=1,action="use_item",yaw=0});
                    Tick(combat,.2f);
                    if(!HasEffect())
                        throw new Exception("PERSONA_FOUR_FAILED use_item effect "+item.visual);
                    if((item.template=="Melee"||item.template=="Utility")&&target.stability>=100)
                        throw new Exception("PERSONA_FOUR_FAILED use_item hit "+item.visual);
                }
                finally { combat.Dispose(); }
                Debug.Log("PERSONA_FOUR_PASS "+item.visual+" equip drop recover consume use_item");
            }
            Debug.Log("PERSONA_FOUR_CLIENT_AUDIT_PASSED four assets; fixed sample eight");
        }

        static Match CreateMatch(PersonaAsset asset)
        {
            var match=new Match("FOUR",new MatchRules{revealDuration=.02f,handInterval=10000,finalTime=20000,recoverDuration=.1f,eatDuration=.1f});
            match.Add(new Profile{playerId="owner",nickname="owner",personas=new[]{asset}});
            match.Add(new Profile{playerId="other",nickname="other",personas=new[]{new PersonaAsset{assetId="other",name="书",templateId="Melee",visualId="book",color="#AA91ED"}}});
            match.Receive("owner",new Command{sequence=0,action="start"});
            match.Tick(.02f);
            return match;
        }

        static void Tick(Match match,float duration)
        {
            for(int i=0;i<Mathf.CeilToInt(duration/.02f);i++)match.Tick(.02f);
        }
    }
}
#endif
