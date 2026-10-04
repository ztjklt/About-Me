using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    [Serializable] public sealed class MachineState
    {
        public float angle,height,previousAngle,previousHeight,lastTick=-1,triggerAt;
        public bool powered,used;
        public string operatorId="",mode="idle";
    }
    public struct MachineBox
    {
        public string id;public Vector3 center,size;public Quaternion rotation;public bool support;
        public bool Contains(Vector3 point,float margin=0)
        {var q=Quaternion.Inverse(rotation)*(point-center);var half=size*.5f+Vector3.one*margin;return Mathf.Abs(q.x)<half.x&&Mathf.Abs(q.y)<half.y&&Mathf.Abs(q.z)<half.z;}
        public bool Surface(Vector3 point,float foot,out float height)
        {
            height=-999;if(!support)return false;var normal=rotation*Vector3.up;var top=center+normal*size.y*.5f;
            height=top.y-(normal.x*(point.x-top.x)+normal.z*(point.z-top.z))/normal.y;
            var q=Quaternion.Inverse(rotation)*(new Vector3(point.x,height,point.z)-center);
            return Mathf.Abs(q.x)<=size.x*.5f&&Mathf.Abs(q.z)<=size.z*.5f;
        }
    }
    public partial class Match
    {
        bool MachineEffect(EffectState e)=>e.kind=="PartyMechanic"&&e.machine!=null&&(PartyMechanicRegistry.Find(e.mechanicId)?.family=="machinery"||e.mechanicId=="gapped-cage"||e.mechanicId=="solid-shadow");
        static bool MachinePlatform(string id)=>id=="turntable-wheel"||id=="balance-plank"||id=="floor-elevator"||id=="seesaw-flip";
        static bool MachineDoor(string id)=>id=="timed-door"||id=="weight-switch";
        public static IEnumerable<MachineBox> MachineShapes(EffectState e,bool previous=false)
        {
            if(e.machine==null)yield break;var m=e.machine;float angle=previous?m.previousAngle:m.angle,height=previous?m.previousHeight:m.height;
            var yaw=Quaternion.LookRotation(e.velocity);var center=e.position;float baseY=center.y-.15f;
            MachineBox Box(string id,Vector3 offset,Vector3 size,Quaternion turn,bool support=false)=>new MachineBox{id=e.id+"/"+id,center=center+yaw*offset,size=size,rotation=yaw*turn,support=support};
            if(e.mechanicId=="solid-shadow")yield return Box("player-only-shadow",new Vector3(0,.27f,0),new Vector3(1.6f,.16f,1.6f),Quaternion.identity,true);
            if(e.mechanicId=="gapped-cage")
            {yield return Box("door-back",new Vector3(0,.60f,-1.2f),new Vector3(2.8f,1.2f,.16f),Quaternion.identity);yield return Box("door-left",new Vector3(-1.4f,.60f,0),new Vector3(.16f,1.2f,2.4f),Quaternion.identity);yield return Box("door-right",new Vector3(1.4f,.60f,0),new Vector3(.16f,1.2f,2.4f),Quaternion.identity);}
            if(MachinePlatform(e.mechanicId))
            {
                bool tilt=e.mechanicId=="balance-plank"||e.mechanicId=="seesaw-flip";
                var size=e.mechanicId=="floor-elevator"?new Vector3(2.2f,.16f,2.2f):new Vector3(4,.16f,1.6f);
                yield return Box("deck",new Vector3(0,(tilt?.45f:.10f)+height,0),size,tilt?Quaternion.Euler(0,0,angle):Quaternion.Euler(0,angle,0),true);
            }
            if(MachineDoor(e.mechanicId)&&!m.powered)yield return Box("door",new Vector3(0,.85f,0),new Vector3(3.2f,1.7f,.20f),Quaternion.identity);
            if(e.mechanicId=="rotating-sweep"&&m.mode!="warning")yield return Box("sweep",new Vector3(0,.27f,0),new Vector3(4.3f,.12f,.12f),Quaternion.Euler(0,angle,0));
            if(e.mechanicId=="rotary-gate")
            {yield return Box("arm-x",new Vector3(0,.50f,0),new Vector3(3.2f,.15f,.12f),Quaternion.Euler(0,angle,0));yield return Box("arm-z",new Vector3(0,.50f,0),new Vector3(.12f,.15f,3.2f),Quaternion.Euler(0,angle,0));}
        }
        public IEnumerable<MachineBox> MachineGeometry()=>Effects.Where(MachineEffect).SelectMany(e=>MachineShapes(e));
        public float SurfaceFloor(Vector3 p,float foot=1)
        {
            float floor=CampusGeometry.Floor(p,foot);
            foreach(var box in MachineGeometry())if(!(box.id.Contains("/player-only")&&foot<.9f)&&box.Surface(p,foot,out var height)&&height<=p.y-foot+.45f&&height>floor)floor=height;
            return floor;
        }
        public bool MachineClear(Vector3 from,Vector3 to,string ignoreId=null)
        {
            foreach(var box in MachineGeometry())
            {
                if(box.id.Contains("/player-only"))continue;
                if(ignoreId!=null&&box.id.StartsWith(ignoreId+"/",StringComparison.Ordinal))continue;
                var inverse=Quaternion.Inverse(box.rotation);var start=inverse*(from-box.center);var ray=inverse*(to-from);var half=box.size*.5f;float near=0,far=1;bool miss=false;
                for(int axis=0;axis<3;axis++)
                {
                    if(Mathf.Abs(ray[axis])<.00001f){if(Mathf.Abs(start[axis])>half[axis]){miss=true;break;}continue;}
                    float a=(-half[axis]-start[axis])/ray[axis],b=(half[axis]-start[axis])/ray[axis];near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));if(near>far){miss=true;break;}
                }
                if(!miss&&far>=0&&near<=1)return false;
            }
            return true;
        }
        public Vector3 MoveOnMachines(Vector3 old,Vector3 next,float radius=.4f,float foot=1)
        {
            foreach(var box in MachineGeometry())
            {
                if(box.support)continue;var sample=next;sample.y=next.y-foot+.8f;
                // Safe doors are solid for the whole body height. Low rods remain physical and jumpable.
                if(box.id.Contains("/door")&&box.Contains(sample,radius))return old;
            }
            return next;
        }
        Vector3 MachineButton(EffectState e)=>e.position-Quaternion.Euler(0,90,0)*e.velocity*2.35f;
        bool MachineBusy(Player p)=>Effects.Any(e=>MachineEffect(e)&&e.machine.operatorId==p.profile.playerId);
        void StopMachineOperator(Player p)
        {foreach(var e in Effects.Where(MachineEffect))if(e.machine.operatorId==p.profile.playerId){e.machine.operatorId="";e.machine.powered=false;}}
        bool TryMachineInteraction(Player p,string target)
        {
            if(MachineBusy(p)){StopMachineOperator(p);return true;}
            if(!string.IsNullOrEmpty(target)||p.carrying!=""||p.grabbing!=""||p.publicProp!=""||IsGrabbed(p))return false;
            var e=Effects.Where(MachineEffect).Where(x=>x.mechanicId=="turntable-wheel"||x.mechanicId=="timed-door"||x.mechanicId=="floor-elevator")
                .Where(x=>Vector3.Distance(p.position,MachineButton(x)+Vector3.up*.85f)<1.65f&&LootLineClear(p.position,MachineButton(x))).OrderBy(x=>(p.position-MachineButton(x)).sqrMagnitude).FirstOrDefault();
            if(e==null)return false;
            // Ground Persona recovery takes priority; never steal E from a nearby real life.
            if(Items.Any(x=>x.state==ItemState.Ground&&Vector3.Distance(x.position,p.position)<Rules.interactionRange))return false;
            if(e.mechanicId=="turntable-wheel")
            {if(e.machine.operatorId!=""){Say("摇柄有人使用，等他松开或打断。");return true;}CancelAction(p);p.chargingSince=-1;e.machine.operatorId=p.profile.playerId;SetAction(p,"pickup",e.id);Say("摇台中：移动、Q、再次 E 或受击停止。");}
            else ActivateMachineButton(e,p);
            return true;
        }
        void ActivateMachineButton(EffectState e,Player p)
        {
            if(!TempoAllows(e)){Say("鼓圈内按钮等强拍：看圆环倒计时，再按 E / F。");return;}
            if(e.mechanicId=="timed-door"){e.machine.powered=true;e.machine.triggerAt=Time+1;e.machine.mode="open";CombatEvent("party_door_open",p);}
            if(e.mechanicId=="floor-elevator"&&!e.machine.used){e.machine.used=true;e.machine.mode="up";e.machine.triggerAt=0;CombatEvent("party_elevator_start",p);}
        }
        bool TryMachineMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var existing=Effects.FirstOrDefault(e=>MachineEffect(e)&&e.sourceInstance==item.id);
            if(existing!=null)
            {
                if(d.id=="timed-door"){if(Vector3.Distance(p.position,existing.position)<6&&CampusGeometry.ClearSegment(p.position,existing.position)&&MachineClear(p.position,existing.position,existing.id))ActivateMachineButton(existing,p);else Say("离软门太远或隔墙，无法按按钮。");p.nextAttack=Time+.45f;return true;}
                if(d.id=="wind-up-cart"){ReleaseMachineCart(existing,p);p.nextAttack=Time+.45f;return true;}
            }
            var spot=GroundParty(p.position+Forward(p)*3.7f);var yaw=Quaternion.LookRotation(Forward(p));
            // Whole footprint / full 1.7m height and rising deck are checked, not just the pivot.
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)
            {
                var sample=spot+yaw*new Vector3(x*1.1f,0,z*.8f);
                if(CampusGeometry.Outside(sample)||Mathf.Abs(CampusGeometry.Floor(sample,.1f)-CampusGeometry.Floor(spot,.1f))>.12f||CampusGeometry.Blocked(sample+Vector3.up, .65f)||CampusGeometry.Blocked(sample+Vector3.up*2.2f,.65f))
                {Say("机关需一块平整、无遮挡且有上方空间的空地。");p.nextAttack=Time+.3f;return true;}
            }
            if(Effects.Where(MachineEffect).Any(e=>Flat(e.position-spot).magnitude<5)||Players.Values.Any(x=>!x.eliminated&&Flat(x.position-spot).magnitude<2.5f))
            {Say("机关范围有人或已有机关，请换空地。");p.nextAttack=Time+.3f;return true;}
            StopMachineOperator(p);partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e2=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,position=spot,velocity=Forward(p),createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,stage="machine",machine=new MachineState()};
            if(d.id=="rotating-sweep"){e2.machine.mode="warning";e2.machine.triggerAt=Time+.7f;}
            if(d.id=="wind-up-cart"){e2.targetId="device-"+e2.id;var pos=spot;pos.y=CampusGeometry.Floor(pos,.1f)+.6f;Props.Add(new PublicProp{id=e2.targetId,visual=e2.visual,position=pos,hitAfter=float.MaxValue});e2.machine.mode="ready";}
            Effects.Add(e2);return true;
        }
        bool ReleaseMachineCart(EffectState e,Player p)
        {
            var prop=Props.Find(x=>x.id==e.targetId);if(prop==null||e.machine.mode!="ready")return true;
            if(prop.holder!=""&&prop.holder!=p.profile.playerId){Say("车被别人拿着，请让持车者 F 放行。");return true;}
            if(!CanReach(p,prop.position,5)||!LootLineClear(p.position,prop.position)){Say("靠近车再放行。");return true;}
            if(prop.holder==p.profile.playerId){prop.holder="";p.publicProp="";e.velocity=Forward(p);}
            e.machine.mode="driving";e.machine.triggerAt=Time+2;prop.velocity=e.velocity*3;prop.thrower="";CombatEvent("party_cart_start",p);return true;
        }
        bool UseHeldMachineCart(Player p,PublicProp prop)
        {var e=Effects.FirstOrDefault(x=>MachineEffect(x)&&x.mechanicId=="wind-up-cart"&&x.targetId==prop.id);if(e==null)return false;ReleaseMachineCart(e,p);return true;}
        bool OnMachineBox(Player p,MachineBox b)
        {var pos=p.position;pos.y=MotorY(p);return !p.eliminated&&!p.airborne&&p.verticalSpeed<=0&&b.Surface(pos,1,out var top)&&Mathf.Abs(pos.y-1-top)<.24f;}
        bool LooseOnMachine(PublicProp p,MachineBox b)=>p.holder==""&&b.Surface(p.position,.6f,out var top)&&Mathf.Abs(p.position.y-.6f-top)<.22f;
        void CarryMachineDeck(EffectState e)
        {
            if(!MachinePlatform(e.mechanicId))return;var before=MachineShapes(e,true).First();var after=MachineShapes(e).First();
            Vector3 Delta(Vector3 point){var local=Quaternion.Inverse(before.rotation)*(point-before.center);var next=after.center+after.rotation*local;return Vector3.ClampMagnitude(next-point,.09f);}
            foreach(var p in Players.Values.Where(x=>OnMachineBox(x,before)&&!BodyLocked(x)||x.bodyMode==BodyMode.GettingUp&&before.Surface(x.getUpPosition,1,out var height)&&Mathf.Abs(x.getUpPosition.y-1-height)<.24f))
            {var delta=Delta(new Vector3(p.position.x,MotorY(p)-1,p.position.z));p.position+=delta;p.motorHeight+=delta.y;if(p.bodyMode==BodyMode.GettingUp)p.getUpPosition+=delta;if(BodyWorld!=null)BodyWorld.Translate(p,delta);}
            foreach(var item in Items.Where(x=>x.state==ItemState.Ground))
                if(before.Surface(item.position,.35f,out var top)&&Mathf.Abs(item.position.y-.35f-top)<.22f){item.position+=Delta(item.position-Vector3.up*.35f);item.velocity.y=0;}
            foreach(var prop in Props.Where(x=>LooseOnMachine(x,before))){prop.position+=Delta(prop.position-Vector3.up*.6f);prop.velocity.y=0;}
        }
        void TickMachinesBeforeMotor(float dt)
        {foreach(var e in Effects.Where(MachineEffect).ToArray())TickPartyEffect(e,dt);}
        void TickMachine(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            var m=e.machine;if(m==null){RemovePartyEffect(e);return;}e.remaining=Mathf.Max(0,e.deadline-Time);
            if(Time>=e.deadline){RemovePartyEffect(e);return;}if(m.lastTick==Time)return;m.lastTick=Time;m.previousAngle=m.angle;m.previousHeight=m.height;
            var yaw=Quaternion.LookRotation(e.velocity);var right=yaw*Vector3.right;var button=MachineButton(e);bool wasOpen=m.powered;
            bool GroundAt(Vector3 point,float foot,Vector3 at,float range)=>Mathf.Abs(point.y-foot-(e.position.y-.15f))<.30f&&Flat(point-at).magnitude<range&&LootLineClear(at,point);
            if(d.id=="step-switch")m.powered=Players.Values.Any(p=>!p.eliminated&&!p.airborne&&GroundAt(new Vector3(p.position.x,MotorY(p),p.position.z),1,button,.72f));
            if(d.id=="weight-switch")m.powered=Props.Any(p=>OrdinaryPublic(p)&&GroundAt(p.position,.6f,button,.72f));
            if(MachineDoor(d.id))
            {
                if(d.id=="timed-door"&&Time>=m.triggerAt)m.powered=false;
                if(!m.powered&&wasOpen)
                {
                    var closed=MachineShapes(e).First();bool overlaps=Players.Values.Any(p=>!p.eliminated&&closed.Contains(p.position,.65f))||Props.Any(p=>closed.Contains(p.position,.65f))||Items.Any(x=>(x.state==ItemState.Ground||x.state==ItemState.Carried||x.state==ItemState.Thrown)&&closed.Contains(x.position,.4f));
                    if(overlaps){m.powered=true;m.mode="waiting_clear";}else m.mode="closed";
                }
                else m.mode=m.powered?"open":"closed";
            }
            if(d.id=="turntable-wheel")
            {
                bool channel=m.operatorId!=""&&Players.TryGetValue(m.operatorId,out var op)&&op.connected&&!BodyLocked(op)&&Time>=op.staggerUntil&&!IsGrabbed(op)&&Time-op.lastInput<=.35f&&op.carrying==""&&op.grabbing==""&&op.publicProp==""&&op.input.x*op.input.x+op.input.z*op.input.z<.01f&&Vector3.Distance(op.position,button+Vector3.up*.85f)<1.65f;
                float vane=WindLinkedSpeed(e);m.powered=channel||Mathf.Abs(vane)>.01f;if(!channel)m.operatorId="";if(m.powered)m.angle=Mathf.Repeat(m.angle+(channel?50:vane)*dt,360);
            }
            if(d.id=="floor-elevator")
            {
                if(m.mode=="up"){m.height=Mathf.MoveTowards(m.height,1.2f,.65f*dt);if(m.height>=1.2f){m.mode="wait";m.triggerAt=Time+1;}}
                else if(m.mode=="wait"&&Time>=m.triggerAt)m.mode="down";
                else if(m.mode=="down"){m.height=Mathf.MoveTowards(m.height,0,.65f*dt);if(m.height<=0)m.mode="returned";}
            }
            if(d.id=="balance-plank"||d.id=="seesaw-flip")
            {
                var box=MachineShapes(e,true).First();var people=Players.Values.Where(p=>OnMachineBox(p,box)&&!BodyLocked(p)).ToArray();var weights=Props.Where(p=>LooseOnMachine(p,box)&&OrdinaryPublic(p)).ToArray();
                float torque=people.Sum(p=>Vector3.Dot(p.position-e.position,right))+weights.Sum(p=>Vector3.Dot(p.position-e.position,right)*1.5f);
                m.angle=Mathf.MoveTowards(m.angle,Mathf.Clamp(-torque*7,-12,12),30*dt);
                if(d.id=="seesaw-flip"&&Mathf.Abs(torque)>1&&Time>=m.triggerAt)
                {
                    var light=people.Where(p=>Vector3.Dot(p.position-e.position,right)*torque<-.15f).ToArray();bool loaded=people.Any(p=>Vector3.Dot(p.position-e.position,right)*torque>.15f)||weights.Length>0;
                    if(loaded&&light.Length>0){foreach(var p in light){LaunchParty(p,4.2f,Vector3.zero);CombatEvent("party_seesaw_launch",owner,p);}m.triggerAt=Time+1.8f;}
                }
            }
            if(d.id=="rotating-sweep")
            {
                if(m.mode=="warning"&&Time>=m.triggerAt)m.mode="sweeping";
                if(m.mode=="sweeping")
                {
                    m.angle=Mathf.Repeat(m.angle+100*dt,360);var rod=MachineShapes(e).First();
                    foreach(var p in Players.Values.Where(p=>!p.eliminated&&!p.airborne&&!BodyLocked(p)))
                    {
                        var at=new Vector3(p.position.x,MotorY(p)-.60f,p.position.z);
                        if(!rod.Contains(at,.33f)||!LootLineClear(e.position,p.position)||(e.touchTimes.TryGetValue(p.profile.playerId,out var until)&&Time<until))continue;
                        e.touchTimes[p.profile.playerId]=Time+1.2f;var tangent=Vector3.Cross(Vector3.up,Flat(p.position-e.position)).normalized*d.force;
                        Hit(p,owner,d.damage,d.force,false,impulseOverride:tangent);CombatEvent("party_sweep",owner,p);
                    }
                }
            }
            if(d.id=="rotary-gate")
            {
                float speed=0;
                foreach(var p in Players.Values.Where(x=>!x.eliminated&&!x.airborne&&!BodyLocked(x)))
                {
                    var delta=Flat(p.position-e.position);if(delta.magnitude<.55f||delta.magnitude>1.95f||Mathf.Abs(MotorY(p)-1-(e.position.y-.15f))>.35f)continue;
                    var input=new Vector3(p.input.x,0,p.input.z);var tangent=Vector3.Cross(Vector3.up,delta.normalized);float request=Vector3.Dot(input,tangent);if(Mathf.Abs(request)>.1f&&MachineShapes(e).Any(b=>b.Contains(p.position-Vector3.up*.45f,.40f)))speed+=request*85;
                }
                speed=Mathf.Clamp(speed,-85,85);m.powered=Mathf.Abs(speed)>.1f;m.angle=Mathf.Repeat(m.angle+speed*dt,360);
                if(m.powered)foreach(var prop in Props.Where(p=>OrdinaryPublic(p)&&MachineShapes(e).Any(b=>b.Contains(p.position,.35f))))
                {prop.velocity+=Vector3.Cross(Vector3.up,Flat(prop.position-e.position)).normalized*Mathf.Sign(speed)*5*dt;prop.velocity=Vector3.ClampMagnitude(prop.velocity,3);}
            }
            if(d.id=="wind-up-cart")
            {
                var prop=Props.Find(x=>x.id==e.targetId);if(prop==null){RemovePartyEffect(e);return;}e.position=prop.position; // A cart's warning follows the actual public object.
                if(m.mode=="driving")
                {
                    if(prop.holder!=""){m.mode="ready";prop.velocity=Vector3.zero;}
                    else
                    {
                        var from=prop.position;var wanted=from+e.velocity*3*dt;var next=CampusGeometry.Move(from,wanted,.35f,.6f);
                        var hit=Effects.Where(MachineEffect).Where(x=>x!=e&&(x.mechanicId=="timed-door"||x.mechanicId=="floor-elevator")).FirstOrDefault(x=>DistanceToSegment(MachineButton(x),from,next)<.7f&&LootLineClear(from,MachineButton(x)));
                        if(hit!=null){ActivateMachineButton(hit,owner);m.mode="spent";prop.velocity=Vector3.zero;CombatEvent("party_cart_button",owner);}
                        else if(Time>=m.triggerAt||(next-wanted).sqrMagnitude>.00001f||MoveOnMachines(from,next,.35f,.6f)!=next){m.mode="spent";prop.velocity=Vector3.zero;}
                        else prop.velocity=e.velocity*3;
                    }
                }
            }
            CarryMachineDeck(e);
        }
        void MachineMotor(Player p,ref Vector3 desired,float dt)
        {
            foreach(var e in Effects.Where(MachineEffect))
            {
                var m=e.machine;
                if(m.operatorId==p.profile.playerId){desired=Vector3.zero;p.fieldStatus="持续摇转台 · 移动 / E / Q 停止";}
                if(e.mechanicId=="step-switch"&&m.powered)
                {var delta=Flat(p.position-e.position);if(delta.magnitude<4&&delta.sqrMagnitude>.01f&&Vector3.Dot(delta.normalized,e.velocity)>.85f&&Mathf.Abs(p.position.y-e.position.y)<2&&LootLineClear(e.position,p.position)){desired+=e.velocity*2;p.fieldStatus="踩板联动风扇 · 离板停";}}
                if(e.mechanicId=="balance-plank"&&OnMachineBox(p,MachineShapes(e).First())&&Mathf.Abs(m.angle)>7)
                {desired+=Quaternion.LookRotation(e.velocity)*Vector3.right*Mathf.Sign(-m.angle)*1.4f;p.fieldStatus="倾台滑向低侧 · 跳开 / 配平";}
            }
        }
        bool MachineLoose(ref Vector3 velocity,Vector3 point,float foot,float dt)
        {
            bool driven=false;foreach(var e in Effects.Where(MachineEffect))
            {
                if(e.mechanicId=="step-switch"&&e.machine.powered)
                {var delta=Flat(point-e.position);if(delta.magnitude<4&&delta.sqrMagnitude>.01f&&Vector3.Dot(delta.normalized,e.velocity)>.85f&&Mathf.Abs(point.y-e.position.y)<2&&LootLineClear(e.position,point)){velocity+=e.velocity*6*dt;driven=true;}}
                if(e.mechanicId=="balance-plank"&&Mathf.Abs(e.machine.angle)>7&&MachineShapes(e).First().Surface(point,foot,out var top)&&Mathf.Abs(point.y-foot-top)<.2f)
                {velocity+=Quaternion.LookRotation(e.velocity)*Vector3.right*Mathf.Sign(-e.machine.angle)*4*dt;driven=true;}
            }
            return driven;
        }
    }
}
