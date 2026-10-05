using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public static class AnimalPersonaCatalog
    {
        public static readonly string[] Visuals={"cat","animal-dog","animal-rabbit","animal-hamster"};
        public static bool IsAnimal(string visual)=>Visuals.Contains(visual);
    }
    public partial class Match
    {
        const float AnimalRange=12,AnimalDuration=4,AnimalSpeed=7;
        Player ChooseAnimalTarget(PersonaInstance item)
        {
            var nearby=Players.Values.Where(p=>p.profile.playerId!=item.thrownBy&&p.connected&&!p.eliminated
                &&p.bodyMode!=BodyMode.GettingUp&&Time>=p.bodyProtectedUntil
                &&Mathf.Abs(p.position.y-item.position.y)<1.8f
                &&Vector3.Distance(p.position,item.position)<=AnimalRange&&PartyClear(item.position,p.position))
                .OrderBy(p=>Vector3.Distance(p.position,item.position)).ToArray();
            if(nearby.Length==0)return null;
            float closest=Vector3.Distance(nearby[0].position,item.position);
            var pool=nearby.Where(p=>Vector3.Distance(p.position,item.position)<=closest+2.5f).Take(3).ToArray();
            return pool[UnityEngine.Random.Range(0,pool.Length)];
        }
        void LandSingleUsePersona(PersonaInstance item,Vector3 position)
        {
            if(item==null||item.state!=ItemState.Thrown)return;
            item.state=ItemState.Ground;item.thrownBy="";item.chaseTarget="";item.carrierId="";
            item.position=CampusGeometry.SafeDrop(position);item.position.y=SurfaceFloor(item.position,.35f)+.35f;
            item.velocity=Vector3.zero;
        }
        bool TickAnimalPersona(PersonaInstance item,float dt)
        {
            // A short launch uses the ordinary throw arc before the animal begins running.
            if(Time-item.thrownAt<.16f)return false;
            if(Time-item.thrownAt>=AnimalDuration||!Players.TryGetValue(item.thrownBy,out var owner)||owner.eliminated)
            {LandSingleUsePersona(item,item.position);return true;}
            if(!Players.TryGetValue(item.chaseTarget,out var target)||target.eliminated||!target.connected
                ||target.bodyMode==BodyMode.GettingUp||Time<target.bodyProtectedUntil)
            {target=ChooseAnimalTarget(item);item.chaseTarget=target?.profile.playerId??"";}
            if(target==null||!PartyClear(item.position,target.position)||Vector3.Distance(item.position,target.position)>AnimalRange)
            {LandSingleUsePersona(item,item.position);return true;}
            var from=item.position;var direction=target.position-from;direction.y=0;
            item.velocity=direction.normalized*AnimalSpeed;
            var wanted=from+item.velocity*dt;wanted.y=SurfaceFloor(wanted,.35f)+.35f;
            var next=CampusGeometry.Move(from,wanted,.2f,.35f);
            if(!MachineClear(from,next)||(new Vector2(next.x-wanted.x,next.z-wanted.z)).sqrMagnitude>.0001f)
            {LandSingleUsePersona(item,from);return true;}
            var shotPosition=from;var velocity=item.velocity;
            int guarded=GuardShot(item.id,ref shotPosition,ref velocity,ref item.thrownBy,ref item.pushScale,item.guardTouches,next);
            if(guarded!=0){item.chaseTarget="";item.position=shotPosition;item.velocity=velocity;if(guarded==1)LandSingleUsePersona(item,shotPosition);return true;}
            item.position=next;
            if(DistanceToSegment(target.position,from,next)<1.1f)
            {
                Hit(target,owner,Mathf.Max(Rules.maxStability,target.stability),11,true,false,true,horizontalScale:item.pushScale);
                LandSingleUsePersona(item,next);
            }
            return true;
        }
        void StartSingleUsePartyThrow(Player p,PersonaInstance item,EffectState e)
        {
            e.ownsThrownPersona=true;
            item.state=ItemState.Thrown;item.thrownBy=p.profile.playerId;item.thrownAt=Time;
            item.carrierId="";item.position=e.position;item.velocity=Vector3.zero;p.equipped="";
        }
        void LandSingleUsePartyThrow(EffectState e)
        {if(!e.ownsThrownPersona)return;e.ownsThrownPersona=false;LandSingleUsePersona(Item(e.sourceInstance),e.position);}
    }
}
