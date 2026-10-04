using System;
using System.Collections.Generic;
using UnityEngine;
namespace AboutMe
{
    /// <summary>Cosmetic meshes on the original ToyActor. Never drives a body bone or collider.</summary>
    public sealed class KimiGirlOutfit : MonoBehaviour
    {
        [Serializable] public class Helper {public string name,parent;public Vector3 position;}
        public string[] boneNames;
        public Vector3[] bonePositions;
        public Helper[] helpers;
        public SkinnedMeshRenderer skin;
        readonly List<Transform> auxiliary=new List<Transform>();
        readonly Dictionary<Transform,Vector3> restAnchors=new Dictionary<Transform,Vector3>();
        ToyActor actor;Vector3 lastPosition;bool hasPosition;float sway;
        public int AuxiliaryBoneCount=>auxiliary.Count;
        public static KimiGirlOutfit Attach(ToyActor actor)
        {
            var prefab=Resources.Load<GameObject>("CampusDoll/KimiGirlModular");
            if(!prefab)throw new InvalidOperationException("Kimi modular outfit prefab is missing. Run KimiGirlModularTools.Import.");
            var instance=Instantiate(prefab,actor.root.transform,false);var outfit=instance.GetComponent<KimiGirlOutfit>();
            outfit.Bind(actor);return outfit;
        }
        void Bind(ToyActor owner)
        {
            actor=owner;var actual=new Dictionary<string,Transform>();
            foreach(var b in owner.Skin.bones)actual.Add(b.name,b);
            var neutral=new Dictionary<string,Vector3>();for(int i=0;i<boneNames.Length;i++)neutral.Add(boneNames[i],bonePositions[i]);
            foreach(var h in helpers)
            {
                if(!actual.TryGetValue(h.parent,out var parent))throw new InvalidOperationException("Missing outfit anchor "+h.parent);
                var t=new GameObject(h.name).transform;t.SetParent(parent,false);t.localPosition=h.position-neutral[h.parent];
                auxiliary.Add(t);restAnchors.Add(t,t.localPosition);actual.Add(h.name,t);
            }
            var mapped=new Transform[boneNames.Length];for(int i=0;i<mapped.Length;i++)mapped[i]=actual[boneNames[i]];
            skin.bones=mapped;skin.rootBone=owner.pelvis;skin.updateWhenOffscreen=false;
            skin.localBounds=new Bounds(new Vector3(0,.3f,0),new Vector3(4,4.6f,4));
        }
        public void Pose(float dt,float opening,float belly)
        {
            if(actor==null)return;
            var root=actor.root.transform;var movement=hasPosition&&dt>.0001f?root.InverseTransformDirection(root.position-lastPosition)/dt:Vector3.zero;
            bool reset=!hasPosition||Vector3.Distance(root.position,lastPosition)>3;
            lastPosition=root.position;hasPosition=true;
            float target=Mathf.Clamp(movement.x,-5,5)*.7f;sway=reset?0:Mathf.Lerp(sway,target,1-Mathf.Exp(-Mathf.Min(dt,.1f)*12));
            foreach(var b in auxiliary)
            {
                Vector3 angles=Vector3.zero;
                if(b.name.StartsWith("Kimi_Hair"))
                {
                    // Only the hair counters the exaggerated feeding head tilt; the body is untouched.
                    float lean=Mathf.Clamp(-movement.z*.4f,-3,4);
                    float feedingCounter=Mathf.Clamp01(opening)*52;
                    angles=new Vector3(lean+feedingCounter,0,-sway*.55f);
                    b.localPosition=Quaternion.Euler(feedingCounter,0,0)*restAnchors[b];
                }
                else if(b.name.StartsWith("Kimi_Skirt"))
                {
                    // Hinge each panel at its waist edge so lifting the hem clears the knee without folding through the belly.
                    Vector3 left=actor.pelvis.InverseTransformDirection(actor.leftShin.position-actor.leftLeg.position).normalized;
                    Vector3 right=actor.pelvis.InverseTransformDirection(actor.rightShin.position-actor.rightLeg.position).normalized;
                    if(b.name=="Kimi_SkirtFront")angles.x=-Mathf.Clamp(Mathf.Max(left.z,right.z)*68,0,58);
                    if(b.name=="Kimi_SkirtBack")angles.x=Mathf.Clamp(-Mathf.Min(left.z,right.z)*68,0,58);
                    if(b.name=="Kimi_SkirtLeft")angles.z=-Mathf.Clamp(-left.x*40,0,36);
                    if(b.name=="Kimi_SkirtRight")angles.z=Mathf.Clamp(right.x*40,0,36);
                }
                

                b.localRotation=Quaternion.Euler(angles);
            }
            skin.SetBlendShapeWeight(0,Mathf.Clamp01(opening)*100);
            skin.SetBlendShapeWeight(1,Mathf.Clamp01(belly)*100);
        }
        public void Release()=>ClearHelpers(true);
        void ClearHelpers(bool detach)
        {
            foreach(var t in auxiliary)if(t)
            {
                t.gameObject.SetActive(false);
                if(detach)t.SetParent(null,false);
                if(Application.isPlaying)Destroy(t.gameObject);else DestroyImmediate(t.gameObject);
            }
            auxiliary.Clear();restAnchors.Clear();
        }
        // During owner teardown the helpers may already be inside Unity's destruction pass.
        // Explicit outfit switches detach first; whole-owner teardown must not reparent them.
        void OnDestroy()=>ClearHelpers(false);
    }
}
