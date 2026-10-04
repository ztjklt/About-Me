using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        bool VirtualAlive(EffectState e)=>Effects.Contains(e)&&e.kind=="PartyMechanic"&&e.health>0&&Time<e.deadline&&Item(e.sourceInstance)!=null&&Item(e.sourceInstance).state!=ItemState.Consumed&&Players.TryGetValue(e.owner,out var owner)&&!owner.eliminated;
        void CancelSelfVirtual(Player p)
        {foreach(var e in Effects.Where(e=>e.mechanicId=="quantum-echo"&&e.owner==p.profile.playerId).ToArray())RemovePartyEffect(e);}
        bool VirtualBlocks(Player p,string action)=>Effects.Any(e=>e.mechanicId=="quantum-echo"&&e.owner==p.profile.playerId&&e.stage=="record")&&(action=="use_item"||action=="interact");
        string VirtualStatus(Player p)=>Effects.Any(e=>e.mechanicId=="quantum-echo"&&e.owner==p.profile.playerId&&e.stage=="record")?"回响录制1秒 · 移动留路 / Q 停止":null;
        bool TryVirtualMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var direction=Forward(p);var spot=GroundParty(p.position+direction*3.7f);EffectState target=null;Vector3[] ice=null;
            var first=Effects.FirstOrDefault(x=>x.mechanicId=="shot-portal"&&x.sourceInstance==item.id&&x.stage=="portal_wait");
            if(d.id=="shot-portal"&&first!=null)
            {float distance=Flat(spot-first.position).magnitude;if(distance<2||distance>8||!LootLineClear(first.position,spot)){Say("第二光框要相距2–8米且无遮挡，移动并朝向出口再按 F。");p.nextAttack=Time+.3f;return true;}}
            if(d.id=="weather-vane")
            {target=Effects.Where(x=>x.mechanicId=="turntable-wheel"&&VirtualAlive(x)&&Flat(x.position-p.position).magnitude<6&&LootLineClear(p.position,MachineButton(x))).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();if(target==null){Say("先放一个近处可见的手摇转台，再接纸风车。");p.nextAttack=Time+.3f;return true;}spot=GroundParty(MachineButton(target)+direction*.65f);}
            if(d.id=="frost-lock")
            {ice=Effects.Where(x=>x.mechanicId=="wet-trail"&&x.path!=null&&VirtualAlive(x)).SelectMany(x=>x.path).Where(x=>Flat(x-p.position).magnitude<d.radius&&LootLineClear(p.position,x)).Distinct().Take(6).ToArray();if(ice.Length==0){Say("附近没有可凝结的湿区；先铺湿迹或请同伴铺。");p.nextAttack=Time+.3f;return true;}spot=ice[0];}
            if(d.id!="quantum-echo"&&d.id!="weather-vane"&&d.id!="frost-lock")
            {
                float footprint=d.id=="pixel-stamp"?1.5f:d.id=="solid-shadow"?.9f:.6f;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {var sample=spot+Quaternion.LookRotation(direction)*new Vector3(x*footprint,0,z*footprint);if(CampusGeometry.Outside(sample)||Mathf.Abs(CampusGeometry.Floor(sample,.1f)-CampusGeometry.Floor(spot,.1f))>.12f||CampusGeometry.Blocked(sample+Vector3.up,.55f)){Say("光框、格地或影板需要平整且无遮挡的空地。");p.nextAttack=Time+.3f;return true;}}
                if(d.id=="solid-shadow"&&(Players.Values.Any(x=>!x.eliminated&&Flat(x.position-spot).magnitude<1.5f)||MachineGeometry().Any(b=>b.Contains(spot+Vector3.up*.7f,.8f)))){Say("影板范围有人或机关，请换空地。");p.nextAttack=Time+.3f;return true;}
            }
            if(d.id=="quantum-echo"){CancelSelfMovement(p);CancelSelfLoot(p);CancelSelfGuards(p);spot=p.position;}
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,position=spot,velocity=direction,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,stage="virtual"};
            if(d.id=="shot-portal")
            {if(first==null){e.stage="portal_wait";e.deadline=Time+6;Say("第一弹体光框已放，移动并朝向出口再 F；仅攻击弹体可穿。");}else{first.stage="portal_in";e.stage="portal_out";first.targetId=e.id;e.targetId=first.id;first.deadline=e.deadline;Say("双光框已连：普通弹体穿过后沿出口箭头射出。");}}
            if(d.id=="quantum-echo"){e.stage="record";e.path=new[]{p.position};e.triggerAt=Time+1;e.nextHit=Time+.16f;}
            if(d.id=="weather-vane")e.targetId=target.id;
            if(d.id=="frost-lock"){e.path=ice;e.stage="frozen";}
            if(d.id=="solid-shadow"){e.stage="shadow";e.machine=new MachineState();}
            Effects.Add(e);return true;
        }
        bool PixelBright(EffectState e,Player p)
        {
            var q=Quaternion.Inverse(Quaternion.LookRotation(e.velocity))*Flat(p.position-e.position);
            if(Mathf.Abs(q.x)>=1.5f||Mathf.Abs(q.z)>=1.5f)return false;
            int x=Mathf.FloorToInt(q.x+1.5f),z=Mathf.FloorToInt(q.z+1.5f),beat=Mathf.FloorToInt(Time-e.createdAt);
            return (x+z+beat)%2==0;
        }
        bool FrozenAt(Vector3 point,float foot)
        {return Effects.Any(e=>e.mechanicId=="frost-lock"&&e.stage=="frozen"&&VirtualAlive(e)&&e.path!=null&&e.path.Any(x=>Flat(point-x).magnitude<1.2f&&Mathf.Abs(point.y-foot-CampusGeometry.Floor(x,.1f))<.3f&&LootLineClear(x,point)));}
        void DryFrozenPoints(EffectState sponge,Player owner)
        {
            foreach(var ice in Effects.Where(e=>e.mechanicId=="frost-lock"&&e.path!=null).ToArray())
            {
                var points=ice.path.ToList();foreach(var point in ice.path)
                {if(sponge.chainIndex>=2)break;if(Flat(point-sponge.position).magnitude>sponge.radius||!PartyClear(sponge.position,point))continue;points.Remove(point);sponge.chainIndex++;}
                ice.path=points.ToArray();if(points.Count==0)RemovePartyEffect(ice);
                if(sponge.chainIndex>=2){sponge.stage="saturated";sponge.deadline=Time+.35f;CombatEvent("party_dry",owner,reason:"ice");break;}
            }
        }
        Vector3 PublicWind(Vector3 point)
        {
            var sum=Vector3.zero;
            foreach(var e in Effects.Where(VirtualAlive))
            {
                if(e.mechanicId=="fan-volume"&&FieldCone(e,point))sum+=e.velocity;
                if(e.mechanicId=="step-switch"&&e.machine!=null&&e.machine.powered){var q=Flat(point-e.position);if(q.magnitude<4&&Vector3.Dot(q.normalized,e.velocity)>.85f&&LootLineClear(e.position,point))sum+=e.velocity;}
            }
            return Vector3.ClampMagnitude(sum,1);
        }
        float WindLinkedSpeed(EffectState table)
        {
            float speed=0;foreach(var vane in Effects.Where(e=>e.mechanicId=="weather-vane"&&e.targetId==table.id&&VirtualAlive(e)))
            {var wind=PublicWind(vane.position+Vector3.up*.8f);speed+=Vector3.Dot(wind,vane.velocity)*50;}
            return Mathf.Clamp(speed,-50,50);
        }
        bool TempoAllows(EffectState machine)
        {
            var button=MachineButton(machine);
            return !Effects.Any(e=>e.mechanicId=="heartbeat-tempo"&&VirtualAlive(e)&&Flat(button-e.position).magnitude<e.radius&&LootLineClear(e.position,button)&&Mathf.Repeat(Time-e.createdAt,1.4f)>.25f);
        }
        bool VirtualShot(EffectState shot,float dt)
        {
            if(!OrdinaryShot(shot))return false;var from=shot.position;var next=from+shot.velocity*dt;
            foreach(var e in Effects.Where(e=>e.mechanicId=="shot-portal"&&(e.stage=="portal_in"||e.stage=="portal_out")&&VirtualAlive(e)).OrderBy(e=>(e.position-from).sqrMagnitude).ToArray())
            {
                string marker="virtual-portal-"+e.sourceInstance;if(shot.guardTouches.Contains(marker)||shot.guardTouches.Count>=24)continue;
                var center=e.position+Vector3.up*.85f;
                if(!GuardPlane(center,e.velocity,.8f,from,next,true,out var point)||!LootLineClear(from,point))continue;
                var exit=Effects.Find(x=>x.id==e.targetId&&VirtualAlive(x));if(exit==null)continue;
                var rotation=Quaternion.LookRotation(exit.velocity)*Quaternion.Inverse(Quaternion.LookRotation(-e.velocity));
                var destination=exit.position+Vector3.up*.85f+exit.velocity*.4f+rotation*(point-center);
                if(CampusGeometry.Blocked(destination,.2f,.3f)||!MachineClear(exit.position+Vector3.up*.85f,destination)){shot.guardTouches.Add(marker);continue;}
                shot.guardTouches.Add(marker);shot.position=destination;shot.velocity=rotation*shot.velocity;CombatEvent("party_shot_portal",Players[e.owner],reason:shot.id);return true;
            }
            foreach(var e in Effects.Where(e=>e.mechanicId=="vector-arrow"&&VirtualAlive(e)).OrderBy(e=>(e.position-from).sqrMagnitude))
            {
                string marker="virtual-arrow-"+e.id;if(shot.guardTouches.Contains(marker)||shot.guardTouches.Count>=24)continue;
                var center=e.position+Vector3.up*.85f;
                if((Flat(next-center).magnitude>e.radius&&!GuardSphere(center,e.radius,from,next))||Mathf.Abs(next.y-center.y)>1.2f||!LootLineClear(e.position,next))continue;
                shot.guardTouches.Add(marker);float speed=Flat(shot.velocity).magnitude;shot.velocity=e.velocity*speed+Vector3.up*shot.velocity.y;CombatEvent("party_vector_turn",Players[e.owner],reason:shot.id);return true;
            }
            return false;
        }
        Vector3 VirtualPublicMove(PublicProp prop,Vector3 from,Vector3 next)
        {
            if(prop.id.StartsWith("device-",StringComparison.Ordinal)||prop.holder!=""||(prop.visual!="box"&&prop.visual!="trash"))return next;
            foreach(var e in Effects.Where(e=>e.mechanicId=="light-lattice"&&VirtualAlive(e)))
                if(GuardPlane(e.position+Vector3.up*.75f,e.velocity,e.radius,from,next,false,out var point)&&CampusGeometry.ClearSegment(from,point))
                {prop.velocity=Vector3.zero;prop.thrower="";return from;}
            return next;
        }
        void TickVirtualPublicForces(float dt)
        {
            foreach(var e in Effects.Where(e=>e.mechanicId=="virtual-gravity"&&VirtualAlive(e)))
            foreach(var prop in Props.Where(OrdinaryPublic))
                if(Flat(prop.position-e.position).magnitude<e.radius&&Mathf.Abs(prop.position.y-e.position.y)<2&&LootLineClear(e.position,prop.position))prop.velocity=Vector3.ClampMagnitude(prop.velocity+e.velocity*(4*dt),6);
        }
        void TickVirtual(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);if(Time>=e.deadline){RemovePartyEffect(e);return;}
            if(e.machine!=null){if(e.machine.lastTick==Time)return;e.machine.lastTick=Time;}
            if(d.id=="solid-shadow"){if(e.remaining<.8f)e.stage="shadow_fading";return;}
            if(d.id=="pixel-stamp")
            {
                foreach(var p in Players.Values.Where(p=>ControlTarget(p)&&OnPartyGround(p)&&PixelBright(e,p)&&Mathf.Abs(SurfaceFloor(p.position)-CampusGeometry.Floor(e.position,.1f))<.3f&&LootLineClear(e.position,p.position)).ToArray())
                {if(e.touchTimes.TryGetValue(p.profile.playerId,out var at)&&Time-at<1.2f)continue;e.touchTimes[p.profile.playerId]=Time;LaunchParty(p,4.6f,Vector3.zero);CombatEvent("party_pixel_hop",owner,p);}
                return;
            }
            if(d.id=="weather-vane")
            {var table=Effects.Find(x=>x.id==e.targetId&&x.mechanicId=="turntable-wheel"&&VirtualAlive(x));if(table==null){RemovePartyEffect(e);return;}var wind=PublicWind(e.position+Vector3.up*.8f);e.force=wind.magnitude;e.path=new[]{e.position+wind};return;}
            if(d.id=="quantum-echo")
            {
                if(e.stage=="record")
                {
                    if(!MovementOwnerValid(e,owner)||owner.carrying!=""||owner.publicProp!=""||owner.grabbing!=""){RemovePartyEffect(e);return;}
                    if(Time>=e.nextHit){var points=e.path.ToList();if(points.Count<7)points.Add(owner.position);e.path=points.ToArray();e.nextHit=Time+.16f;}
                    if(Time>=e.triggerAt){e.stage="replay";e.chainIndex=0;e.triggerAt=Time;e.position=e.path[0];e.deadline=Time+1.1f;}
                    return;
                }
                int index=Mathf.Min(e.path.Length-1,Mathf.FloorToInt((Time-e.triggerAt)*Mathf.Max(1,e.path.Length-1)));float fraction=Mathf.Repeat((Time-e.triggerAt)*Mathf.Max(1,e.path.Length-1),1);
                var nextPoint=e.path[Mathf.Min(index+1,e.path.Length-1)];e.position=Vector3.Lerp(e.path[index],nextPoint,fraction);
                if(index>=e.chainIndex)
                {var direction=Flat(nextPoint-e.path[index]);if(direction.sqrMagnitude<.01f)direction=e.velocity;else direction.Normalize();foreach(var prop in Props.Where(OrdinaryPublic).Where(x=>Flat(x.position-e.position).magnitude<.9f&&LootLineClear(e.position,x.position)).Take(6)){prop.velocity=Vector3.ClampMagnitude(prop.velocity+direction*.8f,4);CombatEvent("party_echo_push",owner,reason:prop.id);}e.chainIndex=index+1;}
            }
        }
    }
}
