"""Blender: normalize the reviewed Tripo parts and export native-engine mesh data."""
import bpy,bmesh,json,math,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/characters-v1';stats={}
for name,title in [('head','Head'),('headphones','Headphones'),('sling','SlingBag')]:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 result=json.loads((OUT/(name+'-job.json')).read_text());bpy.ops.import_scene.gltf(filepath=result['model_file'])
 objects=[o for o in bpy.context.selected_objects if o.type=='MESH'];bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 if name=='head':
  modifier=o.modifiers.new('Surface cleanup','SMOOTH');modifier.factor=.45;modifier.iterations=8;bpy.ops.object.modifier_apply(modifier=modifier.name)
 if name!='head':
  xy=np.array([[v.co.x,v.co.y] for v in o.data.vertices]);values,vectors=np.linalg.eigh(np.cov(xy.T));axis=vectors[:,np.argmax(values)];angle=-math.atan2(axis[1],axis[0]);rotation=Matrix.Rotation(angle,4,'Z')
  for v in o.data.vertices:v.co=rotation@v.co
  ys=[v.co.y for v in o.data.vertices]
  if name=='sling' and sum(ys)/len(ys)>(min(ys)+max(ys))/2:
   for v in o.data.vertices:v.co=Matrix.Rotation(math.pi,4,'Z')@v.co
 lo=Vector([min(v.co[i] for v in o.data.vertices) for i in range(3)]);hi=Vector([max(v.co[i] for v in o.data.vertices) for i in range(3)]);dim=hi-lo;center=(lo+hi)/2
 for v in o.data.vertices:v.co=Vector([(v.co[i]-center[i])/dim[i] for i in range(3)])
 o.data.update();o.data.calc_loop_triangles()
 vertices=[{'x':round(v.co.x,6),'y':round(v.co.z,6),'z':round(-v.co.y,6)} for v in o.data.vertices]
 triangles=[i for triangle in o.data.loop_triangles for i in triangle.vertices]
 (OUT/(title+'.json')).write_text(json.dumps({'vertices':vertices,'triangles':triangles},separators=(',',':')))
 bpy.ops.export_scene.gltf(filepath=str(OUT/(title+'.glb')),export_format='GLB',use_selection=True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/(title+'.fbx')),use_selection=True,add_leaf_bones=False)
 stats[title]={'triangles':len(triangles)//3,'vertices':len(vertices),'source_task':result['task_id'],'credits':result['credits_consumed'],'raw_bounds':list(dim)}
(OUT/'mesh-statistics.json').write_text(json.dumps(stats,indent=2));print('CHARACTER_PARTS_EXPORTED',stats)
