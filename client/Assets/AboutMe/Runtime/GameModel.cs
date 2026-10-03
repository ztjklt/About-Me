using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class PersonaAsset
    {
        public string assetId, name, templateId, visualId, color, source;
        public string emblemPath, emblemCrop, emblemSource, emblemLicense, generatedBy;
        public float generationConfidence;
        public PersonaAsset Copy() => (PersonaAsset)MemberwiseClone();
        public PublicPersonaAsset PublicCopy() => new PublicPersonaAsset
        { assetId=assetId, name=name, templateId=templateId, visualId=visualId, color=color,
          emblemPath=emblemPath, emblemCrop=emblemCrop, generatedBy=generatedBy };
    }
    // A separate wire type prevents private profile fields from being serialized.
    [Serializable] public class PublicPersonaAsset
    {
        public string assetId, name, templateId, visualId, color;
        public string emblemPath, emblemCrop, generatedBy;
    }
    [Serializable] public class Profile
    {
        public string playerId, nickname, color = "#77C8BD";
        public int outfit, revision, appearanceRevision;
        public AppearanceData appearance;
        public bool dsGirlUnlocked,claudeGirlUnlocked;
        public string characterSkin = "normal";
        public PersonaAsset[] personas = Array.Empty<PersonaAsset>();
        public GenerationTerm[] generationTerms = Array.Empty<GenerationTerm>();
    }
    [Serializable] public class CatalogEntry { public string templateId, visualId, label, color; }
    [Serializable] public class Catalog { public CatalogEntry[] items; }
    public enum MatchPhase { Lobby, Reveal, Battle, FinalBurst, Finished }
    public enum ItemState { Inventory, Equipped, Ground, Carried, Consumed, Thrown }

    [Serializable] public class MatchRules
    {
        public float moveSpeed = 5.5f, runSpeed = 7.8f, jumpSpeed = 8, gravity = 23, maxStability = 100;
        public float lightDamage = 22, heavyDamage = 48, lightInterval = .5f, heavyInterval = 1.05f;
        public float eatDuration = 2.5f, recoverDuration = 1.5f, switchDuration = .3f;
        public float revealDuration = 5, handInterval = 150, finalTime = 450;
        public float grabReach = 1.25f, grabContactDistance=1.12f, grabBreakDistance = 2.6f, interactionRange = 2.4f;
        public float grabDuration=2, grabCooldown=1.5f, escapeDuration=.7f, grabImmunity=1;
        public float grabSpring=18, grabDamping=3, grabMaxForce=16;
        public int fallDrop = 1, handDrop = 1, maxPlayers = 6;
    }
    [Serializable] public class Command
    {
        public int sequence;
        public float x, z, yaw;
        public string action = "", itemId = "";
        public float charge;
        public bool run, escapeHeld;
    }
    [Serializable] public class PersonaInstance
    {
        public string id, ownerId, carrierId = "", thrownBy = "";
        public PersonaAsset asset;
        public ItemState state;
        public Vector3 position, velocity;
        public PersonaInstance Copy() => (PersonaInstance)MemberwiseClone();
        public PublicPersonaInstance PublicCopy() => new PublicPersonaInstance
        { id=id, ownerId=ownerId, carrierId=carrierId, asset=asset?.PublicCopy(), state=state, position=position, velocity=velocity };
    }
    [Serializable] public class PublicPersonaInstance
    {
        public string id, ownerId, carrierId;
        public PublicPersonaAsset asset;
        public ItemState state;
        public Vector3 position, velocity;
    }
    public class Player
    {
        public Profile profile;
        public BodyMode bodyMode;
        public float bodyStateAt,bodyProtectedUntil,motorHeight;
        public int knockdownId,bodyGrabBone=1;
        public bool droppedThisFall,highFall,getUpFaceUp,restoreStabilityOnGetUp;
        public Vector3 getUpPosition,impactPoint,bodyGrabAnchor;
        public BodyPose bodyPose;
        public Vector3 position, impulse, velocity, tickOrigin;
        public int actionId, actionVariant, impactId;
        public string actionKind="idle", actionTarget="", pendingAction="";
        public float actionStartedAt, pendingAt, pendingCharge, nextGrab, grabImmuneUntil, grabStartedAt, grabTension, grabOverload, escapeStartedAt=-1;
        public Vector3 impactImpulse, grabberLocalAnchor, targetLocalAnchor;
        public float yaw, stability = 100, verticalSpeed, staggerUntil, nextAttack, switchUntil, lastHit, cushionUntil;
        public bool connected = true, ready, eliminated, airborne;
        public float fallOriginHeight, runBlend, locomotionSpeed;
        public bool running;
        public string equipped = "", carrying = "", grabbing = "", grabKind = "", interaction = "";
        public float interactionStart, lastInput, chargingSince = -1;
        public Command input = new Command();
        public int sequence = -1;
        public string publicProp = "";
    }
    [Serializable] public class PublicProp
    {
        public string id, visual, holder="", thrower="";
        public Vector3 position, velocity;
        public float hitAfter;
    }
    [Serializable] public class PublicPlayer
    {
        public string id, nickname, color, equippedName, equippedVisual, equippedColor, carrying, grabbing, interaction,publicProp;
        public string equippedEmblemPath, equippedEmblemCrop, equippedGeneratedBy;
        public int outfit, remaining;
        public BodyMode bodyMode;
        public float bodyStateAt,bodyProtectedUntil;
        public int knockdownId;
        public bool getUpFaceUp;
        public BodyPose bodyPose;
        public AppearanceData appearance;
        public string characterSkin;
        public Vector3 position, velocity, impactImpulse, grabAnchor;
        public int actionId, actionVariant, impactId;
        public string actionKind="idle", actionTarget="";
        public float actionStartedAt, simulationTime, grabTension, escapeProgress;
        public float yaw, stability, stagger, progress, charge, locomotionSpeed;
        public bool running, airborne;
        public bool connected, ready, eliminated, reveal, grabbed;
    }
    [Serializable] public class EffectState
    {
        public string id, owner, kind, visual, color, targetId = "";
        public string emblemPath, emblemCrop, generatedBy;
        public Vector3 position, velocity;
        public float remaining, damage, force, nextHit, radius = .7f;
        public int health, bounces;
        public bool heavy, knockdown, landed;
        public List<string> hit = new List<string>();
    }
    [Serializable] public class EatRecord { public string eater, owner, persona, visual,eaterId,ownerId,assetId,instanceId; }
    [Serializable] public class MatchEvent { public int id; public string text; }
    [Serializable] public class MatchRank { public string playerId, nickname; public int rank, eaten, remaining; public bool eliminated; }
    [Serializable] public class Snapshot
    {
        public string room, matchId, you, host, winner, endReason;
        public string[] winners = Array.Empty<string>();
        public MatchRank[] rankings = Array.Empty<MatchRank>();
        public int handEventId, serverTick, protocolVersion;
        public CombatEvent[] combatEvents = Array.Empty<CombatEvent>();
        public GrabLinkState[] grabLinks = Array.Empty<GrabLinkState>();
        public float simulationTime, handStartedAt;
        public MatchPhase phase;
        public float time, handAt, finalAt, handAnimation;
        public PublicPlayer[] players;
        public PublicPersonaInstance[] ground, inventory, revealed;
        public Vector3 handPosition;
        public bool handFist;
        public EffectState[] effects;
        public PublicProp[] props;
        public EatRecord[] eaten;
        public MatchEvent[] events;
    }

    // This is the sole owner of match state. Clients never decrement counts or transfer items.
    public partial class Match
    {
        public readonly string Room;
        public string MatchId = Guid.NewGuid().ToString("N"), Host, Winner = "";
        public string EndReason = "";
        public string[] Winners = Array.Empty<string>();
        public MatchRank[] Rankings = Array.Empty<MatchRank>();
        public int HandEventId;
        public MatchPhase Phase;
        public readonly MatchRules Rules;
        public readonly Dictionary<string, Player> Players = new Dictionary<string, Player>();
        public readonly List<PersonaInstance> Items = new List<PersonaInstance>();
        public readonly List<EffectState> Effects = new List<EffectState>();
        public readonly List<PublicProp> Props = new List<PublicProp>();
        public readonly List<EatRecord> Eaten = new List<EatRecord>();
        public readonly List<MatchEvent> Events = new List<MatchEvent>();
        public float Time, BattleStarted, LastHand = -999;
        Vector3 handPosition;
        float nextHand;
        bool handFist, handPending;
        int eventId, handIndex;
        public Match(string room, MatchRules rules)
        {
            Room = room; Rules = rules;
            Vector3[] positions={new Vector3(-18,1,-18),new Vector3(18,1,-18),new Vector3(-23,1,8),new Vector3(23,1,8),new Vector3(-17,1,-20),new Vector3(17,1,-20)};
            for(int i=0;i<positions.Length;i++)Props.Add(new PublicProp{id="prop-"+i,visual=i<4?"box":"trash",position=positions[i]});
        }
        public static Vector3 Spawn(int index) => new Vector3((index%3-1)*6,1,-22+(index/3)*4);
        static string VerifiedCharacterSkin(Profile profile) => profile!=null&&profile.dsGirlUnlocked&&profile.characterSkin=="ds-girl"?"ds-girl":profile!=null&&profile.claudeGirlUnlocked&&profile.characterSkin=="claude-girl"?"claude-girl":"normal";
        public bool Add(Profile profile)
        {
            if (Players.TryGetValue(profile.playerId, out var existing))
            { if(Phase==MatchPhase.Lobby)
                {existing.profile.appearance=AppearanceData.Resolve(profile.appearance,profile.color,profile.outfit);existing.profile.dsGirlUnlocked=profile.dsGirlUnlocked;existing.profile.claudeGirlUnlocked=profile.claudeGirlUnlocked;existing.profile.characterSkin=VerifiedCharacterSkin(profile);}
                existing.connected = true; existing.lastInput = Time; existing.sequence = -1; return true; }
            if (Phase != MatchPhase.Lobby || Players.Count >= Rules.maxPlayers || profile.personas.Length == 0) return false;
            if (Host == null) Host = profile.playerId;
            profile.appearance=AppearanceData.Resolve(profile.appearance,profile.color,profile.outfit);
            profile.characterSkin=VerifiedCharacterSkin(profile);
            Players[profile.playerId] = new Player { profile = profile, position = Spawn(Players.Count), stability = Rules.maxStability };
            foreach (var asset in profile.personas)
                Items.Add(new PersonaInstance { id = Guid.NewGuid().ToString("N"), ownerId = profile.playerId, asset = asset.Copy(), state = ItemState.Inventory });
            EquipFirst(Players[profile.playerId]);
            Say(profile.nickname + " 加入了房间");
            return true;
        }
        public void Disconnect(string id)
        {
            if (!Players.TryGetValue(id, out var p)) return;
            if (Phase == MatchPhase.Lobby)
            {
                Players.Remove(id); Items.RemoveAll(x => x.ownerId == id);
                if (Host == id) Host = Players.Keys.FirstOrDefault();
            }
            else { p.connected = false; p.input = new Command(); Interrupt(p); ReleaseAllGrabs(p); CancelAction(p); }
        }
        public int Remaining(string id) => Items.Count(x => x.ownerId == id && x.state != ItemState.Consumed);
        public PersonaInstance Item(string id) => Items.Find(x => x.id == id);
        public bool Active => Phase == MatchPhase.Battle || Phase == MatchPhase.FinalBurst;
        public void Say(string text)
        {
            Events.Add(new MatchEvent { id = ++eventId, text = text });
            if (Events.Count > 12) Events.RemoveAt(0);
        }
        public void Receive(string id, Command input)
        {
            if (!Players.TryGetValue(id, out var p) || input.sequence <= p.sequence || !p.connected) return;
            if (!Finite(input.x) || !Finite(input.z) || !Finite(input.yaw)) return;
            p.sequence = input.sequence; p.lastInput = Time;
            var movement = Vector2.ClampMagnitude(new Vector2(input.x, input.z), 1);
            input.x = movement.x; input.z = movement.y; p.input = input;
            if (input.action == "ready" && Phase == MatchPhase.Lobby) p.ready = !p.ready;
            if (input.action == "start" && Phase == MatchPhase.Lobby && Host == id && Players.Count >= 2)
            { Phase = MatchPhase.Reveal; Time = 0; LastHand = -999; HandEventId = 0; Say("你的自我，登场！展示阶段不可抢夺。"); }
            if (!Active || p.eliminated) return;
            if (input.action == "escape") input.escapeHeld=true;
            if (input.action == "escape_end") { input.escapeHeld=false; p.escapeStartedAt=-1; }
            if (input.action == "cancel") { Interrupt(p); ReleaseGrab(p); CancelAction(p); p.chargingSince = -1; p.escapeStartedAt=-1; SetAction(p,"release"); return; }
            if (Time < p.staggerUntil || BodyLocked(p)) return;
            switch (input.action)
            {
                case "jump":
                    // The physical pelvis bobs above the motor's grounded height while running.
                    // Judge takeoff from the motor path so a supported running stride can jump.
                    float motorY=BodyWorld!=null?p.motorHeight:p.position.y;
                    float ground=Floor(new Vector3(p.position.x,motorY,p.position.z))+1;
                    if(!IsGrabbed(p)&&!p.airborne&&p.verticalSpeed<=0&&Mathf.Abs(motorY-ground)<=.15f)
                    {p.verticalSpeed=Rules.jumpSpeed;SetAction(p,"jump");}
                    Interrupt(p);break;
                case "use_item": UseItem(p); break;
                case "equip": Equip(p, input.itemId); break;
                case "grab": Grab(p, false); break;
                case "grab_item": Grab(p, true); break;
                case "interact": Interact(p,input.itemId); break;
                case "drop": Interrupt(p); ReleaseGrab(p); break;
            }
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static float Floor(Vector3 p) => CampusGeometry.Floor(p);
        void EquipFirst(Player p)
        {
            var item = Items.Find(x => x.ownerId == p.profile.playerId && x.state == ItemState.Inventory);
            if (item != null) { p.equipped = item.id; item.state = ItemState.Equipped; }
        }
        void Equip(Player p, string id)
        {
            var target = Item(id);
            if (target == null || target.ownerId != p.profile.playerId || target.state != ItemState.Inventory || Time < p.switchUntil) return;
            Interrupt(p); ReleaseGrab(p); CancelAction(p); p.chargingSince = -1;
            SetAction(p,"equip");
            var current = Item(p.equipped); if (current != null) current.state = ItemState.Inventory;
            p.equipped = target.id; target.state = ItemState.Equipped; p.switchUntil = Time + Rules.switchDuration;
        }
        public void Tick(float dt)
        {
            if (dt <= 0 || dt > .1f || Phase == MatchPhase.Finished) return;
            Time += dt; serverTick++;
            if (Phase == MatchPhase.Reveal)
            {
                BodyWorld?.WarmOne(Players.Values);
                if (Time >= Rules.revealDuration)
                { Phase = MatchPhase.Battle; BattleStarted = Time; nextHand = Time + Rules.handInterval; Say("你们可以开始互相介绍了。"); BodyWorld?.PublishInitial(this); }
                return;
            }
            if (!Active) return;
            if (Time - BattleStarted + .0001f >= Rules.finalTime)
            {
                Time = BattleStarted + Rules.finalTime;
                Finish("timeout");return;
            }
            if (Phase == MatchPhase.Battle && Time + .0001f >= nextHand)
            {
                nextHand += Rules.handInterval; LastHand = Time; HandEventId++;
                var targets = Players.Values.Where(x => !x.eliminated).OrderByDescending(x => Remaining(x.profile.playerId)).ToArray();
                if (targets.Length > 0)
                { var p = targets[(handIndex++ % 3 == 2) ? (handIndex % targets.Length) : 0]; handPosition=p.position;handFist=p.position.y>4;handPending=true; Say("大手点名：" + p.profile.nickname); }
            }
            if(handPending && Time-LastHand>=.65f)
            {
                handPending=false;
                foreach(var target in Players.Values.Where(x=>!x.eliminated))
                {
                    var delta=target.position-handPosition;
                    if(new Vector2(delta.x,delta.z).magnitude>3.2f||Mathf.Abs(delta.y)>2)continue;
                    if(target.bodyMode==BodyMode.GettingUp||Time<target.bodyProtectedUntil||BodyDown(target))continue;
                    var direction=handFist?new Vector3(-Mathf.Sign(handPosition.x),0,0):new Vector3(delta.x,0,delta.z).normalized;
                    if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
                    BeginBodyFall(target,direction*8+Vector3.up*3,target.position+Vector3.up*.3f);
                    target.stability=0;target.lastHit=Time;DropBodyOnce(target);
                    CombatEvents.Add(new CombatEvent{id=++combatEventId,tick=serverTick,time=Time,kind="hit",sourceId="hand",targetId=target.profile.playerId,point=target.position,impulse=target.impactImpulse,severity=2});
                }
            }
            if(BodyWorld!=null)foreach(var player in Players.Values)if(!player.eliminated)BodyWorld.Get(player);
            foreach(var player in Players.Values)player.tickOrigin=player.position;
            TickCombat(dt);
            foreach (var p in Players.Values)
            {
                if (p.eliminated) continue;
                if (Time - p.lastInput > .35f) { p.input.x = p.input.z = 0; p.input.run=false; p.input.escapeHeld=false; p.chargingSince = -1; }
                BodyTickState(p);
                bool stunned = Time < p.staggerUntil || BodyLocked(p);
                bool grabbed=Players.Values.Any(other=>other.grabbing==p.profile.playerId);
                p.running=p.connected&&!stunned&&!grabbed&&p.interaction==""&&p.grabbing==""&&p.chargingSince<0&&p.input.run&&(p.input.x*p.input.x+p.input.z*p.input.z)>.01f;
                p.runBlend=stunned||grabbed||p.interaction!=""||p.grabbing!=""||p.chargingSince>=0?0:Mathf.MoveTowards(p.runBlend,p.running?1:0,dt/.18f);
                var desired = !stunned && p.interaction == "" ? new Vector3(p.input.x,0,p.input.z) * Mathf.Lerp(Rules.moveSpeed,Rules.runSpeed,p.runBlend) : Vector3.zero;
                if (p.chargingSince >= 0) desired *= .45f;
                if (p.grabbing!="") desired *= .45f;
                else if (grabbed) desired *= .55f;
                if (!stunned) p.yaw = p.input.yaw;
                if(p.position.y< -8)BodyRespawn(p,Players.Keys.ToList().IndexOf(p.profile.playerId));
                if(!(BodyWorld!=null&&BodyLocked(p)))
                {
                    var old=p.position;if(BodyWorld!=null)old.y=p.motorHeight;var next=old+(desired+(BodyWorld==null?p.impulse:Vector3.zero))*dt;
                    float oldSurface=Floor(old);
                    if(Mathf.Abs(old.y-(oldSurface+1))<.10f&&p.verticalSpeed<=0){p.fallOriginHeight=oldSurface;p.airborne=false;}
                    next=CampusGeometry.Move(old,next);if(BlockedByBarricade(next))next=old;
                    var travelled=next-old;travelled.y=0;p.locomotionSpeed=dt>0?Mathf.Min(desired.magnitude,Mathf.Max(0,Vector3.Dot(travelled,desired.normalized)/dt)):0;
                    p.verticalSpeed-=Rules.gravity*dt;next.y+=p.verticalSpeed*dt;
                    float surface=Floor(next),floor=surface+1;
                    if(BodyWorld!=null&&desired.sqrMagnitude>.01f&&!BodyLocked(p)&&p.verticalSpeed<=0)
                    {
                        var probe=next+desired.normalized*.55f;
                        foreach(var step in CampusGeometry.Solids)
                        {
                            if(!step.name.StartsWith("stair_"))continue;var lo=step.center-step.size*.5f;var hi=step.center+step.size*.5f;
                            if(probe.x>=lo.x&&probe.x<=hi.x&&probe.z>=lo.z&&probe.z<=hi.z&&hi.y>surface&&hi.y-surface<=.55f)
                            {floor=Mathf.Max(floor,hi.y+1.08f);}
                        }
                    }
                    if(p.airborne&&p.fallOriginHeight-surface>2&&p.fallOriginHeight-(next.y-1)>.6f&&!BodyLocked(p))
                        BeginBodyFall(p,Vector3.zero,p.position,true);
                    if(next.y<=floor&&p.verticalSpeed<=0)
                    {
                        next.y=floor;
                        if(p.airborne&&p.fallOriginHeight-surface>2&&!BodyLocked(p))BeginBodyFall(p,Vector3.zero,p.position,true);
                        if(BodyWorld==null&&p.bodyMode==BodyMode.Falling)BodyLanded(p,Mathf.Abs(p.verticalSpeed));
                        p.airborne=false;p.fallOriginHeight=surface;p.verticalSpeed=0;
                    }
                    else p.airborne=true;
                    if(!(BodyWorld!=null&&BodyLocked(p))){p.position=next;p.motorHeight=next.y;}
                    p.impulse=Vector3.Lerp(p.impulse,Vector3.zero,dt*5);
                }
                else {p.locomotionSpeed=0;p.impulse=Vector3.zero;}
                if(Time-p.lastHit>2.5f&&!BodyLocked(p))p.stability=Mathf.Min(Rules.maxStability,p.stability+dt*18);

                if (p.carrying != "")
                {
                    var carry = Item(p.carrying);
                    if (carry != null) carry.position = p.position + Forward(p)*.8f + Vector3.up*.45f;
                    if (p.interaction != "" && carry != null)
                    {
                        float duration = p.interaction == "recover" ? Rules.recoverDuration : Rules.eatDuration;
                        if (Time - p.interactionStart >= duration) { CompleteInteraction(p, carry); if(Phase==MatchPhase.Finished)return; }
                    }
                }
            }
            if(BodyWorld!=null)BodyWorld.Step(this,dt);
            // Legacy pure-rule fixtures retain analytic separation; runtime uses real body contacts.
            var living = Players.Values.Where(x=>!x.eliminated).OrderBy(x=>x.profile.playerId,StringComparer.Ordinal).ToArray();
            if(BodyWorld==null)for (int i=0;i<living.Length;i++) for(int j=i+1;j<living.Length;j++)
            {
                var a=living[i]; var b=living[j]; var d=b.position-a.position; d.y=0;
                if(d.sqrMagnitude < 1.21f && Mathf.Abs(a.position.y-b.position.y)<1.6f)
                { var n=d.sqrMagnitude<.001f?Vector3.right:d.normalized; float push=(1.1f-d.magnitude)*.5f; var aNext=CampusGeometry.Move(a.position,a.position-n*push);var bNext=CampusGeometry.Move(b.position,b.position+n*push);if(!BlockedByBarricade(aNext))a.position=aNext;if(!BlockedByBarricade(bNext))b.position=bNext; }
            }
            if(BodyWorld==null)foreach(var player in Players.Values)player.velocity=Vector3.ClampMagnitude((player.position-player.tickOrigin)/dt,30);
            foreach (var item in Items.Where(x => x.state == ItemState.Ground))
            {
                item.velocity += Vector3.down * Rules.gravity * dt; item.position = CampusGeometry.Move(item.position,item.position+item.velocity*dt,.15f,.35f);
                float ground = CampusGeometry.Floor(item.position,.35f) + .35f;
                if(item.position.y < ground) { item.position.y=ground; item.velocity=new Vector3(item.velocity.x*.9f,Mathf.Abs(item.velocity.y)*.25f,item.velocity.z*.9f); if(item.velocity.magnitude<.5f)item.velocity=Vector3.zero; }
                if(CampusGeometry.Outside(item.position))
                { item.position = new Vector3(Mathf.Clamp(item.position.x,-11,11),2,Mathf.Clamp(item.position.z,-11,11)); item.velocity=Vector3.zero; }
            }
            TickThrownPersonas(dt);
            TickEffects(dt);
            foreach(var prop in Props)
            {
                if(prop.holder!=""&&Players.TryGetValue(prop.holder,out var holder)) {prop.position=holder.position+Forward(holder);prop.velocity=Vector3.zero;continue;}
                prop.velocity+=Vector3.down*Rules.gravity*dt;prop.position=CampusGeometry.Move(prop.position,prop.position+prop.velocity*dt,.35f,.6f);
                if(prop.position.y<CampusGeometry.Floor(prop.position,.6f)+.6f){prop.position.y=CampusGeometry.Floor(prop.position,.6f)+.6f;prop.velocity=new Vector3(prop.velocity.x*.96f,0,prop.velocity.z*.96f);}
                if(prop.position.y< -6){prop.position=new Vector3(0,1,-6);prop.velocity=Vector3.zero;}
                if(prop.velocity.magnitude>4&&Time>prop.hitAfter&&Players.TryGetValue(prop.thrower,out var thrower))
                foreach(var p in Players.Values.Where(x=>x!=thrower&&!x.eliminated).ToArray())
                if(Vector3.Distance(p.position,prop.position)<1.5f){Hit(p,thrower,12,4,true);prop.hitAfter=Time+.8f;prop.velocity*=.3f;break;}
            }
        }
        void ReleaseAllGrabs(Player target)
        {
            ReleaseGrab(target);
            foreach(var other in Players.Values.Where(x=>x.grabbing==target.profile.playerId).ToArray())ReleaseGrab(other);
        }
        static Vector3 Forward(Player p) => Quaternion.Euler(0,p.yaw,0)*Vector3.forward;
        static bool PhysicalThrow(PersonaAsset asset) => asset!=null &&
            (asset.templateId=="Projectile" || asset.visualId=="football-cc0");
        void ResolveAttack(Player p, bool heavy, float charge)
        {
            var equipped=Item(p.equipped); string template=equipped?.asset.templateId ?? "Unarmed";
            p.nextAttack=Time+(heavy?Rules.heavyInterval:Rules.lightInterval);
            var carriedProp=Props.Find(x=>x.id==p.publicProp);
            if(carriedProp!=null)
            {
                if(heavy){carriedProp.holder="";carriedProp.thrower=p.profile.playerId;carriedProp.velocity=Forward(p)*12+Vector3.up*3;p.publicProp="";}
                else foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated&&Vector3.Distance(x.position,p.position)<2.3f).ToArray())Hit(target,p,12,2,false);
                return;
            }
            float damage=(heavy?Rules.heavyDamage*(.65f+.65f*charge):Rules.lightDamage)*(equipped==null?.4f:1);
            float force=heavy?6+charge*3:4.5f;
            if(heavy && PhysicalThrow(equipped?.asset))
            {
                // The equipped instance itself travels and remains a recoverable life.
                equipped.state=ItemState.Thrown;equipped.thrownBy=p.profile.playerId;
                equipped.position=p.position+Forward(p)*.8f+Vector3.up*.25f;
                equipped.velocity=Forward(p)*14+Vector3.up*3.5f;
                p.equipped="";
                return;
            }
            // The single use_item input always supplies full internal charge. Only these
            // two force-focused templates knock down on a non-depleting hit; the rest
            // still knock down and drop one Persona when stability reaches zero.
            bool knockdown=heavy&&(template=="SpecialAbility"||template=="HeavyWeapon");
            if(heavy && template=="Tether")
            {
                var target=Players.Values.Where(x=>x!=p&&!x.eliminated&&Vector3.Distance(x.position,p.position)<=6f)
                    .Where(x=>Vector3.Dot(Forward(p),(x.position-p.position).normalized)>.25f)
                    .OrderBy(x=>Vector3.Distance(x.position,p.position)).FirstOrDefault();
                if(target!=null&&Time>=target.bodyProtectedUntil&&target.bodyMode!=BodyMode.GettingUp)
                {
                    var pull=p.position-target.position;pull.y=0;
                    if(BodyWorld!=null)BodyWorld.Impact(target,pull.normalized*5,target.position,false);else target.impulse+=pull.sqrMagnitude<.01f?Vector3.zero:pull.normalized*9;
                    Interrupt(target);ReleaseAllGrabs(target);CancelAction(target);RecordImpact(target,p,pull.normalized*9);
                    if(!BodyDown(target))target.staggerUntil=Mathf.Max(target.staggerUntil,Time+.28f);
                    Effects.Add(new EffectState{id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,targetId=target.profile.playerId,kind="Tether",visual=equipped.asset.visualId,color=equipped.asset.color,position=(p.position+target.position)*.5f,remaining=.22f});
                }
                p.nextAttack=Time+1.35f;return;
            }
            if(heavy && template=="Cushion")
            {
                p.cushionUntil=Time+1.15f;
                Effects.Add(new EffectState{id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,kind="Cushion",visual=equipped.asset.visualId,color=equipped.asset.color,position=p.position,remaining=1.15f,radius=1.05f});
                p.nextAttack=Time+1.4f;return;
            }
            if(heavy && template=="Barricade")
            {
                var spot=CampusGeometry.SafeDrop(p.position+Forward(p)*1.65f);spot.y=CampusGeometry.Floor(spot,.5f)+.5f;
                Effects.Add(new EffectState{id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,kind="Barricade",visual=equipped.asset.visualId,color=equipped.asset.color,position=spot,remaining=7,health=2,radius=.85f});
                p.nextAttack=Time+2f;return;
            }
            if(heavy && (template=="Projectile"||template=="Disc"||template=="Deployable"||template=="Summon"||template=="Ricochet"))
            {
                bool schoolDisc=equipped!=null&&equipped.asset.visualId=="school-disc";
                Effects.Add(new EffectState { id=Guid.NewGuid().ToString("N"), owner=p.profile.playerId, kind=schoolDisc?"ThrownDisc":template,
                    visual=equipped.asset.visualId,color=equipped.asset.color,position=p.position+Forward(p),
                    emblemPath=equipped.asset.emblemPath,emblemCrop=equipped.asset.emblemCrop,generatedBy=equipped.asset.generatedBy,
                    velocity=schoolDisc?Forward(p)*18+Vector3.up*2.2f:Forward(p)*(template=="Disc"?14:12),
                    remaining=schoolDisc?6:template=="Deployable"?8:template=="Summon"?7:template=="Ricochet"?3:2,
                    damage=damage,force=force,heavy=true,knockdown=knockdown,bounces=template=="Ricochet"?2:0 });
                return;
            }
            float reach=heavy?2.8f:2.2f;
            if(heavy && template=="Utility") { reach=5; damage=28; force=2; p.nextAttack=Time+5; }
            if(heavy && template=="SpecialAbility") { reach=4.5f; force=15; p.nextAttack=Time+7; }
            if(template=="HeavyWeapon") { damage*=1.25f; force*=1.2f; p.nextAttack+=.3f; }
            Effects.Add(new EffectState {id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,kind="Pulse",color=equipped?.asset.color??"#FFFFFF",position=p.position+Forward(p),remaining=.25f,damage=reach});
            if(heavy)DamageBarricades(p,p.position+Forward(p)*reach,reach);
            foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated).ToArray())
            {
                var delta=target.position-p.position;
                if(delta.magnitude<=reach && CampusGeometry.ClearSegment(p.position+Vector3.up*.25f,target.position+Vector3.up*.25f) && (template=="SpecialAbility" || Vector3.Dot(Forward(p),delta.normalized)>.1f)) Hit(target,p,damage,force,heavy,knockdown);
            }
        }
        public void Hit(Player target,Player attacker,float damage,float force,bool heavy,bool knockdown=false,bool keepDepletedUntilGetUp=false)
        {
            if(!Active||target.eliminated||target.bodyMode==BodyMode.GettingUp||Time<target.bodyProtectedUntil)return;
            var direction=target.position-attacker.position;direction.y=0;if(direction.sqrMagnitude<.001f)direction=Forward(attacker);direction.Normalize();
            var point=BodyWorld==null?target.position+Vector3.up*.2f:BodyWorld.Contact(target,attacker.position+Vector3.up*.2f);
            if(BodyDown(target))
            {if(BodyWorld!=null)BodyWorld.Impact(target,direction*.7f,point,false);else target.impulse+=direction*.7f;return;}
            if(heavy&&target.cushionUntil>Time)
            {target.cushionUntil=0;Hit(attacker,target,0,6,false);Say(target.profile.nickname+" 用抱枕弹开了 "+attacker.profile.nickname+"！");return;}
            Interrupt(target);ReleaseAllGrabs(target);CancelAction(target);target.chargingSince=-1;
            target.stability=Mathf.Max(0,target.stability-damage);target.lastHit=Time;
            bool depleted=target.stability<=0;bool falls=knockdown||depleted;
            var kick=direction*(falls?7:heavy?11.5f:5.8f)+(falls?Vector3.up*3:Vector3.zero);
            if(falls)BeginBodyFall(target,kick,point);
            else
            {
                target.bodyMode=BodyMode.Stagger;target.bodyStateAt=Time;target.staggerUntil=Time+(heavy?.38f:.18f);
                target.impactPoint=point;RecordImpact(target,attacker,kick);
                if(BodyWorld!=null)BodyWorld.Impact(target,kick,point,false);else target.impulse+=kick;
            }
            if(depleted)
            {
                DropBodyOnce(target);
                target.restoreStabilityOnGetUp=keepDepletedUntilGetUp;
                target.stability=keepDepletedUntilGetUp?0:Rules.maxStability*.65f;
            }
            if(falls)CombatEvents.Add(new CombatEvent{id=++combatEventId,tick=serverTick,time=Time,kind="hit",sourceId=attacker.profile.playerId,targetId=target.profile.playerId,point=point,impulse=kick,severity=2,impactId=target.impactId});
            else if(CombatEvents.Count>0){var e=CombatEvents[CombatEvents.Count-1];e.point=point;e.severity=heavy?1:0;}
        }
        void DamageBarricades(Player attacker,Vector3 point,float reach)
        {
            foreach(var barrier in Effects.Where(x=>x.kind=="Barricade"&&x.owner!=attacker.profile.playerId&&Vector3.Distance(x.position,point)<=reach+x.radius).ToArray())
            {
                barrier.health--;if(barrier.health<=0){Effects.Remove(barrier);Say(attacker.profile.nickname+" 打碎了掩体！");}
            }
        }
        bool BlockedByBarricade(Vector3 point)=>Effects.Any(x=>x.kind=="Barricade"&&Vector3.Distance(x.position,point)<x.radius+.38f);
        void TickThrownPersonas(float dt)
        {
            foreach(var item in Items.Where(x=>x.state==ItemState.Thrown).ToArray())
            {
                var from=item.position;
                var velocity=item.velocity+Vector3.down*Rules.gravity*dt;
                var wanted=from+velocity*dt;
                var next=CampusGeometry.Move(from,wanted,.2f,.35f);
                bool wall=(new Vector2(next.x-wanted.x,next.z-wanted.z)).sqrMagnitude>.0001f;
                var target=Players.Values.Where(x=>x.profile.playerId!=item.thrownBy&&!x.eliminated)
                    .Where(x=>DistanceToSegment(x.position,from,next)<1.1f&&CampusGeometry.ClearSegment(from,x.position))
                    .OrderBy(x=>DistanceToSegment(x.position,from,next)).FirstOrDefault();
                item.position=next;
                if(target!=null && Players.TryGetValue(item.thrownBy,out var thrower))
                {
                    // Depletion goes through Hit, preserving the existing single-drop and
                    // get-up protection rules rather than making a second damage path.
                    Hit(target,thrower,Mathf.Max(Rules.maxStability,target.stability),11,true,false,true);
                    item.state=ItemState.Ground;item.thrownBy="";
                    item.velocity=velocity*.18f+Vector3.up*1.2f;
                    continue;
                }
                float floor=CampusGeometry.Floor(next,.35f)+.35f;
                if(wall||next.y<=floor||CampusGeometry.Outside(next))
                {
                    item.state=ItemState.Ground;item.thrownBy="";
                    if(CampusGeometry.Outside(next))
                    {item.position=CampusGeometry.SafeDrop(next);item.velocity=Vector3.zero;}
                    else
                    {item.position.y=Mathf.Max(next.y,floor);item.velocity=new Vector3(velocity.x*.3f,Mathf.Max(0,-velocity.y)*.2f,velocity.z*.3f);}
                }
                else item.velocity=velocity;
            }
        }
        void TickEffects(float dt)
        {
            foreach(var e in Effects.ToArray())
            {
                e.remaining-=dt;
                if(e.remaining<=0) { Effects.Remove(e); continue; }
                if(e.kind=="Pulse")continue;
                if(!Players.TryGetValue(e.owner,out var owner))continue;
                if(e.kind=="Tether"||e.kind=="Cushion")
                {
                    if(e.kind=="Cushion")e.position=owner.position;
                    continue;
                }
                if(e.kind=="Barricade")
                {
                    foreach(var player in Players.Values.Where(x=>!x.eliminated))
                    {
                        var push=player.position-e.position;push.y=0;
                        if(push.sqrMagnitude<e.radius*e.radius){var repel=(push.sqrMagnitude<.01f?Vector3.right:push.normalized)*4*dt;if(BodyWorld!=null)BodyWorld.Impact(player,repel,player.position,false);else player.impulse+=repel;}
                    }
                    continue;
                }
                if(e.kind=="Summon")
                {
                    var target=Players.Values.Where(p=>p!=owner&&!p.eliminated).OrderBy(p=>Vector3.Distance(p.position,e.position)).FirstOrDefault();
                    if(target!=null)e.velocity=(target.position-e.position).normalized*6;
                }
                if(e.kind=="ThrownDisc")
                {
                    if(e.landed)continue;
                    var discNext=e.position+e.velocity*dt;
                    e.velocity+=Vector3.down*Rules.gravity*dt;
                    var hit=Players.Values.Where(p=>p!=owner&&!p.eliminated)
                        .FirstOrDefault(p=>Vector3.Distance(discNext,p.position)<1.25f);
                    if(hit!=null)
                    {
                        e.position=discNext;
                        Hit(hit,owner,e.damage,e.force,e.heavy,e.knockdown);
                        e.hit.Add(hit.profile.playerId);
                        LandThrownDisc(e,e.position);
                        continue;
                    }
                    if(CampusGeometry.Outside(discNext)||CampusGeometry.Blocked(discNext,.2f,.25f))
                    {
                        LandThrownDisc(e,e.position);
                        continue;
                    }
                    e.position=discNext;
                    var floor=CampusGeometry.Floor(e.position,.08f)+.08f;
                    if(e.position.y<=floor)LandThrownDisc(e,e.position);
                    continue;
                }
                if(e.kind=="Disc" && e.remaining<.8f)e.velocity=(owner.position-e.position).normalized*15;
                if(e.kind=="Deployable") { e.velocity*=Mathf.Max(0,1-dt*5); e.position.y=.5f; }
                var next=e.position+e.velocity*dt;
                if(e.kind=="Ricochet"&&(CampusGeometry.Outside(next)||CampusGeometry.Blocked(next,.2f,.5f)))
                {
                    if(e.bounces--<=0){Effects.Remove(e);continue;}
                    if(Mathf.Abs(e.velocity.x)>=Mathf.Abs(e.velocity.z))e.velocity.x=-e.velocity.x;else e.velocity.z=-e.velocity.z;
                    next=e.position+e.velocity*dt;
                }
                e.position=next;
                foreach(var p in Players.Values.Where(p=>p!=owner&&!p.eliminated).ToArray())
                {
                    if(Vector3.Distance(e.position,p.position)<1.25f && (e.kind=="Summon"?Time>=e.nextHit:!e.hit.Contains(p.profile.playerId)))
                    {
                        Hit(p,owner,e.damage,e.force,e.heavy,e.knockdown); e.hit.Add(p.profile.playerId); e.nextHit=Time+1.4f;
                        if(e.kind=="Projectile")e.remaining=0;
                    }
                }
            }
        }
        static void LandThrownDisc(EffectState effect,Vector3 position)
        {
            effect.position=position;
            effect.position.y=CampusGeometry.Floor(position,.08f)+.08f;
            effect.velocity=Vector3.zero;
            effect.landed=true;
            effect.remaining=6;
        }
        public void Drop(Player p,int count)
        {
            var candidates=Items.Where(x=>x.ownerId==p.profile.playerId&&(x.state==ItemState.Inventory||x.state==ItemState.Equipped))
                .OrderByDescending(x=>x.state==ItemState.Equipped).Take(count).ToArray();
            for(int i=0;i<candidates.Length;i++)
            {
                var item=candidates[i]; item.state=ItemState.Ground; item.carrierId="";
                var safe=CampusGeometry.SafeDrop(p.position);
                item.position=safe+Vector3.up*.4f;
                item.velocity=Quaternion.Euler(0,i*137+Time*17,0)*new Vector3(3,5,0);
                if(p.equipped==item.id)p.equipped="";
            }
        }
        public void Interact(Player p,string targetId=null)
        {
            if(p.interaction!="") { Interrupt(p); return; }
            if(IsGrabbed(p)) return;
            ReleaseGrab(p); CancelAction(p); p.chargingSince=-1;
            var item=Items.Where(x=>x.state==ItemState.Ground&&(string.IsNullOrEmpty(targetId)||x.id==targetId)&&Vector3.Distance(x.position,p.position)<Rules.interactionRange)
                .OrderBy(x=>Vector3.Distance(x.position,p.position)).FirstOrDefault();
            if(item==null)return;
            item.state=ItemState.Carried;item.carrierId=p.profile.playerId;item.velocity=Vector3.zero;
            p.carrying=item.id;p.interaction=item.ownerId==p.profile.playerId?"recover":"eat";p.interactionStart=Time; SetAction(p,p.interaction,item.id);
        }
        public void Interrupt(Player p)
        {
            var item=Item(p.carrying);
            if(item!=null && item.state==ItemState.Carried && item.carrierId==p.profile.playerId)
            { item.state=ItemState.Ground;item.carrierId="";item.position=p.position+Forward(p);item.velocity=Vector3.up*2; }
            p.carrying="";p.interaction="";
        }
        void CompleteInteraction(Player p,PersonaInstance item)
        {
            if(!Active)return;
            if(item.state!=ItemState.Carried||item.carrierId!=p.profile.playerId)return;
            if(item.ownerId==p.profile.playerId)
            { item.state=ItemState.Inventory; Say(p.profile.nickname+" 找回了「"+item.asset.name+"」"); }
            else
            {
                item.state=ItemState.Consumed;
                var original=Players[item.ownerId];
                Eaten.Add(new EatRecord {eater=p.profile.nickname,owner=original.profile.nickname,persona=item.asset.name,visual=item.asset.visualId,eaterId=p.profile.playerId,ownerId=item.ownerId,assetId=item.asset.assetId,instanceId=item.id});
                Say(p.profile.nickname+" 吃掉了 "+original.profile.nickname+" 的「"+item.asset.name+"」！");
                if(Remaining(item.ownerId)==0) { original.eliminated=true;Interrupt(original);ReleaseAllGrabs(original);CancelAction(original);Say(original.profile.nickname+" 失去了全部自我"); }
            }
            item.carrierId="";p.carrying="";p.interaction="";
            if(p.equipped=="")EquipFirst(p);
            var survivors=Players.Values.Where(x=>!x.eliminated).ToArray();
            if(survivors.Length==1 && Players.Count>=2) { Finish("last_survivor"); }
        }
        public void Finish(string reason)
        {
            if(Phase==MatchPhase.Finished)return;
            foreach(var p in Players.Values){Interrupt(p);ReleaseAllGrabs(p);CancelAction(p);p.input=new Command();p.chargingSince=-1;p.velocity=Vector3.zero;}
            handPending=false;LastHand=-999;EndReason=reason;Phase=MatchPhase.Finished;
            Rankings=Players.Values.Select(p=>new MatchRank{playerId=p.profile.playerId,nickname=p.profile.nickname,
                eaten=Eaten.Count(e=>e.eaterId==p.profile.playerId),remaining=Remaining(p.profile.playerId),eliminated=p.eliminated})
                .OrderBy(r=>r.eliminated).ThenByDescending(r=>r.eaten).ThenByDescending(r=>r.remaining).ThenBy(r=>r.playerId,StringComparer.Ordinal).ToArray();
            for(int i=0;i<Rankings.Length;i++)
                Rankings[i].rank=i>0&&Rankings[i].eliminated==Rankings[i-1].eliminated&&Rankings[i].eaten==Rankings[i-1].eaten&&Rankings[i].remaining==Rankings[i-1].remaining?Rankings[i-1].rank:i+1;
            Winners=Rankings.Where(r=>!r.eliminated&&r.rank==1).Select(r=>r.playerId).ToArray();
            Winner=Winners.Length==1?Winners[0]:"";
            Say(reason=="timeout"?"时间到！按吞噬数结算，同分比较剩余 Persona。":"最后一位朋友保住了自我！");
        }
        public Snapshot View(string recipient)
        {
            return new Snapshot {protocolVersion=3,serverTick=serverTick,combatEvents=CombatEvents.ToArray(),grabLinks=GrabLinks(),room=Room,matchId=MatchId,you=recipient,host=Host,winner=Winner,winners=Winners,rankings=Rankings,endReason=EndReason,simulationTime=Time,handEventId=HandEventId,handStartedAt=LastHand,phase=Phase,handPosition=handPosition,handFist=handFist,
                time=Active||Phase==MatchPhase.Finished?Time-BattleStarted:Time,handAt=nextHand-BattleStarted,finalAt=Rules.finalTime,handAnimation=Active&&HandEventId>0?Mathf.Max(0,1.5f-(Time-LastHand)):0,
                players=Players.Values.Select(p=> {
                    var item=Item(p.equipped);return new PublicPlayer {id=p.profile.playerId,nickname=p.profile.nickname,color=p.profile.color,outfit=p.profile.outfit,appearance=p.profile.appearance?.Copy(),characterSkin=VerifiedCharacterSkin(p.profile),
                        equippedName=item?.asset.name??"空手",equippedVisual=item?.asset.visualId??"",equippedColor=item?.asset.color??"#FFFFFF",remaining=Remaining(p.profile.playerId),
                        equippedEmblemPath=item?.asset.emblemPath??"",equippedEmblemCrop=item?.asset.emblemCrop??"",equippedGeneratedBy=item?.asset.generatedBy??"",
                        bodyMode=p.bodyMode,bodyStateAt=p.bodyStateAt,bodyProtectedUntil=p.bodyProtectedUntil,knockdownId=p.knockdownId,getUpFaceUp=p.getUpFaceUp,bodyPose=p.bodyPose,position=p.position,velocity=p.velocity,simulationTime=Time,actionId=p.actionId,actionKind=p.actionKind,actionVariant=p.actionVariant,actionTarget=p.actionTarget,actionStartedAt=p.actionStartedAt,impactId=p.impactId,impactImpulse=p.impactImpulse,grabAnchor=GrabAnchor(p),grabTension=p.grabTension,escapeProgress=p.escapeStartedAt<0?0:Mathf.Clamp01((Time-p.escapeStartedAt)/Rules.escapeDuration),yaw=p.yaw,stability=p.stability,running=Active&&p.running,airborne=p.airborne,locomotionSpeed=Active?p.locomotionSpeed:0,stagger=Mathf.Max(0,p.staggerUntil-Time),connected=p.connected,ready=p.ready,eliminated=p.eliminated,reveal=Phase==MatchPhase.Reveal,
                        carrying=p.carrying,grabbing=p.grabbing,grabbed=Players.Values.Any(other=>other.grabbing==p.profile.playerId),interaction=p.interaction,publicProp=p.publicProp,charge=p.chargingSince<0?0:Mathf.Clamp01((Time-p.chargingSince)/1.2f),
                        progress=p.interaction==""?0:Mathf.Clamp01((Time-p.interactionStart)/(p.interaction=="eat"?Rules.eatDuration:Rules.recoverDuration))}; }).ToArray(),
                ground=Items.Where(x=>x.state==ItemState.Ground||x.state==ItemState.Carried||x.state==ItemState.Thrown).Select(x=>x.PublicCopy()).ToArray(),
                inventory=Items.Where(x=>x.ownerId==recipient&&(x.state==ItemState.Inventory||x.state==ItemState.Equipped)).Select(x=>x.PublicCopy()).ToArray(),
                revealed=Phase==MatchPhase.Reveal?Players.Values.SelectMany(p=>Items.Where(x=>x.ownerId==p.profile.playerId).Take(2).Select((x,i)=>{var copy=x.PublicCopy();copy.id="reveal-"+x.id;copy.position=p.position+new Vector3(i==0?-1.5f:1.5f,.9f,0);return copy;})).ToArray():Array.Empty<PublicPersonaInstance>(),
                effects=Effects.ToArray(),props=Props.ToArray(),eaten=Eaten.ToArray(),events=Events.ToArray()};
        }
    }
}
