using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        LineRenderer[] lootPointers;
        void PresentLoot(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            bool privateInfo=d.id=="own-item-beacon"||d.id=="loose-radar";
            bool ended=e.stage=="ended"||e.stage=="burst";
            var color=e.stage=="burst"?new Color(1,.4f,.15f):d.id=="public-hot-potato"?new Color(1,.55f,.1f):d.id=="trip-line"?new Color(.94f,.43f,.54f):new Color(.17f,.75f,.6f);
            ring.startColor=ring.endColor=color;ring.loop=true;ring.positionCount=64;
            float floor=CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y;
            float radius=d.id=="carry-cradle"?.8f:d.id=="trip-line"?.45f:d.radius;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,floor,Mathf.Sin(a)*radius));}
            ring.gameObject.SetActive(!ended&&d.id!="tongs-place"&&d.id!="rescue-tow"&&d.id!="float-lift");
            model.SetActive(!ended&&!e.targetId.StartsWith("device-")&&e.stage!="line_end");model.transform.localPosition=Vector3.up*(privateInfo?.45f:.2f);
            if(privateInfo)model.transform.localRotation=Quaternion.Euler(0,now*50,0);
            if(lootPointers==null){lootPointers=new LineRenderer[6];for(int i=0;i<6;i++)lootPointers[i]=FieldLine(transform,"Bounded loot pointer "+i);}
            var owner=state.players.FirstOrDefault(x=>x.id==e.owner);Vector3[] points=e.path??System.Array.Empty<Vector3>();
            if(d.id=="tongs-place"||d.id=="rescue-tow"||d.id=="float-lift")points=state.ground.Where(x=>x.id==e.targetId).Select(x=>x.position).ToArray();
            for(int i=0;i<lootPointers.Length;i++)
            {
                var line=lootPointers[i];bool visible=!ended&&i<points.Length&&d.id!="carry-cradle"&&d.id!="gentle-push-clear"&&e.stage!="line_end";line.gameObject.SetActive(visible);if(!visible)continue;
                line.useWorldSpace=true;line.positionCount=2;line.startColor=line.endColor=color;
                if(e.stage=="line_armed"){line.gameObject.SetActive(i==0);line.SetPosition(0,points[0]+Vector3.up*.12f);line.SetPosition(1,points[1]+Vector3.up*.12f);}
                else{line.SetPosition(0,privateInfo||d.id=="rescue-tow"||d.id=="tongs-place"?(owner?.position??e.position)+Vector3.up*.3f:e.position+Vector3.up*.2f);line.SetPosition(1,points[i]+Vector3.up*.15f);}
            }
            label.gameObject.SetActive(!ended&&e.stage!="line_end");label.transform.localPosition=Vector3.up*1.55f+(camera?-camera.transform.right*2.4f:Vector3.zero);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="own-item-beacon"?(e.hit.Count==0?"8米内无自己的地面生命":"自己的生命指路 · E 回收"):d.id=="loose-radar"?"可见地面生命 "+e.hit.Count+" · Q 关闭":d.id=="tongs-place"?"夹移中 · 完成后仍需 E":d.id=="carry-cradle"?"托盘 "+e.hit.Count+"/2 · G 搬 / E 取":d.id=="public-hot-potato"?(e.stage=="offered"?"邀接中 · 对方 G 自愿接 / F 扔":"烫包 · G 拿 / F 邀接 / 可拆"):d.id=="rescue-tow"?"拖救自己的生命 · Q 松":d.id=="float-lift"?"浮托 · 谁都能 E 取":d.id=="mischief-swap-props"?"两箱交换预警 · G 拿起可取消":d.id=="gentle-push-clear"?"轻推倒地者 · 起身时间不变":e.stage=="line_wait"?"第一端 · 移动1.2–5米再 F":"跑过绊线 · 走 / 跳过 · 可拆";
            label.text=help+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
    }
}
