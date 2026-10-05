using UnityEngine;
namespace AboutMe
{
    // Sample the imported skin/bone clip; authority movement and hits remain separate.
    public sealed class AnimalPersonaMotion : MonoBehaviour
    {
        public Transform model;
        public Vector3 restPosition;
        public AnimationClip walk;
        public float walkSpeed=1.5f;
        Transform[] bones;
        Vector3[] positions,scales;
        Quaternion[] rotations;
        bool wasRunning;
        void Initialize()
        {
            if(bones!=null||!model)return;
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            bones=model.GetComponentsInChildren<Transform>(true);
            positions=new Vector3[bones.Length];scales=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
        }
        public void Present(bool running,float time)
        {
            if(!model)return;
            Initialize();
            if(running&&walk&&walk.length>0)
            {
                walk.SampleAnimation(model.gameObject,Mathf.Repeat(time*walkSpeed,walk.length));
                // Import axis correction and normalization belong to the wrapper, not root motion.
                model.localPosition=positions[0];model.localRotation=rotations[0];model.localScale=scales[0];
            }
            else if(wasRunning)
                for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
            wasRunning=running;
        }
    }
}
