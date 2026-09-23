using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AboutMe.Editor
{
    public static class DeskBackdropTools
    {
        const string Root = "Assets/AboutMe/Resources/DeskCampus";
        static readonly string[] Palette = {"#F5EBD7", "#DFB88C", "#ABC9A9", "#ABC9D7", "#E7B1BA", "#F1CD73", "#757A73", "#CFE3EA"};
        static Material[] materials;
        static int meshId;
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        [MenuItem("About Me/Build memory desk background")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Meshes");
            materials = Palette.Select((color, i) => {
                string path = Root + "/Palette-" + i + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(Shader.Find("AboutMe/MemoryDesk")); AssetDatabase.CreateAsset(mat, path); }
                mat.shader=Shader.Find("AboutMe/MemoryDesk");mat.SetFloat("_Wood",i==1?1:0);mat.SetFloat("_Sky",i==7?1:0);
                mat.color = ToyFactory.ColorOf(color);EditorUtility.SetDirty(mat);return mat;
            }).ToArray();
            meshId = 0; meshCache.Clear();
            var root = new GameObject("Memory campus · desk surroundings"); root.AddComponent<DeskBackdrop>();
            var plinth = Group("01 · Architectural model plinth", root.transform);
            // The four floor solids are the exact existing ground footprint; no filled-in fall notches.
            foreach (var solid in CampusGeometry.Solids.Where(s => s.name.StartsWith("ground_main") || s.name.StartsWith("entry_bridge") || s.name.StartsWith("front_corner")))
            {
                float top = solid.center.y - solid.size.y * .5f;
                Box(solid.name + " support", plinth, new Vector3(solid.center.x, (top + DeskBackdrop.DeskTop) * .5f, solid.center.z),
                    new Vector3(solid.size.x, top - DeskBackdrop.DeskTop, solid.size.z), 0, .32f);
            }
            var desk = Group("02 · Pale wooden desk", root.transform);
            Box("Desk top · below rescue line", desk, new Vector3(0, -16, 10), new Vector3(282, 4, 196), 1, .8f);
            foreach(float x in new[]{-120f,120f})foreach(float z in new[]{-73f,91f})
                Box("Desk leg",desk,new Vector3(x,-44,z),new Vector3(7,52,7),1,.5f);
            // Restrained wood join lines, far below the playable surface.
            // Grain is shaded in world space; tiny contrasting join meshes looked like a tiled floor.

            var books = Group("03 · Three memory books", root.transform);
            Book(books, "Mint book", new Vector3(-28, -14, 65), new Vector3(57, 12, 31), 2, -5);
            Book(books, "Blue book", new Vector3(-31, -2, 64), new Vector3(54, 12, 29), 3, 3);
            Book(books, "Rose book", new Vector3(-27, 10, 66), new Vector3(55, 12, 30), 4, -4);

            var cup = Group("04 · Mint pencil cup", root.transform); cup.localPosition = new Vector3(-66, -14, 7);
            Part("Hollow mint cup", cup, Tube(11, 10, 28, 32), Vector3.zero, 2);
            Part("Cup inner shadow", cup, Cylinder(9.9f, 1, 24), new Vector3(0, 1, 0), 6);
            Pencil(cup, "Yellow pencil", new Vector3(-4, 1, 0), 44, 1.65f, 5, new Vector3(-7, 0, -8));
            Pencil(cup, "Rose pencil", new Vector3(3, 1, 3), 38, 1.5f, 4, new Vector3(9, 20, -5));
            Pencil(cup, "Blue pencil", new Vector3(0, 1, -4), 40, 1.75f, 3, new Vector3(-9, 0, 6));

            var photo = Group("05 · A landscape memory", root.transform); photo.localPosition = new Vector3(66, -14, 4); photo.localRotation = Quaternion.Euler(0, -22, 0);
            Box("Frame backing", photo, new Vector3(0, 20, 1), new Vector3(28, 40, 2), 1, .6f);
            Box("Cream photo mat", photo, new Vector3(0, 20, -.1f), new Vector3(24.5f, 36.5f, .4f), 0);
            Box("Pale blue photograph", photo, new Vector3(0, 20, -.34f), new Vector3(20, 29, .08f), 3);
            Part("Sun in photo", photo, Cylinder(3, .1f, 24), new Vector3(5, 28, -.43f), 5).localRotation = Quaternion.Euler(90, 0, 0);
            Polygon("Distant hills", photo, new[]{new Vector3(-10,5.5f,-.5f),new Vector3(-10,17,-.5f),new Vector3(-4,23,-.5f),new Vector3(5,16,-.5f),new Vector3(10,19,-.5f),new Vector3(10,5.5f,-.5f)}, 2);
            Polygon("Near hills", photo, new[]{new Vector3(-10,5.5f,-.55f),new Vector3(-10,11,-.55f),new Vector3(1,15,-.55f),new Vector3(10,10,-.55f),new Vector3(10,5.5f,-.55f)}, 0);
            Box("Back easel foot", photo, new Vector3(0, 8, 7), new Vector3(3, 18, 2), 1).localRotation = Quaternion.Euler(-30,0,0);

            var front = Group("06 · Oversized drawing pencil", root.transform);
            Pencil(front, "Desk pencil", new Vector3(-30, -11.7f, -61), 66, 2.25f, 5, new Vector3(0, 8, -90));
            BuildSouth(root.transform);
            var room = Group("07 · Quiet sunlit room", root.transform);
            Box("Back wall", room, new Vector3(0, 31, 119), new Vector3(292, 202, 3), 0);
            Box("Left wall", room, new Vector3(-145, 31, 0), new Vector3(3,202,240), 0);
            Box("Right wall", room, new Vector3(145,31,0), new Vector3(3,202,240), 0);
            Box("Front wall", room, new Vector3(0,31,-120), new Vector3(292,202,3), 0);
            Box("Ceiling", room, new Vector3(0,132,0), new Vector3(292,3,242), 0);
            Box("Room floor", room, new Vector3(0,-71.5f,0), new Vector3(292,3,242), 1);
            Box("Window sky", room, new Vector3(0,52,116.9f), new Vector3(166,85,.6f), 7);
            for(int i=-1;i<=1;i++) Box("Window upright "+i,room,new Vector3(i*83,52,115.8f),new Vector3(2.4f,88,2.5f),0,.2f);
            foreach(float y in new[]{9.5f,52,94.5f})Box("Window crossbar",room,new Vector3(0,y,115.7f),new Vector3(168,2,3),0,.2f);
            Box("Deep windowsill",room,new Vector3(0,8,114),new Vector3(175,2,9),1,.4f);
            // Clouds are deliberately sparse relief shapes; no moving distraction above the arena.
            // Soft cloud shapes are part of the window shader, without rectangular cloud slabs.
            Box("Left wall inset",room,new Vector3(-143.3f,39,44),new Vector3(.3f,37,35),1);
            Box("Left wall picture",room,new Vector3(-143,39,44),new Vector3(.3f,32,30),3);
            Box("Right wall inset",room,new Vector3(143.3f,41,-32),new Vector3(.3f,42,32),1);
            Box("Right wall picture",room,new Vector3(143,41,-32),new Vector3(.3f,37,27),2);
            Box("Front wall dado",room,new Vector3(0,9,-118.3f),new Vector3(287,2,.4f),1);

            // Window depth: quiet distant skyline, tree crowns, and gathered curtain folds.
            for(int i=0;i<7;i++)
            {
                float x=-53+i*18, height=9+(i%3)*5;
                Box("Distant skyline "+i,room,new Vector3(x,10+height*.5f,116.45f),new Vector3(8,height,.2f),3);
            }
            foreach(float x in new[]{-65f,58f})
            {
                Box("Window tree stem",room,new Vector3(x,27,115.9f),new Vector3(1.2f,32,.6f),1);
                for(int i=0;i<3;i++)
                    Ellipsoid("Soft distant foliage",room,new Vector3(x+(i-1)*8,34+(i%2)*10,115.6f),new Vector3(13,17,.55f),2);
            }
            foreach(float side in new[]{-1f,1f})for(int i=0;i<4;i++)
                Box("Curtain fold",room,new Vector3(side*(88+i*4),52,112.8f+(i%2)*1.4f),new Vector3(4.8f,89,3),0,1.1f);
            Plant(root.transform,"North window plant",new Vector3(94,-14,59),1.1f);

            BakeDeskShadows(root);

            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Backdrop.prefab");
            UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            Validate(prefab);
        }

        static Transform Group(string name, Transform parent) { var t = new GameObject(name).transform;t.SetParent(parent,false);return t; }
        static Transform Part(string name,Transform parent,Mesh mesh,Vector3 position,int color)
        {
            var t=Group(name,parent);t.localPosition=position;t.gameObject.isStatic=true;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=materials[color];
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return t;
        }
        static Transform Box(string name,Transform parent,Vector3 pos,Vector3 size,int color,float bevel=0)
        {return Part(name,parent,BoxMesh(size,bevel),pos,color);}

        static void BuildSouth(Transform parent)
        {
            var south=Group("08 · South writing and memory corner",parent);
            Book(south,"Mint sketchbook",new Vector3(-56,-14,-69),new Vector3(30,3.4f,23),2,12);
            Book(south,"Rose diary",new Vector3(-55,-10.6f,-68),new Vector3(28,3.7f,21),4,5);
            Box("Diary ribbon",south,new Vector3(-51,-6.85f,-75),new Vector3(2,.08f,21),5);
            var notes=Group("Loose memories and binder clip",south);notes.localPosition=new Vector3(53,-13.7f,-64);notes.localRotation=Quaternion.Euler(0,-12,0);
            Box("Mint note pad",notes,Vector3.zero,new Vector3(21,.55f,17),2,.2f);
            Box("Cream letter",notes,new Vector3(-4,.4f,1),new Vector3(20,.15f,14),0);
            for(int i=0;i<3;i++)Box("Pencil handwriting",notes,new Vector3(-6,.5f,i*2-2),new Vector3(9-i,.015f,.13f),1);
            Box("Binder clip",notes,new Vector3(5,1,-4),new Vector3(4,1.2f,2),6,.2f);
            Part("Clip handle",notes,Tube(1.25f,1,.18f,12),new Vector3(5,1.8f,-4),5).localRotation=Quaternion.Euler(90,0,0);
            Plant(south,"South leafy plant",new Vector3(-85,-14,-77),1.35f);
            Lamp(south,new Vector3(77,-14,-78));

            var chair=Group("Quiet desk chair beyond desk",south);chair.localPosition=new Vector3(2,-70,-105);
            Box("Chair upholstered back",chair,new Vector3(0,64,-4),new Vector3(33,32,5),2,2);
            Box("Chair seat",chair,new Vector3(0,36,3),new Vector3(31,4,21),2,1.5f);
            foreach(float x in new[]{-11f,11f})Box("Chair back support",chair,new Vector3(x,47,-3),new Vector3(2,40,2),1,.4f);
            foreach(float x in new[]{-11f,11f})foreach(float z in new[]{-4f,10f})
                Box("Chair leg",chair,new Vector3(x,17,z),new Vector3(2.5f,34,2.5f),1,.3f);

            var board=Group("South wall memory board",south);board.localPosition=new Vector3(0,38,-116.7f);
            Box("Maple pinboard frame",board,Vector3.zero,new Vector3(84,43,2),1,.8f);
            Box("Warm linen pinboard",board,new Vector3(0,0,1.1f),new Vector3(80,39,.35f),0);
            for(int i=0;i<4;i++)
            {
                var photo=Group("Pinned memory "+(i+1),board);photo.localPosition=new Vector3(-27+i*18,i%2==0?3:-4,1.5f);photo.localRotation=Quaternion.Euler(0,0,(i%2==0?-1:1)*6);
                Box("Photo paper",photo,Vector3.zero,new Vector3(14,18,.2f),0);
                Box("Printed pastel sky",photo,new Vector3(0,2,.15f),new Vector3(11,11,.12f),3);
                Box("Printed landscape",photo,new Vector3(0,-1,.24f),new Vector3(11,5,.08f),i%2==0?2:4);
                Part("Printed sun",photo,Cylinder(1.5f,.06f,12),new Vector3(2,4,.34f),5).localRotation=Quaternion.Euler(90,0,0);
                Box("Washi tape",photo,new Vector3(0,8.3f,.5f),new Vector3(6,2.3f,.15f),i%2==0?4:5);
            }
            Box("Shelf beneath memory board",south,new Vector3(0,8,-112),new Vector3(92,2,10),1,.4f);
            Book(south,"Shelf blue album",new Vector3(-27,9,-110),new Vector3(24,4,9),3,0);
            Book(south,"Shelf rose album",new Vector3(-28,13,-110),new Vector3(22,3,9),4,0);
            Box("Memory storage box",south,new Vector3(27,14,-111),new Vector3(22,10,9),2,.7f);
            Box("Box label",south,new Vector3(27,14,-106.3f),new Vector3(9,4,.15f),0);
        }

        static Transform Ellipsoid(string name,Transform parent,Vector3 position,Vector3 radii,int color)
        {
            const int rings=6,sides=12;var v=new List<Vector3>();var t=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {float a=y*Mathf.PI/rings,b=x*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)));}
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {int a=y*(sides+1)+x,b=a+sides+1;t.AddRange(new[]{a,a+1,b,b,a+1,b+1});}
            var part=Part(name,parent,SaveMesh("Soft low-poly sphere",v,t),position,color);part.localScale=radii;return part;
        }
        static void Plant(Transform parent,string name,Vector3 position,float scale)
        {
            var plant=Group(name,parent);plant.localPosition=position;plant.localScale=Vector3.one*scale;
            Part("Clay pot",plant,Cylinder(6,10,16,8),Vector3.zero,0);
            Part("Soil",plant,Cylinder(7.2f,.2f,16),Vector3.up*9.9f,6);
            Part("Stem",plant,Cylinder(.5f,23,8,.35f),Vector3.up*10,1);
            for(int i=0;i<5;i++)
            {
                float angle=i*2.4f;var leaf=Ellipsoid("Sculpted leaf",plant,new Vector3(Mathf.Cos(angle)*5,17+i*3,Mathf.Sin(angle)*5),new Vector3(3.4f,8,2.5f),2);
                leaf.localRotation=Quaternion.Euler(Mathf.Sin(angle)*38,0,-Mathf.Cos(angle)*38);
            }
        }
        static void Lamp(Transform parent,Vector3 position)
        {
            var lamp=Group("South butter-yellow reading lamp",parent);lamp.localPosition=position;
            Part("Round lamp base",lamp,Cylinder(10,1.6f,24),Vector3.zero,5);
            Part("Lamp upright",lamp,Cylinder(1.2f,22,12),new Vector3(2,1,0),5);
            var arm=Part("Angled lamp arm",lamp,Cylinder(1.1f,18,12),new Vector3(2,23,0),5);arm.localRotation=Quaternion.Euler(0,0,38);
            var shade=Group("Lamp shade",lamp);shade.localPosition=new Vector3(-9,34,0);shade.localRotation=Quaternion.Euler(0,0,-12);
            Part("Bell shade",shade,Cylinder(9,9,24,3),Vector3.zero,5);
            Part("Warm diffuser",shade,Cylinder(8.6f,.15f,24),new Vector3(0,-.1f,0),0);
        }

        // Offline shadow texture, projected from the actual static geometry. No runtime light,
        // collider or shadow caster is added. RG stores sunlight/contact occlusion respectively.
        static void BakeDeskShadows(GameObject root)
        {
            const int size=512;var sunMask=new float[size*size];var contactMask=new float[size*size];
            Vector3 ray=Quaternion.Euler(50,-137,0)*Vector3.forward;
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.transform.parent.name.StartsWith("07")&&!f.transform.parent.name.StartsWith("02")).ToList();
            var campus=Resources.Load<GameObject>("CampusV04/Campus");filters.AddRange(campus.GetComponentsInChildren<MeshFilter>());
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;var a=new Vector2[vertices.Length];var b=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 p=filter.transform.TransformPoint(vertices[i]);float height=Mathf.Max(0,p.y-DeskBackdrop.DeskTop);
                    var projected=p+ray*(height/-ray.y);
                    a[i]=new Vector2((projected.x/282+.5f)*(size-1),(projected.z/236+.5f)*(size-1));
                    b[i]=new Vector2((p.x/282+.5f)*(size-1),(p.z/236+.5f)*(size-1));
                }
                for(int i=0;i<indices.Length;i+=3)
                {Raster(sunMask,size,a[indices[i]],a[indices[i+1]],a[indices[i+2]]);Raster(contactMask,size,b[indices[i]],b[indices[i+1]],b[indices[i+2]]);}
            }
            for(int i=0;i<3;i++)sunMask=Blur(sunMask,size,3);
            contactMask=Blur(Blur(contactMask,size,2),size,2);
            var texture=new Texture2D(size,size,TextureFormat.RGB24,false,true);var colors=new Color[size*size];
            for(int i=0;i<colors.Length;i++)colors[i]=new Color(sunMask[i],contactMask[i],0,1);
            texture.SetPixels(colors);texture.Apply();string path=Root+"/TabletopShadows.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            foreach(var material in materials){material.SetTexture("_DeskShadow",AssetDatabase.LoadAssetAtPath<Texture2D>(path));EditorUtility.SetDirty(material);}
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static void Raster(float[] pixels,int size,Vector2 a,Vector2 b,Vector2 c)
        {
            float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.001f)return;
            int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,size-1),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,size-1);
            int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,size-1),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,size-1);
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {var p=new Vector2(x+.5f,y+.5f);float u=Cross(b-p,c-p)/area,v=Cross(c-p,a-p)/area;if(u>=0&&v>=0&&u+v<=1)pixels[y*size+x]=1;}
        }
        static float[] Blur(float[] input,int size,int radius)
        {
            var temp=new float[input.Length];var output=new float[input.Length];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float sum=0;for(int k=-radius;k<=radius;k++)sum+=input[y*size+Mathf.Clamp(x+k,0,size-1)];temp[y*size+x]=sum/(radius*2+1);}
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float sum=0;for(int k=-radius;k<=radius;k++)sum+=temp[Mathf.Clamp(y+k,0,size-1)*size+x];output[y*size+x]=sum/(radius*2+1);}
            return output;
        }
        static Mesh SaveMesh(string key,List<Vector3> vertices,List<int> indices)
        {
            if(meshCache.TryGetValue(key,out var cached))return cached;
            var mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/Meshes/Part-"+(meshId++)+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            meshCache.Add(key,mesh);return mesh;
        }
        static Mesh BoxMesh(Vector3 size,float bevel)
        {
            string key="Box "+size.ToString("F3")+" bevel "+bevel; if(meshCache.TryGetValue(key,out var cached))return cached;
            var vertices=new List<Vector3>();var indices=new List<int>();var h=size*.5f;
            bevel=Mathf.Min(bevel,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.95f);
            var normals=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var n in normals)
            {
                var u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(n,u);
                float hn=Vector3.Dot(h,new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z)));
                float hu=Vector3.Dot(h,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                float hv=Vector3.Dot(h,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
                var us=bevel>0?new[]{-hu,-hu+bevel,hu-bevel,hu}:new[]{-hu,hu};
                var vs=bevel>0?new[]{-hv,-hv+bevel,hv-bevel,hv}:new[]{-hv,hv};int start=vertices.Count;
                foreach(float y in vs)foreach(float x in us)
                {
                    var p=n*hn+u*x+v*y;
                    if(bevel>0){var inner=h-Vector3.one*bevel;var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));p=q+(p-q).normalized*bevel;}
                    vertices.Add(p);
                }
                for(int y=0;y<vs.Length-1;y++)for(int x=0;x<us.Length-1;x++)
                {int a=start+y*us.Length+x,b=a+1,c=a+us.Length,d=c+1;indices.AddRange(new[]{a,b,d,a,d,c});}
            }
            return SaveMesh(key,vertices,indices);
        }
        static Mesh Cylinder(float radius,float height,int sides,float topRadius=-1)
        {
            if(topRadius<0)topRadius=radius;string key=$"Cylinder {radius} {height} {sides} {topRadius}";
            if(meshCache.TryGetValue(key,out var cached))return cached;
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int k=v.Count;
                v.AddRange(new[]{new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),new Vector3(Mathf.Cos(a)*topRadius,height,Mathf.Sin(a)*topRadius),new Vector3(Mathf.Cos(b)*topRadius,height,Mathf.Sin(b)*topRadius),new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),Vector3.zero,Vector3.up*height});
                t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3,k+4,k,k+3,k+5,k+2,k+1});
            }
            return SaveMesh(key,v,t);
        }
        static Mesh Tube(float outer,float inner,float height,int sides)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int k=v.Count;
                foreach(float angle in new[]{a,b})foreach(var pair in new[]{new Vector2(outer,0),new Vector2(outer,height),new Vector2(inner,height),new Vector2(inner,1)})v.Add(new Vector3(Mathf.Cos(angle)*pair.x,pair.y,Mathf.Sin(angle)*pair.x));
                t.AddRange(new[]{k,k+1,k+5,k,k+5,k+4,k+1,k+2,k+6,k+1,k+6,k+5,k+2,k+3,k+7,k+2,k+7,k+6});
            }
            return SaveMesh($"Tube {outer} {inner} {height} {sides}",v,t);
        }
        static void Polygon(string name,Transform parent,Vector3[] vertices,int color)
        {
            var t=new List<int>();for(int i=1;i<vertices.Length-1;i++)t.AddRange(new[]{0,i,i+1});
            Part(name,parent,SaveMesh(name,vertices.ToList(),t),Vector3.zero,color);
        }
        static void Book(Transform parent,string name,Vector3 pos,Vector3 size,int color,float yaw)
        {
            var book=Group(name,parent);book.localPosition=pos;book.localRotation=Quaternion.Euler(0,yaw,0);
            Box("Pages",book,new Vector3(0,size.y*.5f,0),size-new Vector3(1.2f,1.2f,1),0,.35f);
            foreach(float y in new[]{.45f,size.y-.45f})Box("Rounded cover",book,new Vector3(0,y,0),new Vector3(size.x,.9f,size.z),color,.3f);
            Box("Spine",book,new Vector3(-size.x*.5f+.6f,size.y*.5f,0),new Vector3(1.2f,size.y,size.z),color,.35f);
            for(int i=1;i<4;i++)Box("Page signature",book,new Vector3(.4f,size.y*i/4,-size.z*.5f+.48f),new Vector3(size.x-2,.09f,.06f),1);
        }
        static void Pencil(Transform parent,string name,Vector3 pos,float height,float radius,int color,Vector3 rotation)
        {
            var p=Group(name,parent);p.localPosition=pos;p.localRotation=Quaternion.Euler(rotation);
            Part("Hexagonal lacquer",p,Cylinder(radius,height-7,6),Vector3.up*3,color);
            Part("Eraser",p,Cylinder(radius,2.5f,6),Vector3.zero,4);
            Part("Ferrule",p,Cylinder(radius*1.02f,1.5f,6),Vector3.up*2.5f,0);
            Part("Sharpened wood",p,Cylinder(radius,3.2f,6,.38f),Vector3.up*(height-4),1);
            Part("Graphite",p,Cylinder(.38f,.8f,6,0),Vector3.up*(height-.8f),6);
        }
        public static void Validate(GameObject prefab=null)
        {
            if(!prefab)prefab=Resources.Load<GameObject>(DeskBackdrop.ResourcePath);
            if(!prefab)throw new Exception("Missing desk background prefab");
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);var renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);
            int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),colors=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count();
            if(triangles>15000||colors>8)throw new Exception("Background budget exceeded");
            if(prefab.GetComponentsInChildren<Collider>(true).Length!=0||prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0||prefab.GetComponentsInChildren<Mirror.NetworkIdentity>(true).Length!=0)throw new Exception("Background must be visual only");
            if(renderers.Any(r=>r.shadowCastingMode!=ShadowCastingMode.Off))throw new Exception("Background casts realtime shadows");
            Directory.CreateDirectory("../artifacts/desk-campus");
            File.WriteAllText("../artifacts/desk-campus/asset-budget.json",$"{{\"triangles\":{triangles},\"sharedMaterials\":{colors},\"renderers\":{renderers.Length},\"colliders\":0,\"deskTop\":{DeskBackdrop.DeskTop},\"passed\":true}}");
            Debug.Log($"DESK_BUDGET_PASS triangles={triangles} materials={colors} renderers={renderers.Length}");
        }

        [MenuItem("About Me/Review memory desk background")]
        public static void Review()
        {
            Validate();CampusImport.Preview();
            var camera=UnityEngine.Object.FindObjectOfType<Camera>();
            string output="../artifacts/desk-campus/views";Directory.CreateDirectory(output);
            void Capture(string name,int width,int height)
            {
                var rt=new RenderTexture(width,height,24){antiAliasing=4};camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());
                camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
            Capture("overview-16x9",1600,900);Capture("overview-4x3",1200,900);Capture("overview-wide",1950,900);
            camera.fieldOfView=60;camera.transform.position=new Vector3(0,48,-107);camera.transform.LookAt(new Vector3(0,-6,5));Capture("environment-overview",1600,900);camera.fieldOfView=48;
            camera.transform.position=new Vector3(69,51,-91);camera.transform.LookAt(new Vector3(0,0,8));Capture("desk-three-quarter",1600,900);
            foreach(var view in new[]{("south",new Vector3(0,29,55),new Vector3(0,9,-76)),("east",new Vector3(-54,26,0),new Vector3(68,3,0)),("west",new Vector3(54,26,0),new Vector3(-68,3,0))})
            {camera.fieldOfView=58;camera.transform.position=view.Item2;camera.transform.LookAt(view.Item3);Capture("environment-"+view.Item1,1600,900);}
            camera.fieldOfView=48;
            foreach(float level in new[]{1f,5.5f})foreach(float pitch in new[]{10f,32f,65f})for(int yaw=0;yaw<360;yaw+=45)
            {
                var target=level>2?new Vector3(27.75f,level+.5f,3):new Vector3(0,level+.5f,-23);
                camera.transform.position=CampusGeometry.CameraPosition(target,target+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-8));camera.transform.LookAt(target);
                Capture($"level-{level}-pitch-{pitch}-yaw-{yaw}",960,540);
            }
            var fall=DeskBackdropAudit.CheckFall();if(!fall.Item1)throw new Exception("Desk fall/rescue audit failed");
            File.WriteAllText("../artifacts/desk-campus/fall-check.json",$"{{\"passed\":true,\"lowestFeet\":{fall.Item2},\"deskTop\":{DeskBackdrop.DeskTop}}}");
            ProjectTools.TestRules();
            Debug.Log("DESK_REVIEW_PASS 56 views; four directions; fall rescue and existing rule suite");
        }

        public static void BuildAndReview(){Build();Review();}

        public static void BuildMac()
        {
            Validate();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/AboutMe/Scenes/Bootstrap.unity"},locationPathName=Environment.GetEnvironmentVariable("ABOUTME_MAC_OUTPUT")??"../builds/mac/About Me.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Desk macOS build failed");
            Debug.Log("DESK_MAC_BUILD_PASS");
        }
        public static void BuildWeb()
        {
            Validate();PlayerSettings.WebGL.template="PROJECT:AboutMe";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/AboutMe/Scenes/Bootstrap.unity"},locationPathName="../builds/web",target=BuildTarget.WebGL,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Desk WebGL build failed");
            Debug.Log("DESK_WEB_BUILD_PASS");
        }
    }
}
