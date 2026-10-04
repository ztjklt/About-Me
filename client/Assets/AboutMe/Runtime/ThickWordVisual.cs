using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace AboutMe
{
    // Real extruded font outlines: cached shared Mesh, one renderer, no TextMesh,
    // texture plane, font network request, per-frame mesh build or convex cooking.
    public static class ThickWordVisual
    {
        public const int MaxCodepoints=12;
        public const float Depth=.16f;
        const int GlyphCacheLimit=256, PageCacheLimit=8;
        const long IdleMeshBudget=24*1024*1024;
        struct Entry {public int page,offset,size;public float advance;}
        sealed class Glyph {public Vector2[] vertices;public int[] triangles;public int[] rings;public float advance;public long used;}
        sealed class Page {public TextAsset asset;public byte[] bytes;public long used;}
        sealed class Word {public Mesh mesh;public int references;public long used,bytes;}
        static Dictionary<int,Entry> index;
        static readonly Dictionary<int,Glyph> glyphs=new Dictionary<int,Glyph>();
        static readonly Dictionary<int,Page> pages=new Dictionary<int,Page>();
        static readonly Dictionary<string,Word> words=new Dictionary<string,Word>();
        static long clock;
        public static int GlyphLoads {get;private set;}
        public static int MeshBuilds {get;private set;}
        public static int MeshHits {get;private set;}
        public static int SupportedCodepoints {get {EnsureIndex();return index.Count;}}
        public static int CachedGlyphs=>glyphs.Count;
        public static int CachedPages=>pages.Count;
        public static int CachedWords=>words.Count;
        public static long MeshBytes {get {long result=0;foreach(var w in words.Values)result+=w.bytes;return result;}}
        public static long PageBytes {get {long result=0;foreach(var p in pages.Values)result+=p.bytes.Length;return result;}}

        static void EnsureIndex()
        {
            if(index!=null)return;
            var asset=Resources.Load<TextAsset>("ThickWords/index");
            if(!asset)throw new InvalidOperationException("本地厚字字体轮廓资源缺失 / Local thick-word font outlines are missing.");
            using(var reader=new BinaryReader(new MemoryStream(asset.bytes)))
            {
                if(new string(reader.ReadChars(4))!="TWG2")throw new InvalidDataException("Unsupported thick-word font index");
                int count=reader.ReadInt32();index=new Dictionary<int,Entry>(count);
                for(int i=0;i<count;i++)index.Add(reader.ReadInt32(),new Entry{page=reader.ReadInt32(),offset=reader.ReadInt32(),size=reader.ReadInt32(),advance=reader.ReadSingle()});
            }
            Resources.UnloadAsset(asset);
        }
        static List<int> Codepoints(string text,out string reason)
        {
            reason="";var result=new List<int>();
            if(string.IsNullOrWhiteSpace(text)){reason="请输入可见文字 / Enter a visible word.";return null;}
            for(int i=0;i<text.Length;i++)
            {
                if(char.IsHighSurrogate(text[i]))
                {
                    if(i+1>=text.Length||!char.IsLowSurrogate(text[i+1])){reason="文字编码不完整 / Invalid Unicode text.";return null;}
                    result.Add(char.ConvertToUtf32(text[i],text[++i]));
                }
                else if(char.IsLowSurrogate(text[i])){reason="文字编码不完整 / Invalid Unicode text.";return null;}
                else result.Add(text[i]);
            }
            if(result.Count>MaxCodepoints){reason="实体文字最多 12 个字符 / Physical words support at most 12 Unicode characters.";return null;}
            return result;
        }
        public static bool HasGlyphs(string text)=>TryValidate(text,out _);
        public static bool TryValidate(string text,out string reason)
        {
            var cps=Codepoints(text,out reason);if(cps==null)return false;
            try {EnsureIndex();}
            catch(Exception error){reason=error.Message;return false;}
            foreach(int cp in cps)if(!index.ContainsKey(cp))
            {reason="本地字体没有可挤出的字形 "+char.ConvertFromUtf32(cp)+" (U+"+cp.ToString("X4")+") / No physical glyph in the local font. Unsupported emoji and invisible characters cannot form physical words.";return false;}
            reason="";return true;
        }
        public static GameObject Create(string text,string abilityId,Transform parent,string color)
        {
            if(TryCreate(text,abilityId,parent,color,out var model,out var reason))return model;
            Debug.LogWarning("THICK_WORD_REJECTED: "+reason);return null;
        }
        public static bool TryCreate(string text,string abilityId,Transform parent,string color,out GameObject model,out string reason)
        {
            model=null;if(!TryValidate(text,out reason))return false;
            try
            {
                if(!words.TryGetValue(text,out var word))
                {
                    var mesh=BuildWord(text);word=new Word{mesh=mesh,bytes=mesh.vertexCount*32L+mesh.triangles.Length*2L};
                    words.Add(text,word);MeshBuilds++;
                }
                else MeshHits++;
                word.references++;word.used=++clock;
                model=new GameObject("Thick word · "+text);model.transform.SetParent(parent,false);
                model.AddComponent<MeshFilter>().sharedMesh=word.mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial=ToyFactory.Material(color);
                var collider=model.AddComponent<BoxCollider>();collider.center=word.mesh.bounds.center;collider.size=word.mesh.bounds.size;collider.isTrigger=true;
                var grip=new GameObject("Grip");grip.transform.SetParent(model.transform,false);
                var meta=model.AddComponent<ThickWordMetadata>();meta.Text=text;meta.AbilityId=abilityId??"";
                var visual=model.AddComponent<PersonaVisualMeta>();visual.visualId="trait-word";visual.source="AboutMeFont.otf genuine CFF glyph outlines";visual.license="OFL-1.1; bundled Noto Sans CJK SC notices";visual.colliderRadius=word.mesh.bounds.extents.magnitude;visual.swallowScale=.6f;
                model.AddComponent<ThickWordMeshLease>().Key=text;
                TrimWords();reason="";return true;
            }
            catch(Exception error){if(model)Dispose(model);model=null;reason="厚字模型创建失败 / Thick-word model failed: "+error.Message;return false;}
        }
        static Glyph GetGlyph(int cp)
        {
            if(glyphs.TryGetValue(cp,out var cached)){cached.used=++clock;return cached;}
            var e=index[cp];
            if(e.size==0)return new Glyph{vertices=Array.Empty<Vector2>(),triangles=Array.Empty<int>(),rings=Array.Empty<int>(),advance=e.advance};
            if(!pages.TryGetValue(e.page,out var page))
            {
                var asset=Resources.Load<TextAsset>("ThickWords/page-"+e.page.ToString("000"));
                if(!asset)throw new InvalidDataException("Missing thick-word glyph page "+e.page);
                page=new Page{asset=asset,bytes=asset.bytes};pages.Add(e.page,page);
            }
            page.used=++clock;
            Glyph g;
            using(var compressed=new MemoryStream(page.bytes,e.offset,e.size,false))
            using(var inflate=new DeflateStream(compressed,CompressionMode.Decompress))
            using(var reader=new BinaryReader(inflate))
            {
                int n=reader.ReadInt32(),t=reader.ReadInt32(),r=reader.ReadInt32();
                g=new Glyph{vertices=new Vector2[n],triangles=new int[t],rings=new int[r*2],advance=e.advance,used=++clock};
                for(int i=0;i<n;i++)g.vertices[i]=new Vector2(reader.ReadInt16()/2048f,reader.ReadInt16()/2048f);
                for(int i=0;i<t;i++)g.triangles[i]=reader.ReadInt32();
                for(int i=0;i<r*2;i++)g.rings[i]=reader.ReadInt32();
            }
            glyphs.Add(cp,g);GlyphLoads++;
            while(glyphs.Count>GlyphCacheLimit){int oldest=-1;long age=long.MaxValue;foreach(var p in glyphs)if(p.Value.used<age){oldest=p.Key;age=p.Value.used;}glyphs.Remove(oldest);}
            while(pages.Count>PageCacheLimit){int oldest=-1;long age=long.MaxValue;foreach(var p in pages)if(p.Value.used<age){oldest=p.Key;age=p.Value.used;}Resources.UnloadAsset(pages[oldest].asset);pages.Remove(oldest);}
            return g;
        }
        static Mesh BuildWord(string text)
        {
            var cps=Codepoints(text,out _);var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();float cursor=0;
            foreach(int cp in cps)
            {
                var glyph=GetGlyph(cp);int offset=vertices.Count,n=glyph.vertices.Length;
                foreach(var v in glyph.vertices){vertices.Add(new Vector3(v.x+cursor,v.y,Depth*.5f));normals.Add(Vector3.forward);}
                foreach(var v in glyph.vertices){vertices.Add(new Vector3(v.x+cursor,v.y,-Depth*.5f));normals.Add(Vector3.back);}
                for(int i=0;i<glyph.triangles.Length;i+=3)
                {
                    int a=glyph.triangles[i],b=glyph.triangles[i+1],c=glyph.triangles[i+2];
                    triangles.Add(offset+a);triangles.Add(offset+b);triangles.Add(offset+c);
                    triangles.Add(offset+n+a);triangles.Add(offset+n+c);triangles.Add(offset+n+b);
                }
                for(int r=0;r<glyph.rings.Length;r+=2)
                {
                    int start=glyph.rings[r],count=glyph.rings[r+1];
                    for(int i=0;i<count;i++)
                    {
                        var a=glyph.vertices[start+i];var b=glyph.vertices[start+(i+1)%count];
                        var normal=new Vector3(b.y-a.y,a.x-b.x,0).normalized;int s=vertices.Count;
                        vertices.Add(new Vector3(a.x+cursor,a.y,-Depth*.5f));vertices.Add(new Vector3(b.x+cursor,b.y,-Depth*.5f));
                        vertices.Add(new Vector3(b.x+cursor,b.y,Depth*.5f));vertices.Add(new Vector3(a.x+cursor,a.y,Depth*.5f));
                        for(int j=0;j<4;j++)normals.Add(normal);
                        triangles.Add(s);triangles.Add(s+1);triangles.Add(s+2);triangles.Add(s);triangles.Add(s+2);triangles.Add(s+3);
                    }
                }
                cursor+=glyph.advance+.055f;
            }
            if(vertices.Count>65535)throw new InvalidDataException("Physical word exceeds mesh vertex budget");
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            float scale=Mathf.Min(1.25f/Mathf.Max(bounds.size.x,.01f),.65f/Mathf.Max(bounds.size.y,.01f));
            // Normalize XY only. A 12-character word still has visible .16 m depth.
            // ToyActor faces local +Z. Mirror local X so the front is readable
            // from +Z (Unity camera right is -X when looking toward -Z).
            for(int i=0;i<vertices.Count;i++){var v=vertices[i];vertices[i]=new Vector3(-(v.x-bounds.center.x)*scale,(v.y-bounds.center.y)*scale,v.z);var normal=normals[i];normals[i]=new Vector3(-normal.x,normal.y,normal.z);}
            for(int i=0;i<triangles.Count;i+=3){int b=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=b;}
            var mesh=new Mesh{name="Extruded AboutMeFont · "+text};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
        }
        internal static void Release(string key)
        {if(key!=null&&words.TryGetValue(key,out var word)){word.references=Mathf.Max(0,word.references-1);word.used=++clock;}TrimWords();}
        static void TrimWords()
        {
            while(words.Count>96||MeshBytes>IdleMeshBudget)
            {
                string oldest=null;long age=long.MaxValue;
                foreach(var p in words)if(p.Value.references==0&&p.Value.used<age){oldest=p.Key;age=p.Value.used;}
                if(oldest==null)break;Dispose(words[oldest].mesh);words.Remove(oldest);
            }
        }
        // Only after all physical-word instances have been destroyed (editor audit/domain reset).
        public static bool ClearUnusedCaches()
        {
            foreach(var w in words.Values)if(w.references>0)return false;
            foreach(var w in words.Values)Dispose(w.mesh);words.Clear();glyphs.Clear();
            foreach(var p in pages.Values)Resources.UnloadAsset(p.asset);pages.Clear();return true;
        }
        static void Dispose(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
    public sealed class ThickWordMetadata : MonoBehaviour {public string Text,AbilityId;}
    [ExecuteAlways] public sealed class ThickWordMeshLease : MonoBehaviour
    {internal string Key;void OnDestroy(){ThickWordVisual.Release(Key);}}
}
