"""Run with Blender. Creates exact scene geometry and generation references.
World coordinates use Blender X/Y ground, Z up; all dimensions are metres.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/campus-v04'
REF=OUT/'references'
REF.mkdir(parents=True,exist_ok=True)
COL={'paper':'FFF9EF','cream':'F5EFE3','field':'ECECE5','ink':'202A3D','mint':'79CFC5','pink':'F3AFC2','yellow':'FFD773','blue':'9FAFF2','orange':'F49A6B','green':'78BE8C','red':'E77770','lilac':'C9AFDA','trunk':'9C826D','box':'CEAE85','room':'CAD5D1'}
MATS={}
def mat(name):
    if name in MATS:return MATS[name]
    h=COL.get(name,name); rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    lin=[c/12.92 if c<.04045 else ((c+.055)/1.055)**2.4 for c in rgb]
    m=bpy.data.materials.new(name);m.diffuse_color=(*lin,1);m.use_nodes=True
    m.node_tree.nodes.clear();bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs[0],out.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*lin,1);bs.inputs['Roughness'].default_value=.9
    MATS[name]=m;return m
def box(name,p,s,color='paper',parent=None,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.dimensions=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat(color))
    if parent:o.parent=parent
    if bevel:
        b=o.modifiers.new('one segment edge','BEVEL');b.width=bevel;b.segments=1
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name)
    return o
def cyl(name,p,r,d,color='paper',vertices=6,parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d,location=p);o=bpy.context.object;o.name=name;o.data.materials.append(mat(color))
    if parent:o.parent=parent
    return o
def empty(name,p=(0,0,0),rot=0):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=p;o.rotation_euler.z=rot;return o
def icon(kind,parent,color='paper'):
    # Front faces toward -Y; icons are 3D geometry, not generated text.
    def b(n,x,z,w,h):return box('icon_'+n,(x,-.32,z),(w,.1,h),color,parent)
    def disc(n,x,z,r):
        o=cyl('icon_'+n,(x,-.34,z),r,.12,color,12,parent);o.rotation_euler.x=math.pi/2;return o
    if kind=='music':
        b('stem',.2,.12,.17,1.25);b('beam',.52,.7,.8,.18);disc('note',-.03,-.47,.3)
    elif kind=='camera':
        b('camera',0,0,1.55,.95);b('prism',-.28,.57,.6,.22)
        o=disc('lens',0,0,.34);o.data.materials.clear();o.data.materials.append(mat('ink'))
        o=disc('glass',0,-.01,.22);o.location.y=-.42
    elif kind=='maker':
        disc('gear',0,0,.52)
        for i in range(8):
            a=i*math.pi/4;o=b('tooth',.57*math.cos(a),.57*math.sin(a),.3,.32);o.rotation_euler.y=-a
        o=disc('hole',0,0,.22);o.location.y=-.43;o.data.materials.clear();o.data.materials.append(mat('ink'))
    elif kind=='anime':
        verts=[(0,-.36,0)]+[(math.sin(i*math.pi/5)*(.78 if i%2==0 else .34),-.36,math.cos(i*math.pi/5)*(.78 if i%2==0 else .34)) for i in range(10)]
        mesh=bpy.data.meshes.new('star');mesh.from_pydata(verts,[],[(0,i+1,(i+1)%10+1) for i in range(10)]);mesh.update();o=bpy.data.objects.new('icon_star',mesh);bpy.context.collection.objects.link(o);o.parent=parent;o.data.materials.append(mat(color))
    elif kind=='basketball':
        disc('ball',0,0,.71)
        for x,z,w,h in [(0,0,.1,1.4),(0,0,1.4,.1),(-.4,0,.08,1.15),(.4,0,.08,1.15)]:
            o=b('seam',x,z,w,h);o.location.y=-.43;o.data.materials.clear();o.data.materials.append(mat('ink'))
    elif kind=='dice':
        b('dice',0,0,1.3,1.3)
        for x,z in [(-.38,-.38),(.38,-.38),(0,0),(-.38,.38),(.38,.38)]:
            o=disc('pip',x,z,.11);o.location.y=-.43;o.data.materials.clear();o.data.materials.append(mat('ink'))

def bay(name,p,rot,color='mint',symbol=None):
    root=empty(name,p,rot)
    box('bay_back',(0,.65,2.1),(7,.25,4.2),'field',root)
    for x in [-3.25,3.25]:box('bay_pillar',(x,0,2.25),(.5,1.3,4.5),'paper',root, .04)
    box('bay_header',(0,0,4.2),(7,1.3,.6),'paper',root,.04)
    box('bay_sill',(0,0,.35),(6,1.2,.7),'paper',root,.03)
    box('bay_inside',(0,.4,2.2),(5.9,.12,2.9),color,root)
    for x in [-2.95,2.95]:box('bay_inner_frame',(x,-.12,2.2),(.12,.16,2.85),'ink',root)
    box('bay_top_frame',(0,-.12,3.6),(6,.16,.12),'ink',root)
    box('bay_closed_bar',(0,-.23,1.04),(6,.18,.18),'ink',root)
    if symbol:
        sign=empty('sign_'+symbol);sign.parent=root;sign.location=(0,-.3,2.45)
        box('sign_border',(0,-.11,0),(2.25,.18,2.15),'ink',sign,.12)
        box('sign_fill',(0,-.23,0),(2.04,.08,1.94),color,sign,.08);icon(symbol,sign)
    return root

def setup():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    s=bpy.context.scene;s.render.engine='BLENDER_EEVEE';s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.world.color=(.8,.8,.8);s.world.use_nodes=True
    s.world.node_tree.nodes['Background'].inputs[0].default_value=(.78,.8,.85,1)
    s.world.node_tree.nodes['Background'].inputs[1].default_value=.55
    s.view_settings.view_transform='Standard';s.view_settings.look='None'
    bpy.ops.object.light_add(type='SUN',location=(0,-20,50));sun=bpy.context.object;sun.rotation_euler=(math.radians(25),math.radians(-20),math.radians(-25));sun.data.energy=1.3;sun.data.angle=.25
    bpy.ops.object.camera_add();s.camera=bpy.context.object;s.camera.data.clip_end=500

def render(name,pos,target,ortho=None,size=(1536,864)):
    s=bpy.context.scene;c=s.camera;c.location=pos;c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
    c.data.type='ORTHO' if ortho else 'PERSP';c.data.ortho_scale=ortho or 90;c.data.lens=30
    s.render.resolution_x,s.render.resolution_y=size;s.render.filepath=str(REF/name);bpy.ops.render.render(write_still=True)

def campus():
    setup()
    box('ground_main',(0,3.5,-.45),(80,57,.9),'cream')
    box('entry_bridge',(0,-28.5,-.45),(20,7,.9),'cream')
    for x in [-35.5,35.5]:box('front_corner',(x,-28.5,-.45),(9,7,.9),'cream')
    box('central_plaza',(0,1,.015),(36,30,.03),'field')
    cyl('hand_warning_flat',(0,1,.04),5,.025,'yellow',6)
    # Shallow classroom shells: actual posts, beams, recessed back walls and roof volumes.
    # Only exterior depth is built; the opening's lower wall prevents room entry.
    box('back_wall',(0,28,4.5),(72,.5,9),'paper')
    for side in [-1,1]:box('wing_wall',(side*35,3,4.5),(.5,50,9),'paper')
    def classroom(name,position,rotation=0,width=8):
        root=empty(name,position,rotation)
        # local front is -Y; back wall is 2.7 metres behind the colonnade.
        for x in [-width/2+.3,width/2-.3]:
            box('classroom_post',(x,0,2.15),(.6,.85,4.3),'paper',root,.025)
        box('classroom_beam',(0,0,4.12),(width,1,.55),'paper',root)
        box('classroom_ceiling',(0,1.25,4.4),(width,3.5,.4),'paper',root)
        box('classroom_floor',(0,1.25,.08),(width,3.5,.16),'cream',root)
        box('classroom_back',(0,2.65,2.1),(width-.5,.2,4.15),'room',root)
        box('classroom_closed_sill',(0,0,.55),(width-.6,.5,1.1),'paper',root)
        # Door plus broad classroom window sits behind the structural frame.
        box('classroom_door_frame',(-width*.32,2.45,1.65),(1.5,.16,3.05),'ink',root)
        box('classroom_door',(-width*.32,2.33,1.65),(1.27,.12,2.85),'mint',root)
        box('classroom_door_glass',(-width*.32,2.25,2.1),(.94,.07,1.1),'blue',root)
        box('classroom_window_frame',(.9,2.45,2.25),(3.6,.16,2.0),'ink',root)
        box('classroom_window',(.9,2.32,2.25),(3.35,.12,1.76),'blue',root)
        for x in [-.15,.95,2.05]:box('classroom_mullion',(x,2.20,2.25),(.10,.08,1.8),'paper',root)
        box('classroom_transom',(.9,2.19,2.55),(3.4,.08,.1),'paper',root)
    for z in [0,4.5]:
        for x in [-28,-20,-12,12,20,28]:classroom('RearClassroom',(x,25,z))
        for side in [-1,1]:
            for y in [-14,-6,2,10,18]:classroom('WingClassroom',(side*31.8,y,z),-side*math.pi/2)
    # Thick continuous slabs and caps make the silhouette read as a two-storey campus.
    for z in [4.25,8.85]:
        box('rear_facade_band',(0,24.7,z),(72,.75,.55),'paper')
        box('rear_color_band',(0,24.29,z+.23),(72,.08,.18),'mint')
        for side in [-1,1]:
            box('wing_facade_band',(side*31.5,2,z),(.75,48,.55),'paper')
            box('wing_color_band',(side*31.09,2,z+.23),(.08,48,.18),'mint')
    box('rear_roof',(0,26.75,9.2),(73,5,.65),'paper')
    box('rear_parapet',(0,28.65,9.65),(73,.4,.5),'mint')
    for side in [-1,1]:
        box('wing_roof',(side*33,2,9.2),(5,49,.65),'paper')
        box('wing_parapet',(side*35.2,2,9.65),(.4,49,.5),'mint')
        for y in [-21,23]:
            box('corner_column',(side*31.3,y,4.4),(.85,.85,8.8),'paper')
            box('corner_cap',(side*31.3,y,8.6),(.95,.95,.55),'yellow')
    box('central_building',(0,26.5,5.2),(9,3,10.4),'paper')
    box('central_mint_panel',(0,24.8,6.25),(5.4,.5,7.9),'mint')
    for x in [-3.7,3.7]:
        box('central_pilaster',(x,24.8,5.2),(.7,.7,10.4),'paper')
        box('campus_banner',(x,24.37,6.6),(.65,.1,4.7),'yellow')
    crest=cyl('hex_crest',(0,24.4,8.5),1.25,.18,'paper',6);crest.rotation_euler.x=math.pi/2
    box('central_entry_lintel',(0,24.5,3.25),(6,.8,.6),'paper')
    box('central_entry_closed',(0,24.9,1.5),(5.5,.3,3),'blue')
    # Precise upper surfaces: walkable U, with bridges to stair tops.
    box('deck_rear',(0,22.25,4.25),(60,4.5,.5),'paper')
    for x in [-27.75,27.75]:box('deck_wing',(x,3,4.25),(4.5,42,.5),'paper')
    for side in [-1,1]:
        box('stair_landing',(side*24.5,-8.2,4.25),(11,4,.5),'paper')
        for i in range(18):
            top=(i+1)*.25;y=-18+(i+.5)*(7.8/18)
            box('stair_step',(side*21,y,top/2),(4,7.8/18,top),'paper')
            box('stair_edge',(side*21,y-(7.8/36)+.04,top+.008),(4,.07,.016),'mint')
        # rail in alternating safe sections leaves purposeful push/fall gaps.
        for y in [-15,-3,9,19]:
            box('upper_rail',(side*25.45,y,5.13),(.18,6,1.25),'paper')
            box('upper_rail_cap',(side*25.45,y,5.79),(.26,6,.16),'ink')
        for y in [-17,-7,3,13,23]:box('column',(side*28.8,y,2),(.55,.55,4),'paper',bevel=.025)
    for x in [-22,-8,8,22]:
        box('rear_rail',(x,19.85,5.13),(9,.18,1.25),'paper')
        box('rear_rail_cap',(x,19.85,5.79),(9,.26,.16),'ink')
    # Six clubs: two on ground, four upstairs, facing the plaza.
    for args in [('Maker',(-31,-19,0),0,'yellow','maker'),('BoardGames',(31,-19,0),0,'green','dice'),('Music',(-31.4,-14,4.5),math.pi/2,'red','music'),('Photography',(-31.4,10,4.5),math.pi/2,'blue','camera'),('Anime',(31.4,10,4.5),-math.pi/2,'lilac','anime'),('Basketball',(31.4,-14,4.5),-math.pi/2,'orange','basketball')]:bay(*args)
    covers=[(-12,-7,3.5,1.4,1.2),(12,6,3.5,1.4,1.2),(0,13.5,4,1.4,1.2),(-13,9,3.5,1.6,1.2),(13,-9,3.5,1.6,1.2)]
    for i,(x,y,w,d,h) in enumerate(covers):
        o=box('fixed_cover_'+str(i),(x,y,h/2),(w,d,h),['mint','lilac','pink','yellow','blue'][i])
        if i>=3:
            for v in o.data.vertices:
                if v.co.z>0:v.co.x*=.7
    for x,y in [(-22,15),(22,15),(-36,-21),(36,-21)]:
        cyl('tree_planter',(x,y,.55),1.8,1.1,'paper',6)
        box('tree_trunk',(x,y,2),(.45,.45,2.6),'trunk')
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=2,location=(x,y,3.8));bpy.context.object.name='fixed_geometric_tree';bpy.context.object.data.materials.append(mat('green'))
    for i,(x,y) in enumerate([(-18,-18),(18,-18),(-23,8),(23,8)]):box('box_prop_'+str(i),(x,y,.5),(1,1,1),'box')
    for i,x in enumerate([-17,17]):
        box('bin_prop_'+str(i),(x,-20,.7),(.9,.9,1.4),'mint',bevel=.08)
        box('bin_lid_'+str(i),(x,-20,1.4),(1,1,.12),'ink')
    for x in [-6,0,6]:cyl('return_marker',(x,-22,.025),1.1,.03,'mint',6)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'campus-structure.blend'))
    bpy.ops.export_scene.gltf(filepath=str(OUT/'campus-structure.glb'),export_format='GLB',export_cameras=False,export_lights=False)
    render('blockout-entrance.png',(0,-78,18),(0,5,3.2))
    render('blockout-overview.png',(68,-86,92),(0,1,0),132)
    render('blockout-top.png',(0,0,120),(0,0,0),88,(1408,1126))
    return covers

def module_ref():
    setup();bay('CampusBay',(0,0,0),0)
    render('blockout-bay.png',(9,-12,8),(0,0,2.2),10,(1024,1024))

def hand_ref():
    setup();box('palm',(0,0,0),(3.3,3.6,.85),'yellow',bevel=.22)
    for i,(x,l) in enumerate([(-1.17,2.6),(-.39,3.0),(.39,2.8),(1.17,2.3)]):
        box('finger_'+str(i),(x,1.6+l/2,0),(.66,l,.72),'yellow',bevel=.16)
    thumb=box('thumb',(-2,0.6,0),(1.6,.9,.75),'yellow',bevel=.2);thumb.rotation_euler.z=.45
    box('wrist',(0,-2.35,0),(2.3,1.3,.8),'paper',bevel=.15)
    render('blockout-hand.png',(7,-9,12),(0,1,0),9,(1024,1024))

if __name__=='__main__':
    covers=campus();module_ref();hand_ref()
    (OUT/'layout.json').write_text(json.dumps({'units':'metres','blender_axes':'X lateral, Y depth, Z up','width':80,'depth':64,'plaza':[36,30],'upper_height':4.5,'walkway_width':4.5,'stairs':{'width':4,'run':7.8,'start_y':-18,'centres_x':[-21,21]},'covers':covers},indent=2))
    print('CAMPUS_REFERENCES_READY')
