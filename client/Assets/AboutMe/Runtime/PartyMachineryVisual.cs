using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        readonly Dictionary<string,GameObject> machineParts=new Dictionary<string,GameObject>();
        GameObject buttonModel,signalModel;LineRenderer machineWire;
        void PresentMachinery(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            if(e.machine==null)return;var m=e.machine;var yaw=Quaternion.LookRotation(e.velocity);var button=e.position-yaw*Vector3.right*2.35f;
            bool buttonVisible=d.id=="step-switch"||d.id=="weight-switch"||d.id=="turntable-wheel"||d.id=="timed-door"||d.id=="floor-elevator";
            if(!buttonModel)buttonModel=ToyFactory.Shape("Public control plate",PrimitiveType.Cube,transform,Vector3.zero,new Vector3(1.3f,.10f,1.3f),"#F5CB77");
            buttonModel.SetActive(buttonVisible);buttonModel.transform.position=button+Vector3.up*(m.powered?.01f:.05f);buttonModel.transform.rotation=yaw;
            if(!signalModel)signalModel=ToyFactory.Shape("Signal switch lamp",PrimitiveType.Cube,transform,Vector3.zero,new Vector3(.14f,.25f,.14f),"#77C8BD");
            signalModel.SetActive(buttonVisible);signalModel.transform.position=button+Vector3.up*.24f;signalModel.transform.localScale=new Vector3(.14f,m.powered?.4f:.18f,.14f);
            if(!machineWire)machineWire=FieldLine(transform,"Visible switch connection");machineWire.gameObject.SetActive(buttonVisible);machineWire.useWorldSpace=true;machineWire.positionCount=2;machineWire.SetPosition(0,button+Vector3.up*.08f);machineWire.SetPosition(1,e.position+Vector3.up*.08f);machineWire.startColor=machineWire.endColor=m.powered?new Color(.1f,.8f,.55f):new Color(.85f,.6f,.15f);
            var shapes=Match.MachineShapes(e).ToArray();
            foreach(var key in machineParts.Keys.ToArray())machineParts[key].SetActive(shapes.Any(b=>b.id==key));
            foreach(var b in shapes)
            {
                if(!machineParts.TryGetValue(b.id,out var go)){go=ToyFactory.Shape("Public machine "+b.id,PrimitiveType.Cube,transform,Vector3.zero,b.size,b.support?"#86ADCB":"#F49A6B");machineParts[b.id]=go;}
                go.SetActive(true);go.transform.SetPositionAndRotation(b.center,b.rotation);go.transform.localScale=b.size;
            }
            model.SetActive(d.id!="wind-up-cart");model.transform.localScale=Vector3.one*.65f;model.transform.position=e.position+Vector3.up*(d.id=="step-switch"?.45f:1.25f);model.transform.rotation=yaw;
            ring.loop=true;ring.positionCount=64;ring.gameObject.SetActive(true);ring.startColor=ring.endColor=m.mode=="warning"?new Color(1,.4f,.15f):new Color(.2f,.73f,.72f);
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*d.radius,CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y,Mathf.Sin(a)*d.radius));}
            label.gameObject.SetActive(true);label.transform.position=e.position+Vector3.up*2.25f+(camera?-camera.transform.right*2.5f:Vector3.zero);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="step-switch"?(m.powered?"有人踩板 · 风扇吹向箭头":"踩侧板开风 · 离板停"):d.id=="weight-switch"?(m.powered?"门开 / 等重叠物离开":"仅公共箱桶压板开门"):d.id=="turntable-wheel"?(m.operatorId!=""?"正在摇台 · E / Q / 移动停止":"侧面摇柄按 E · 谁都可摇"):d.id=="timed-door"?(m.powered?(m.mode=="waiting_clear"?"等待人和物离开再合":"开门1秒 · 侧钮 E"):"侧钮 E / 原主 F 开门"):d.id=="rotating-sweep"?(m.mode=="warning"?"即将转杆 · 跳过安全":"低杆扫动 · 跳过 / 拆根部"):d.id=="balance-plank"?"位置重量倾台 · 跳离 / 放箱配平":d.id=="floor-elevator"?(m.used?"升降 "+m.height.ToString("0.0")+"米 · 单次往返":"侧面 E 开关 · 公共升降"):d.id=="rotary-gate"?"走向杆推动 · 箱桶会被另一臂带动":d.id=="wind-up-cart"?(m.mode=="ready"?"F 放行 / G 拿车改方向":m.mode=="driving"?"向前行驶 · 撞按钮一次停":"车已停 · 可搬走 / 拆除"):"双端配重 · 较重端弹起轻端";
            label.text=help+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s · 两击拆除";phase=e.stage;
        }
    }
}
