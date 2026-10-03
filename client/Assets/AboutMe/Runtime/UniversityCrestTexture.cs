using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace AboutMe
{
    // Only content-addressed images served by our API are accepted, never AI/user URLs.
    public sealed class UniversityCrestTexture : MonoBehaviour
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> retryAt = new Dictionary<string, float>();
        static readonly HashSet<string> loading = new HashSet<string>();
        static UniversityCrestTexture runner;
        static Mesh face;
        Renderer[] faces;
        GameObject fallback;
        string path;
        Material material;
        public static bool ValidPath(string value) => Regex.IsMatch(value ?? "", @"^/assets/university-crests/[0-9a-f]{64}\.png$");
        public static Mesh FaceMesh
        {
            get
            {
                if(face)return face;
                const int segments=64;
                var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];
                uv[0]=new Vector2(.5f,.5f);
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments;float x=Mathf.Cos(a),y=Mathf.Sin(a);
                    vertices[i+1]=new Vector3(x*.29f,y*.29f,0);uv[i+1]=new Vector2(.5f+x*.5f,.5f+y*.5f);
                    triangles[i*3]=0;triangles[i*3+1]=(i+1)%segments+1;triangles[i*3+2]=i+1;
                }
                face=new Mesh{name="University crest face"};face.vertices=vertices;face.uv=uv;face.triangles=triangles;face.RecalculateNormals();face.RecalculateBounds();return face;
            }
        }
        public static void Attach(Transform disc,string crestPath,GameObject nameFallback)
        {
            if(!ValidPath(crestPath))return;
            var component=disc.gameObject.AddComponent<UniversityCrestTexture>();component.path=crestPath;component.fallback=nameFallback;
            component.faces=new Renderer[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Official university crest · top":"Official university crest · bottom");go.transform.SetParent(disc,false);
                go.transform.localPosition=new Vector3(0,i==0?.116f:-.082f,0);go.transform.localRotation=Quaternion.Euler(i==0?90:-90,0,0);
                go.AddComponent<MeshFilter>().sharedMesh=FaceMesh;component.faces[i]=go.AddComponent<MeshRenderer>();go.SetActive(false);
            }
            if(Application.isPlaying)component.StartCoroutine(component.ApplyWhenReady());
        }
        void OnEnable()
        {
            if(Application.isPlaying&&faces!=null&&ValidPath(path))StartCoroutine(ApplyWhenReady());
        }
        static string CacheFile(string key) => Path.Combine(Application.persistentDataPath,"university-crests",key+".png");
        static string Key(string url) => Hash128.Compute(url).ToString();
        static void EnsureRunner()
        {
            if(runner)return;
            var go=new GameObject("University crest downloads");UnityEngine.Object.DontDestroyOnLoad(go);runner=go.AddComponent<UniversityCrestTexture>();
        }
        IEnumerator ApplyWhenReady()
        {
            string api=GameNetwork.Game?GameNetwork.Game.ApiUrl:"";
            if(string.IsNullOrWhiteSpace(api))yield break;
            string url=api.TrimEnd('/')+path,key=Key(url);
            EnsureRunner();
            if(!cache.ContainsKey(key)&&!loading.Contains(key)&&(!retryAt.TryGetValue(key,out var next)||Time.realtimeSinceStartup>=next))
            {loading.Add(key);runner.StartCoroutine(Download(url,key));}
            while(loading.Contains(key))yield return null;
            if(cache.TryGetValue(key,out var texture)&&texture)Apply(texture);
        }
        static IEnumerator Download(string url,string key)
        {
            Texture2D texture=null;
            try
            {
                var file=CacheFile(key);
                if(File.Exists(file))
                {
                    var data=File.ReadAllBytes(file);texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(data.Length>5*1024*1024||!texture.LoadImage(data,false)){UnityEngine.Object.Destroy(texture);texture=null;}
                }
            }
            catch(IOException) { texture=null; }
            if(!texture)
            {
                using(var req=UnityWebRequestTexture.GetTexture(url))
                {
                    req.timeout=20;yield return req.SendWebRequest();
                    if(req.result==UnityWebRequest.Result.Success&&req.downloadedBytes<=5*1024*1024)
                    {
                        texture=DownloadHandlerTexture.GetContent(req);
                        if(texture&&texture.width<=1024&&texture.height<=1024)
                        {
                            try {var file=CacheFile(key);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,req.downloadHandler.data);}catch(IOException){}
                        }
                        else {if(texture)UnityEngine.Object.Destroy(texture);texture=null;}
                    }
                }
            }
            if(texture){texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;cache[key]=texture;}
            else retryAt[key]=Time.realtimeSinceStartup+15;
            loading.Remove(key);
        }
        public void Apply(Texture2D texture)
        {
            if(material){if(Application.isPlaying)UnityEngine.Object.Destroy(material);else UnityEngine.Object.DestroyImmediate(material);}
            var basis=Resources.Load<Material>("ToyBaseMaterial");material=basis?new Material(basis):new Material(Shader.Find("Standard"));
            material.name="Official university emblem";material.color=Color.white;material.mainTexture=texture;
            // Opaque white backing keeps transparent source logos legible on either face.
            material.SetFloat("_Glossiness",.16f);
            foreach(var renderer in faces){renderer.sharedMaterial=material;renderer.gameObject.SetActive(true);}
            if(fallback)fallback.SetActive(false);
        }
        void OnDestroy(){if(material){if(Application.isPlaying)UnityEngine.Object.Destroy(material);else UnityEngine.Object.DestroyImmediate(material);}}
    }
}
