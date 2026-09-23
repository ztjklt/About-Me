using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AboutMe.Editor
{
    /// <summary>Renders existing Persona prefabs into compact inventory sprites.</summary>
    public static class PersonaThumbnailExport
    {
        const string Folder="Assets/AboutMe/Resources/PersonaThumbs";

        [MenuItem("About Me/Persona visuals/Render inventory thumbnails")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.72f,.76f,.82f);
            var light=new GameObject("Thumbnail softbox").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(35,-35,0);
            var camera=new GameObject("Thumbnail camera").AddComponent<Camera>();
            camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(0,0,0,0);camera.cullingMask=~0;
            camera.nearClipPlane=.01f;camera.farClipPlane=100;
            const int renderSize=384;
            var rt=new RenderTexture(renderSize,renderSize,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            camera.targetTexture=rt;
            int count=0,fallbacks=0;
            foreach(var definition in PersonaVisualCatalog.All)
            {
                if(string.IsNullOrWhiteSpace(definition.resourcePath))continue;
                var prefab=Resources.Load<GameObject>(definition.resourcePath);
                if(!prefab)throw new Exception("PERSONA_THUMB_SOURCE_MISSING "+definition.visualId);
                var objectRoot=UnityEngine.Object.Instantiate(prefab);
                objectRoot.name="Thumbnail "+definition.visualId;
                Bounds bounds=new Bounds(Vector3.zero,Vector3.zero);bool any=false;
                foreach(var renderer in objectRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if(!renderer.enabled)continue;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(!any)throw new Exception("PERSONA_THUMB_EMPTY "+definition.visualId);
                Vector3 direction=new Vector3(1,.72f,1).normalized;
                camera.transform.position=bounds.center+direction*Mathf.Max(3,bounds.size.magnitude*3);
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize=Mathf.Max(.12f,Mathf.Max(bounds.extents.x,bounds.extents.y,bounds.extents.z)*1.65f);
                camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(renderSize,renderSize,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,renderSize,renderSize),0,0);image.Apply();
                var framed=FrameObject(image);
                string path=Folder+"/"+definition.visualId+".png";
                if(!HasReadableFootprint(framed))
                {
                    UnityEngine.Object.DestroyImmediate(framed);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(objectRoot);
                    AssetDatabase.DeleteAsset(path);fallbacks++;continue;
                }
                File.WriteAllBytes(path,framed.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(framed);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(objectRoot);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.maxTextureSize=256;
                importer.textureCompression=TextureImporterCompression.Compressed;
                importer.SaveAndReimport();count++;
            }
            camera.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log("PERSONA_THUMBNAILS_READY "+count+" visual sprites; "+fallbacks+" small props use template icons");
        }

        static bool HasReadableFootprint(Texture2D image)
        {
            int left=image.width,right=-1,bottom=image.height,top=-1;
            var pixels=image.GetPixels32();
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {
                if(pixels[y*image.width+x].a<32)continue;
                left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
            }
            return right>=left&&Math.Max(right-left+1,top-bottom+1)>=24;
        }

        static Texture2D FrameObject(Texture2D source)
        {
            var pixels=source.GetPixels32();int size=source.width;
            int left=size,top=size,right=-1,bottom=-1;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                if(pixels[y*size+x].a<20)continue;
                left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
            }
            if(right<left)throw new Exception("PERSONA_THUMB_TRANSPARENT");
            float span=Mathf.Max(right-left+1,top-bottom+1),cx=(left+right)*.5f,cy=(bottom+top)*.5f;
            const int outputSize=160;const float objectSize=132;
            var colors=new Color[outputSize*outputSize];
            for(int y=0;y<outputSize;y++)for(int x=0;x<outputSize;x++)
            {
                float sx=cx+(x-(outputSize-1)*.5f)*span/objectSize;
                float sy=cy+(y-(outputSize-1)*.5f)*span/objectSize;
                colors[y*outputSize+x]=sx<0||sx>=size||sy<0||sy>=size?Color.clear:source.GetPixelBilinear((sx+.5f)/size,(sy+.5f)/size);
            }
            var framed=new Texture2D(outputSize,outputSize,TextureFormat.RGBA32,false);
            framed.SetPixels(colors);framed.Apply();return framed;
        }
    }
}
