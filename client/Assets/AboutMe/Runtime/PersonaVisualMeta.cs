using UnityEngine;

namespace AboutMe
{
    // Stored on every generated prefab so inspection tools and future animation code have a
    // stable place to read anchors and collision sizing without changing the network model.
    public sealed class PersonaVisualMeta : MonoBehaviour
    {
        public string visualId, templateId, source, license;
        public float colliderRadius, swallowScale;
        public Transform Grip => transform.Find("Grip");
    }
}
