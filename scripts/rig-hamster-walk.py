"""Manual skinning and toy biped stride for the existing Tripo hamster, no API calls.

Runs in an isolated background Blender process. Original mesh/UV/textures retained.
"""
import bpy,math,json
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/animal-persona-animation/hamster-manual';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'client/Assets/AboutMe/Art/TripoAnimals/animal-hamster/model.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in meshes:
 world=Matrix.Rotation(-math.pi/2,4,'Z')@o.matrix_world
 for v in o.data.vertices:v.co=world@v.co
 o.parent=None;o.matrix_world=Matrix.Identity(4)
points=[v.co for o in meshes for v in o.data.vertices];lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
size=hi-lo;center=(lo+hi)/2;scale=1/max(size)
for o in meshes:
 for v in o.data.vertices:v.co=(v.co-Vector((center.x,center.y,lo.z)))*scale
w,d,h=size*scale
arm=bpy.data.armatures.new('Hamster manual skin');rig=bpy.data.objects.new('HamsterRig',arm);bpy.context.scene.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
 b=arm.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=arm.edit_bones[parent]
 return b
bone('Root',(0,0,0),(0,0,.12*h)).use_deform=False
bone('Body',(0,0,.25*h),(0,0,.70*h),'Root');bone('Head',(0,0,.70*h),(0,0,.91*h),'Body')
for side,s in [('L',1),('R',-1)]:
 bone('Foot.'+side,(s*.24*w,-.12*d,.065*h),(s*.24*w,-.34*d,.065*h),'Body')
 bone('Paw.'+side,(s*.35*w,-.15*d,.53*h),(s*.35*w,-.37*d,.46*h),'Body')
bpy.ops.object.mode_set(mode='OBJECT')
counts={b.name:0 for b in arm.bones}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for o in meshes:
 groups={b.name:o.vertex_groups.new(name=b.name) for b in arm.bones}
 for v in o.data.vertices:
  x,y,z=v.co;side='L' if x>=0 else 'R'
  foot=(1-smooth(.10*h,.26*h,z))*smooth(.08*w,.18*w,abs(x))
  paw=smooth(.25*w,.35*w,abs(x))*(1-smooth(.57*h,.64*h,z))*smooth(.35*h,.43*h,z)*(1-smooth(-.08*d,.1*d,y))
  head=smooth(.64*h,.75*h,z)
  weights={'Foot.'+side:foot,'Paw.'+side:paw,'Head':head,'Body':max(0,1-foot-paw-head)};total=sum(weights.values())
  for n,value in weights.items():
   if value>0:groups[n].add([v.index],value/total,'REPLACE');counts[n]+=1
 mod=o.modifiers.new('Manual weighted limbs','ARMATURE');mod.object=rig;o.parent=rig
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=31
# Feet have a contact phase and a lifting swing phase, alternating half a cycle.
for frame in range(1,32):
 phase=(frame-1)/30
 for side,offset in [('L',0),('R',.5)]:
  t=(phase+offset)%1;pb=rig.pose.bones['Foot.'+side]
  if t<.60:forward=.055*d-(t/.60)*.11*d;lift=0
  else:
   u=(t-.60)/.40;forward=-.055*d+u*.11*d;lift=math.sin(math.pi*u)*.034*h
  delta=Vector((0,-forward,lift));pb.location=pb.bone.matrix_local.to_3x3().inverted()@delta;pb.keyframe_insert('location',frame=frame)
  paw=rig.pose.bones['Paw.'+side];paw.rotation_mode='XYZ';paw.rotation_euler.x=math.sin(phase*2*math.pi+offset*2*math.pi)*.13;paw.keyframe_insert('rotation_euler',frame=frame)
 body=rig.pose.bones['Body'];body.rotation_mode='XYZ';body.rotation_euler.y=math.sin(phase*2*math.pi)*.018;body.keyframe_insert('rotation_euler',frame=frame)
 head=rig.pose.bones['Head'];head.rotation_mode='XYZ';head.rotation_euler.y=-math.sin(phase*2*math.pi)*.012;head.keyframe_insert('rotation_euler',frame=frame)
action=rig.animation_data.action;action.name='Hamster_Walk'
from bpy_extras import anim_utils
bag=anim_utils.action_get_channelbag_for_slot(action,rig.animation_data.action_slot)
for fc in bag.fcurves:
 for kp in fc.keyframe_points:kp.interpolation='LINEAR'
 fc.modifiers.new('CYCLES')
scene.frame_set(1)
for mat in bpy.data.materials:
 if mat.use_nodes:
  shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
  if shader:shader.inputs['Metallic'].default_value=0;shader.inputs['Roughness'].default_value=.6
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'hamster-walk.blend'))
missing=sum(1 for o in meshes for v in o.data.vertices if not v.groups)
print('HAMSTER_WEIGHT_COUNTS',missing,counts)
assert missing==0 and counts['Foot.L']>0 and counts['Foot.R']>0
(OUT/'provenance.json').write_text(json.dumps({'source':'existing Tripo hamster mesh; local manual skinning and baked walk','source_task_id':'6b4e5bec-7887-4120-9def-5070a30f4b9e','credits_consumed':0,'bones':len(arm.bones),'weighted_vertices':counts,'unweighted_vertices':missing,'source_bounds':{'min':list(lo),'max':list(hi)},'Blender_version':bpy.app.version_string},indent=2)+'\n')
print('HAMSTER_MANUAL_SKIN_CREATED',counts)
