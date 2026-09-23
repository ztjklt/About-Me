import bpy,runpy,json,sys,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/campus-v04';a=runpy.run_path(str(ROOT/'scripts/campus-v04-build.py'));a['setup']()
name=sys.argv[-1]
path=next((OUT/'tripo-out').glob('campus-v04-'+name+'-model-*/model.glb'));bpy.ops.import_scene.gltf(filepath=str(path))
obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@v.co for o in obs for v in o.data.vertices]
lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)]);c=(lo+hi)/2;r=max(hi-lo)
for o in obs:
 print('STATS',name,len(o.data.vertices),sum(len(p.vertices)-2 for p in o.data.polygons),'BOUNDS',list(lo),list(hi))
 print('NORMALS',[(round(p.area,4),[round(x,3) for x in p.normal]) for p in sorted(o.data.polygons,key=lambda p:p.area,reverse=True)[:15]])
for view,pos in [('front',(0,-3,1.5)),('top',(0,0,3)),('rear',(0,3,1.5))]:a['render'](name+'-probe-'+view+'.png',c+Vector(pos)*r,c,r*1.5,(900,900))
