using UnityEngine;

namespace AboutMe
{
    // Presentation only: warnings and particles follow the authority's phase and clock.
    public sealed class PartyMechanicVisual : MonoBehaviour
    {
        static Material warningMaterial;
        GameObject model;LineRenderer ring,rope;GameObject[] arrows;TextMesh label;ParticleSystem burst;string phase="";
        public static GameObject Create(EffectState e,Transform parent)
        {
            var root=new GameObject("Party warning "+e.mechanicId);root.transform.SetParent(parent,false);var v=root.AddComponent<PartyMechanicVisual>();
            v.model=ToyFactory.Item(e.visual,e.color,root.transform,"");
            var circle=new GameObject("Visible warning circle");circle.transform.SetParent(root.transform,false);v.ring=circle.AddComponent<LineRenderer>();
            v.ring.useWorldSpace=false;v.ring.loop=true;v.ring.positionCount=64;v.ring.startWidth=v.ring.endWidth=.10f;
            if(!warningMaterial)warningMaterial=new Material(Resources.Load<Shader>("PartyWarning"));v.ring.sharedMaterial=warningMaterial;
            var text=new GameObject("Public countdown");text.transform.SetParent(root.transform,false);v.label=text.AddComponent<TextMesh>();v.label.font=Resources.Load<Font>("AboutMeFont");v.label.fontSize=42;v.label.characterSize=.045f;v.label.anchor=TextAnchor.MiddleCenter;v.label.color=new Color(.18f,.12f,.13f);
            if(v.label.font)v.label.GetComponent<MeshRenderer>().sharedMaterial=v.label.font.material;
            return root;
        }
        void PresentMovement(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            float left=Mathf.Max(0,e.deadline-now);bool ended=e.stage=="ended"||e.stage=="burst";
            var color=d.id=="paired-teleport"?(e.stage=="portal_out"?new Color(.67f,.34f,1):new Color(.15f,.7f,1)):new Color(.1f,.72f,.63f);
            color.a=ended?Mathf.Clamp01(left*3):.95f;ring.startColor=ring.endColor=color;
            float y=CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y;
            ring.positionCount=64;
            for(int i=0;i<64;i++)
            {
                float angle=i*Mathf.PI*2/64;var point=new Vector3(Mathf.Cos(angle)*e.radius,y,Mathf.Sin(angle)*e.radius);
                if(d.id=="belt-floor")
                {
                    float t=i/16f;int side=i/16;float f=t-side;
                    point=side==0?new Vector3(Mathf.Lerp(-.9f,.9f,f),y,-2):side==1?new Vector3(.9f,y,Mathf.Lerp(-2,2,f)):side==2?new Vector3(Mathf.Lerp(.9f,-.9f,f),y,2):new Vector3(-.9f,y,Mathf.Lerp(2,-2,f));
                    point=Quaternion.LookRotation(e.velocity)*point;
                }
                ring.SetPosition(i,point);
            }
            ring.gameObject.SetActive(d.id!="grapple-anchor"&&d.id!="glide-open"&&d.id!="uppercut-piston");
            model.SetActive(!ended);model.transform.localPosition=Vector3.zero;model.transform.localScale=Vector3.one;
            if(d.id=="glide-open"){model.transform.localPosition=Vector3.up*1.2f;model.transform.localScale=Vector3.one*2;}
            if(d.id=="inflate-shove")model.transform.localScale=Vector3.one*Mathf.Lerp(1,3,Mathf.Clamp01((now-e.createdAt)/d.lifetime));
            if(d.id=="spring-pad"){model.transform.localRotation=Quaternion.Euler(90,0,0);model.transform.localScale=new Vector3(2,2,1);model.transform.localPosition=Vector3.up*(.06f+Mathf.Sin(now*8)*.035f);}
            if(d.id=="belt-floor")
            {
                model.SetActive(false);
                if(arrows==null){arrows=new GameObject[3];for(int i=0;i<3;i++)arrows[i]=ToyFactory.Shape("Conveyor direction",PrimitiveType.Cube,transform,Vector3.zero,new Vector3(.25f,.04f,.5f),"#39BCAA");}
                for(int i=0;i<3;i++){arrows[i].transform.localPosition=e.velocity*(-1.7f+Mathf.Repeat(now*2.8f+i*1.2f,3.4f))+Vector3.up*y;arrows[i].transform.rotation=Quaternion.LookRotation(e.velocity);}
            }
            if(d.id=="grapple-anchor")
            {
                if(!rope){var obj=new GameObject("Visible grapple rope");obj.transform.SetParent(transform,false);rope=obj.AddComponent<LineRenderer>();rope.sharedMaterial=warningMaterial;rope.startWidth=rope.endWidth=.07f;rope.positionCount=2;rope.useWorldSpace=true;rope.startColor=rope.endColor=color;}
                var anchor=System.Array.Find(state.props,x=>x.id==e.targetId);rope.gameObject.SetActive(anchor!=null&&!ended);if(anchor!=null){rope.SetPosition(0,e.position+Vector3.up*.4f);rope.SetPosition(1,anchor.position+Vector3.up*.3f);}
            }
            label.gameObject.SetActive(!ended);label.transform.localPosition=Vector3.up*(d.id=="glide-open"?2:1.25f);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="spring-pad"?"走上去弹跳 · 可拆":d.id=="uppercut-piston"?"向上挑起":d.id=="pogo-hop"?"连续弹跳 · Q 停止":d.id=="inflate-shove"?"膨胀推开 · Q 停止":d.id=="stomp-pulse"?"落地震荡 · 跳起躲避":d.id=="short-dash"?"滑冲 · 碰撞停止":d.id=="grapple-anchor"?"锚点牵引 · Q 松开":d.id=="glide-open"?"滑翔 · Q 收伞":d.id=="belt-floor"?"传送带 · 跳起 / 拆除":e.stage=="portal_wait"?"入口 · 等待放出口":e.stage=="portal_out"?"出口 · 紫色": "入口 · 走入停留 0.6s";
            label.text=help+"\n"+left.ToString("0.0")+"s";phase=e.stage;
        }
        public void Present(EffectState e,float now,Camera camera,Snapshot state)
        {
            var d=PartyMechanicRegistry.Find(e.mechanicId);if(d==null)return;
            transform.position=e.position;
            if(d.family=="movement"){PresentMovement(e,d,now,camera,state);return;}
            bool exploding=e.stage=="burst",isRing=e.stage=="ring";
            model.SetActive(!exploding&&!isRing&&!e.targetId.StartsWith("device-"));
            if(e.stage=="flying")model.transform.Rotate(Vector3.up,280*Time.deltaTime);
            float radius=isRing?Mathf.Max(.05f,e.radius):d.radius;
            float y=CampusGeometry.Floor(e.position,.1f)+.05f-e.position.y;
            var color=e.stage=="sensing"||e.stage=="attached"?new Color(1,.18f,.16f):exploding?new Color(1,.65f,.12f):new Color(1,.42f,.24f);
            color.a=exploding?Mathf.Clamp01(e.remaining*2):.9f;ring.startColor=ring.endColor=color;
            for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius));}
            ring.gameObject.SetActive(e.stage!="flying");
            label.gameObject.SetActive(!exploding&&!isRing);label.transform.localPosition=new Vector3(0,.85f,0);
            if(camera)label.transform.rotation=camera.transform.rotation;
            float remaining=e.stage=="sensing"?Mathf.Max(0,e.triggerAt-now):Mathf.Max(0,e.deadline-now);
            label.text=e.stage=="watching"?"感应圈 · 跳过 / 绕开":e.mechanicId=="remote-burst"?"遥控装置 · 可搬走 / 拆除":e.stage=="attached"?"按住 Q 撕掉 · "+remaining.ToString("0.0")+"s":e.stage=="pulling"?"吸入中 · 快离开":(e.mechanicId=="relay-burst"?e.chainIndex+" 号 · ":"")+remaining.ToString("0.0")+"s";
            if(exploding&&phase!="burst")
            {
                var particles=new GameObject("Bounded confetti burst");particles.transform.SetParent(transform,false);burst=particles.AddComponent<ParticleSystem>();burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=burst.main;main.duration=.6f;main.loop=false;main.playOnAwake=false;main.maxParticles=48;main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.6f);main.startSpeed=new ParticleSystem.MinMaxCurve(2,6);main.startSize=new ParticleSystem.MinMaxCurve(.08f,.2f);main.startColor=color;main.gravityModifier=.2f;
                var emission=burst.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,36)});
                var shape=burst.shape;shape.shapeType=e.mechanicId=="cone-burst"?ParticleSystemShapeType.Cone:ParticleSystemShapeType.Sphere;shape.radius=.15f;shape.angle=30;
                if(e.mechanicId=="cone-burst")particles.transform.rotation=Quaternion.LookRotation(e.velocity.sqrMagnitude>.01f?e.velocity:Vector3.forward);
                var fade=burst.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(new Color(1,.94f,.58f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
                burst.GetComponent<ParticleSystemRenderer>().sharedMaterial=TraitToyEffects.SpriteMaterial(false);burst.Play();
            }
            phase=e.stage;
        }
    }
}
