"""Bake a short biped stride on the successful Tripo rabbit rig, no API calls."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/animal-persona-animation/rabbit-local';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'artifacts/animal-persona-animation/rabbit-rig/model.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.vertex_groups)>0]
for o in list(bpy.context.scene.objects):
 if o.type=='MESH' and o not in meshes:bpy.data.objects.remove(o,do_unlink=True)
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=31;scene.render.fps=30
driven=[]
def rotate(name,axis,angle,frame):
 pb=rig.pose.bones[name];local=pb.bone.matrix_local.to_3x3().inverted()@Vector(axis);pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion(local.normalized(),angle);pb.keyframe_insert('rotation_quaternion',frame=frame)
 if name not in driven:driven.append(name)
for frame in range(1,32):
 phase=(frame-1)/30*math.tau
 for upper,lower,paw,offset in [('bone_15','bone_16','tripo::0_Right_Limb_0',0),('tripo::1_Left_Limb_1','tripo::1_Left_Limb_2','tripo::0_Left_Limb_0',math.pi)]:
  swing=math.sin(phase+offset);rotate(upper,(0,1,0),swing*.36,frame);rotate(lower,(0,1,0),max(0,swing)*-.25,frame);rotate(paw,(0,1,0),-swing*.12,frame)
 rotate('tripo::Spine_0',(1,0,0),math.sin(phase)*.015,frame)
 rotate('tripo::Head_1',(1,0,0),-math.sin(phase)*.012,frame)
action=rig.animation_data.action;action.name='Rabbit_Walk_Local'
from bpy_extras import anim_utils
bag=anim_utils.action_get_channelbag_for_slot(action,rig.animation_data.action_slot)
for fc in bag.fcurves:
 for kp in fc.keyframe_points:kp.interpolation='LINEAR'
 fc.modifiers.new('CYCLES')
scene.frame_set(1)
for mat in bpy.data.materials:
 if mat.use_nodes:
  shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
  if shader:
   shader.inputs['Metallic'].default_value=0;shader.inputs['Roughness'].default_value=.6
   for link in list(shader.inputs['Normal'].links):mat.node_tree.links.remove(link)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'rabbit-walk.blend'))
(OUT/'provenance.json').write_text(json.dumps({'source':'successful Tripo biped rig; locally authored walk following failed refunded retarget','rig_task_id':'bef66302-0a23-4ddb-bd89-b45b5cfef86c','rig_credits_consumed':25,'local_animation_credits':0,'driven_bones':driven,'removed_helper_geometry':True,'Blender_version':bpy.app.version_string},indent=2)+'\n')
print('RABBIT_LOCAL_WALK_CREATED',driven)
