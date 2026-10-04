#!/usr/bin/env python3
"""Export genuine AboutMeFont CFF contours; no bitmap/TextMesh substitutes.

Reproduce: python -m venv /tmp/thick-word-font; pip install -r
scripts/thick-word-font-requirements.txt; python scripts/export-thick-word-glyphs.py
Binary TWG2: count, then sorted <codepoint, page, offset, compressed_size, advance>;
each raw-deflate glyph contains XY vertices, triangle indices, and contour rings.
Coordinates are in font-em units. Rings retain holes for side-wall extrusion.
"""
import hashlib, json, math, struct, zlib
from pathlib import Path
import numpy as np
import mapbox_earcut
from fontTools.ttLib import TTFont
from fontTools.pens.basePen import BasePen
from shapely.geometry import Polygon, LineString
from shapely.ops import unary_union, polygonize

ROOT = Path(__file__).resolve().parents[1]
FONT = ROOT / 'client/Assets/AboutMe/Resources/AboutMeFont.otf'
OUT = ROOT / 'client/Assets/AboutMe/Resources/ThickWords'
TOLERANCE = 1.75  # <= 0.00175 em before normalization, deterministic adaptive flattening

class FlattenPen(BasePen):
    def __init__(self, glyphset):
        super().__init__(glyphset); self.contours=[]; self.current=[]
    def _moveTo(self,p): self.current=[p]
    def _lineTo(self,p): self.current.append(p)
    def _curveToOne(self,a,b,c):
        def curve(p,a,b,c,depth=0):
            dx,dy=c[0]-p[0],c[1]-p[1]; length=math.hypot(dx,dy)
            flat=max(abs(dx*(v[1]-p[1])-dy*(v[0]-p[0]))/length for v in (a,b)) if length else max(math.dist(p,a),math.dist(p,b))
            if flat<=TOLERANCE or depth>=12: self.current.append(c); return
            mid=lambda u,v:((u[0]+v[0])*.5,(u[1]+v[1])*.5)
            pa,ab,bc=mid(p,a),mid(a,b),mid(b,c); pab,abc=mid(pa,ab),mid(ab,bc); q=mid(pab,abc)
            curve(p,pa,pab,q,depth+1);curve(q,abc,bc,c,depth+1)
        curve(self.current[-1],a,b,c)
    def _qCurveToOne(self,a,b):
        p=self.current[-1]
        self._curveToOne((p[0]+2*(a[0]-p[0])/3,p[1]+2*(a[1]-p[1])/3),(b[0]+2*(a[0]-b[0])/3,b[1]+2*(a[1]-b[1])/3),b)
    def _closePath(self):
        points=[]
        for p in self.current:
            p=(round(p[0],4),round(p[1],4))
            if not points or p!=points[-1]: points.append(p)
        if len(points)>1 and points[0]==points[-1]:points.pop()
        if len(points)>=3:self.contours.append(points)
        self.current=[]
    def _endPath(self): self._closePath()

def winding(point,contour):
    x,y=point;total=0
    for a,b in zip(contour,contour[1:]+contour[:1]):
        cross=(b[0]-a[0])*(y-a[1])-(x-a[0])*(b[1]-a[1])
        if a[1]<=y<b[1] and cross>0:total+=1
        if b[1]<=y<a[1] and cross<0:total-=1
    return total

