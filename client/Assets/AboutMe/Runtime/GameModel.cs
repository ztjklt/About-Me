using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class PersonaAsset
    {
        public string assetId, name, templateId, visualId, color, source;
        public PersonaAsset Copy() => (PersonaAsset)MemberwiseClone();
    }
    [Serializable] public class Profile
    {
        public string playerId, nickname, color = "#77C8BD";
        public int outfit, revision;
        public PersonaAsset[] personas = Array.Empty<PersonaAsset>();
    }
    [Serializable] public class CatalogEntry { public string templateId, visualId, label, color; }
    [Serializable] public class Catalog { public CatalogEntry[] items; }
    public enum MatchPhase { Lobby, Reveal, Battle, FinalBurst, Finished }
    public enum ItemState { Inventory, Equipped, Ground, Carried, Consumed }

    [Serializable] public class MatchRules
    {
        public float moveSpeed = 5.5f, jumpSpeed = 8, gravity = 23, maxStability = 100;
        public float lightDamage = 22, heavyDamage = 48, lightInterval = .5f, heavyInterval = 1.05f;
        public float eatDuration = 2.5f, recoverDuration = 1.5f, switchDuration = .3f;
        public float revealDuration = 5, handInterval = 300, finalTime = 900;
        public float grabReach = 2.1f, grabBreakDistance = 4, interactionRange = 2.4f;
        public int fallDrop = 1, handDrop = 1, maxPlayers = 6;
    }
    [Serializable] public class Command
    {
        public int sequence;
        public float x, z, yaw;
        public string action = "", itemId = "";
        public float charge;
    }
    [Serializable] public class PersonaInstance
    {
        public string id, ownerId, carrierId = "";
        public PersonaAsset asset;
        public ItemState state;
        public Vector3 position, velocity;
        public PersonaInstance Copy() => (PersonaInstance)MemberwiseClone();
    }
    public class Player
    {
        public Profile profile;
        public Vector3 position, impulse;
        public float yaw, stability = 100, verticalSpeed, staggerUntil, nextAttack, switchUntil, lastHit;
        public bool connected = true, ready, eliminated;
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
        public int outfit, remaining;
        public Vector3 position;
        public float yaw, stability, stagger, progress, charge;
        public bool connected, ready, eliminated, reveal;
    }
    [Serializable] public class EffectState
    {
        public string id, owner, kind, visual, color;
        public Vector3 position, velocity;
        public float remaining, damage, force, nextHit;
        public bool heavy;
        public List<string> hit = new List<string>();
    }
    [Serializable] public class EatRecord { public string eater, owner, persona, visual,eaterId,ownerId,assetId,instanceId; }
    [Serializable] public class MatchEvent { public int id; public string text; }
    [Serializable] public class Snapshot
    {
        public string room, matchId, you, host, winner;
        public MatchPhase phase;
        public float time, handAt, finalAt, handAnimation;
        public PublicPlayer[] players;
        public PersonaInstance[] ground, inventory, revealed;
        public Vector3 handPosition;
        public EffectState[] effects;
        public PublicProp[] props;
        public EatRecord[] eaten;
        public MatchEvent[] events;
    }

    // This is the sole owner of match state. Clients never decrement counts or transfer items.
    public class Match
    {
        public readonly string Room;
        public string MatchId = Guid.NewGuid().ToString("N"), Host, Winner = "";
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
        int eventId, handIndex;
        public Match(string room, MatchRules rules)
        {
            Room = room; Rules = rules;
            string[] visuals={"chair","box","trash"};
            for(int i=0;i<3;i++)Props.Add(new PublicProp{id="prop-"+i,visual=visuals[i],position=new Vector3(-8+i*6,1,-6)});
        }
        public static readonly Vector3[] Obstacles = {new Vector3(-4,0,-3),new Vector3(4,0,3),new Vector3(0,0,6)};
        public static Vector3 Spawn(int index) => new Vector3(Mathf.Cos(index * Mathf.PI / 3) * 7, 1, Mathf.Sin(index * Mathf.PI / 3) * 7);
        public bool Add(Profile profile)
        {
            if (Players.TryGetValue(profile.playerId, out var existing))
            { existing.connected = true; existing.lastInput = Time; existing.sequence = -1; return true; }
            if (Phase != MatchPhase.Lobby || Players.Count >= Rules.maxPlayers || profile.personas.Length == 0) return false;
            if (Host == null) Host = profile.playerId;
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
            else { p.connected = false; p.input = new Command(); Interrupt(p); ReleaseGrab(p); }
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
            { Phase = MatchPhase.Reveal; Time = 0; LastHand = 0; Say("你的自我，登场！展示阶段不可抢夺。"); }
            if (!Active || p.eliminated) return;
            if (input.action == "cancel") { Interrupt(p); ReleaseGrab(p); p.chargingSince = -1; return; }
            if (Time < p.staggerUntil) return;
            switch (input.action)
            {
                case "jump": if (p.position.y <= Floor(p.position) + 1.03f) p.verticalSpeed = Rules.jumpSpeed; Interrupt(p); break;
                case "light": Attack(p, false, 0); break;
                case "charge": if (Time >= p.nextAttack && p.chargingSince < 0) { Interrupt(p); p.chargingSince = Time; } break;
                case "heavy":
                    if (p.chargingSince >= 0) Attack(p, true, Mathf.Clamp01((Time - p.chargingSince) / 1.2f));
                    p.chargingSince = -1; break;
                case "equip": Equip(p, input.itemId); break;
                case "grab": Grab(p, false); break;
                case "grab_item": Grab(p, true); break;
                case "interact": Interact(p); break;
                case "drop": Interrupt(p); ReleaseGrab(p); break;
            }
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static float Floor(Vector3 p)
        {
            if (Mathf.Abs(p.x) > 14 || Mathf.Abs(p.z) > 14) return -100;
            foreach (var o in Obstacles) if (Mathf.Abs(p.x-o.x)<1.5f && Mathf.Abs(p.z-o.z)<1.5f) return 1.4f;
            return 0;
        }
        void EquipFirst(Player p)
        {
            var item = Items.Find(x => x.ownerId == p.profile.playerId && x.state == ItemState.Inventory);
            if (item != null) { p.equipped = item.id; item.state = ItemState.Equipped; }
        }
        void Equip(Player p, string id)
        {
            var target = Item(id);
            if (target == null || target.ownerId != p.profile.playerId || target.state != ItemState.Inventory || Time < p.switchUntil) return;
            Interrupt(p); ReleaseGrab(p); p.chargingSince = -1;
            var current = Item(p.equipped); if (current != null) current.state = ItemState.Inventory;
            p.equipped = target.id; target.state = ItemState.Equipped; p.switchUntil = Time + Rules.switchDuration;
        }
        public void Tick(float dt)
        {
            if (dt <= 0 || dt > .1f) return;
            Time += dt;
            if (Phase == MatchPhase.Reveal)
            {
                if (Time >= Rules.revealDuration)
                { Phase = MatchPhase.Battle; BattleStarted = Time; nextHand = Time + Rules.handInterval; Say("你们可以开始互相介绍了。"); }
                return;
            }
            if (!Active) return;
            if (Phase == MatchPhase.Battle && Time - BattleStarted >= Rules.finalTime)
            {
                Phase = MatchPhase.FinalBurst; LastHand = Time;handPosition=Vector3.zero;
                foreach (var p in Players.Values.Where(x => !x.eliminated)) { Interrupt(p);ReleaseGrab(p); Drop(p, int.MaxValue); p.staggerUntil = Time + 1; }
                Say("FINAL PERSONA BURST · 抢救你的自我！");
            }
            else if (Phase == MatchPhase.Battle && Time >= nextHand)
            {
                nextHand += Rules.handInterval; LastHand = Time;
                var targets = Players.Values.Where(x => !x.eliminated).OrderByDescending(x => Remaining(x.profile.playerId)).ToArray();
                if (targets.Length > 0)
                { var p = targets[(handIndex++ % 3 == 2) ? (handIndex % targets.Length) : 0]; handPosition=p.position;Interrupt(p);ReleaseGrab(p); Drop(p, Rules.handDrop); p.staggerUntil = Time+1.2f; Say("大手点名：" + p.profile.nickname); }
            }
            foreach (var p in Players.Values)
            {
                if (p.eliminated) continue;
                if (Time - p.lastInput > .35f) { p.input.x = p.input.z = 0; p.chargingSince = -1; }
                bool stunned = Time < p.staggerUntil;
                var desired = !stunned && p.interaction == "" ? new Vector3(p.input.x,0,p.input.z) * Rules.moveSpeed : Vector3.zero;
                if (p.chargingSince >= 0) desired *= .45f;
                if (!stunned) p.yaw = p.input.yaw;
                var old = p.position; var next = old + (desired + p.impulse) * dt;
                // The server uses the same bounded arena geometry on every platform.
                if (Floor(next) > old.y - .6f) { next.x = old.x; next.z = old.z; }
                p.verticalSpeed -= Rules.gravity * dt; next.y += p.verticalSpeed * dt;
                float floor = Floor(next) + 1;
                if (next.y < floor) { next.y = floor; if (p.verticalSpeed < -17) { Drop(p,1); p.staggerUntil=Time+.7f; } p.verticalSpeed = 0; }
                p.position = next; p.impulse = Vector3.Lerp(p.impulse, Vector3.zero, dt * 5);
                if (Time - p.lastHit > 2.5f) p.stability = Mathf.Min(Rules.maxStability, p.stability + dt * 18);
                if (p.position.y < -8) { Interrupt(p); Drop(p, Rules.fallDrop); p.position = Spawn(Players.Keys.ToList().IndexOf(p.profile.playerId)); p.impulse=Vector3.zero; p.verticalSpeed=0; p.staggerUntil=Time+.6f; }
                TickGrab(p, dt);
                if (p.carrying != "")
                {
                    var carry = Item(p.carrying);
                    if (carry != null) carry.position = p.position + Forward(p)*.8f + Vector3.up*.45f;
                    if (p.interaction != "" && carry != null)
                    {
                        float duration = p.interaction == "recover" ? Rules.recoverDuration : Rules.eatDuration;
                        if (Time - p.interactionStart >= duration) CompleteInteraction(p, carry);
                    }
                }
            }
            // Soft body separation creates pushing without client-controlled physics authority.
            var living = Players.Values.Where(x=>!x.eliminated).ToArray();
            for (int i=0;i<living.Length;i++) for(int j=i+1;j<living.Length;j++)
            {
                var a=living[i]; var b=living[j]; var d=b.position-a.position; d.y=0;
                if(d.sqrMagnitude < 1.21f && Mathf.Abs(a.position.y-b.position.y)<1.6f)
                { var n=d.sqrMagnitude<.001f?Vector3.right:d.normalized; float push=(1.1f-d.magnitude)*.5f; a.position-=n*push; b.position+=n*push; }
            }
            foreach (var item in Items.Where(x => x.state == ItemState.Ground))
            {
                item.velocity += Vector3.down * Rules.gravity * dt; item.position += item.velocity * dt;
                float ground = Floor(item.position) + .35f;
                if(item.position.y < ground) { item.position.y=ground; item.velocity=new Vector3(item.velocity.x*.9f,Mathf.Abs(item.velocity.y)*.25f,item.velocity.z*.9f); if(item.velocity.magnitude<.5f)item.velocity=Vector3.zero; }
                if(item.position.y < -6 || Mathf.Abs(item.position.x)>17 || Mathf.Abs(item.position.z)>17)
                { item.position = new Vector3(Mathf.Clamp(item.position.x,-11,11),2,Mathf.Clamp(item.position.z,-11,11)); item.velocity=Vector3.zero; }
            }
            TickEffects(dt);
            foreach(var prop in Props)
            {
                if(prop.holder!=""&&Players.TryGetValue(prop.holder,out var holder)) {prop.position=holder.position+Forward(holder);prop.velocity=Vector3.zero;continue;}
                prop.velocity+=Vector3.down*Rules.gravity*dt;prop.position+=prop.velocity*dt;
                if(prop.position.y<Floor(prop.position)+.6f){prop.position.y=Floor(prop.position)+.6f;prop.velocity=new Vector3(prop.velocity.x*.96f,0,prop.velocity.z*.96f);}
                if(prop.position.y< -6){prop.position=new Vector3(0,1,-6);prop.velocity=Vector3.zero;}
                if(prop.velocity.magnitude>4&&Time>prop.hitAfter&&Players.TryGetValue(prop.thrower,out var thrower))
                foreach(var p in Players.Values.Where(x=>x!=thrower&&!x.eliminated).ToArray())
                if(Vector3.Distance(p.position,prop.position)<1.5f){Hit(p,thrower,12,4,true);prop.hitAfter=Time+.8f;prop.velocity*=.3f;break;}
            }
        }
        static Vector3 Forward(Player p) => Quaternion.Euler(0,p.yaw,0)*Vector3.forward;
        void TickGrab(Player p, float dt)
        {
            if (p.grabbing == "") return;
            if (!Players.TryGetValue(p.grabbing,out var target) || target.eliminated || Time < p.staggerUntil || Vector3.Distance(p.position,target.position)>Rules.grabBreakDistance)
            { ReleaseGrab(p); return; }
            if(p.grabKind=="item" && target.equipped=="") { ReleaseGrab(p); return; }
            Interrupt(target);
            var delta = p.position + Forward(p)*1.2f - target.position;
            target.impulse += Vector3.ClampMagnitude(delta * 12 * dt, 1);
            target.stability = Mathf.Max(0, target.stability - dt*9);
            if (target.stability <= 0) { Drop(target,1); target.stability=Rules.maxStability*.6f; target.staggerUntil=Time+.9f; ReleaseGrab(p); }
        }
        void Grab(Player p, bool item)
        {
            if(p.grabbing!=""||p.publicProp!="") { ReleaseGrab(p); return; }
            Interrupt(p); p.chargingSince=-1;
            var target = Players.Values.Where(x=>x!=p&&!x.eliminated&&(!item||x.equipped!="")&&Vector3.Distance(x.position,p.position)<Rules.grabReach)
                .OrderBy(x=>Vector3.Distance(x.position,p.position)).FirstOrDefault();
            if(target==null)
            {
                if(item)return;
                var prop=Props.Where(x=>x.holder==""&&Vector3.Distance(x.position,p.position)<Rules.grabReach).OrderBy(x=>Vector3.Distance(x.position,p.position)).FirstOrDefault();
                if(prop!=null){prop.holder=p.profile.playerId;p.publicProp=prop.id;}
                return;
            }
            p.grabbing=target.profile.playerId; p.grabKind=item?"item":"body"; Interrupt(target);
        }
        void ReleaseGrab(Player p)
        {
            p.grabbing=""; p.grabKind="";
            var prop=Props.Find(x=>x.id==p.publicProp);if(prop!=null){prop.holder="";prop.velocity=Forward(p)*2;}
            p.publicProp="";
        }
        void Attack(Player p, bool heavy, float charge)
        {
            if(Time < p.nextAttack || Time < p.switchUntil) return;
            Interrupt(p);
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
            float force=heavy?5+charge*9:3;
            if(heavy && (template=="Projectile"||template=="Disc"||template=="Deployable"||template=="Summon"))
            {
                Effects.Add(new EffectState { id=Guid.NewGuid().ToString("N"), owner=p.profile.playerId, kind=template,
                    visual=equipped.asset.visualId,color=equipped.asset.color,position=p.position+Forward(p),
                    velocity=Forward(p)*(template=="Disc"?14:12),remaining=template=="Deployable"?8:template=="Summon"?7:2,
                    damage=damage,force=force,heavy=true });
                return;
            }
            float reach=heavy?2.8f:2.2f;
            if(heavy && template=="Utility") { reach=5; damage=28; force=2; p.nextAttack=Time+5; }
            if(heavy && template=="SpecialAbility") { reach=4.5f; force=15; p.nextAttack=Time+7; }
            if(template=="HeavyWeapon") { damage*=1.25f; force*=1.2f; p.nextAttack+=.3f; }
            Effects.Add(new EffectState {id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,kind="Pulse",color=equipped?.asset.color??"#FFFFFF",position=p.position+Forward(p),remaining=.25f,damage=reach});
            foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated).ToArray())
            {
                var delta=target.position-p.position;
                if(delta.magnitude<=reach && (template=="SpecialAbility" || Vector3.Dot(Forward(p),delta.normalized)>.1f)) Hit(target,p,damage,force,heavy);
            }
        }
        public void Hit(Player target, Player attacker, float damage, float force, bool heavy)
        {
            if(!Active || target.eliminated) return;
            Interrupt(target); ReleaseGrab(target); target.chargingSince=-1;
            target.stability=Mathf.Max(0,target.stability-damage); target.lastHit=Time;
            var direction=target.position-attacker.position; direction.y=0;
            if(direction.sqrMagnitude<.001f)direction=Forward(attacker);
            target.impulse+=direction.normalized*force;
            target.staggerUntil=Time+(heavy?.65f:.16f);
            if(heavy && target.stability<=35) { Drop(target,1); target.stability=Rules.maxStability*.65f; target.verticalSpeed=4; target.staggerUntil=Time+1.1f; }
        }
        void TickEffects(float dt)
        {
            foreach(var e in Effects.ToArray())
            {
                e.remaining-=dt;
                if(e.remaining<=0) { Effects.Remove(e); continue; }
                if(e.kind=="Pulse")continue;
                if(!Players.TryGetValue(e.owner,out var owner))continue;
                if(e.kind=="Summon")
                {
                    var target=Players.Values.Where(p=>p!=owner&&!p.eliminated).OrderBy(p=>Vector3.Distance(p.position,e.position)).FirstOrDefault();
                    if(target!=null)e.velocity=(target.position-e.position).normalized*6;
                }
                if(e.kind=="Disc" && e.remaining<.8f)e.velocity=(owner.position-e.position).normalized*15;
                if(e.kind=="Deployable") { e.velocity*=Mathf.Max(0,1-dt*5); e.position.y=.5f; }
                e.position+=e.velocity*dt;
                foreach(var p in Players.Values.Where(p=>p!=owner&&!p.eliminated).ToArray())
                {
                    if(Vector3.Distance(e.position,p.position)<1.25f && (e.kind=="Summon"?Time>=e.nextHit:!e.hit.Contains(p.profile.playerId)))
                    {
                        Hit(p,owner,e.damage,e.force,e.heavy); e.hit.Add(p.profile.playerId); e.nextHit=Time+1.4f;
                        if(e.kind=="Projectile")e.remaining=0;
                    }
                }
            }
        }
        public void Drop(Player p,int count)
        {
            var candidates=Items.Where(x=>x.ownerId==p.profile.playerId&&(x.state==ItemState.Inventory||x.state==ItemState.Equipped))
                .OrderByDescending(x=>x.state==ItemState.Equipped).Take(count).ToArray();
            for(int i=0;i<candidates.Length;i++)
            {
                var item=candidates[i]; item.state=ItemState.Ground; item.carrierId="";
                var safe=p.position; safe.x=Mathf.Clamp(safe.x,-11,11);safe.z=Mathf.Clamp(safe.z,-11,11);safe.y=Mathf.Max(safe.y,1.3f);
                item.position=safe+Vector3.up*.4f;
                item.velocity=Quaternion.Euler(0,i*137+Time*17,0)*new Vector3(3,5,0);
                if(p.equipped==item.id)p.equipped="";
            }
        }
        public void Interact(Player p)
        {
            if(p.interaction!="") { Interrupt(p); return; }
            ReleaseGrab(p); p.chargingSince=-1;
            var item=Items.Where(x=>x.state==ItemState.Ground&&Vector3.Distance(x.position,p.position)<Rules.interactionRange)
                .OrderBy(x=>Vector3.Distance(x.position,p.position)).FirstOrDefault();
            if(item==null)return;
            item.state=ItemState.Carried;item.carrierId=p.profile.playerId;item.velocity=Vector3.zero;
            p.carrying=item.id;p.interaction=item.ownerId==p.profile.playerId?"recover":"eat";p.interactionStart=Time;
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
                if(Remaining(item.ownerId)==0) { original.eliminated=true;Interrupt(original);ReleaseGrab(original);Say(original.profile.nickname+" 失去了全部自我"); }
            }
            item.carrierId="";p.carrying="";p.interaction="";
            if(p.equipped=="")EquipFirst(p);
            var survivors=Players.Values.Where(x=>!x.eliminated).ToArray();
            if(survivors.Length==1 && Players.Count>=2) { Winner=survivors[0].profile.playerId;Phase=MatchPhase.Finished;Say(survivors[0].profile.nickname+" 保住了最后的自我！"); }
        }
        public Snapshot View(string recipient)
        {
            return new Snapshot {room=Room,matchId=MatchId,you=recipient,host=Host,winner=Winner,phase=Phase,handPosition=handPosition,
                time=Active||Phase==MatchPhase.Finished?Time-BattleStarted:Time,handAt=nextHand-BattleStarted,finalAt=Rules.finalTime,handAnimation=Mathf.Max(0,1.5f-(Time-LastHand)),
                players=Players.Values.Select(p=> {
                    var item=Item(p.equipped);return new PublicPlayer {id=p.profile.playerId,nickname=p.profile.nickname,color=p.profile.color,outfit=p.profile.outfit,
                        equippedName=item?.asset.name??"空手",equippedVisual=item?.asset.visualId??"",equippedColor=item?.asset.color??"#FFFFFF",remaining=Remaining(p.profile.playerId),
                        position=p.position,yaw=p.yaw,stability=p.stability,stagger=Mathf.Max(0,p.staggerUntil-Time),connected=p.connected,ready=p.ready,eliminated=p.eliminated,reveal=Phase==MatchPhase.Reveal,
                        carrying=p.carrying,grabbing=p.grabbing,interaction=p.interaction,publicProp=p.publicProp,charge=p.chargingSince<0?0:Mathf.Clamp01((Time-p.chargingSince)/1.2f),
                        progress=p.interaction==""?0:Mathf.Clamp01((Time-p.interactionStart)/(p.interaction=="eat"?Rules.eatDuration:Rules.recoverDuration))}; }).ToArray(),
                ground=Items.Where(x=>x.state==ItemState.Ground||x.state==ItemState.Carried).Select(x=>x.Copy()).ToArray(),
                inventory=Items.Where(x=>x.ownerId==recipient&&(x.state==ItemState.Inventory||x.state==ItemState.Equipped)).Select(x=>x.Copy()).ToArray(),
                revealed=Phase==MatchPhase.Reveal?Players.Values.SelectMany(p=>Items.Where(x=>x.ownerId==p.profile.playerId).Take(2).Select((x,i)=>{var copy=x.Copy();copy.id="reveal-"+x.id;copy.position=p.position+new Vector3(i==0?-1.5f:1.5f,.9f,0);return copy;})).ToArray():Array.Empty<PersonaInstance>(),
                effects=Effects.ToArray(),props=Props.ToArray(),eaten=Eaten.ToArray(),events=Events.ToArray()};
        }
    }
}
