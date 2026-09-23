using System;
using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    /// <summary>
    /// Applies the game's presentation poses through Unity Humanoid muscle space.
    /// Gameplay movement, contacts and damage remain external.
    /// </summary>
    public sealed class DragonGirlMotionAdapter : MonoBehaviour
    {
        static readonly string[] requiredBones={"Hips","Spine","Chest","UpperChest","Neck","Neck_Twist_A","Head",
            "Left_Shoulder","Left_UpperArm","Left_LowerArm","Left_Hand","Right_Shoulder","Right_UpperArm","Right_LowerArm","Right_Hand",
            "Left_UpperLeg","Left_LowerLeg","Left_Foot","Left_Toes","Right_UpperLeg","Right_LowerLeg","Right_Foot","Right_Toes"};
        readonly DollCombatMotion sampler=new DollCombatMotion();
        readonly Dictionary<string,int> muscles=new Dictionary<string,int>();
        Animator animator;HumanPoseHandler handler;HumanPose restPose,currentPose;
        float[] restMuscles,displayMuscles;
        Transform presentationRoot;
        Vector3 presentationPosition;
        Quaternion presentationRotation;
        bool initialized;
        public string LastMotionClip {get;private set;}="procedural";
        public int DrivenBoneCount {get{if(!initialized)Initialize();return requiredBones.Length;}}

        public void Initialize()
        {
            if(initialized)return;
            var transforms=GetComponentsInChildren<Transform>(true);var names=new HashSet<string>(Array.ConvertAll(transforms,item=>item.name));
            foreach(string required in requiredBones)if(!names.Contains(required))throw new InvalidOperationException("Dragon girl is missing bone "+required);
            animator=GetComponentInChildren<Animator>(true);
            if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new InvalidOperationException("Dragon girl requires a valid Humanoid avatar");
            presentationRoot=animator.transform.parent&&animator.transform.parent.name=="MotionPivot"?animator.transform.parent:animator.transform;
            presentationPosition=presentationRoot.localPosition;presentationRotation=presentationRoot.localRotation;
            handler=new HumanPoseHandler(animator.avatar,animator.transform);
            restPose=new HumanPose();handler.GetHumanPose(ref restPose);
            restMuscles=(float[])restPose.muscles.Clone();displayMuscles=(float[])restMuscles.Clone();
            for(int i=0;i<HumanTrait.MuscleName.Length;i++)muscles[HumanTrait.MuscleName[i]]=i;
            Require("Spine Front-Back","Spine Left-Right","Spine Twist Left-Right","Chest Front-Back","Chest Left-Right","Chest Twist Left-Right",
                "Left Arm Front-Back","Left Arm Down-Up","Left Arm Twist In-Out","Left Forearm Stretch",
                "Right Arm Front-Back","Right Arm Down-Up","Right Arm Twist In-Out","Right Forearm Stretch",
                "Left Upper Leg Front-Back","Left Upper Leg In-Out","Left Lower Leg Stretch",
                "Right Upper Leg Front-Back","Right Upper Leg In-Out","Right Lower Leg Stretch");
            initialized=true;ResetPose();
        }

        public void ResetPose()
        {
            if(!initialized){Initialize();return;}
            sampler.Reset();Array.Copy(restMuscles,displayMuscles,restMuscles.Length);
            currentPose=new HumanPose{bodyPosition=restPose.bodyPosition,bodyRotation=restPose.bodyRotation,muscles=(float[])restMuscles.Clone()};
            handler.SetHumanPose(ref currentPose);presentationRoot.localPosition=presentationPosition;presentationRoot.localRotation=presentationRotation;
            LastMotionClip="procedural";
        }

        public void Apply(DollCombatMotion.Input input)
        {
            if(!initialized)Initialize();
            float dt=Mathf.Clamp(input.dt,0,.1f);var pose=sampler.Sample(input);var target=(float[])restMuscles.Clone();
            Add(target,"Spine Front-Back",pose.bodyEuler.x/50f);Add(target,"Spine Twist Left-Right",pose.bodyEuler.y/55f);Add(target,"Spine Left-Right",-pose.bodyEuler.z/45f);
            Add(target,"Chest Front-Back",pose.bodyEuler.x/65f);Add(target,"Chest Twist Left-Right",pose.bodyEuler.y/70f);Add(target,"Chest Left-Right",-pose.bodyEuler.z/60f);
            Head(target,pose.headEuler);
            Arm(target,"Left",pose.leftUpperArmEuler,pose.leftForearmEuler);Arm(target,"Right",pose.rightUpperArmEuler,pose.rightForearmEuler);
            Leg(target,"Left",pose.leftThighEuler,pose.leftShinEuler);Leg(target,"Right",pose.rightThighEuler,pose.rightShinEuler);
            float blend=dt<=0?1:1-Mathf.Exp(-dt*22);for(int i=0;i<displayMuscles.Length;i++)displayMuscles[i]=Mathf.Lerp(displayMuscles[i],Mathf.Clamp(target[i],-1,1),blend);
            currentPose.bodyPosition=restPose.bodyPosition;currentPose.bodyRotation=restPose.bodyRotation;currentPose.muscles=displayMuscles;handler.SetHumanPose(ref currentPose);
            Vector3 offset=pose.bodyOffset+pose.fallOffset;presentationRoot.localPosition=Vector3.Lerp(presentationRoot.localPosition,presentationPosition+transform.InverseTransformVector(transform.TransformVector(offset)),blend);
            presentationRoot.localRotation=Quaternion.Slerp(presentationRoot.localRotation,presentationRotation*Quaternion.Euler(pose.fallEuler),blend);
            LastMotionClip=string.IsNullOrEmpty(input.actionKind)?(input.runBlend>.5f?"run":"locomotion"):input.actionKind;
        }

        void Head(float[] values,Vector3 euler)
        {
            AddIf(values,"Neck Nod Down-Up",-euler.x/70f,.35f);AddIf(values,"Neck Turn Left-Right",euler.y/70f,.35f);AddIf(values,"Neck Tilt Left-Right",-euler.z/55f,.35f);
            AddIf(values,"Head Nod Down-Up",-euler.x/70f,.65f);AddIf(values,"Head Turn Left-Right",euler.y/70f,.65f);AddIf(values,"Head Tilt Left-Right",-euler.z/55f,.65f);
        }
        void Arm(float[] values,string side,Vector3 upper,Vector3 lower)
        {
            float mirror=side=="Left"?1:-1;
            Add(values,side+" Arm Front-Back",-upper.x/105f);Add(values,side+" Arm Down-Up",-upper.z*mirror/95f);
            Add(values,side+" Arm Twist In-Out",upper.y*mirror/85f);Add(values,side+" Forearm Stretch",-lower.x/120f);
        }
        void Leg(float[] values,string side,Vector3 upper,Vector3 lower)
        {
            float mirror=side=="Left"?1:-1;
            Add(values,side+" Upper Leg Front-Back",-upper.x/100f);Add(values,side+" Upper Leg In-Out",upper.z*mirror/65f);
            AddIf(values,side+" Upper Leg Twist In-Out",upper.y*mirror/80f,1);Add(values,side+" Lower Leg Stretch",-lower.x/125f);
            AddIf(values,side+" Foot Up-Down",lower.x/180f,1);
        }
        void Add(float[] values,string name,float value){values[muscles[name]]=Mathf.Clamp(values[muscles[name]]+value,-1,1);}
        void AddIf(float[] values,string name,float value,float scale){if(muscles.TryGetValue(name,out int index))values[index]=Mathf.Clamp(values[index]+value*scale,-1,1);}
        void Require(params string[] names){foreach(string name in names)if(!muscles.ContainsKey(name))throw new InvalidOperationException("Humanoid muscle is missing: "+name);}
        void OnDestroy(){if(handler!=null)handler.Dispose();handler=null;}
    }
}
