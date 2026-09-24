"""Refresh only the editable fitting body in the finished .blend, preserving reviewed accessories."""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/claude-girl-modular'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'ClaudeGirl_Modular.blend'))
old=bpy.data.objects.get('Original body wearing Claude outfit')
if old:bpy.data.objects.remove(old,do_unlink=True)
d=json.loads((OUT/'claude-base-neutral.json').read_text());rig=bpy.data.objects.get('Claude_Fitting_Rig');names=[b['name'] for b in d['bones']]
mesh=bpy.data.meshes.new('Unchanged original body with mouth and brows');vertices=[(v['x'],-v['z'],v['y']) for v in d['vertices']];faces=[list(reversed(d['triangles'][i:i+3])) for i in range(0,len(d['triangles']),3)]
mesh.from_pydata(vertices,[],faces);mesh.update();o=bpy.data.objects.new('Original body wearing Claude outfit',mesh);bpy.context.collection.objects.link(o);o.parent=rig
mesh.materials.append(bpy.data.materials['Claude Toy vertex colours']);attr=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
for i,c in enumerate(d['colors']):attr.data[i].color=(c['r'],c['g'],c['b'],c['a'])
for p in mesh.polygons:p.use_smooth=True
groups=[o.vertex_groups.new(name=n) for n in names]
for i,w in enumerate(d['weights']):
 for j in range(4):
  weight=w.get('m_Weight'+str(j),0)
  if weight>0:groups[w['m_BoneIndex'+str(j)]].add([i],weight,'REPLACE')
m=o.modifiers.new('Original shared skeleton','ARMATURE');m.object=rig
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
# Open the source with the completed model framed in material view.
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_distance=4.2;area.spaces.active.region_3d.view_location=(0,0,.3)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ClaudeGirl_Modular.blend'))
print('Complete source body refreshed without modifying reviewed accessory buffers')
