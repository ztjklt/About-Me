"""Normalize Tripo meshes, retain modular sources, assemble exact campus, export Unity data."""
import bpy,bmesh,math,json,runpy
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/campus-v04';ENG=OUT/'engine';ENG.mkdir(exist_ok=True)
a=runpy.run_path(str(ROOT/'scripts/campus-v04-build.py'));mat=a['mat'];stats={}
def import_mesh(name,angle):
    path=Path(json.loads((OUT/'jobs'/f'{name}-model.json').read_text())['model_file'])
    bpy.ops.import_scene.gltf(filepath=str(path));obs=[o for o in bpy.context.selected_objects if o.type=='MESH']
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object;o.name='Tripo_'+name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    rotation=Matrix.Rotation(math.radians(angle),4,'Z')
    for v in o.data.vertices:v.co=rotation@v.co
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
    return o

def bounds(o):return Vector([min(v.co[i] for v in o.data.vertices) for i in range(3)]),Vector([max(v.co[i] for v in o.data.vertices) for i in range(3)])
def colorize(o,fn):
    o.data.materials.clear();keys=['paper','field','ink','mint','yellow']
    for key in keys:o.data.materials.append(mat(key))
    for poly in o.data.polygons:poly.material_index=keys.index(fn(poly.center));poly.use_smooth=False

def data_export(name,objects):
    groups={}
    for o in objects:
        o.data.calc_loop_triangles()
        for tri in o.data.loop_triangles:
            material=o.data.materials[tri.material_index];key=material.name.split('.')[0]
            color='#'+a['COL'].get(key,'FFF9EF');g=groups.setdefault(color,{'name':key,'color':color,'vertices':[],'triangles':[]})
            offset=len(g['vertices'])
            for index in tri.vertices:
                p=o.matrix_world@o.data.vertices[index].co
                g['vertices'].append({'x':round(p.x,5),'y':round(p.z,5),'z':round(p.y,5)})
            # XYZ -> XZY is a reflection: flip winding.
            g['triangles'].extend([offset,offset+2,offset+1])
    (ENG/(name+'.json')).write_text(json.dumps({'meshes':list(groups.values())},separators=(',',':')))
    stats[name]={'triangles':sum(len(g['triangles'])//3 for g in groups.values()),'materials':len(groups),'bounds_metres':{}}
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),use_selection=True,export_format='GLB')

# Prepare normalized reusable assets.
a['setup']();bay=import_mesh('bay',-27.34);lo,hi=bounds(bay);dim=hi-lo
for v in bay.data.vertices:v.co=Vector(((v.co.x-(lo.x+hi.x)/2)*7/dim.x,(v.co.y-(lo.y+hi.y)/2)*1.3/dim.y,(v.co.z-lo.z)*4.5/dim.z))
bay.data.update()
def bay_color(p):
    if abs(p.x)<2.55 and .9<p.z<3.15:return 'field'
    if abs(p.x)<2.6 and 3.4<p.z<3.95:return 'mint'
    if abs(p.x)<2.8 and .8<p.z<3.3:return 'ink'
    return 'paper'
colorize(bay,bay_color);data_export('ClubBay',[bay]);baydata=bay.data.copy()
a['render']('asset-club-bay.png',(9,-12,8),(0,0,2.2),10,(1100,1100))

# Export open hand and a locally bent fist from the same paid source.
a['setup']();hand=import_mesh('hand',-10);lo,hi=bounds(hand);dim=hi-lo
for v in hand.data.vertices:v.co=Vector(((v.co.x-(lo.x+hi.x)/2)*4.8/dim.x,(v.co.y-(lo.y+hi.y)/2)*7/dim.y,(v.co.z-(lo.z+hi.z)/2)*.95/dim.z))
hand.data.update();colorize(hand,lambda p:'paper' if p.y< -2.35 else 'yellow')
# API's requested face limit is not assumed to be a strict delivered cap.
triangles=sum(len(p.vertices)-2 for p in hand.data.polygons)
if triangles>4000:
    d=hand.modifiers.new('local mobile face budget','DECIMATE');d.ratio=3900/triangles;bpy.context.view_layer.objects.active=hand;bpy.ops.object.modifier_apply(modifier=d.name)
