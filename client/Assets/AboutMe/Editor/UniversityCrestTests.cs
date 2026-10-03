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
            var texture=new Texture2D(2,2);decal.Apply(texture);
            Check(!disc.transform.Find("school name").gameObject.activeSelf,"name overlays emblem");
            Check(!disc.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text=="DeepSeek 生成"),"extra logo label present");
            UnityEngine.Object.DestroyImmediate(disc);UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("ABOUTME_UNIVERSITY_CREST_TESTS_PASSED latest projection / two faces / original emblem only");
        }
        public static void BuildDeliverables(){Run();ProjectTools.BuildMac();ProjectTools.BuildWeb();}
    }
}
