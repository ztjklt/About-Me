using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class MachineryDeliveryReview
    {
        public static void Render()
        {
            var group=new GameObject("Forty new original props");group.transform.position=new Vector3(1000,1000,1000);
            var camera=new GameObject("Recent props review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.4f;camera.backgroundColor=new Color(.94f,.92f,.86f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Recent props review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(30,-20,0);
            int i=0;foreach(var r in OriginalPersonaModels.All.Skip(OriginalPersonaModels.All.Length-40))
            {
                var model=Object.Instantiate(Resources.Load<GameObject>("PersonaProps/"+r.id),group.transform,false);model.transform.localPosition=new Vector3((i%8-3.5f)*1.2f,(2-i/8)*1.15f,0);model.transform.localRotation=Quaternion.Euler(-10,-18,0);
                var text=new GameObject("Model label").AddComponent<TextMesh>();text.transform.SetParent(group.transform,false);text.transform.localPosition=model.transform.localPosition+Vector3.down*.47f;text.transform.localRotation=Quaternion.Euler(0,180,0);text.text=r.label;text.font=Resources.Load<Font>("AboutMeFont");text.fontSize=45;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.13f,.17f,.24f);if(text.font)text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;i++;
            }
            camera.transform.position=group.transform.position+Vector3.forward*10;camera.transform.LookAt(group.transform.position);var rt=new RenderTexture(1800,1250,24);var old=RenderTexture.active;camera.targetTexture=rt;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(1800,1250,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,1250),0,0);image.Apply();string path=Path.GetFullPath("../artifacts/machinery-mechanics-live");Directory.CreateDirectory(path);File.WriteAllBytes(path+"/forty-new-props.png",image.EncodeToPNG());Object.DestroyImmediate(image);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(group);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);}
        }
    }
}
