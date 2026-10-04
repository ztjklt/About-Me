using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        bool BorrowedBy(PersonaInstance item,Player p)=>item!=null&&item.asset.visualId==TraitAbilityCatalog.VisualId&&item.state==ItemState.Equipped&&item.carrierId==p.profile.playerId&&item.ownerId!=p.profile.playerId;
        void ReleaseBorrowedWord(Player p)
        {
            var item=Item(p.equipped);if(!BorrowedBy(item,p))return;
            CancelSelfFields(p);CancelSelfGuards(p);CancelSelfMovement(p);CancelSelfLoot(p);CancelAction(p);
            item.state=ItemState.Ground;item.carrierId="";item.position=CampusGeometry.SafeDrop(p.position)+Vector3.up*.4f;item.velocity=Vector3.up*2;p.equipped="";
        }
        void BorrowWord(Player p,string targetId=null)
        {
            if(IsGrabbed(p)||Time<p.switchUntil)return;
            var target=Items.Where(x=>x.asset.visualId==TraitAbilityCatalog.VisualId&&x.state==ItemState.Ground&&(string.IsNullOrEmpty(targetId)||x.id==targetId)&&Vector3.Distance(x.position,p.position)<Rules.interactionRange&&LootLineClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();
            if(target==null)return;
            StopMachineOperator(p);CancelSelfFields(p);CancelSelfLoot(p);CancelSelfGuards(p);CancelSelfMovement(p);Interrupt(p);ReleaseGrab(p);CancelAction(p);p.chargingSince=-1;
            ReleaseBorrowedWord(p);var previous=Item(p.equipped);if(previous!=null)previous.state=ItemState.Inventory;
            p.equipped=target.id;target.state=ItemState.Equipped;target.carrierId=target.ownerId==p.profile.playerId?"":p.profile.playerId;target.velocity=Vector3.zero;p.switchUntil=Time+Rules.switchDuration;
            SetAction(p,"equip",target.id);Say(p.profile.nickname+" 使用了「"+target.asset.name+"」；生命仍属于原主人。");
        }
        bool SwallowBorrowedWord(Player p)
        {
            var item=Item(p.equipped);if(!BorrowedBy(item,p))return false;
            CancelSelfFields(p);CancelSelfLoot(p);CancelSelfGuards(p);CancelSelfMovement(p);CancelAction(p);p.chargingSince=-1;p.equipped="";
            item.state=ItemState.Carried;p.carrying=item.id;p.interaction="eat";p.interactionStart=Time;SetAction(p,"eat",item.id);return true;
        }
    }
}
