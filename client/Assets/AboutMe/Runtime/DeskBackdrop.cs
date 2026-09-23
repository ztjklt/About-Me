using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace AboutMe
{
    // Visual-only surroundings. Never use this geometry for gameplay or network state.
    public sealed class DeskBackdrop : MonoBehaviour
    {
        public const float DeskTop = -14f;
        public const string ResourcePath = "DeskCampus/Backdrop";
        readonly List<Mesh> runtimeMeshes = new List<Mesh>();

        public static DeskBackdrop Create()
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (!prefab) throw new System.InvalidOperationException("Desk campus backdrop missing. Run About Me/Build memory desk background.");
            var instance = Instantiate(prefab);
            var backdrop = instance.GetComponent<DeskBackdrop>();
            if (Application.isPlaying) backdrop.CombineByMaterial();
            return backdrop;
        }

        // Large, low-detail surroundings share eight materials. Combining explicitly avoids
        // front-to-back sorting splitting Unity's automatic static batches into many draws.
        // Only runtime instances change; every source prefab object stays editable.
        void CombineByMaterial()
        {
            var source = GetComponentsInChildren<MeshRenderer>();
            foreach (var group in source.GroupBy(renderer => renderer.sharedMaterial))
            {
                var pieces = group.Select(renderer => new CombineInstance {
                    mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                    transform = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                }).ToArray();
                var mesh = new Mesh { name = "Desk combined · " + group.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pieces, true, true);mesh.RecalculateBounds();runtimeMeshes.Add(mesh);
                var child = new GameObject(mesh.name);child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var combinedRenderer = child.AddComponent<MeshRenderer>();combinedRenderer.sharedMaterial = group.Key;
                combinedRenderer.shadowCastingMode = ShadowCastingMode.Off;combinedRenderer.receiveShadows = false;
                combinedRenderer.lightProbeUsage = LightProbeUsage.Off;combinedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (var renderer in source) renderer.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var mesh in runtimeMeshes) if (mesh) Destroy(mesh);
        }

        public static void ConfigureView(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ToyFactory.ColorOf("#F3E5D1");
            camera.farClipPlane = 420;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 155;
            RenderSettings.fogEndDistance = 410;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ToyFactory.ColorOf("#D2E2EB");
            RenderSettings.ambientEquatorColor = ToyFactory.ColorOf("#D7C7AE");
            RenderSettings.ambientGroundColor = ToyFactory.ColorOf("#A69179");
        }

        public static void ConfigureLight(Light sun)
        {
            sun.transform.rotation=Quaternion.Euler(50,-137,0);
            sun.color=ToyFactory.ColorOf("#FFF0D9");sun.intensity=1.1f;
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.68f;
            QualitySettings.shadowDistance=120;
        }
    }
}
