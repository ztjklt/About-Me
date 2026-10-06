using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public partial class Match
    {
        // One successful contact per target and deployment. Protection must not
        // consume the contact, and the existing Hit path owns the single life drop.
        void HitContactHazard(EffectState effect,Player owner,Player target)
        {
            if((effect.deadline>0&&Time>=effect.deadline)||target==owner||target.eliminated||BodyLocked(target)||Time<target.bodyProtectedUntil||effect.hit.Contains(target.profile.playerId))return;
            var direction=Flat(target.position-effect.position);
            if(direction.sqrMagnitude<.001f)direction=Forward(owner);
            Hit(target,owner,Rules.maxStability,7,true,false,true,effect.position,direction.normalized*7+Vector3.up*3);
            if(BodyDown(target))
            {effect.hit.Add(target.profile.playerId);CombatEvent("contact_hazard",owner,target,reason:effect.mechanicId??"Barricade");}
        }

        void HitBarricadeContact(Player target,Vector3 attempted)
        {
            if(!OnPartyGround(target))return;
            foreach(var effect in Effects.Where(e=>e.kind=="Barricade"&&Vector3.Distance(e.position,attempted)<e.radius+.38f).ToArray())
                if(Players.TryGetValue(effect.owner,out var owner)&&PartyClear(effect.position,target.position))HitContactHazard(effect,owner,target);
        }
    }
}
