"""Inspect an animal FBX/GLB in an isolated background Blender process."""
import bpy,sys,argparse,json,math
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--input',required=True);p.add_argument('--out',required=True);p.add_argument('--inspect-only',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
path=Path(args.input).resolve()
if path.suffix.lower()=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path))
else:bpy.ops.import_scene.gltf(filepath=str(path))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
scene=bpy.context.scene
# Match the Unity toy material rather than FBX's imported metallic defaults.
for mat in bpy.data.materials:
 if mat.use_nodes:
  shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
  if shader:
   shader.inputs['Metallic'].default_value=0
   shader.inputs['Roughness'].default_value=.6
   for link in list(shader.inputs['Normal'].links):mat.node_tree.links.remove(link)
def points():
 dg=bpy.context.evaluated_depsgraph_get();ps=[]
 for o in meshes:
  e=o.evaluated_get(dg);m=e.to_mesh();ps.extend(e.matrix_world@v.co for v in m.vertices);e.to_mesh_clear()
 return ps
actions=list(bpy.data.actions)
start,end=actions[0].frame_range if actions else (1,1)
scene.frame_set(int(start));ps=points();lo=Vector(tuple(min(v[i] for v in ps) for i in range(3)));hi=Vector(tuple(max(v[i] for v in ps) for i in range(3)))
report={'input':str(path),'bounds':{'min':list(lo),'max':list(hi)},'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'groups':len(o.vertex_groups),'materials':[m.name for m in o.data.materials]} for o in meshes],'rigs':[{'name':o.name,'bones':len(o.data.bones),'bone_names':[b.name for b in o.data.bones]} for o in rigs],'actions':[{'name':a.name,'range':list(a.frame_range)} for a in actions],'images':[{'name':i.name,'size':list(i.size),'packed':bool(i.packed_file)} for i in bpy.data.images]}
snapshots=[]
for fraction in [0,.25,.5,.75,1]:
 scene.frame_set(int(start+(end-start)*fraction));snapshots.append(points())
report['max_deformation']=max((a-b).length for s in snapshots[1:] for a,b in zip(snapshots[0],s))
report['loop_seam']=max((a-b).length for a,b in zip(snapshots[0],snapshots[-1]))
(out/'report.json').write_text(json.dumps(report,indent=2)+'\n')
if args.inspect_only:print(json.dumps(report));sys.exit(0)
container=bpy.data.objects.new('Display normalization',None);scene.collection.objects.link(container)
for o in list(scene.objects):
 if o!=container and o.parent is None:o.parent=container
scale=1/max(hi-lo);container.scale=(scale,)*3;container.location=Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z))*scale
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Review floor'
material=bpy.data.materials.new('Cream');material.diffuse_color=(.8,.75,.67,1);floor.data.materials.append(material)
bpy.ops.object.camera_add(location=(2.5,-3,1.65));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.45;scene.camera=camera
for pos,power,size in [((2,-3,4),500,4),((-2,1,3),350,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Review world');scene.world.use_nodes=True;bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.75,.75,.75,1);bg.inputs['Strength'].default_value=.7
try:scene.render.engine='CYCLES'
except TypeError:pass
scene.cycles.samples=12;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
for index,fraction in enumerate([0,.25,.5,.75]):
 scene.frame_set(int(start+(end-start)*fraction));scene.render.filepath=str(out/f'frame-{index}.png');bpy.ops.render.render(write_still=True)
print('ANIMAL_ANIMATION_REVIEW',json.dumps({'bounds':report['bounds'],'bones':[r['bones'] for r in report['rigs']],'actions':report['actions'],'max_deformation':report['max_deformation'],'loop_seam':report['loop_seam']}))
