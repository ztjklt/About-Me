"""Author DS accessories around the exported, unchanged campus doll. Blender 4.x/5.x.
Coordinates in the interchange JSON are Unity metres (Y up, Z forward).
No external generation, textures, rigging or paid API calls are used by this fallback.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]; OUT=ROOT/'artifacts/ds-girl-modular'; ART=ROOT/'client/Assets/AboutMe/Art/DSGirlModular'
ART.mkdir(parents=True,exist_ok=True)
pi=math.pi
COL={'hair':'#354D91','tip':'#70ACD1','navy':'#263765','blue':'#3F649B','white':'#F6F3E9','gold':'#D1B477','shadow':'#D5DCE8'}
def rgb(c):
 c=COL.get(c,c).lstrip('#');return tuple(int(c[i:i+2],16)/255 for i in (0,2,4))+(1.,)
def mix(a,b,t):return tuple(a[i]*(1-t)+b[i]*t for i in range(4))
def color_hair(p):return mix(rgb('hair'),rgb('tip'),max(0,min(1,(.55-p[1])/.9))*.90)
def vjson(v):return dict(zip(('x','y','z'),v))
helpers=[('DS_HairBack','Head',(0,.86,-.46)),('DS_HairLeft','Head',(-.46,.65,.07)),('DS_HairRight','Head',(.46,.65,.07)),
 ('DS_SkirtFront','Hips',(0,-.32,0)),('DS_SkirtBack','Hips',(0,-.32,0)),('DS_SkirtLeft','Hips',(0,-.32,0)),('DS_SkirtRight','Hips',(0,-.32,0)),('DS_Tail','Hips',(0,-.37,-.31))]
parts=[]
def constant(b):return lambda p:{b:1.}
def hairw(b):
 return lambda p:{'Head':1-max(0,min(1,(.9-p[1])/.9)),b:max(0,min(1,(.9-p[1])/.9))}
def skirtw(p):
 x,y,z=p;t=max(0,min(1,(-y-.32)/.4));b='DS_SkirtFront' if abs(z/.48)>=abs(x/.64) and z>=0 else 'DS_SkirtBack' if abs(z/.48)>=abs(x/.64) else 'DS_SkirtRight' if x>0 else 'DS_SkirtLeft'
 return {'Hips':1-t*.88,b:t*.88}
def torso_w(p):
 t=max(0,min(1,(p[1]+.15)/.45));return {'Torso':1-t*.65,'Chest':t*.65}
def add(name,verts,faces,col,bone):
 fn=constant(bone) if isinstance(bone,str) else bone
 cs=[col(p) if callable(col) else rgb(col) for p in verts]
 ws=[{b:w for b,w in fn(p).items() if w>1e-8} for p in verts]
 parts.append(dict(name=name,verts=[list(p) for p in verts],faces=faces,colors=cs,weights=ws))
def ellipsoid(name,center,size,col,bone,rings=5,seg=10):
 vs=[];fs=[]
 for r in range(rings+1):
  a=pi*r/rings
  for s in range(seg):
   q=2*pi*s/seg;vs.append((center[0]+size[0]*math.sin(a)*math.cos(q),center[1]+size[1]*math.cos(a),center[2]+size[2]*math.sin(a)*math.sin(q)))
 for r in range(rings):
  for s in range(seg):
   a=r*seg+s;b=r*seg+(s+1)%seg;c=a+seg;d=b+seg
   if r>0:fs.append([a,c,b])
   if r<rings-1:fs.append([b,c,d])
 add(name,vs,fs,col,bone)
def path(name,points,radii,col,bone,seg=8,flat=1):
 # Smooth Catmull-Rom centreline; flattened cross section gives broad sculpted locks.
 pp=[Vector(p) for p in points];vs=[];fs=[];count=(len(pp)-1)*4+1
 for k in range(count):
  u=k/(count-1)*(len(pp)-1);j=min(len(pp)-2,int(u));t=u-j
  p0=pp[max(0,j-1)];p1=pp[j];p2=pp[j+1];p3=pp[min(len(pp)-1,j+2)]
  p=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
  tangent=((-p0+p2)+2*(2*p0-5*p1+4*p2-p3)*t+3*(-p0+3*p1-3*p2+p3)*t*t).normalized()
  ref=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
  side=tangent.cross(ref).normalized();other=side.cross(tangent).normalized();rr=radii[j]*(1-t)+radii[j+1]*t
  for s in range(seg):
   a=s/seg*2*pi;vs.append(tuple(p+side*math.cos(a)*rr+other*math.sin(a)*rr*flat))
 for k in range(count-1):
  for s in range(seg):
   a=k*seg+s;b=k*seg+(s+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,b,c],[b,d,c]])
 fs.extend([[0,s+1,s] for s in range(1,seg-1)]);off=(count-1)*seg;fs.extend([[off,off+s,off+s+1] for s in range(1,seg-1)])
 add(name,vs,fs,col,bone)
def ribbon(name,c,w,h,col,bone,back=False):
 z=c[2];sgn=-1 if back else 1
 for side in [-1,1]:
  ellipsoid(name+' loop', (c[0]+side*w*.29,c[1]+.01,z), (w*.32,h*.5,.035),col,bone,4,8)
  vs=[(c[0]+side*w*.06,c[1]-.015,z+.006*sgn),(c[0]+side*w*.28,c[1]-.05,z+.012*sgn),(c[0]+side*w*.29,c[1]-h*.95,z),(c[0]+side*w*.16,c[1]-h*.8,z),(c[0]+side*w*.07,c[1]-h*.93,z)]
  add(name+' ribbon',vs,[[0,1,2],[0,2,3],[0,3,4]],col,bone)
 ellipsoid(name+' knot',c,(w*.11,h*.25,.044),col,bone,4,8)
# Scalp shell, forehead intentionally open; original facial geometry is kept.
vs=[];fs=[];seg=32;rings=8
for r in range(rings+1):
 for s in range(seg):
  ph=s*2*pi/seg;front=max(0,math.cos(ph));theta=(2.42-1.18*front)*r/rings
  vs.append((.511*math.sin(theta)*math.sin(ph),.88+.522*math.cos(theta),.477*math.sin(theta)*math.cos(ph)-.016))
for r in range(rings):
 for s in range(seg):
  a=r*seg+s;b=r*seg+(s+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,b,c],[b,d,c]])
add('Crown open face',vs,fs,color_hair,'Head')
# Swept bangs in five clearly modelled clumps.
for i,x in enumerate([-.34,-.19,0,.19,.34]):
 tip_y=[.89,1.00,.965,1.01,.88][i]; tip_x=x+(.055 if x<0 else -.035)
 path('Fringe '+str(i),[(x*.5,1.35,.27),(x,1.24,.41),(x*1.04,1.12,.464),(tip_x,tip_y,.448)], [.08,.125,.102,.008], 'hair','Head',seg=8,flat=.40)
# Long back locks leave the waist bow visible.
for i,x in enumerate([-.39,-.22,0,.22,.39]):
 side=-1 if x<0 else 1;bottom=-.10+abs(x)*.24
 path('Back wave '+str(i),[(x*.85,1.30,-.30),(x*1.18,.78,-.49),(x*1.20,.38,-.53),(x+side*.105,.10,-.54),(x+side*.04,bottom,-.50),(x+side*.16,bottom+.07,-.49)], [.018,.165,.17,.14,.095,.006],color_hair,hairw('DS_HairBack'),seg=8,flat=.46)
for side in [-1,1]:
 b='DS_HairLeft' if side<0 else 'DS_HairRight'
 path('Face curl',[(side*.43,1.04,.23),(side*.48,.72,.30),(side*.49,.44,.36),(side*.55,.21,.36),(side*.48,.08,.39),(side*.41,.17,.4)], [.13,.13,.10,.09,.058,.006],color_hair,hairw(b),seg=8,flat=.66)
 path('Outer wave',[(side*.48,1.02,-.06),(side*.53,.60,-.22),(side*.58,.33,-.36),(side*.68,.09,-.39),(side*.58,-.11,-.37),(side*.49,-.03,-.34)], [.025,.13,.13,.12,.08,.008],color_hair,hairw(b),seg=8,flat=.60)
 path('Whale fin sidelock',[(side*.44,.90,.01),(side*.58,.69,.03),(side*.77,.63,.02),(side*.83,.69,.01)], [.12,.14,.08,.002], 'hair', 'Head',seg=8,flat=.42)
 ribbon('Blue hair bow',(side*.49,1.065,.385),.23,.14,'blue','Head')
# Curved ahoge.
path('Ahoge',[(0,1.37,-.03),(-.12,1.57,-.02),(-.06,1.68,-.015),(.09,1.68,-.01),(.16,1.58,.00)], [.044,.05,.047,.032,.002],'hair','Head',seg=7,flat=.65)
# Ruffled maid headband arches over the crown in the X/Y plane.
for i in range(13):
 a=pi*(.06+.88*i/12);p=(.535*math.cos(a),.89+.565*math.sin(a),-.055)
 ellipsoid('Headband pleat',p,(.076,.068,.09),'white','Head',4,8)
path('Headband piping',[(.54,.99,.004),(.40,1.30,.004),(0,1.47,.004),(-.40,1.30,.004),(-.54,.99,.004)], [.023]*5,'shadow','Head',seg=6)
# Bell skirt with broad pleats, a hollow open shell and a thin inner hem.
seg=40;rings=7;vs=[];fs=[]
def radii(t):return (.38+.26*t,.295+.17*t)
for r in range(rings):
 t=r/(rings-1);rx,rz=radii(t)
 for s in range(seg):
  a=s*2*pi/seg;pleat=1+.026*t*math.cos(10*a);vs.append((rx*math.sin(a)*pleat,-.31-.43*t,rz*math.cos(a)*pleat))
for r in range(rings-1):
 for s in range(seg):
  a=r*seg+s;b=r*seg+(s+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,c,b],[b,c,d]])
add('Bell skirt shell',vs,fs,lambda p:mix(rgb('navy'),rgb('blue'),max(0,min(.50,(-p[1]-.32)*.7))),skirtw)
def skirtband(name,t0,t1,col,waves=0):
 vs=[];fs=[]
 for t in [t0,t1]:
  rx,rz=radii(t);rx+=.009;rz+=.009
  for s in range(81):
   a=s/80*2*pi;f=1+.026*t*math.cos(10*a)+waves*math.cos(40*a)
   vs.append((rx*f*math.sin(a),-.31-.43*t,rz*f*math.cos(a)))
 for i in range(80):fs.extend([[i,i+81,i+1],[i+1,i+81,i+82]])
 add(name,vs,fs,col,skirtw)
skirtband('Gold hem',.875,.90,'gold');skirtband('Lower blue edge',.96,1.01,'navy');skirtband('White pleated petticoat',1.005,1.14,'white',.018)
skirtband('Petticoat lower edge',1.12,1.145,'shadow',.018)
# Apron front, shaped to the actual skirt rather than a flat floating plane.
vs=[];fs=[];nx=12;ny=7
for j in range(ny):
 t=j/(ny-1);y=-.285-.405*t;half=.255+.09*math.sin(t*pi*.85)
 for i in range(nx+1):
  u=i/nx*2-1;x=u*half;yy=y+.095*abs(u)**3*t**3;tt=max(0,(-yy-.31)/.43);rx,rz=radii(tt);z=rz*math.sqrt(max(.01,1-(x/rx)**2))+.034
  vs.append((x,yy,z))
for j in range(ny-1):
 for i in range(nx):
  a=j*(nx+1)+i;fs.extend([[a,a+nx+1,a+1],[a+1,a+nx+1,a+nx+2]])
add('White rounded apron',vs,fs,'white',skirtw)
for side in [-1,1]:
 points=[]
 for j in range(6):
  t=j/5;y=-.285-.405*t+.095*t**3;x=side*(.255+.09*math.sin(t*pi*.85));rx,rz=radii(max(0,(-y-.31)/.43));points.append((x,y,rz*math.sqrt(max(.01,1-(x/rx)**2))+.046))
 path('Apron ruffle sides',points,[.027]*6,'white',skirtw,seg=6)
for i in range(11):
 x=(i/10*2-1)*.28;y=-.69+.06*(abs(x)/.28)**3;t=(-y-.31)/.43;rx,rz=radii(t)
 ellipsoid('Apron scallop',(x,y-.019,rz*math.sqrt(max(.01,1-(x/rx)**2))+.045),(.037,.033,.021),'white',skirtw,4,7)
# White chest panel and buttons; the body and sleeves underneath remain the original doll.
vs=[];fs=[]
for j in range(6):
 y=.34-j*.104;w=.235 if j<4 else .21
 for i in range(9):
  x=(i/8*2-1)*w;z=.324+.013*math.cos(i*pi/2);vs.append((x,y,z))
for j in range(5):
 for i in range(8):
  a=j*9+i;fs.extend([[a,a+9,a+1],[a+1,a+9,a+10]])
add('Pleated white bib',vs,fs,'white',torso_w)
for side in [-1,1]:
 path('Bib lace',[(side*.24,.33,.326),(side*.255,.12,.326),(side*.23,-.17,.326)],[.028,.03,.025],'white',torso_w,seg=6)
for y in [.14,.02,-.10]:ellipsoid('Bib button',(0,y,.36),(.026,.026,.014),'navy',torso_w,4,8)
ribbon('Collar bow',(0,.31,.373),.28,.14,'navy',torso_w)
ellipsoid('Blue brooch',(0,.325,.421),(.04,.045,.014),'tip',torso_w,4,8)
# Waist ribbon and back bow.
for x in [-.24,.24]:
 for y in [-.22,-.30]:ellipsoid('Gold waist button',(x,y,.325),(.02,.02,.015),'gold','Hips',4,7)
ribbon('Back apron bow',(0,-.30,-.36),.66,.25,'white','Hips',True)
# Whale insignia is actual small relief geometry, readable with no texture.
ellipsoid('Whale applique',(-.025,-.55,.465),(.088,.054,.012),'blue',skirtw,5,12)
path('Whale tail',[(.015,-.56,.474),(.08,-.545,.475),(.10,-.50,.475)],[.026,.019,.009],'blue',skirtw,seg=6)
for side in [-1,1]:path('Whale fluke',[(.10,-.51,.477),(.10+side*.036,-.475,.477),(.10+side*.048,-.457,.477)],[.012,.018,.001],'blue',skirtw,seg=6)
ellipsoid('Whale eye',(-.07,-.538,.48),(.008,.008,.005),'white',skirtw,3,6)
for i in [-1,0,1]:path('Whale spout', [(-.06,-.484,.478),(-.06+i*.022,-.448,.478),(-.06+i*.032,-.446,.478)],[.006,.007,.001],'blue',skirtw,seg=5)
# Tail curves sideways so it reads from front and back without extending the hitbox.
path('Whale tail appendage',[(0,-.36,-.31),(.30,-.52,-.48),(.66,-.62,-.46),(.82,-.44,-.41),(.85,-.28,-.40)], [.12,.14,.13,.09,.045], 'navy','DS_Tail',seg=10,flat=.72)
for side in [-1,1]:path('Tail fluke',[(.85,-.3,-.40),(.85+side*.11,-.19,-.40),(.85+side*.17,-.09,-.405)], [.055,.095,.003],'hair','DS_Tail',seg=8,flat=.38)
# Clearance to the measured original head centre (0.91, not the old 0.88 proxy).
for part in parts:
 if all(all(b=='Head' or b.startswith('DS_Hair') for b in w) for w in part['weights']):
  for v in part['verts']:v[1]+=.03
# Fit scalp/fringe to the real exported head, whose cheeks/crown are not a sphere.
if (OUT/'ds-base-neutral.json').exists():
 from mathutils.bvhtree import BVHTree
 d=json.loads((OUT/'ds-base-neutral.json').read_text());head_index=next(i for i,b in enumerate(d['bones']) if b['name']=='Head')
 head_faces=[];vv=[Vector(tuple(v[k] for k in ('x','y','z'))) for v in d['vertices']]
 def head_vertex(i):
  w=d['weights'][i];return any(w.get('m_BoneIndex'+str(j))==head_index and w.get('m_Weight'+str(j),0)>.9 for j in range(4))
 for i in range(0,len(d['triangles']),3):
  f=d['triangles'][i:i+3]
  if all(head_vertex(j) for j in f):head_faces.append(f)
 tree=BVHTree.FromPolygons(vv,head_faces,all_triangles=True);center=Vector((0,.91,0))
 for part in parts:
  if not (part['name'].startswith('Crown') or part['name'].startswith('Fringe')):continue
  for i,v in enumerate(part['verts']):
   delta=Vector(v)-center;direction=delta.normalized();hit=tree.ray_cast(center+direction*2,-direction,2)
   if hit[0] is not None:
    radius=(hit[0]-center).length+.027
    if delta.length<radius:part['verts'][i]=list(center+direction*radius)
# Expose white socks while keeping the waist and original legs in exactly the same place.
for part in parts:
 if any(any(b.startswith('DS_Skirt') for b in w) for w in part['weights']):
  for v in part['verts']:
   if v[1]<-.31:v[1]=-.31+(v[1]+.31)*.78
# Build Blender source: keep every named element editable; use one vertex-colour material.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('DS Toy vertex colours');mat.use_nodes=True
nodes=mat.node_tree.nodes;bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None) or nodes.new('ShaderNodeBsdfPrincipled');out=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None) or nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(bsdf.outputs['BSDF'],out.inputs['Surface']);col=nodes.new('ShaderNodeVertexColor');col.layer_name='Color';mat.node_tree.links.new(col.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.63
base=json.loads((OUT/'base-neutral.json').read_text());names=[b['name'] for b in base['bones']]+[h[0] for h in helpers];positions={b['name']:tuple(b['position'][k] for k in ['x','y','z']) for b in base['bones']};positions.update({h[0]:h[2] for h in helpers})
arm=bpy.data.armatures.new('Original toy + cosmetic bones');rig=bpy.data.objects.new('DS_Fitting_Rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def cv(p):return (p[0],-p[2],p[1])
for name in names:
 b=arm.edit_bones.new(name);b.head=cv(positions[name]);b.tail=b.head+Vector((0,0,.08))
for b in base['bones']:
 if b['parent'] in arm.edit_bones:arm.edit_bones[b['name']].parent=arm.edit_bones[b['parent']]
for name,parent,p in helpers:arm.edit_bones[name].parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
def object_mesh(name,verts,faces,colors,weights):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata([cv(p) for p in verts],[],[list(reversed(f)) for f in faces]);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);mesh.materials.append(mat)
 color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
 for i,c in enumerate(colors):color.data[i].color=c
 for poly in mesh.polygons:poly.use_smooth=True
 groups={n:o.vertex_groups.new(name=n) for n in set(b for ws in weights for b in ws)}
 for i,ws in enumerate(weights):
  for b,w in ws.items():groups[b].add([i],w,'REPLACE')
 mod=o.modifiers.new('Original rig binding','ARMATURE');mod.object=rig;o.parent=rig
 return o
allv=[];allt=[];allc=[];allw=[];regions=[]
for p in parts:
 o=object_mesh(p['name'],p['verts'],p['faces'],p['colors'],p['weights']);offset=len(allv);allv+=p['verts'];allc+=p['colors'];allw+=p['weights'];allt += [i+offset for f in p['faces'] for i in f];regions.append({'name':p['name'],'vertexStart':offset,'vertexCount':len(p['verts']),'triangles':len(p['faces'])})
basev=[tuple(v[k] for k in ('x','y','z')) for v in base['vertices']];basec=[tuple(c[k] for k in ('r','g','b','a')) for c in base['colors']]
basew=[]
for w in base['weights']:
 ws={}
 for i in range(4):
  if w.get('m_Weight'+str(i),0)>0:ws[names[w['m_BoneIndex'+str(i)]]]=w['m_Weight'+str(i)]
 basew.append(ws)
body=object_mesh('REFERENCE original neutral doll - do not remodel',basev,[base['triangles'][i:i+3] for i in range(0,len(base['triangles']),3)],basec,basew);body.hide_render=True;body.hide_set(True)
assembled=None
if (OUT/'ds-base-neutral.json').exists():
 d=json.loads((OUT/'ds-base-neutral.json').read_text());ww=[]
 for w in d['weights']:
  ww.append({names[w['m_BoneIndex'+str(i)]]:w['m_Weight'+str(i)] for i in range(4) if w.get('m_Weight'+str(i),0)>0})
 assembled=object_mesh('Original body wearing DS outfit', [tuple(v[k] for k in ('x','y','z')) for v in d['vertices']], [d['triangles'][i:i+3] for i in range(0,len(d['triangles']),3)], [tuple(c[k] for k in ('r','g','b','a')) for c in d['colors']], ww)
weights=[]
for ws in allw:
 vals=list(ws.items());w={}
 for i in range(4):w['boneIndex'+str(i)]=names.index(vals[i][0]) if i<len(vals) else 0;w['weight'+str(i)]=vals[i][1] if i<len(vals) else 0
 weights.append(w)
data={'boneNames':names,'bonePositions':[vjson(positions[n]) for n in names],'helpers':[{'name':n,'parent':p,'position':vjson(v)} for n,p,v in helpers],
 'vertices':[vjson(p) for p in allv],'triangles':allt,'colors':[dict(zip(('r','g','b','a'),c)) for c in allc],'weights':weights,'regions':regions}
assert len(allt)//3<=12000,len(allt)//3
(OUT/'accessories.json').write_text(json.dumps(data,separators=(',',':')))
(OUT/'mesh-statistics.json').write_text(json.dumps({'triangles':len(allt)//3,'vertices':len(allv),'parts':len(parts),'cosmeticBones':len(helpers),'baseBones':len(base['bones']),'textures':0,'tripoCredits':0,'source':'Locally authored Blender fallback after Tripo reachability failure'},indent=2))
# Keep FBX as an editable exchange deliverable; engine import uses the exact Unity-space data.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in bpy.data.objects:
 if o.type=='MESH' and o!=body and o!=assembled:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ART/'DSGirl_Accessories.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'DSGirl_Modular.blend'))
print('DS_MODULAR_ASSETS',len(allt)//3,'triangles',len(allv),'vertices')
