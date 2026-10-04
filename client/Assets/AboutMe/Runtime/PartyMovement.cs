using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public partial class Match
    {
        static bool SelfMovement(string id)=>id=="pogo-hop"||id=="inflate-shove"||id=="stomp-pulse"||id=="short-dash"||id=="grapple-anchor"||id=="glide-open";
        EffectState MovementFor(Player p)=>Effects.FirstOrDefault(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&SelfMovement(e.mechanicId)&&e.stage!="burst"&&e.stage!="ended");
        void CancelSelfMovement(Player p)
        {
            CancelSelfFields(p);CancelSelfControl(p);CancelSelfVirtual(p);CancelSelfLinks(p);StopArenaOperator(p);
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&SelfMovement(e.mechanicId)).ToArray())RemovePartyEffect(e);
            p.portalEntry="";p.portalStartedAt=0;
        }
        bool MovementBlocks(Player p,string action)
        {
            if(action=="interact"&&Time<p.interactBlockedUntil)return true;
            var e=MovementFor(p);if(e==null)return false;
            return (e.mechanicId=="glide-open"||e.mechanicId=="pogo-hop")&&(action=="use_item"||action=="interact");
        }
        float MotorY(Player p)=>BodyWorld==null?p.position.y:p.motorHeight;
        bool OnPartyGround(Player p)=>!p.airborne&&Mathf.Abs(MotorY(p)-SurfaceFloor(p.position+Vector3.up*.2f)-1)<.2f;
        static Vector3 Flat(Vector3 v){v.y=0;return v;}
        string MovementStatus(Player p)
        {
            if(p.portalEntry!="")return "传送准备中 · 离开圈 / Q 取消";
            var e=MovementFor(p);if(e==null)return "";var d=PartyMechanicRegistry.Find(e.mechanicId);
            return (d?.label??"")+" · "+Mathf.Max(0,e.deadline-Time).ToString("0.0")+"s · Q 停止";
        }
        bool MovementOwnerValid(EffectState e,Player p)
        {
            var source=Item(e.sourceInstance);
            return source!=null&&source.state==ItemState.Equipped&&p.equipped==e.sourceInstance&&p.connected&&!p.eliminated&&!IsGrabbed(p)&&!BodyLocked(p)&&Time>=p.staggerUntil&&Time-p.lastInput<=.35f;
        }
        bool TryMovementMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            bool air=p.airborne&&MotorY(p)>CampusGeometry.Floor(p.position+Vector3.up)+1.2f;
            if((d.id=="glide-open"||d.id=="stomp-pulse")&&!air)
            {Say(d.label+"：先跳起，再使用道具。");p.nextAttack=Time+.35f;return true;}
            if(d.id=="short-dash"&&!OnPartyGround(p))
            {Say("滑冲需要先落地。");p.nextAttack=Time+.35f;return true;}
            PublicProp anchor=null;
            if(d.id=="grapple-anchor")
            {
                anchor=Props.Where(x=>(x.visual=="box"||x.visual=="trash")&&x.holder==""&&Vector3.Distance(x.position,p.position)<=d.radius&&Vector3.Dot(Forward(p),Flat(x.position-p.position).normalized)>.5f&&PartyClear(p.position,x.position))
                    .OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();
                if(anchor==null){Say("前方没有可抓的锚点：靠近箱子或垃圾桶，保持路线可见。");p.nextAttack=Time+.35f;return true;}
            }
            var direction=new Vector3(p.input.x,0,p.input.z);if(direction.sqrMagnitude<.01f)direction=Forward(p);direction.Normalize();
            var position=GroundParty(p.position+Forward(p)*1.8f);
            if(!SelfMovement(d.id)&&d.id!="uppercut-piston"&&CampusGeometry.Blocked(position+Vector3.up*.85f))
            {Say("放置位置被挡住，请换个空地。");p.nextAttack=Time+.35f;return true;}
            if(d.id=="paired-teleport")
            {
                var first=Effects.FirstOrDefault(candidate=>candidate.kind=="PartyMechanic"&&candidate.sourceInstance==item.id&&candidate.stage=="portal_wait");
                if(first!=null)
                {
                    float distance=Vector3.Distance(first.position,position);
                    if(distance<1.6f||distance>8||!PartyClear(first.position,position)||CampusGeometry.Blocked(position+Vector3.up*.85f))
                    {Say("出口需要在 1.6–8 米内、清楚可见且有站立空间。");p.nextAttack=Time+.35f;return true;}
                }
            }
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,mechanicId=d.id,sourceInstance=item.id,
                visual=item.asset.visualId,color=item.asset.color,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,
                position=SelfMovement(d.id)?p.position:position,velocity=direction,stage="deployed",damage=d.damage,force=d.force};
            if(d.id=="uppercut-piston")
            {
                e.position=p.position+Forward(p)*.8f;e.stage="burst";Effects.Add(e);
                foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated&&!BodyLocked(x)&&Vector3.Distance(x.position,p.position)<=d.radius&&Vector3.Dot(Forward(p),Flat(x.position-p.position).normalized)>.5f&&PartyClear(p.position,x.position)).ToArray())
                {Hit(target,p,d.damage,d.force,false,false,false,p.position,Vector3.up*d.force);if(Time>=target.bodyProtectedUntil&&!BodyLocked(target))LaunchParty(target,8.5f,Vector3.zero);e.hit.Add(target.profile.playerId);}
                DamagePartyDevices(p,e.position,d.radius);CombatEvent("party_launch",p,reason:d.id);return true;
            }
            if(d.id=="pogo-hop")e.stage="pogo";
            if(d.id=="inflate-shove")e.stage="inflate";
            if(d.id=="stomp-pulse"){e.stage="stomp_mark";e.height=MotorY(p);e.landed=false;}
            if(d.id=="short-dash"){e.stage="dash";SetAction(p,"dash");}
            if(d.id=="grapple-anchor"){e.stage="reeling";e.targetId=anchor.id;SetAction(p,"reel",anchor.id);}
            if(d.id=="glide-open"){e.stage="glide";p.verticalSpeed=Mathf.Min(0,p.verticalSpeed);SetAction(p,"glide");}
            if(d.id=="paired-teleport")
            {
                var first=Effects.FirstOrDefault(x=>x.kind=="PartyMechanic"&&x.sourceInstance==item.id&&x.stage=="portal_wait");
                if(first==null){e.stage="portal_wait";Say("入口已放置，再移动到出口位置按 F。");}
                else {e.stage="portal_out";first.stage="portal_in";first.targetId=e.id;e.targetId=first.id;first.deadline=e.deadline;Say("双垫已连接：主动走进入口，准备 0.6 秒后传送。");}
            }
            Effects.Add(e);return true;
        }
        void LaunchParty(Player p,float speed,Vector3 horizontal)
        {
            p.verticalSpeed=speed;p.airborne=true;p.fallOriginHeight=SurfaceFloor(p.position+Vector3.up*.2f);
            if(horizontal.sqrMagnitude>0){if(BodyWorld!=null)BodyWorld.Impact(p,horizontal,p.position,false);else p.impulse+=horizontal;}
            SetAction(p,"jump");CombatEvent("party_launch",p,impulse:horizontal+Vector3.up*speed);
        }
        void PartyMotor(Player p,ref Vector3 desired,float dt)
        {
            if(!BodyLocked(p)&&Time>=p.staggerUntil&&OnPartyGround(p))desired+=BeltVelocity(p.position,1);
            var e=MovementFor(p);if(e==null)return;
            if(!MovementOwnerValid(e,p)){RemovePartyEffect(e);return;}
            if(e.mechanicId=="inflate-shove")desired*=.4f;
            if(e.mechanicId=="glide-open")desired*=.65f;
            if(e.mechanicId=="pogo-hop")
            {
                var facing=Mathf.Atan2(e.velocity.x,e.velocity.z)*Mathf.Rad2Deg;
                var angle=Mathf.MoveTowardsAngle(facing,p.input.yaw,(p.airborne?30:120)*dt);e.velocity=Quaternion.Euler(0,angle,0)*Vector3.forward;
                desired=e.velocity*3.3f;
            }
            if(e.mechanicId=="short-dash"||e.mechanicId=="grapple-anchor")
            {
                float speed=e.mechanicId=="short-dash"?12:8;
                var direction=e.velocity;
                if(e.mechanicId=="grapple-anchor")
                {
                    var anchor=Props.Find(x=>x.id==e.targetId&&x.holder=="");
                    if(anchor==null||!PartyClear(p.position,anchor.position)){RemovePartyEffect(e);desired=Vector3.zero;Say("锚点路线断开了。");return;}
                    var delta=Flat(anchor.position-p.position);if(delta.magnitude<1){RemovePartyEffect(e);desired=Vector3.zero;return;}direction=delta.normalized;
                }
                var from=p.position;from.y=MotorY(p);var next=from+direction*speed*dt;
                bool wall=CampusGeometry.Blocked(next)||!CampusGeometry.ClearSegment(from+Vector3.up*.1f,next+Vector3.up*.1f)||BlockedByBarricade(next);
                var target=Players.Values.Where(x=>x!=p&&!x.eliminated&&DistanceToSegment(x.position,from,next)<1&&Mathf.Abs(x.position.y-from.y)<1.2f).FirstOrDefault();
                if(wall||target!=null)
                {
                    desired=Vector3.zero;EndMovement(e,p);
                    if(target!=null){Hit(target,p,0,2,false,false,false,p.position,direction*2);if(BodyWorld!=null)BodyWorld.Impact(p,-direction,p.position,false);else p.impulse-=direction;}
                    return;
                }
                desired=direction*speed;
            }
        }
        void PartyVertical(Player p)
        {var e=MovementFor(p);if(e!=null&&e.mechanicId=="glide-open"&&MovementOwnerValid(e,p))p.verticalSpeed=Mathf.Max(-2,p.verticalSpeed);}
        void EndMovement(EffectState e,Player p)
        {if(e.mechanicId=="short-dash")p.interactBlockedUntil=Time+.3f;e.stage="ended";e.deadline=Time+.18f;e.remaining=.18f;}
        Vector3 BeltVelocity(Vector3 position,float footOffset)
        {
            if(Mathf.Abs(position.y-footOffset-CampusGeometry.Floor(position,footOffset))>.23f)return Vector3.zero;
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.mechanicId=="belt-floor"&&e.stage=="deployed"))
            {
                var delta=Flat(position-e.position);var side=Vector3.Cross(Vector3.up,e.velocity);
                if(Mathf.Abs(Vector3.Dot(delta,e.velocity))<=2&&Mathf.Abs(Vector3.Dot(delta,side))<=.9f&&Mathf.Abs(CampusGeometry.Floor(position,footOffset)-CampusGeometry.Floor(e.position,.1f))<.3f&&PartyClear(e.position,position))return e.velocity*2.8f;
            }
            return Vector3.zero;
        }
        bool PartyLooseBelt(ref Vector3 velocity,Vector3 position,float footOffset,float dt)
        {
            var belt=BeltVelocity(position,footOffset);if(belt.sqrMagnitude<.01f)return false;
            var flat=Vector3.MoveTowards(Flat(velocity),belt,8*dt);velocity=new Vector3(flat.x,velocity.y,flat.z);return true;
        }
        void TickMovement(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(e.stage=="burst"||e.stage=="ended")
            {if(Time>=e.deadline){if(d.id=="short-dash")owner.interactBlockedUntil=Mathf.Max(owner.interactBlockedUntil,Time+.3f);RemovePartyEffect(e);}return;}
            if(SelfMovement(d.id))
            {
                if(!MovementOwnerValid(e,owner)){RemovePartyEffect(e);return;}
                e.position=owner.position;
                if(d.id=="glide-open"&&!owner.airborne){RemovePartyEffect(e);return;}
                if(d.id=="pogo-hop"&&OnPartyGround(owner)&&owner.verticalSpeed<=0)
                {
                    if(!e.touchTimes.TryGetValue(owner.profile.playerId,out var last)||Time-last>.2f){LaunchParty(owner,6.2f,e.velocity*.7f);e.touchTimes[owner.profile.playerId]=Time;}
                }
                if(d.id=="inflate-shove")
                {
                    e.radius=Mathf.Lerp(.4f,d.radius,Mathf.Clamp01((Time-e.createdAt)/d.lifetime));
                    foreach(var target in Players.Values.Where(x=>x!=owner&&!x.eliminated&&!BodyLocked(x)&&Time>=x.bodyProtectedUntil&&Vector3.Distance(x.position,owner.position)<e.radius+.4f&&PartyClear(owner.position,x.position)).ToArray())
                    {
                        var direction=Flat(target.position-owner.position).normalized;
                        if(!e.hit.Contains(target.profile.playerId)){Hit(target,owner,0,d.force,false,false,false,owner.position,direction*d.force);e.hit.Add(target.profile.playerId);}
                        if(BodyWorld!=null){BodyWorld.Impact(target,direction*(4*dt),target.position,false);BodyWorld.Impact(owner,-direction*(2*dt),owner.position,false);}else {target.impulse+=direction*(4*dt);owner.impulse-=direction*(2*dt);}
                    }
                }
                if(d.id=="stomp-pulse")
                {
                    e.height=Mathf.Max(e.height,MotorY(owner));
                    if(!owner.airborne&&owner.verticalSpeed<=0)
                    {
                        e.position=GroundParty(owner.position);e.stage="burst";e.deadline=Time+.4f;e.remaining=.4f;
                        foreach(var target in Players.Values.Where(x=>x!=owner&&!x.eliminated&&OnPartyGround(x)&&Vector3.Distance(x.position,e.position)<d.radius&&Mathf.Abs(CampusGeometry.Floor(x.position)-CampusGeometry.Floor(e.position,.1f))<.3f&&PartyClear(e.position,x.position)).ToArray())
                        {var direction=Flat(target.position-e.position).normalized;Hit(target,owner,Mathf.Min(30,d.damage+Mathf.Max(0,e.height-MotorY(owner))*3),d.force,true,false,false,e.position,direction*d.force+Vector3.up);e.hit.Add(target.profile.playerId);}
                        CombatEvent("party_stomp",owner,reason:d.id);return;
                    }
                }
            }
            if(d.id=="spring-pad")
            {
                foreach(var p in Players.Values.Where(x=>!x.eliminated&&!BodyLocked(x)&&Time>=x.bodyProtectedUntil&&OnPartyGround(x)&&Flat(x.position-e.position).magnitude<d.radius&&Mathf.Abs(CampusGeometry.Floor(x.position)-CampusGeometry.Floor(e.position,.1f))<.3f&&PartyClear(e.position,x.position)).ToArray())
                {
                    if(e.touchTimes.TryGetValue(p.profile.playerId,out var last)&&Time-last<1)continue;
                    LaunchParty(p,8,Flat(p.velocity).sqrMagnitude>.05f?Flat(p.velocity).normalized*2:e.velocity*2);e.touchTimes[p.profile.playerId]=Time;
                }
            }
            if(d.id=="belt-floor")foreach(var p in Players.Values.Where(x=>!x.eliminated&&BodyDown(x)))
            {var belt=BeltVelocity(p.position,.6f);if(belt.sqrMagnitude>.01f&&BodyWorld!=null)BodyWorld.Impact(p,belt*dt,p.position,false);}
            if(d.id=="paired-teleport"&&e.stage=="portal_in")TickPortal(e,dt);
            if(Time+.0001f>=e.deadline)
            {if(d.id=="short-dash")owner.interactBlockedUntil=Mathf.Max(owner.interactBlockedUntil,Time+.3f);RemovePartyEffect(e);}
        }
        void TickPortal(EffectState entrance,float dt)
        {
            var exit=Effects.Find(e=>e.id==entrance.targetId&&e.stage=="portal_out");if(exit==null){RemovePartyEffect(entrance);return;}
            foreach(var p in Players.Values)
            {
                bool close=Flat(p.position-entrance.position).magnitude<.75f;
                bool eligible=!p.eliminated&&p.connected&&!BodyLocked(p)&&!IsGrabbed(p)&&p.grabbing==""&&p.interaction==""&&p.carrying==""&&p.publicProp==""&&OnPartyGround(p)&&Time-p.lastInput<.35f;
                if(p.portalEntry==entrance.id)
                {
                    if(!close||!eligible){p.portalEntry="";continue;}
                    if(Time-p.portalStartedAt<.6f)continue;
                    var landing=exit.position+exit.velocity*.95f;landing.y=CampusGeometry.Floor(exit.position,.1f)+1;
                    if(CampusGeometry.Blocked(landing,.55f)||BlockedByBarricade(landing)||!CampusGeometry.ClearSegment(exit.position+Vector3.up*.8f,landing))
                    {p.portalEntry="";Say("出口被挡住，传送已取消。");continue;}
                    p.position=landing;p.motorHeight=landing.y;p.verticalSpeed=0;p.impulse=p.velocity=Vector3.zero;p.airborne=false;p.portalEntry="";
                    BodyWorld?.Reset(p);SetAction(p,"teleport");entrance.touchTimes[p.profile.playerId]=Time+1;exit.touchTimes[p.profile.playerId]=Time+1;CombatEvent("party_teleport",p,reason:entrance.id);continue;
                }
                var input=new Vector3(p.input.x,0,p.input.z);
                bool voluntary=input.sqrMagnitude>.04f&&Vector3.Dot(input.normalized,Flat(entrance.position-p.position).normalized)>.1f;
                if(close&&eligible&&voluntary&&(!entrance.touchTimes.TryGetValue(p.profile.playerId,out var until)||Time>=until))
                {p.portalEntry=entrance.id;p.portalStartedAt=Time;}
            }
        }
    }
}
