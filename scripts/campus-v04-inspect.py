import bpy,json,runpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/campus-v04'
a=runpy.run_path(str(ROOT/'scripts/campus-v04-build.py'));a['setup']()
job=json.loads((OUT/'jobs/bay-model.json').read_text());bpy.ops.import_scene.gltf(filepath=job['model_file'])
obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in obs:
    print('MODEL',o.name,'DIM',list(o.dimensions),'LOC',list(o.location),'ROT',list(o.rotation_euler),'BOUNDS',[[round(v,4) for v in o.matrix_world@Vector(c)] for c in o.bound_box], 'TRIS',sum(len(p.vertices)-2 for p in o.data.polygons))
points=[o.matrix_world@v.co for o in obs for v in o.data.vertices];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)]);c=(lo+hi)/2;r=max(hi-lo)
for view,pos in [('front',(0,-3,1.5)),('rear',(0,3,1.5)),('side',(3,0,1.5))]:
    a['render']('bay-probe-'+view+'.png',c+Vector(pos)*r,c,r*1.7,(900,900))
