using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] class ProbeEventSnapshot { public int id;public string text;public MatchPhase phase;public float time; }
    [Serializable] class ProbePlayerSkin { public string playerId,characterSkin; }
    [Serializable] class ProbeSkinDefinition { public string skin=""; }
    [Serializable] class ProbeSkinCatalog { public ProbeSkinDefinition[] skins=Array.Empty<ProbeSkinDefinition>(); }
    [Serializable] class WordProbeAsset { public string assetId,instanceId,ownerId,wordCategory,wordText,abilityId,color; }
    [Serializable] class WordProbeObservation { public string instanceId,ownerId,holderId,state,abilityId;public int ownerRemaining;public float time,cooldown; }
    [Serializable] class ProbePublicFrame
    {
        public string playerId,actionKind,wordCategory,wordText,abilityId,color,instanceId,ownerId;
        public int serverTick,actionId,remaining;public float time,cooldown;public bool borrowed,modelVerified,heldWordMeshVerified;
    }
    [Serializable] class ProbeModelEvidence
    {
        public string playerId,characterSkin,resourcePath,meshName;public int vertices,bones,frames,reconnectFrames;public bool verified;
    }
    [Serializable] class WordProbeReport
    {
        public int protocolVersion,physicalFrames,players,groundWordInstances,borrowCommands,useCommands;
        public bool passed,realApiIdentity,privacyChecked,reconnectRequested,reconnectValidated;
        public string playerId,room,matchId,failure;
        public string[] identities,abilityIds,effectAbilityIds;
        public WordProbeAsset[] assets;public WordProbeObservation[] observations;public ProbePlayerSkin[] playerSkins,reconnectPlayerSkins;public ProbePublicFrame[] publicFrames;public ProbeModelEvidence[] modelInstances;
    }
    // Explicit test client, never substituted for a real person in normal play.
    public class NetworkProbe : MonoBehaviour
    {
        GameNetwork net;float next,startTime,nextInteract;bool ready,started;int seen,sequence,requiredPlayers=2,timeoutSeconds;
        string marker,eventOutput;bool host;int lastEvent;
        float nextTrace,reconnectAt;bool reconnectRequested,reconnecting,reconnected;string originalId;int physicalFrames;
        bool wordProbe,reconnectValidated,wordEquipmentChosen;string wordOutput,wordPreferredAbility;float wordNextUse,borrowReleaseAt;
        int wordUseCommands,wordBorrowCommands;bool privacyChecked;
        readonly Dictionary<string,WordProbeAsset> wordAssets=new Dictionary<string,WordProbeAsset>();
        readonly Dictionary<string,string> wordStates=new Dictionary<string,string>();
        readonly HashSet<string> wordOwners=new HashSet<string>(),wordEffects=new HashSet<string>(),groundWords=new HashSet<string>();
        readonly Dictionary<string,string> playerSkins=new Dictionary<string,string>(),reconnectSkins=new Dictionary<string,string>();
        HashSet<string> knownSkins;
        readonly Dictionary<string,ToyActor> probeActors=new Dictionary<string,ToyActor>();
        readonly Dictionary<string,ProbeModelEvidence> modelEvidence=new Dictionary<string,ProbeModelEvidence>();
        readonly List<ProbePublicFrame> publicFrames=new List<ProbePublicFrame>();int lastPublicTick=-1;Transform probeActorRoot;
        readonly List<WordProbeObservation> wordObservations=new List<WordProbeObservation>();
        void Start()
        {
            net=GetComponent<GameNetwork>();var args=Environment.GetCommandLineArgs();
            string Arg(string key,string fallback=""){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            wordProbe=args.Contains("--word-probe");wordOutput=Arg("--word-output");wordPreferredAbility=Arg("--word-preferred-ability");
            requiredPlayers=int.Parse(Arg("--players","2"));reconnectRequested=args.Contains("--reconnect");
            marker=Arg("--marker","/tmp/about-me-room");eventOutput=Arg("--event-output");host=args.Contains("--create-room");
            net.Endpoint=Arg("--endpoint","ws://127.0.0.1:7777");
            string realToken=Environment.GetEnvironmentVariable("ABOUTME_PROBE_TOKEN");
            net.Sample=string.IsNullOrEmpty(realToken);
            if(wordProbe&&(net.Sample||string.IsNullOrEmpty(wordOutput))){Debug.LogError("WORD_PROBE_REQUIRES_REAL_TOKEN_AND_OUTPUT");Application.Quit(10);return;}
            if(!int.TryParse(Arg("--probe-timeout",net.Sample?"100":"220"),out timeoutSeconds)||timeoutSeconds<30||timeoutSeconds>900)
            {Debug.LogError("PROBE_TIMEOUT_ARGUMENT_INVALID expected 30..900 seconds");Application.Quit(9);return;}
            net.Token=net.Sample?Arg("--identity",Guid.NewGuid().ToString("N")):realToken;
            net.Nickname=net.Sample?"TEST "+net.Token.Substring(0,Math.Min(5,net.Token.Length)):"TEST REAL";
            int appearanceIndex=host?0:int.TryParse(net.Token.Replace("client",""),out int parsedIndex)?parsedIndex:1;
            net.SampleAppearance=AppearanceData.Demo(appearanceIndex);net.SampleAppearance.expression=new[]{"friendly","joy","confident","sleepy","surprised","determined"}[appearanceIndex%6];net.SampleAppearance.topColor=new[]{"#79CFC5","#F3AFC2","#FFD773","#9FAFF2","#FFF9EF","#202A3D"}[appearanceIndex%6];
            net.RoomCode=host?"":File.ReadAllText(marker).Trim();startTime=UnityEngine.Time.realtimeSinceStartup;net.Connect();
        }
        void Update()
        {
            if(net==null)return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(now-startTime>timeoutSeconds){Debug.LogError("PROBE_TIMEOUT seen="+seen+" limit="+timeoutSeconds);Application.Quit(2);return;}
            if(reconnecting){if(now<reconnectAt)return;reconnecting=false;net.Connect();return;}
            var s=net.State;if(s==null)return;
            if(!string.IsNullOrEmpty(eventOutput))foreach(var evt in s.events.Where(x=>x.id>lastEvent))
            {File.AppendAllText(eventOutput,JsonUtility.ToJson(new ProbeEventSnapshot{id=evt.id,text=evt.text,phase=s.phase,time=s.time})+"\n");lastEvent=evt.id;}
            if(new[]{"source","introduction","generationCandidates","generationTerms","wordAbilities","token"}.Any(key=>JsonUtility.ToJson(s).Contains("\""+key+"\"")))
            {Debug.LogError("PROBE_PRIVATE_SOURCE_LEAK");Application.Quit(8);return;}
            if(wordProbe){
                if(!ObserveWords(s)){WriteWordReport(s,false,"WORD_PROTOCOL_OR_ASSET_INVALID");Application.Quit(10);return;}
                privacyChecked=true;
                if(reconnected&&!reconnecting)reconnectValidated=true;
            }
            if(originalId==null)originalId=s.you;
            if(s.you!=originalId){Debug.LogError("PROBE_RECONNECT_ID_CHANGED");Application.Quit(5);return;}
            if(reconnectRequested&&!reconnected&&s.phase==MatchPhase.Battle&&s.time>4){reconnected=true;reconnecting=true;reconnectAt=now+1;net.StopClient();net.State=null;Debug.Log("PROBE_RECONNECT_STARTED");return;}
            if(s.phase==MatchPhase.Battle||s.phase==MatchPhase.FinalBurst){
                foreach(var person in s.players.Where(p=>!p.eliminated)){
                    var pose=person.bodyPose;
                    if(pose==null||pose.positions==null||pose.rotations==null||pose.positions.Length!=15||pose.rotations.Length!=15||pose.positions.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)||v.magnitude>8)){
                        Debug.LogError("PROBE_PHYSICS_POSE_INVALID player="+person.id+" time="+s.time+" phase="+s.phase+" bones="+(pose?.positions?.Length??-1));Application.Quit(6);return;
                    }
                }
                physicalFrames++;
            }
            if(host&&now>=nextTrace){nextTrace=now+5;File.WriteAllText(marker+".state.json",JsonUtility.ToJson(s));Debug.Log("PROBE_STATE "+s.phase+" ground="+s.ground.Length+" eaten="+s.eaten.Length);}
            if(host&&!File.Exists(marker)){File.WriteAllText(marker,s.room);Debug.Log("PROBE_ROOM "+s.room);}
            if(!ready){net.Send("ready");ready=true;}
            if(host&&!started&&s.players.Length>=requiredPlayers){net.Send("start");started=true;}
            if(s.phase==MatchPhase.Lobby)return;
            seen++;
            if(s.phase==MatchPhase.Finished&&seen>30){if(s.winners==null||s.winners.Length==0||s.rankings.Length!=s.players.Length||s.time>s.finalAt+.05f){Debug.LogError("PROBE_RESULT_INVALID");Application.Quit(4);return;}if(net.Sample&&(s.players.Any(p=>p.appearance==null)||s.players.Select(p=>p.appearance.topColor).Distinct().Count()!=s.players.Length||s.players.Select(p=>p.appearance.expression).Distinct().Count()!=s.players.Length)){Debug.LogError("PROBE_APPEARANCE_FAILED");Application.Quit(3);return;}if(physicalFrames<10||reconnectRequested&&!reconnected){Debug.LogError("PROBE_PHYSICS_OR_RECONNECT_MISSING");Application.Quit(7);return;}if(wordProbe){bool pass=wordOwners.Count>=requiredPlayers&&wordEffects.Count>0&&wordUseCommands>0&&privacyChecked&&(!reconnectRequested||reconnectValidated);WriteWordReport(s,pass,pass?"":"WORD_EVIDENCE_INCOMPLETE");if(!pass){Debug.LogError("WORD_PROBE_EVIDENCE_INCOMPLETE");Application.Quit(10);return;}}Debug.Log("PROBE_PASS physicsFrames="+physicalFrames+" reconnect="+reconnected+" appearances="+s.players.Length+" players="+s.players.Length+" eaten="+s.eaten.Length+" phase="+s.phase);Application.Quit(0);return;}
            if(now<next)return;next=now+.06f;
            var me=s.players.FirstOrDefault(p=>p.id==s.you);if(me==null)return;if(net.Sample&&!net.SampleAppearance.SameAs(me.appearance)){Debug.LogError("PROBE_OWN_APPEARANCE_CHANGED");Application.Quit(3);return;}
            if(wordProbe){DriveWords(s,me,now);return;}
            var available=s.ground.Where(x=>x.state==ItemState.Ground&&(net.Sample||!host||x.ownerId!=s.you)).ToArray();
            if(available.Length>0)
            {
                if(!host)
                {
                    if(!net.Sample&&!me.eliminated)
                    {
                        var away=me.position-available[0].position;away.y=0;
                        if(away.magnitude<4&&away.sqrMagnitude>.01f)
                        {var retreat=away.normalized;net.Send("","",new Vector2(retreat.x,retreat.z),Mathf.Atan2(retreat.x,retreat.z)*Mathf.Rad2Deg,true);return;}
                    }
                    net.Send();return;
                }
                if(me.eliminated){net.Send();return;}
                if(me.interaction!=""){net.Send();return;}
                var item=available.OrderBy(x=>Vector3.Distance(x.position,me.position)).FirstOrDefault();
                if(item==null){net.Send();return;}
                var delta=item.position-me.position;delta.y=0;var step=delta.normalized;
                bool near=Vector3.Distance(item.position,me.position)<2.1f;
                string moveAction=near&&now>=nextInteract?"interact":near?"":(++sequence%12==0?"jump":"");
                if(moveAction=="interact")nextInteract=now+3.5f;
                net.Send(moveAction,"",near?Vector2.zero:new Vector2(step.x,step.z),Mathf.Atan2(step.x,step.z)*Mathf.Rad2Deg,!near);return;
            }
            var target=s.players.FirstOrDefault(p=>p.id!=s.you);if(target==null)return;
            var d=target.position-me.position;d.y=0;var direction=d.normalized;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            string action=(++sequence%15)==0&&(net.Sample||host)?"use_item":"";
            net.Send(action,"",d.magnitude>1.7f?new Vector2(direction.x,direction.z):Vector2.zero,yaw,d.magnitude>3);
        }
        bool ObserveWords(Snapshot s)
        {
            if(s.protocolVersion!=TraitAbilityCatalog.ProtocolVersion)return false;
            if(knownSkins==null){var catalog=Resources.Load<TextAsset>("ai-skins");if(!catalog)return false;var definitions=JsonUtility.FromJson<ProbeSkinCatalog>(catalog.text);if(definitions?.skins==null)return false;knownSkins=new HashSet<string>(definitions.skins.Select(d=>d.skin)){"normal"};}
            foreach(var player in s.players){
                if(string.IsNullOrEmpty(player.characterSkin)||!knownSkins.Contains(player.characterSkin))return false;
                if(playerSkins.TryGetValue(player.id,out var priorSkin)&&priorSkin!=player.characterSkin)return false;
                playerSkins[player.id]=player.characterSkin;
                if(reconnected&&!reconnecting)reconnectSkins[player.id]=player.characterSkin;
            }
            foreach(var item in (s.inventory??Array.Empty<PublicPersonaInstance>()).Concat(s.ground??Array.Empty<PublicPersonaInstance>()).Concat(s.revealed??Array.Empty<PublicPersonaInstance>()))
            {
                if(item.asset?.visualId!=TraitAbilityCatalog.VisualId)continue;
                var d=PartyMechanicRegistry.ForAsset(item.asset);
                if(d==null||d.id!=item.asset.abilityId||item.asset.mechanicId!=d.id||string.IsNullOrEmpty(item.asset.wordText)||string.IsNullOrEmpty(item.asset.assetId))return false;
                wordOwners.Add(item.ownerId);
                wordAssets[item.id]=new WordProbeAsset{assetId=item.asset.assetId,instanceId=item.id,ownerId=item.ownerId,wordCategory=item.asset.wordCategory,wordText=item.asset.wordText,abilityId=d.id,color=item.asset.color};
                if(item.state==ItemState.Ground)groundWords.Add(item.id);
                string state=item.state+":"+item.carrierId;
                if(!wordStates.TryGetValue(item.id,out var previous)||previous!=state){
                    wordStates[item.id]=state;
                    var owner=s.players.FirstOrDefault(p=>p.id==item.ownerId);var holder=s.players.FirstOrDefault(p=>p.id==(string.IsNullOrEmpty(item.carrierId)?item.ownerId:item.carrierId));
                    wordObservations.Add(new WordProbeObservation{instanceId=item.id,ownerId=item.ownerId,holderId=item.carrierId,state=item.state.ToString(),abilityId=d.id,ownerRemaining=owner?.remaining??-1,cooldown=holder?.mechanicCooldown??0,time=s.time});
                }
            }
            foreach(var p in s.players.Where(p=>p.equippedVisual==TraitAbilityCatalog.VisualId))if(PartyMechanicRegistry.ForPlayer(p)?.id!=p.equippedAbilityId)return false;
            foreach(var e in s.effects.Where(e=>e.visual==TraitAbilityCatalog.VisualId)){
                if(PartyMechanicRegistry.Find(e.mechanicId)==null||e.mechanicId!=e.abilityId||string.IsNullOrEmpty(e.sourceInstance))return false;
                wordEffects.Add(e.mechanicId);
            }
            return ObservePublicModels(s);
        }
        bool ObservePublicModels(Snapshot s)
        {
            if(s.serverTick==lastPublicTick)return true;lastPublicTick=s.serverTick;
            if(!probeActorRoot)probeActorRoot=new GameObject("Word probe actual public ToyActors").transform;
            foreach(var p in s.players.Where(p=>!p.eliminated)){
                if(!probeActors.TryGetValue(p.id,out var actor)){actor=new ToyActor("word-probe-"+p.id,p.color,p.outfit,probeActorRoot,p.appearance);probeActors.Add(p.id,actor);}
                actor.DrawAt(p,null,.02f,UnityEngine.Time.unscaledTime,s.simulationTime);
                bool modelVerified=actor.Appearance?.character==AISkinRegistry.CharacterForSkin(p.characterSkin);
                if(!modelEvidence.TryGetValue(p.id,out var proof)){proof=new ProbeModelEvidence{playerId=p.id,characterSkin=p.characterSkin};modelEvidence.Add(p.id,proof);}
                if(p.characterSkin!="normal"){
                    var definition=AISkinRegistry.All.FirstOrDefault(d=>d.skin==p.characterSkin);
                    if(definition==null)return false;
                    proof.resourcePath="CampusDoll/"+definition.english.Replace(" ","")+"Modular";
                    var source=Resources.Load<GameObject>(proof.resourcePath);if(!source)return false;
                    var sourceMeshes=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh).Select(r=>r.sharedMesh).ToArray();
                    var actual=actor.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.sharedMesh&&sourceMeshes.Contains(r.sharedMesh)&&r.enabled&&r.gameObject.activeInHierarchy&&r.rootBone==actor.pelvis);
                    modelVerified&=actual&&actual.sharedMesh.vertexCount>0&&actual.bones.Length>0&&actual.bones.All(b=>b&&b.IsChildOf(actor.root.transform));
                    if(actual){proof.meshName=actual.sharedMesh.name;proof.vertices=actual.sharedMesh.vertexCount;proof.bones=actual.bones.Length;}
                }
                if(!modelVerified)return false;
                proof.verified=true;proof.frames++;if(reconnected&&!reconnecting)proof.reconnectFrames++;
                var equipped=s.inventory.FirstOrDefault(i=>i.state==ItemState.Equipped&&(i.ownerId==p.id&&string.IsNullOrEmpty(i.carrierId)||i.carrierId==p.id));
                bool heldVerified=p.equippedVisual!=TraitAbilityCatalog.VisualId;
                if(p.equippedVisual==TraitAbilityCatalog.VisualId){
                    if(!ColorUtility.TryParseHtmlString(p.equippedColor,out var color))return false;
                    var metadata=actor.held?actor.held.GetComponent<ThickWordMetadata>():null;
                    var mesh=actor.held?actor.held.GetComponent<MeshFilter>():null;
                    var renderer=actor.held?actor.held.GetComponent<MeshRenderer>():null;
                    heldVerified=metadata&&metadata.Text==p.equippedWordText&&metadata.AbilityId==p.equippedAbilityId&&mesh&&mesh.sharedMesh&&mesh.sharedMesh.vertexCount>0&&mesh.sharedMesh.bounds.size.z>=ThickWordVisual.Depth*.9f&&renderer&&renderer.sharedMaterial&&Vector4.Distance(renderer.sharedMaterial.color,color)<.01f;
                    if(!heldVerified)return false;
                }
                // Public observations only: no opponent inventory or private profile/source is copied into this trace.
                publicFrames.Add(new ProbePublicFrame{playerId=p.id,serverTick=s.serverTick,time=s.time,actionId=p.actionId,actionKind=p.actionKind,remaining=p.remaining,
                    wordCategory=p.equippedWordCategory,wordText=p.equippedWordText,abilityId=p.equippedAbilityId,color=p.equippedColor,cooldown=p.mechanicCooldown,
                    instanceId=equipped?.id??"",ownerId=equipped?.ownerId??"",borrowed=equipped!=null&&equipped.ownerId!=p.id,modelVerified=modelVerified,heldWordMeshVerified=heldVerified});
            }
            return true;
        }
        void OnDestroy(){if(probeActorRoot)Destroy(probeActorRoot.gameObject);}
        void DriveWords(Snapshot s,PublicPlayer me,float now)
        {
            if(me.eliminated){net.Send();return;}
            if(!wordEquipmentChosen){
                if(me.equippedVisual==TraitAbilityCatalog.VisualId&&(string.IsNullOrEmpty(wordPreferredAbility)||me.equippedAbilityId==wordPreferredAbility))wordEquipmentChosen=true;
                else {var preferred=s.inventory.FirstOrDefault(i=>i.ownerId==s.you&&i.asset?.visualId==TraitAbilityCatalog.VisualId&&(string.IsNullOrEmpty(wordPreferredAbility)||i.asset.abilityId==wordPreferredAbility));if(preferred!=null){net.Send("equip",preferred.id);return;}}
            }
            var borrowed=s.inventory.FirstOrDefault(i=>i.state==ItemState.Equipped&&i.carrierId==s.you&&i.ownerId!=s.you&&i.asset?.visualId==TraitAbilityCatalog.VisualId);
            if(borrowed!=null&&borrowReleaseAt==0)borrowReleaseAt=now+1.2f;
            if(borrowed!=null&&now>=borrowReleaseAt){net.Send("release_word");borrowReleaseAt=0;return;}
            var loose=s.ground.Where(i=>i.state==ItemState.Ground&&i.ownerId!=s.you&&i.asset?.visualId==TraitAbilityCatalog.VisualId).OrderBy(i=>Vector3.Distance(i.position,me.position)).FirstOrDefault();
            if(loose!=null&&borrowed==null){
                var delta=loose.position-me.position;delta.y=0;var dir=delta.normalized;
                bool near=delta.magnitude<1.8f;
                if(near&&now>=nextInteract){nextInteract=now+1;wordBorrowCommands++;net.Send("borrow_word",loose.id);return;}
                if(!near){net.Send("","",new Vector2(dir.x,dir.z),Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg,true);return;}
            }
            var own=s.inventory.FirstOrDefault(i=>i.ownerId==s.you&&i.asset?.visualId==TraitAbilityCatalog.VisualId);
            if(me.equippedVisual!=TraitAbilityCatalog.VisualId&&own!=null){net.Send("equip",own.id);return;}
            var target=s.players.FirstOrDefault(p=>p.id!=s.you&&!p.eliminated);var direction=target==null?Vector3.forward:target.position-me.position;direction.y=0;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            var movement=direction.magnitude>2.8f?new Vector2(direction.normalized.x,direction.normalized.z):Vector2.zero;
            if(me.equippedVisual==TraitAbilityCatalog.VisualId&&now>=wordNextUse){wordNextUse=now+.45f;wordUseCommands++;net.Send("use_item","",movement,yaw);return;}
            // Keep input fresh, enabling holder-dependent formal abilities without introducing a synthetic effect.
            net.Send("","",movement,yaw);
        }
        void WriteWordReport(Snapshot s,bool passed,string failure)
        {
            if(string.IsNullOrEmpty(wordOutput))return;
            var r=new WordProbeReport{passed=passed,failure=failure,protocolVersion=s.protocolVersion,physicalFrames=physicalFrames,players=s.players.Length,playerId=s.you,room=s.room,matchId=s.matchId,
                realApiIdentity=!net.Sample,privacyChecked=privacyChecked,reconnectRequested=reconnectRequested,reconnectValidated=reconnectValidated,useCommands=wordUseCommands,borrowCommands=wordBorrowCommands,
                groundWordInstances=groundWords.Count,identities=s.players.Select(p=>p.id).ToArray(),assets=wordAssets.Values.OrderBy(a=>a.instanceId).ToArray(),abilityIds=wordAssets.Values.Select(a=>a.abilityId).Distinct().OrderBy(x=>x).ToArray(),effectAbilityIds=wordEffects.OrderBy(x=>x).ToArray(),observations=wordObservations.ToArray(),playerSkins=playerSkins.Select(p=>new ProbePlayerSkin{playerId=p.Key,characterSkin=p.Value}).OrderBy(p=>p.playerId).ToArray(),publicFrames=publicFrames.ToArray(),modelInstances=modelEvidence.Values.ToArray(),reconnectPlayerSkins=reconnectSkins.Select(p=>new ProbePlayerSkin{playerId=p.Key,characterSkin=p.Value}).OrderBy(p=>p.playerId).ToArray()};
            File.WriteAllText(wordOutput,JsonUtility.ToJson(r,true));
        }
    }
}
