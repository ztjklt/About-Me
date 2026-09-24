"""Author Claude accessories around the exported, unchanged campus doll. Blender 4.x/5.x.
Coordinates in the interchange JSON are Unity metres (Y up, Z forward).
No external generation, textures, rigging or paid API calls are used by this fallback.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]; OUT=ROOT/'artifacts/claude-girl-modular'; ART=ROOT/'client/Assets/AboutMe/Art/ClaudeGirlModular'
ART.mkdir(parents=True,exist_ok=True)
pi=math.pi
COL={'hair':'#D66B30','tip':'#F4B86D','navy':'#302A29','blue':'#AC4928','white':'#F2E5CA','gold':'#C99B59','shadow':'#D2BA96'}
def rgb(c):
 c=COL.get(c,c).lstrip('#');return tuple(int(c[i:i+2],16)/255 for i in (0,2,4))+(1.,)
def mix(a,b,t):return tuple(a[i]*(1-t)+b[i]*t for i in range(4))
def color_hair(p):return mix(rgb('hair'),rgb('tip'),max(0,min(1,(.55-p[1])/.9))*.90)
def vjson(v):return dict(zip(('x','y','z'),v))
helpers=[('Claude_HairBack','Head',(0,.86,-.46)),('Claude_HairLeft','Head',(-.46,.65,.07)),('Claude_HairRight','Head',(.46,.65,.07)),
 ('Claude_SkirtFront','Hips',(0,-.32,0)),('Claude_SkirtBack','Hips',(0,-.32,0)),('Claude_SkirtLeft','Hips',(0,-.32,0)),('Claude_SkirtRight','Hips',(0,-.32,0))]
parts=[]
def constant(b):return lambda p:{b:1.}
def hairw(b):
 return lambda p:{'Head':1-max(0,min(1,(.9-p[1])/.9)),b:max(0,min(1,(.9-p[1])/.9))}
def skirtw(p):
 x,y,z=p;t=max(0,min(1,(-y-.32)/.46))*.88
 sx=abs(x/.665)**3;sz=abs(z/.495)**3;den=max(1e-8,sx+sz)
 return {'Hips':1-t,('Claude_SkirtRight' if x>0 else 'Claude_SkirtLeft'):t*sx/den,('Claude_SkirtFront' if z>=0 else 'Claude_SkirtBack'):t*sz/den}
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

def flower(name,c,r,bone):
 # Twelve pointed orange petals with a lighter inner ray and a gold heart.
 for k in range(12):
  a=2*pi*k/12;u=Vector((math.cos(a),math.sin(a),0));v=Vector((-math.sin(a),math.cos(a),0));center=Vector(c)
  vs=[tuple(center+u*r*.25),tuple(center+u*r*.57+v*r*.17),tuple(center+u*r),tuple(center+u*r*.57-v*r*.17)]
  add(name+' petal',vs,[[0,1,2],[0,2,3]],'hair' if k%2 else 'blue',bone)
  vs=[tuple(center+Vector((0,0,.004))+u*r*.26),tuple(center+Vector((0,0,.004))+u*r*.84),tuple(center+Vector((0,0,.004))+u*r*.48+v*r*.055)]
  add(name+' ray',vs,[[0,1,2]],'tip',bone)
 ellipsoid(name+' centre',(c[0],c[1],c[2]+.008),(r*.26,r*.26,.012),'gold',bone,4,8)
def box(name,c,size,col,bone):
 vs=[(c[0]+x*size[0]/2,c[1]+y*size[1]/2,c[2]+z*size[2]/2) for z in [-1,1] for y in [-1,1] for x in [-1,1]]
 fs=[[0,2,1],[1,2,3],[4,5,6],[5,7,6],[0,1,4],[1,5,4],[2,6,3],[3,6,7],[0,4,2],[2,4,6],[1,3,5],[3,7,5]]
 add(name,vs,fs,col,bone)
# Open scalp shell fitted to the unchanged toy head.
vs=[];fs=[];seg=32;rings=8
for r in range(rings+1):
 for s in range(seg):
  ph=s*2*pi/seg;front=max(0,math.cos(ph));theta=(2.42-1.18*front)*r/rings
  vs.append((.511*math.sin(theta)*math.sin(ph),.88+.522*math.cos(theta),.477*math.sin(theta)*math.cos(ph)-.016))
for r in range(rings):
 for s in range(seg):
  a=r*seg+s;b=r*seg+(s+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,b,c],[b,d,c]])
add('Crown open face',vs,fs,color_hair,'Head')
# Asymmetric swept fringe: orange hair frames the face without covering the eyes.
for i,x in enumerate([-.35,-.20,-.025,.17,.34]):
 end=[.90,1.045,1.03,1.10,.93][i]
 path('Fringe '+str(i),[(x*.35+.05,1.355,.20),(x*.8,1.28,.38),(x,1.15,.463),(x-.035,end,.454)],[.07,.13,.11,.005],'hair','Head',seg=8,flat=.4)
# Flowing long hair is shortened above the feet and held behind the shoulder envelope.
for i,x in enumerate([-.39,-.21,0,.21,.39]):
 side=-1 if x<0 else 1
 path('Long copper back lock '+str(i),[(x*.8,1.30,-.30),(x*1.12,.82,-.5),(x*1.26,.28,-.59),(x*1.28,-.14,-.61),(x+side*.15,-.46+abs(x)*.14,-.62),(x+side*.23,-.38+abs(x)*.14,-.6)],[.014,.17,.16,.15,.075,.004],color_hair,hairw('Claude_HairBack'),seg=8,flat=.44)
for side in [-1,1]:
 bone='Claude_HairLeft' if side<0 else 'Claude_HairRight'
 path('Long side sweep',[(side*.45,1.13,-.06),(side*.50,.68,-.13),(side*.62,.19,-.37),(side*.68,-.23,-.47),(side*.60,-.50,-.48),(side*.49,-.42,-.49)],[.04,.125,.12,.11,.065,.004],color_hair,hairw(bone),seg=8,flat=.45)
 path('Slim face framing lock',[(side*.43,1.05,.24),(side*.46,.75,.32),(side*.49,.41,.37),(side*.52,.03,.35),(side*.49,-.14,.33)],[.09,.078,.058,.045,.004],color_hair,hairw(bone),seg=7,flat=.5)
# Black hair bow, cream streamers and a single large orange flower.
ribbon('Black hair bow',(.43,1.22,.20),.29,.20,'navy','Head')
for x,y in [(.46,.94),(.53,.89)]:
 add('Cream hair ribbon',[(x-.032,1.18,.225),(x+.034,1.18,.225),(x+.045,y,.235),(x-.024,y+.015,.245)],[[0,1,2],[0,2,3]],'white','Head')
flower('Hair sunburst',(.405,1.245,.535),.120,'Head')
# Hollow, pleated dress. Its fitted hem stops above the original feet.
def radii(t):return (.38+.285*t,.295+.20*t)
def surface(t,a,offset=0):
 rx,rz=radii(t);f=1+.026*t*math.cos(12*a)
 return ((rx+offset)*math.sin(a)*f,-.31-.46*t,(rz+offset)*math.cos(a)*f)
seg=48;rings=7;vs=[];fs=[]
for r in range(rings):
 for k in range(seg):vs.append(surface(r/(rings-1),k*2*pi/seg))
for r in range(rings-1):
 for k in range(seg):
  a=r*seg+k;b=r*seg+(k+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,c,b],[b,c,d]])
def dress_color(p):
 x,y,z=p
 return rgb('white' if z>.08 and abs(x)<.40 else 'navy')
add('Cream centre black side skirt',vs,fs,dress_color,skirtw)
def band(name,t0,t1,col,pleat=False):
 vs=[];fs=[];n=72
 for t in [t0,t1]:
  for k in range(n+1):
   x,y,z=surface(t,k*2*pi/n,.008)
   if pleat:y+=.012*math.cos(k*2*pi/n*36)
   vs.append((x,y,z))
 for k in range(n):fs.extend([[k,k+n+1,k+1],[k+1,k+n+1,k+n+2]])
 add(name,vs,fs,col,skirtw)
band('Gold hem border',.90,.917,'gold');band('Ivory hem frill',1.,1.12,'white',True);band('Frill lower warm edge',1.105,1.125,'shadow',True)
def panel(name,a0,a1,t0,t1,col,offset=.020):
 vs=[];fs=[];rows=6;cols=8
 for j in range(rows):
  t=t0+(t1-t0)*j/(rows-1)
  for i in range(cols+1):vs.append(surface(t,a0+(a1-a0)*i/cols,offset))
 for j in range(rows-1):
  for i in range(cols):
   a=j*(cols+1)+i;fs.extend([[a,a+cols+1,a+1],[a+1,a+cols+1,a+cols+2]])
 add(name,vs,fs,col,skirtw)
# White lifted overskirt wings reveal dark panels below; a vivid orange asymmetric sash.
for side in [-1,1]:
 panel('Ivory overskirt wing',side*.66,side*1.46,0,.75,'white',.028)
 panel('Wing gold edge',side*.66,side*1.46,.72,.744,'gold',.032)
panel('Orange front sash',.21,.48,-.01,1.0,'hair',.050)
panel('Sash gold edge',.45,.475,.02,1.0,'gold',.054)
panel('Sash lower gold edge',.21,.48,.962,.985,'gold',.054)
# Small skirt flower rosettes and black bows are part of the weighted garment.
for side in [-1,1]:
 c=surface(.55,side*.70,.06)
 ribbon('Skirt black ribbon',c,.20,.14,'navy',skirtw)
 flower('Skirt sunburst',(c[0],c[1]+.035,c[2]+.04),.075,skirtw)
# Low relief embroidery: a stem and paired leaves repeated around the cream hem.
for a in [-.57,-.35,-.14,.06,.70,-.83,2.5,3.1,3.7]:
 points=[surface(.69+i*.055,a,.014) for i in range(4)]
 path('Hem botanical stem',points,[.0038]*4,'gold',skirtw,seg=4)
 for t in [.75,.83]:
  c=Vector(surface(t,a,.017))
  for sign in [-1,1]:
   tip=Vector(surface(t-.048,a+sign*.032,.017));mid=(c+tip)*.5+Vector((0,.008,.003))
   add('Hem embroidery leaf',[tuple(c),tuple(mid),tuple(tip)],[[0,1,2]],'gold',skirtw)
# Front black corset is weighted to the waist/torso; cream shirt and sleeves use original skin.
vs=[];fs=[];seg=36
for r in range(4):
 t=r/3;y=-.295+t*.225;rx=.385+.040*t;rz=.333+.009*t
 for k in range(seg):
  a=k*2*pi/seg;sn=math.sin(a);co=math.cos(a);vs.append((rx*math.copysign(abs(sn)**.5,sn),y,rz*math.copysign(abs(co)**.5,co)))
for r in range(3):
 for k in range(seg):
  a=r*seg+k;b=r*seg+(k+1)%seg;c=a+seg;d=b+seg;fs.extend([[a,c,b],[b,c,d]])
add('Black fitted waist',vs,fs,'navy',torso_w)
for y in [-.13,-.22]:ellipsoid('Corset gold button',(0,y,.36),(.017,.017,.01),'gold',torso_w,3,8)
# Triangular cape ends remain on the chest, clear of forearms and hands.
for side in [-1,1]:
 vs=[(side*.10,.36,.29),(side*.25,.37,.24),(side*.42,.23,.30),(side*.34,.10,.342),(side*.19,.22,.35)]
 add('Cream shoulder cape',vs,[[0,1,4],[1,2,4],[2,3,4]],'white','Chest')
 path('Cape gold piping',[vs[0],vs[4],vs[3],vs[2]],[.007]*4,'gold','Chest',seg=5)
# Black neck ribbon and stacked ivory blouse ruffles.
ribbon('Black neck ribbon',(0,.28,.388),.21,.24,'navy',torso_w)
for i in range(3):
 y=.15-i*.068;w=.11-i*.016
 add('Blouse folded ruffle',[(-w,y,.36),(0,y+.017,.379),(w,y,.36),(0,y-.065,.388)],[[0,1,3],[1,2,3]],'white',torso_w)
flower('Collar sunburst',(0,.30,.45),.086,torso_w)
# Wrist lacing stays on the original forearm bones; the palm/grip remain untouched.
for side,bone in [(-1,'LeftForearm'),(1,'RightForearm')]:
 x=side*.405
 for i in range(3):
  y=.025-i*.045
  path('Cuff lacing',[(x-.035,y,.175),(x+.035,y-.037,.175)],[.0035,.0035],'navy',bone,seg=4)
  path('Cuff lacing',[(x+.035,y,.175),(x-.035,y-.037,.175)],[.0035,.0035],'navy',bone,seg=4)
# Compact belt book replaces a permanently held object so grab/eat animations remain free.
box('Book ivory pages',(-.45,-.43,-.02),(.16,.27,.075),'white','Hips')
for z in [-.066,.026]:box('Brown book cover',(-.45,-.43,z),(.18,.29,.018),'navy','Hips')
box('Book gold spine',(-.545,-.43,-.02),(.020,.29,.11),'gold','Hips')
flower('Book sunburst',(-.45,-.415,.04),.050,'Hips')
# Final head clearance uses the exact exported original face surface.
# Clearance to the measured original head centre (0.91, not the old 0.88 proxy).
for part in parts:
 if all(all(b=='Head' or b.startswith('Claude_Hair') for b in w) for w in part['weights']):
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
# Build Blender source: keep every named element editable; use one vertex-colour material.
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('Claude Toy vertex colours');mat.use_nodes=True
nodes=mat.node_tree.nodes;bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None) or nodes.new('ShaderNodeBsdfPrincipled');out=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None) or nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(bsdf.outputs['BSDF'],out.inputs['Surface']);col=nodes.new('ShaderNodeVertexColor');col.layer_name='Color';mat.node_tree.links.new(col.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.63
base=json.loads((OUT/'base-neutral.json').read_text());names=[b['name'] for b in base['bones']]+[h[0] for h in helpers];positions={b['name']:tuple(b['position'][k] for k in ['x','y','z']) for b in base['bones']};positions.update({h[0]:h[2] for h in helpers})
arm=bpy.data.armatures.new('Original toy + cosmetic bones');rig=bpy.data.objects.new('Claude_Fitting_Rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
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
if (OUT/'claude-base-neutral.json').exists():
 d=json.loads((OUT/'claude-base-neutral.json').read_text());ww=[]
 for w in d['weights']:
  ww.append({names[w['m_BoneIndex'+str(i)]]:w['m_Weight'+str(i)] for i in range(4) if w.get('m_Weight'+str(i),0)>0})
 assembled=object_mesh('Original body wearing Claude outfit', [tuple(v[k] for k in ('x','y','z')) for v in d['vertices']], [d['triangles'][i:i+3] for i in range(0,len(d['triangles']),3)], [tuple(c[k] for k in ('r','g','b','a')) for c in d['colors']], ww)
weights=[]
for ws in allw:
 vals=list(ws.items());w={}
 for i in range(4):w['boneIndex'+str(i)]=names.index(vals[i][0]) if i<len(vals) else 0;w['weight'+str(i)]=vals[i][1] if i<len(vals) else 0
 weights.append(w)
data={'boneNames':names,'bonePositions':[vjson(positions[n]) for n in names],'helpers':[{'name':n,'parent':p,'position':vjson(v)} for n,p,v in helpers],
 'vertices':[vjson(p) for p in allv],'triangles':allt,'colors':[dict(zip(('r','g','b','a'),c)) for c in allc],'weights':weights,'regions':regions}
assert len(allt)//3<=10996,len(allt)//3
(OUT/'accessories.json').write_text(json.dumps(data,separators=(',',':')))
(OUT/'mesh-statistics.json').write_text(json.dumps({'triangles':len(allt)//3,'vertices':len(allv),'parts':len(parts),'cosmeticBones':len(helpers),'baseBones':len(base['bones']),'textures':0,'tripoCredits':0,'source':'Locally authored Blender modular character; no paid service call'},indent=2))
# Keep FBX as an editable exchange deliverable; engine import uses the exact Unity-space data.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in bpy.data.objects:
 if o.type=='MESH' and o!=body and o!=assembled:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ART/'ClaudeGirl_Accessories.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ClaudeGirl_Modular.blend'))
print('Claude_MODULAR_ASSETS',len(allt)//3,'triangles',len(allv),'vertices')
