using System;
using System.Linq;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class UniversityCrestTests
    {
        static void Check(bool ok,string error){if(!ok)throw new Exception(error);}
        public static void Run()
        {
            string path="/assets/university-crests/"+new string('a',64)+".png";
            Check(UniversityCrestTexture.ValidPath(path),"valid path rejected");
            Check(!UniversityCrestTexture.ValidPath("https://evil.com/x.png"),"remote image accepted");
            var a=new PersonaAsset{assetId="sysu",name="中山大学校徽圆盘",templateId="Disc",visualId="school-disc",color="#77C8BD",emblemPath=path,emblemCrop="0,0,1,1"};
            Check(JsonUtility.FromJson<PublicPersonaAsset>(JsonUtility.ToJson(a.PublicCopy())).emblemPath==path,"public projection lost crest");
            var disc=ToyFactory.Item(a.visualId,a.color,null,a.name,a.emblemPath,a.emblemCrop);
            var decal=disc.GetComponent<UniversityCrestTexture>();Check(decal,"missing decal");
            var mesh=UniversityCrestTexture.FaceMesh;
            Check(disc.transform.Find("Official university crest · top").TransformDirection(mesh.normals[0]).y>.9f,"top faces inward");
            Check(disc.transform.Find("Official university crest · bottom").TransformDirection(mesh.normals[0]).y<-.9f,"bottom faces inward");
            Check(Math.Abs(mesh.vertices[1].magnitude-.4f)<.001f,"emblem does not span the whole disc");
            var texture=new Texture2D(64,64);var pixels=Enumerable.Repeat(Color.white,4096).ToArray();
            for(int y=8;y<56;y++)for(int x=8;x<56;x++)pixels[y*64+x]=new Color32(25,145,55,255);
            texture.SetPixels(pixels);texture.Apply();decal.Apply(texture);
            var body=disc.transform.Find("disc").GetComponent<Renderer>();
            Check(body.sharedMaterial.color.g>.5f&&body.sharedMaterial.color.r<.2f,"disc did not adopt emblem green");
            var bounds=UniversityCrestTexture.ArtworkBounds(texture);
            Check(bounds.width<.8f&&bounds.width>.74f,"white padding was not removed");
            Check(!disc.transform.Find("crest").gameObject.activeSelf,"raised inset badge remains");
            Check(!disc.transform.Find("school name").gameObject.activeSelf,"name overlays emblem");
            Check(!disc.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text=="DeepSeek 生成"),"extra logo label present");
            UnityEngine.Object.DestroyImmediate(disc);UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("ABOUTME_UNIVERSITY_CREST_TESTS_PASSED latest projection / two faces / original emblem only");
        }
        public static void BuildDeliverables(){Run();ProjectTools.BuildMac();ProjectTools.BuildWeb();}
    }
}