data_export('HandOpen',[hand]);open_data=hand.data.copy()
a['render']('asset-hand-open.png',(7,-9,12),(0,.3,0),9,(1100,1100))
for v in hand.data.vertices:
    x,y,z=v.co
    if y>.75:
        t=min((y-.75)/2.75,1);theta=t*math.pi*.95;radius=2.75/(math.pi*.95)
        v.co.y=.75+(radius+z)*math.sin(theta);v.co.z=(radius+z)*math.cos(theta)-radius
    elif x< -1.5 and y> -1.8:
        t=min(1,(-x-1.5)/.9);v.co.x=x+1.0*t;v.co.y=y+.25*t;v.co.z=z-1.05*t
hand.data.update();data_export('HandFist',[hand]);fist_data=hand.data.copy()
a['render']('asset-hand-fist.png',(7,9,10),(0,-.5,-.6),7,(1100,1100))

# Open the exact metre-scale layout, replacing only aesthetic bay meshes.
bpy.ops.wm.open_mainfile(filepath=str(OUT/'campus-structure.blend'));a['MATS'].clear()
for m in bpy.data.materials:
    if m.name in a['COL']:a['MATS'][m.name]=m
solids=[]
solid_prefixes=('ground_main','entry_bridge','front_corner','back_wall','wing_wall','deck_','stair_landing','stair_step','upper_rail','rear_rail','column','fixed_cover','tree_planter','tree_trunk','classroom_post','classroom_closed_sill','classroom_floor','classroom_ceiling','classroom_beam','classroom_back','central_building','corner_column')
for o in list(bpy.context.scene.objects):
    if o.type=='MESH' and o.name.startswith(solid_prefixes):
        pts=[o.matrix_world@Vector(c) for c in o.bound_box];lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)]);c=(lo+hi)/2;d=hi-lo
        solids.append({'name':o.name,'center':{'x':c.x,'y':c.z,'z':c.y},'size':{'x':d.x,'y':d.z,'z':d.y}})
    if o.type=='MESH' and o.name.startswith(('box_prop','bin_prop','bin_lid')):bpy.data.objects.remove(o,do_unlink=True)
# Data copies must be reimported because opening a blend replaces all datablocks.
bpy.ops.import_scene.gltf(filepath=str(OUT/'ClubBay.glb'));source=next(o for o in bpy.context.selected_objects if o.type=='MESH');baydata=source.data;source.hide_render=True;source.hide_viewport=True
for name in ['Maker','BoardGames','Music','Photography','Anime','Basketball']:
    root=bpy.data.objects[name]
    for child in list(root.children):
        if child.name.startswith('bay_'):bpy.data.objects.remove(child,do_unlink=True)
        elif child.name.startswith('sign_'):child.location.y=-.8
    new=bpy.data.objects.new('Tripo_ClubBay_'+name,baydata);bpy.context.collection.objects.link(new);new.parent=root
    c=root.matrix_world@Vector((0,0,2.25));size=(1.3,7,4.5) if name not in ['Maker','BoardGames'] else (7,1.3,4.5)
    solids.append({'name':'closed_club_'+name,'center':{'x':c.x,'y':c.z,'z':c.y},'size':{'x':size[0],'y':size[2],'z':size[1]}})
bpy.data.objects.remove(source,do_unlink=True)
# Keep generated reusable modules, icons and precise infrastructure separate in Blender.
bpy.context.view_layer.update();objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];data_export('Campus',objects)
(ENG/'Collision.json').write_text(json.dumps({'solids':solids},separators=(',',':')))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Remember-About-Me-Campus-V04.blend'))
a['render']('map-final-entrance.png',(0,-78,18),(0,5,3.2),None,(1920,1080))
a['render']('map-final-overview.png',(68,-86,92),(0,1,0),132,(1920,1080))
(OUT/'mesh-statistics.json').write_text(json.dumps(stats,indent=2));print('CAMPUS_ASSEMBLED',stats)
