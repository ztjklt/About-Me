using UnityEngine;
namespace AboutMe
{
    // Presentation only: the authority determines position and hits. The unrigged mesh
    // bounds stay intact; a small springing hop makes the chase readable without paid rigging.
    public sealed class AnimalPersonaMotion : MonoBehaviour
    {
        public Transform model;
        public Vector3 restPosition;
        public void Present(bool running,float time)
        {
            if(!model)return;
            float hop=running?Mathf.Abs(Mathf.Sin(time*17))*.045f:0;
            model.localPosition=restPosition+Vector3.up*hop;
        }
    }
}
