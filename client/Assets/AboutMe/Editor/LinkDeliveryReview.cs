using System.IO;
using System.Linq;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class LinkDeliveryReview
    {
        public static void Render()
        {
            var root=new GameObject("Ten distinct link construction props");root.transform.position=new Vector3(1000,1000,1000);
            var camera=new GameObject("Link props camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.85f;camera.backgroundColor=new Color(.94f,.92f,.86f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Link props light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(25,-20,0);int i=0;
            foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="link"))
            {var model=Object.Instantiate(Resources.Load<GameObject>("PersonaProps/"+d.visualId),root.transform,false);model.transform.localPosition=new Vector3((i%5-2)*1.4f,(i/5==0?.65f:-.8f),0);model.transform.localRotation=Quaternion.Euler(-12,-20,0);var t=new GameObject("Mechanic name").AddComponent<TextMesh>();t.transform.SetParent(root.transform,false);t.transform.localPosition=model.transform.localPosition+Vector3.down*.5f;t.transform.localRotation=Quaternion.Euler(0,180,0);t.text=d.label;t.font=Resources.Load<Font>("AboutMeFont");t.fontSize=42;t.characterSize=.04f;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.13f,.17f,.24f);if(t.font)t.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;i++;}
            camera.transform.position=root.transform.position+Vector3.forward*10;camera.transform.LookAt(root.transform.position);var rt=new RenderTexture(1800,850,24);var previous=RenderTexture.active;camera.targetTexture=rt;
            try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(1800,850,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,850),0,0);image.Apply();var folder=Path.GetFullPath("../artifacts/link-mechanics-live");Directory.CreateDirectory(folder);File.WriteAllBytes(folder+"/ten-link-props.png",image.EncodeToPNG());Object.DestroyImmediate(image);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);}
        }
        public static void BuildDeliverables(){OriginalPersonaModelTools.Build();Render();PartyLinkTests.Run();PartyVirtualTests.Run();PartyControlTests.Run();PartyMechanicTests.Run();PartyMovementTests.Run();PartyFieldTests.Run();PartyGuardTests.Run();PartyLootTests.Run();PartyMachineryTests.Run();NpcPracticeTests.Run();PersonalitySkinTests.BuildDeliverables();}
    }
}
