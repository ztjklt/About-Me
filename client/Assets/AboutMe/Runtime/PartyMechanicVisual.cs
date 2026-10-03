using UnityEngine;

namespace AboutMe
{
    // Presentation only: warnings and particles follow the authority's phase and clock.
    public sealed class PartyMechanicVisual : MonoBehaviour
    {
        static Material warningMaterial;
        GameObject model;LineRenderer ring;TextMesh label;ParticleSystem burst;string phase="";
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
        public void Present(EffectState e,float now,Camera camera)
        {
            var d=PartyMechanicRegistry.Find(e.mechanicId);if(d==null)return;
            transform.position=e.position;
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