def glyph_blob(contours,em):
    if not contours:return b'',0,0,0
    # Polygonize then apply font's non-zero winding fill. This preserves holes,
    # disconnected components and overlapping CFF paths (even self crossings).
    lines=[LineString(c+[c[0]]) for c in contours]
    faces=list(polygonize(unary_union(lines)))
    filled=[p for p in faces if sum(winding((p.representative_point().x,p.representative_point().y),c) for c in contours)!=0]
    shape=unary_union(filled)
    polygons=list(shape.geoms) if shape.geom_type=='MultiPolygon' else [shape]
    vertices=[];triangles=[];rings=[];expected=0
    for poly in polygons:
        if poly.is_empty:continue
        # CCW outer, CW holes => material is left of every ring edge.
        from shapely.geometry.polygon import orient
        poly=orient(poly,sign=1)
        local=[];ends=[]
        for ring in [poly.exterior,*poly.interiors]:
            points=list(ring.coords)[:-1];start=len(vertices)+len(local)
            rings.append((start,len(points)));local.extend(points);ends.append(len(local))
        data=np.array(local,dtype=np.float64)
        inds=mapbox_earcut.triangulate_float64(data,np.array(ends,dtype=np.uint32))
        area=0
        for a,b,c in inds.reshape((-1,3)):
            u,v,w=data[a],data[b],data[c]
            cross=(v[0]-u[0])*(w[1]-u[1])-(v[1]-u[1])*(w[0]-u[0]);area+=abs(cross)*.5
            if cross<0:b,c=c,b
            triangles.extend((len(vertices)+int(a),len(vertices)+int(b),len(vertices)+int(c)))
        if abs(area-poly.area)>max(.01,poly.area*1e-7):raise ValueError('hole triangulation area mismatch')
        expected+=poly.area;vertices.extend((x/em,y/em) for x,y in local)
    if not vertices:return b'',0,0,0
    raw=struct.pack('<III',len(vertices),len(triangles),len(rings))
    raw+=b''.join(struct.pack('<hh',round(p[0]*2048),round(p[1]*2048)) for p in vertices)
    raw+=struct.pack('<'+'I'*len(triangles),*triangles)
    raw+=b''.join(struct.pack('<II',*r) for r in rings)
    compress=zlib.compressobj(9,zlib.DEFLATED,-15)
    return compress.compress(raw)+compress.flush(),len(vertices),len(triangles)//3,len(rings)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    font=TTFont(FONT);em=font['head'].unitsPerEm;glyphset=font.getGlyphSet();cmap=font.getBestCmap()
    chunks=[];entries=[];codes=[];failures={};cache={};counts={};offset=0;page=0;page_sizes=[]
    for index,(cp,name) in enumerate(sorted(cmap.items())):
        if name not in cache:
            try:
                pen=FlattenPen(glyphset);glyphset[name].draw(pen);blob,n,t,r=glyph_blob(pen.contours,em)
                cache[name]=(blob,n,t,r)
            except Exception as e:failures[str(cp)]=str(e);continue
        blob,n,t,r=cache[name]
        if not blob:
            if cp==32:entries.append(struct.pack('<IIIIf',cp,0,0,0,font['hmtx'][name][0]/em));codes.append(cp)
            continue # ASCII space is advance only; other invisibles are unsupported
        if name not in counts:
            if offset+len(blob)>262144 and chunks:
                data=b''.join(chunks);(OUT/f'page-{page:03}.bytes').write_bytes(data);page_sizes.append(len(data));page+=1;chunks=[];offset=0
            counts[name]=(page,offset,len(blob),n,t,r);chunks.append(blob);offset+=len(blob)
        pg,pos,size,n,t,r=counts[name]
        entries.append(struct.pack('<IIIIf',cp,pg,pos,size,font['hmtx'][name][0]/em));codes.append(cp)
        if index%2000==0:print(f'{index}/{len(cmap)} glyphs, {(sum(page_sizes)+offset)/1048576:.1f} MiB',flush=True)
    data=b''.join(chunks);(OUT/f'page-{page:03}.bytes').write_bytes(data);page_sizes.append(len(data))
    binary=b'TWG2'+struct.pack('<I',len(entries))+b''.join(entries)
    (OUT/'index.bytes').write_bytes(binary)
    manifest={'schema':'aboutme-thick-word-font-v2','font':'AboutMeFont.otf','fontSha256':hashlib.sha256(FONT.read_bytes()).hexdigest(),'indexSha256':hashlib.sha256(binary).hexdigest(),'pages':[{'path':f'page-{i:03}.bytes','bytes':n,'sha256':hashlib.sha256((OUT/f'page-{i:03}.bytes').read_bytes()).hexdigest()} for i,n in enumerate(page_sizes)],'maxCodepoints':12,'codepoints':codes,'spacingCodepoints':[32],'glyphCount':len(counts),'mappedCodepoints':len(codes),'bytes':len(binary)+sum(page_sizes),'curveToleranceEm':TOLERANCE/em,'positionQuantizationEm':1/2048,'maxContourVertices':max(c[3] for c in counts.values()),'maxFaceTriangles':max(c[4] for c in counts.values()),'triangulationFailures':failures,'geometry':'non-zero CFF winding; CCW front; CW back; all boundary rings extruded','tools':{'fonttools':'4.66.1','shapely':'2.1.2','mapbox-earcut':'2.1.0'}}
    (OUT/'supported-codepoints.json').write_text(json.dumps(manifest,ensure_ascii=False,separators=(',',':'))+'\n')
    print(json.dumps({k:v for k,v in manifest.items() if k!='codepoints'},ensure_ascii=False,indent=2))
    if failures:raise SystemExit('Some font outlines failed; do not advertise missing meshes as supported')

if __name__=='__main__':main()
