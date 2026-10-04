using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        static bool ArenaCargo(string id)=>id=="cargo-winch"||id=="public-launcher";
        EffectState ArenaOperator(Player p)=>Effects.FirstOrDefault(e=>ArenaCargo(e.mechanicId)&&VirtualAlive(e)&&e.machine.operatorId==p.profile.playerId);
        void StopArenaOperator(Player p)
        {foreach(var e in Effects.Where(e=>ArenaCargo(e.mechanicId)&&e.machine!=null&&e.machine.operatorId==p.profile.playerId)){e.machine.operatorId="";e.stage=e.targetId==""?"empty":"loaded";}}
        public float ArenaGravityAt(Vector3 point)
        {return Effects.Any(e=>e.mechanicId=="low-gravity-zone"&&VirtualAlive(e)&&Flat(point-e.position).magnitude<e.radius&&point.y>e.position.y-.5f&&point.y<e.position.y+4&&LootLineClear(e.position+Vector3.up,point))?Rules.gravity*.35f:Rules.gravity;}
        string ArenaStatus(Player p)=>ArenaOperator(p)?.mechanicId=="cargo-winch"?"绞盘操作 · W/S 升降 / E或Q松手":ArenaOperator(p)?.mechanicId=="public-launcher"?"发射器瞄准 · 转向 / 蓄1秒 / F发射 / Q松手":Mathf.Abs(ArenaGravityAt(p.position)-Rules.gravity)>.01f?"公共低重力 · 出区恢复":null;
        bool ArenaPublicHeld(PublicProp prop)=>prop.holder==""&&Effects.Any(e=>ArenaCargo(e.mechanicId)&&e.targetId==prop.id&&VirtualAlive(e));
        void ReleaseArenaCargo(EffectState e)
        {if(!ArenaCargo(e.mechanicId))return;var prop=Props.Find(p=>p.id==e.targetId);if(prop!=null&&prop.holder==""){prop.velocity=Vector3.zero;prop.thrower="";}if(e.machine!=null)e.machine.operatorId="";e.targetId="";}
        bool ArenaOperatorAction(Player p,string action)
        {
            var e=ArenaOperator(p);if(e==null||action==""||action=="interact")return false;
            if(action=="use_item"&&e.mechanicId=="public-launcher")
            {
                var prop=Props.Find(x=>x.id==e.targetId);if(prop==null||prop.holder!=""){StopArenaOperator(p);return true;}
                var direction=Forward(p);var start=e.position+Vector3.up*.75f+direction*.6f;
                if(CampusGeometry.Blocked(start,.35f,.6f)||!LootLineClear(prop.position,start)){Say("发射出口被挡，换朝向再F。");return true;}
                float charge=Mathf.Clamp01(Time-e.triggerAt);prop.position=start;prop.velocity=direction*(6+charge*5)+Vector3.up*(3+charge*3);prop.thrower=p.profile.playerId;prop.hitAfter=Time+.08f;prop.pushScale=1;prop.guardTouches.Clear();e.targetId="";e.machine.operatorId="";e.stage="empty";e.force=0;SetAction(p,"throw",prop.id);CombatEvent("party_public_launch",p,reason:prop.id);Say("同一公共物已射出；发射器可再装载，不生成生命或弹药。");return true;
            }
            if(action=="grab"||action=="grab_item"||action=="equip"){StopArenaOperator(p);return false;}
            return true;
        }
        void ArenaMotor(Player p,ref Vector3 desired)
        {if(ArenaOperator(p)!=null)desired=Vector3.zero;}
        bool ArenaPoint(Vector3 point,float extent)
        {for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){var at=point+new Vector3(x*extent,0,z*extent);if(!LinkPlacement(at)||Mathf.Abs(CampusGeometry.Floor(at,.1f)-(point.y-.15f))>.12f)return false;}return true;}
        bool TryArenaMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var spot=GroundParty(p.position+Forward(p)*3.7f);PublicProp cargo=null;
            if(d.id=="cargo-winch")
            {cargo=Props.Where(OrdinaryPublic).Where(q=>Vector3.Distance(q.position,p.position)<4&&LootLineClear(p.position,q.position)&&!ArenaPublicHeld(q)&&!LinkPublicHeld(q)).OrderBy(q=>(q.position-p.position).sqrMagnitude).FirstOrDefault();if(cargo==null){Say("绞盘需要近处未持有、未固定的公共箱桶。");p.nextAttack=Time+.3f;return true;}spot=GroundParty(cargo.position);}
            float extent=d.id=="crumble-tiles"?1.8f:d.id=="cooperative-pump"?1.15f:.6f;
            if(!ArenaPoint(spot,extent)||!LootLineClear(p.position,spot)||(d.id=="crumble-tiles"||d.id=="cooperative-pump")&&Players.Values.Any(q=>!q.eliminated&&Flat(q.position-spot).magnitude<extent+1)){Say("场地装置需平整空地，不能在人脚下、地图外或其他机关上。");p.nextAttack=Time+.3f;return true;}
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,position=spot,velocity=Forward(p),createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,stage="ready"};
            if(d.id!="low-gravity-zone"&&d.id!="item-sorter")e.machine=new MachineState();
            if(d.id=="crumble-tiles")e.arenaTimers=new float[6];
            if(d.id=="item-sorter")e.stage="own-left";
            if(d.id=="cooperative-pump")e.chainIndex=-1;
            if(ArenaCargo(d.id))e.stage="empty";
            Effects.Add(e);if(cargo!=null)CaptureArenaCargo(e,cargo);return true;
        }
        Vector3 ArenaButton(EffectState e)=>e.position-e.velocity*1.7f;
        void CaptureArenaCargo(EffectState e,PublicProp prop)
        {e.targetId=prop.id;e.path=new[]{prop.position};e.stage="loaded";prop.velocity=Vector3.zero;prop.thrower="";e.machine.height=0;}
        bool ArenaFree(Player p)=>ControlTarget(p)&&!IsGrabbed(p)&&p.grabbing==""&&p.carrying=="";
        bool TryArenaInteraction(Player p,string target)
        {
            var operating=ArenaOperator(p);if(operating!=null){StopArenaOperator(p);Say("已松开公共装置，其他人可接管。");return true;}
            if(!string.IsNullOrEmpty(target)||!ArenaFree(p)||Items.Any(i=>i.state==ItemState.Ground&&Vector3.Distance(i.position,p.position)<Rules.interactionRange))return false;
            var e=Effects.Where(VirtualAlive).Where(e=>e.mechanicId=="cooperative-pump"||e.mechanicId=="item-sorter"||ArenaCargo(e.mechanicId)).Where(e=>Vector3.Distance(p.position,(ArenaCargo(e.mechanicId)?ArenaButton(e):e.position)+Vector3.up*.85f)<2.3f&&LootLineClear(p.position,e.position)).OrderBy(e=>(p.position-e.position).sqrMagnitude).FirstOrDefault();if(e==null)return false;
            if(e.mechanicId=="item-sorter")
            {if(p.publicProp!="")return false;e.stage=e.stage=="own-left"?"own-right":"own-left";CombatEvent("party_sorter_reverse",p);Say("分流方向已翻转，仍保留原主并需正常E回收/吞噬。");return true;}
            if(e.mechanicId=="cooperative-pump")
            {
                if(p.publicProp!="")return false;var right=Quaternion.LookRotation(e.velocity)*Vector3.right;float side=Vector3.Dot(Flat(p.position-e.position),right);int slot=side<-.8f?0:side>.8f?1:-1;
                if(slot<0||Mathf.Abs(Vector3.Dot(Flat(p.position-e.position),e.velocity))>1.1f){Say("站在充气台左或右脚泵上再E。");return true;}
                if(Time<e.triggerAt||slot==e.chainIndex||e.hit.LastOrDefault()==p.profile.playerId){Say("需另一个人、另一侧交替E充气；单人连按无效。");return true;}e.hit.Clear();e.hit.Add(p.profile.playerId);e.chainIndex=slot;e.force=Mathf.Min(1.2f,e.force+.45f);e.triggerAt=Time+.25f;CombatEvent("party_pump",p);return true;
            }
            if(e.targetId=="")
            {
                PublicProp cargo=p.publicProp!=""?Props.Find(q=>q.id==p.publicProp&&q.holder==p.profile.playerId&&(q.visual=="box"||q.visual=="trash")&&!q.id.StartsWith("device-",StringComparison.Ordinal)):Props.Where(OrdinaryPublic).Where(q=>Vector3.Distance(q.position,e.position+Vector3.up*.45f)<1.5f&&!LinkPublicHeld(q)&&!ArenaPublicHeld(q)&&LootLineClear(e.position,q.position)).OrderBy(q=>(q.position-e.position).sqrMagnitude).FirstOrDefault();
                if(cargo==null||Vector3.Distance(cargo.position,e.position+Vector3.up*.6f)>2.1f||!LootLineClear(cargo.position,e.position+Vector3.up*.6f)){Say("用G搬普通箱桶到装置，近处E装载；人物和Persona不能装。");return true;}
                if(cargo.holder==p.profile.playerId){cargo.holder="";p.publicProp="";}CaptureArenaCargo(e,cargo);if(e.mechanicId=="public-launcher")cargo.position=e.position+Vector3.up*.75f;Say("已装载原公共物，空手E开始操作，G可取走。");return true;
            }
            if(p.publicProp!="")return false;if(e.machine.operatorId!=""){Say("操作位有人，等他松开或打断。");return true;}
            CancelSelfMovement(p);CancelAction(p);e.machine.operatorId=p.profile.playerId;e.triggerAt=Time;e.stage="operating";Say(e.mechanicId=="cargo-winch"?"绞盘操作：W/S升降，E或Q松手；别人G仍可拿走载物。":"转向瞄准并蓄1秒，F发射原箱桶；E/Q松手。");return true;
        }
        public static System.Collections.Generic.IEnumerable<MachineBox> ArenaShapes(EffectState e,bool previous)
        {
            var yaw=Quaternion.LookRotation(e.velocity);float height=previous?e.machine.previousHeight:e.machine.height;
            if(e.mechanicId=="crumble-tiles")for(int i=0;i<6;i++)if((e.chainIndex&(1<<i))==0)yield return new MachineBox{id=e.id+"/crumble-"+i,center=e.position+yaw*new Vector3((i%3-1)*1.2f,.60f,(i/3-.5f)*1.2f),size=new Vector3(1.1f,.10f,1.1f),rotation=yaw,support=true};
            if(e.mechanicId=="cooperative-pump")yield return new MachineBox{id=e.id+"/deck",center=e.position+Vector3.up*(.10f+height),size=new Vector3(2,.16f,2),rotation=yaw,support=true};
        }
        void TickArena(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);if(Time>=e.deadline){RemovePartyEffect(e);return;}if(e.machine!=null){if(e.machine.lastTick==Time)return;e.machine.lastTick=Time;e.machine.previousHeight=e.machine.height;}
            if(d.id=="crumble-tiles")
            {
                foreach(var box in MachineShapes(e).ToArray())
                {int i=int.Parse(box.id.Substring(box.id.LastIndexOf('-')+1));if(e.arenaTimers[i]==0&&Players.Values.Any(p=>OnMachineBox(p,box)&&!BodyLocked(p)))e.arenaTimers[i]=Time+.45f;if(e.arenaTimers[i]>0&&Time>=e.arenaTimers[i]){e.chainIndex|=1<<i;CombatEvent("party_tile_crumble",owner,reason:i.ToString());}}
                return;
            }
            if(d.id=="cooperative-pump")
            {e.force=Mathf.Max(0,e.force-.16f*dt);e.machine.height=Mathf.MoveTowards(e.machine.height,e.force,dt);CarryMachineDeck(e);return;}
            if(ArenaCargo(d.id))
            {
                var prop=Props.Find(q=>q.id==e.targetId);if(prop==null||prop.holder!=""){e.targetId="";e.machine.operatorId="";e.stage="empty";return;}
                Player op=null;if(e.machine.operatorId!=""&&(!Players.TryGetValue(e.machine.operatorId,out op)||!ArenaFree(op)||op.publicProp!=""||Time-op.lastInput>.35f||Vector3.Distance(op.position,ArenaButton(e)+Vector3.up*.85f)>2.3f)){e.machine.operatorId="";e.stage="loaded";op=null;}
                if(d.id=="cargo-winch")
                {if(op!=null)e.machine.height=Mathf.Clamp(e.machine.height+op.input.z*dt*.8f,0,1.5f);var next=e.path[0]+Vector3.up*e.machine.height;if(CampusGeometry.Blocked(next,.35f,.6f)||!LootLineClear(prop.position,next)){e.machine.height=e.machine.previousHeight;next=e.path[0]+Vector3.up*e.machine.height;}prop.position=next;}
                else {prop.position=e.position+Vector3.up*.75f;if(op!=null)e.force=Mathf.Clamp01(Time-e.triggerAt);}
                prop.velocity=Vector3.zero;prop.thrower="";return;
            }
            if(d.id=="item-sorter")
            {
                var right=Quaternion.LookRotation(e.velocity)*Vector3.right;
                foreach(var life in Items.Where(i=>i.state==ItemState.Ground&&!e.hit.Contains(i.id)&&Flat(i.position-e.position).magnitude<.85f&&Mathf.Abs(i.position.y-.35f-(e.position.y-.15f))<.25f&&LootLineClear(e.position,i.position)).Take(6).ToArray())
                {bool toLeft=(life.ownerId==e.owner)==(e.stage=="own-left");var destination=GroundParty(e.position+right*(toLeft?-2:2));if(!LootLineClear(life.position,destination))continue;e.hit.Add(life.id);life.velocity=right*(toLeft?-3:3)+Vector3.up*1.5f;CombatEvent("party_item_sort",owner,reason:life.id);}
            }
        }
    }
}
