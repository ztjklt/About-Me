using System;
using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    // Code-only toy VFX. This component presents an effect; authoritative combat stays in Match.
    public sealed class TraitToyEffects : MonoBehaviour
    {
        public static readonly string[] Ids={"fire","frost","shield","curiosity","mischief","gentle"};
        static Material softMaterial,bubbleMaterial;
        readonly List<ParticleSystem> particles=new List<ParticleSystem>();
        float stopAt;int activeStyle;
        public int ParticleCount {get {int count=0;foreach(var p in particles)if(p)count+=p.particleCount;return count;}}
        public const int MaxParticleBudget=240;
        public string ActiveId=>Ids[activeStyle];

        public static Material SpriteMaterial(bool bubble)
        {
            var material=bubble?bubbleMaterial:softMaterial;if(material)return material;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name=bubble?"Code bubble ring":"Code flame lobe",wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float nx=(x+.5f)/32-1,ny=(y+.5f)/32-1,r=Mathf.Sqrt(nx*nx+ny*ny);
                float alpha=bubble?Mathf.Clamp01(1-Mathf.Abs(r-.70f)*10):Mathf.Pow(Mathf.Clamp01(1-r),1.5f);
                if(!bubble)alpha*=.65f+.35f*Mathf.PerlinNoise(x*.11f,y*.11f);
                colors[y*64+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(colors);texture.Apply(false,true);
            var shader=Resources.Load<Shader>("TraitLab/TraitParticles");
            if(!shader)throw new InvalidOperationException("Trait particle shader missing");
            material=new Material(shader){name=bubble?"Toy bubble glow":"Toy flame glow",mainTexture=texture};
            if(bubble)bubbleMaterial=material;else softMaterial=material;return material;
        }
        ParticleSystem Create(string title,Color start,Color end,float speed,float life,float size,int budget,float rate,bool bubble=false)
        {
            var go=new GameObject(title);go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            p.useAutoRandomSeed=false;p.randomSeed=(uint)(33+particles.Count*17);
            var main=p.main;main.duration=1;main.loop=true;main.playOnAwake=false;main.maxParticles=budget;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life*.8f,life);main.startSpeed=speed;
            main.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startColor=start;
            var emission=p.emission;emission.rateOverTime=rate;
            var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=18;shape.radius=.055f;
            var color=p.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(start,0),new GradientColorKey(end,.7f)},
                new[]{new GradientAlphaKey(.2f,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.65f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var scale=p.sizeOverLifetime;scale.enabled=true;
            scale.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.35f,1),new Keyframe(1,.2f)));
            var noise=p.noise;noise.enabled=true;noise.strength=.22f;noise.frequency=.8f;noise.scrollSpeed=.5f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=SpriteMaterial(bubble);
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;particles.Add(p);return p;
        }
        public void Select(int style)
        {
            activeStyle=Mathf.Clamp(style,0,Ids.Length-1);
            foreach(var p in particles)if(p){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(Application.isPlaying)Destroy(p.gameObject);else DestroyImmediate(p.gameObject);}particles.Clear();
            if(activeStyle==0)
            {
                Create("Flame core",new Color(1,.72f,.08f),new Color(1,.17f,.02f),5,.66f,.70f,160,85);
                var embers=Create("Flying embers",new Color(1,.65f,.15f),new Color(1,.25f,.03f),6.2f,.70f,.09f,64,45);
                var shape=embers.shape;shape.angle=24;var main=embers.main;main.gravityModifier=-.15f;
            }
            else if(activeStyle==1)
            {
                var frost=Create("Frost sparkles",new Color(.65f,.95f,1),new Color(.2f,.5f,1),4.8f,.85f,.19f,160,105);
                var shape=frost.shape;shape.angle=24;
            }
            else
            {
                Color[] colors={new Color(1,.8f,.25f),new Color(.4f,1,.72f),new Color(1,.35f,.7f),new Color(1,.65f,.85f)};
                var p=Create(Ids[activeStyle],colors[activeStyle-2],Color.white,.25f,1.3f,activeStyle==5?.35f:.16f,160,75,activeStyle==5||activeStyle==2);
                var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=activeStyle==3?1.1f:.75f;
                p.transform.localPosition=new Vector3(0,-.55f,-.3f);
                if(activeStyle==4){var velocity=p.velocityOverLifetime;velocity.enabled=true;velocity.y=1.1f;velocity.orbitalY=3;}
            }
            Play();
        }
        public void Play()
        {
            stopAt=Time.unscaledTime+1.05f;
            foreach(var p in particles){p.Clear();p.Play();p.Simulate(.45f,false,false,false);p.Play();}
        }
        void Update(){if(Time.unscaledTime<stopAt)return;foreach(var p in particles)if(p.isEmitting)p.Stop(false,ParticleSystemStopBehavior.StopEmitting);}
    }
}
