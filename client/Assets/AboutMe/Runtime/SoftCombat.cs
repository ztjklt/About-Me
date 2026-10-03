using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class CombatEvent
    {
        public int id, tick, actionId, actionVariant, impactId;
        public float time;
        public string kind, actionKind, sourceId, targetId, reason;
        public Vector3 impulse,point;
        public int severity;
    }
    [Serializable] public class GrabLinkState
    {
        public string id, grabberId, targetId, kind;
        public Vector3 grabberLocalAnchor, targetLocalAnchor;
        public float startedAt, tension;
    }
    public partial class Match
    {
        public readonly List<CombatEvent> CombatEvents=new List<CombatEvent>();
        public int serverTick;
        int combatEventId;
        bool IsGrabbed(Player p)=>Players.Values.Any(x=>x.grabbing==p.profile.playerId);
        void CombatEvent(string kind,Player source,Player target=null,Vector3 impulse=default,string reason="")
        {
            CombatEvents.Add(new CombatEvent{id=++combatEventId,tick=serverTick,time=Time,kind=kind,sourceId=source.profile.playerId,targetId=target?.profile.playerId??"",actionId=source.actionId,actionKind=source.actionKind,actionVariant=source.actionVariant,impactId=target?.impactId??0,impulse=impulse,reason=reason});
            if(CombatEvents.Count>96)CombatEvents.RemoveAt(0);
        }
        void SetAction(Player p,string kind,string target="")
        {p.actionId++;p.actionKind=kind;p.actionStartedAt=Time;p.actionTarget=target;p.actionVariant=p.actionId%2;CombatEvent("action",p);}
        void CancelAction(Player p){p.pendingAction="";p.chargingSince=-1;}
        void RecordImpact(Player p,Player source,Vector3 impulse)
        {p.impactId++;p.impactImpulse=impulse;SetAction(p,"hurt",source.profile.playerId);CombatEvent("hit",source,p,impulse);}
        void Attack(Player p,bool heavy,float charge)
        {
            if(Time<p.nextAttack||Time<p.switchUntil||p.pendingAction!="")return;
            Interrupt(p);if(p.grabbing!="")ReleaseGrab(p);p.chargingSince=-1;
            SetAction(p,heavy?"heavy":"light");p.pendingAction=p.actionKind;p.pendingCharge=charge;
            p.pendingAt=Time+(heavy?.16f:.10f);p.nextAttack=Time+(heavy?Rules.heavyInterval:Rules.lightInterval);
        }
        void UseItem(Player p)
        {
            // Attacks are always an explicit use of an equipped Persona (or a
            // public prop being carried). There is no unarmed light attack.
            if(Item(p.equipped)==null&&Props.Find(x=>x.id==p.publicProp)==null)return;
            Attack(p,true,1f);
        }
        void Grab(Player p,bool item)
        {
            if(p.grabbing!=""||p.publicProp!=""){ReleaseGrab(p);return;}
            if(IsGrabbed(p)||p.pendingAction!=""||Time<p.nextGrab||Time<p.nextAttack||Time<p.switchUntil)return;
            Interrupt(p);p.chargingSince=-1;SetAction(p,item?"grab_item":"grab");
            p.pendingAction=p.actionKind;p.pendingAt=Time+.20f;p.nextGrab=Time+.45f;
        }
        bool CanReach(Player p,Vector3 point,float range)
        {
            var delta=point-p.position;
            return delta.magnitude<=range&&Mathf.Abs(delta.y)<=(BodyWorld==null?.8f:1.5f)&&Vector3.Dot(Forward(p),delta.normalized)>=.5f
                &&CampusGeometry.ClearSegment(p.position+Vector3.up*.25f,point+Vector3.up*.25f)
                &&!Effects.Any(e=>e.kind=="Barricade"&&DistanceToSegment(e.position,p.position,point)<e.radius);
        }
        static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude)));}
        void ConnectGrab(Player p,bool item)
        {
            var target=Players.Values.Where(x=>x!=p&&!x.eliminated&&Time>=x.grabImmuneUntil&&Time>=x.bodyProtectedUntil&&x.bodyMode!=BodyMode.GettingUp&&!IsGrabbed(x)&&x.grabbing==""&&(!item||x.equipped!="")&&CanReach(p,BodyWorld==null?x.position:BodyWorld.Contact(x,p.position+Vector3.up*.25f),Rules.grabContactDistance))
                .OrderBy(x=>(x.position-p.position).sqrMagnitude).ThenBy(x=>x.profile.playerId,StringComparer.Ordinal).FirstOrDefault();
            if(target==null)
            {
                if(!item)
                {
                    var prop=Props.Where(x=>x.holder==""&&CanReach(p,x.position,Rules.grabReach)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();
                    if(prop!=null){prop.holder=p.profile.playerId;p.publicProp=prop.id;SetAction(p,"pickup",prop.id);return;}
                }
                p.nextGrab=Time+Rules.grabCooldown;return;
            }
            Interrupt(target);CancelAction(target);ResetEscape(target);
            p.grabbing=target.profile.playerId;p.grabKind=item?"item":"body";p.actionTarget=p.grabbing;p.grabStartedAt=Time;p.grabOverload=0;
            p.grabAnchorYaw=p.yaw;
            p.grabberLocalAnchor=new Vector3(0,.28f,.60f);
            var contact=(p.position-target.position).normalized*.48f;contact.y=.28f;
            p.targetLocalAnchor=Quaternion.Euler(0,-target.yaw,0)*contact;
            if(BodyWorld!=null){var rig=BodyWorld.Get(target);p.bodyGrabBone=rig.ClosestBone(p.position+Vector3.up*.25f);var body=rig.Bodies[p.bodyGrabBone];p.bodyGrabAnchor=Quaternion.Inverse(body.rotation)*(BodyWorld.Contact(target,p.position+Vector3.up*.25f)-body.position);}
            CombatEvent("grab",p,target);
        }
        void ReleaseGrab(Player p)
        {
            bool held=p.grabbing!=""||p.publicProp!="";
            if(p.grabbing!="")
            {
                if(Players.TryGetValue(p.grabbing,out var target))
                {target.grabImmuneUntil=Time+Rules.grabImmunity;ResetEscape(target);target.grabTension=0;CombatEvent("grabRelease",p,target);}
                p.nextGrab=Time+Rules.grabCooldown;
            }
            p.grabbing="";p.grabKind="";p.grabTension=0;p.grabOverload=0;
            var prop=Props.Find(x=>x.id==p.publicProp);
            if(prop!=null){prop.holder="";prop.velocity=Vector3.ClampMagnitude(p.velocity,5);}
            p.publicProp="";
            if(held)SetAction(p,"release");
        }
        Vector3 GrabAnchor(Player p)
        {
            if(p.grabbing!=""&&Players.TryGetValue(p.grabbing,out var t))return t.position+Quaternion.Euler(0,t.yaw,0)*p.targetLocalAnchor;
            var grabber=Players.Values.FirstOrDefault(x=>x.grabbing==p.profile.playerId);
            return grabber==null?Vector3.zero:p.position+Quaternion.Euler(0,p.yaw,0)*grabber.targetLocalAnchor;
        }
        GrabLinkState[] GrabLinks()=>Players.Values.Where(p=>p.grabbing!="").Select(p=>new GrabLinkState{
            id=p.profile.playerId+":"+p.grabStartedAt.ToString("R",System.Globalization.CultureInfo.InvariantCulture),grabberId=p.profile.playerId,targetId=p.grabbing,kind=p.grabKind,
            grabberLocalAnchor=p.grabberLocalAnchor,targetLocalAnchor=p.targetLocalAnchor,startedAt=p.grabStartedAt,tension=p.grabTension}).ToArray();

        // All links use the same pre-integration positions/velocities. Apply forces before either motor moves.
        void ResetEscape(Player p){p.escapeStartedAt=-1;p.escapeCharge=0;p.escapeReleasedAt=-1;}
        float GrabResistance(Player grabber,Player target)
        {
            var away=target.position-grabber.position;away.y=0;
            var movement=new Vector3(target.input.x,0,target.input.z);
            return movement.sqrMagnitude>.01f&&Vector3.Dot(movement.normalized,away.normalized)>.5f?.8f:1;
        }
        void TickCombat(float dt)
        {
            foreach(var p in Players.Values)
            {
                if(p.eliminated)continue;
                if(Time-p.lastInput>.35f){p.input.escapeHeld=false;ResetEscape(p);}
                if(!IsGrabbed(p))ResetEscape(p);
                else if(p.input.escapeHeld)
                {
                    if(p.escapeStartedAt<0)p.escapeStartedAt=Time;
                    p.escapeReleasedAt=-1;p.escapeCharge=Mathf.Min(Rules.escapeDuration,p.escapeCharge+dt);
                    if(p.escapeCharge+.0001f>=Rules.escapeDuration)
                    {ReleaseAllGrabs(p);CancelAction(p);SetAction(p,"escape");}
                }
                else
                {
                    p.escapeStartedAt=-1;
                    if(p.escapeReleasedAt<0)p.escapeReleasedAt=Time;
                    if(Time-p.escapeReleasedAt>Rules.escapeReleaseGrace)p.escapeCharge=Mathf.MoveTowards(p.escapeCharge,0,dt*Rules.escapeDuration/Rules.escapeDecayDuration);
                }
                if(p.pendingAction!=""&&Time+.0001f>=p.pendingAt)
                {
                    var pending=p.pendingAction;p.pendingAction="";
                    if(Time<p.staggerUntil||!p.connected)continue;
                    // Input facing is authoritative even when a command arrives between simulation ticks.
                    p.yaw=p.input.yaw;
                    if(pending=="light"||pending=="heavy")ResolveAttack(p,pending=="heavy",p.pendingCharge);
                    else if(pending=="grab"||pending=="grab_item")ConnectGrab(p,pending=="grab_item");
                }
            }
            foreach(var p in Players.Values)
            {
                if(p.grabbing=="")continue;
                if(!Players.TryGetValue(p.grabbing,out var target)||target.eliminated||!p.connected||!target.connected||Time<p.staggerUntil||Time<target.staggerUntil&&!BodyDown(target)
                    ||Time-p.grabStartedAt>=Rules.grabDuration||Vector3.Distance(p.position,target.position)>Rules.grabBreakDistance||(p.grabKind=="item"&&target.equipped==""))
                {ReleaseGrab(p);continue;}
                p.grabAnchorYaw=Mathf.MoveTowardsAngle(p.grabAnchorYaw,p.input.yaw,Rules.grabTurnSpeed*dt);
                if(BodyWorld!=null)
                {
                    p.grabOverload=p.grabTension>.9f?p.grabOverload+dt:0;
                    if(p.grabOverload>.18f){ReleaseGrab(p);continue;}
                    if(!CampusGeometry.ClearSegment(p.position+Vector3.up*.25f,BodyGrabPoint(p))){ReleaseGrab(p);continue;}
                    if(!BodyDown(target)){target.stability=Mathf.Max(0,target.stability-dt*9);target.lastHit=Time;if(target.stability<=0){BeginBodyFall(target,Vector3.zero,target.position);DropBodyOnce(target);target.stability=Rules.maxStability*.65f;}}
                    continue;
                }
                var a=p.position+Quaternion.Euler(0,p.grabAnchorYaw,0)*p.grabberLocalAnchor;
                var b=target.position+Quaternion.Euler(0,target.yaw,0)*p.targetLocalAnchor;
                if(!CampusGeometry.ClearSegment(a,b)){ReleaseGrab(p);continue;}
                var delta=b-a;delta.y=0;
                var relative=target.velocity-p.velocity;relative.y=0;
                var force=(delta*Rules.grabSpring+relative*Rules.grabDamping)*GrabResistance(p,target);
                p.grabTension=target.grabTension=Mathf.Clamp01(force.magnitude/Rules.grabMaxForce);
                p.grabOverload=force.magnitude>Rules.grabMaxForce*1.8f?p.grabOverload+dt:0;
                if(p.grabOverload>.2f){ReleaseGrab(p);continue;}
                var impulse=Vector3.ClampMagnitude(force,Rules.grabMaxForce)*Mathf.SmoothStep(0,1,Mathf.Clamp01((Time-p.grabStartedAt)/Rules.grabBlendDuration))*dt;
                p.impulse+=impulse;target.impulse-=impulse;
                target.stability=Mathf.Max(0,target.stability-dt*9);target.lastHit=Time;
                if(target.stability<=0){BeginBodyFall(target,Vector3.zero,target.position);DropBodyOnce(target);target.stability=Rules.maxStability*.65f;}
            }
        }
    }
}
