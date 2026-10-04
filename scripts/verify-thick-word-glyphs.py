#!/usr/bin/env python3
"""Validate the complete font derivative and counters independently of Unity."""
import hashlib,json,struct,zlib,importlib.util
from pathlib import Path
import numpy as np
from fontTools.ttLib import TTFont
from shapely.geometry import Polygon,Point
from shapely.ops import unary_union
spec=importlib.util.spec_from_file_location('font_export',Path(__file__).with_name('export-thick-word-glyphs.py'))
export=importlib.util.module_from_spec(spec);spec.loader.exec_module(export)
FlattenPen,winding=export.FlattenPen,export.winding

ROOT=Path(__file__).resolve().parents[1];FONT=ROOT/'client/Assets/AboutMe/Resources/AboutMeFont.otf';OUT=ROOT/'client/Assets/AboutMe/Resources/ThickWords'
manifest=json.loads((OUT/'supported-codepoints.json').read_text());data=(OUT/'index.bytes').read_bytes()
assert hashlib.sha256(FONT.read_bytes()).hexdigest()==manifest['fontSha256']
assert hashlib.sha256(data).hexdigest()==manifest['indexSha256']
assert data[:4]==b'TWG2';count=struct.unpack_from('<I',data,4)[0];entries={}
pages={}
for page in manifest['pages']:
    b=(OUT/page['path']).read_bytes();assert len(b)<=262144 and hashlib.sha256(b).hexdigest()==page['sha256'];pages[int(page['path'][5:8])]=b
worst=(0,0);totals=[0,0];glyph_records={}
for i in range(count):
    cp,page,offset,size,advance=struct.unpack_from('<IIIIf',data,8+i*20);entries[cp]=(page,offset,size,advance)
    if size==0:assert cp==32 and advance>0;continue
    key=(page,offset)
    if key not in glyph_records:
        raw=zlib.decompress(pages[page][offset:offset+size],-15);n,t,r=struct.unpack_from('<III',raw)
        assert len(raw)==12+n*4+t*4+r*8
        assert n>0 and t>0 and r>0 and n*6*12<65535
        tris=np.frombuffer(raw,dtype='<u4',count=t,offset=12+n*4);assert min(tris)>=0 and max(tris)<n
        glyph_records[key]=(raw,n,t,r);totals[0]+=n;totals[1]+=t//3
        if n>worst[1]:worst=(cp,n)
assert list(entries)==manifest['codepoints'] and len(entries)==44802 and not manifest['triangulationFailures']
assert 0x1fae0 not in entries and 0x20 in entries
required=['热情','冷静','好奇','温柔','理性','活力','有序','自由','幽默','勇敢','内向','外向','专注','乐观','谨慎','独立','友好微笑','开心大笑','自信挑眉','慵懒半眯','惊讶睁眼','认真坚定']
required+=['ISTJ','ISFJ','INFJ','INTJ','ISTP','ISFP','INFP','INTP','ESTP','ESFP','ENFP','ENTP','ESTJ','ESFJ','ENFJ','ENTJ']
font=TTFont(FONT);glyphs=font.getGlyphSet();cmap=font.getBestCmap();mask_checks=0
for cp in sorted(set(map(ord,''.join(required)+'B温A8'))):
    page,offset,size,_=entries[cp];raw,n,t,r=glyph_records[(page,offset)]
    xy=np.frombuffer(raw,dtype='<i2',count=n*2,offset=12).reshape((-1,2)).astype(float)/2048
    inds=np.frombuffer(raw,dtype='<u4',count=t,offset=12+n*4).reshape((-1,3))
    shape=unary_union([Polygon(xy[tri]) for tri in inds if Polygon(xy[tri]).area>1e-10])
    pen=FlattenPen(glyphs);glyphs[cmap[cp]].draw(pen)
    minx,miny=xy.min(axis=0);maxx,maxy=xy.max(axis=0)
    for ix in range(40):
        for iy in range(40):
            x=minx+(ix+.5)*(maxx-minx)/40;y=miny+(iy+.5)*(maxy-miny)/40;p=Point(x,y)
            # Ignore the tiny quantization/curve boundary tolerance; compare clear areas.
            if shape.boundary.distance(p)<.002:continue
            expected=sum(winding((x*1000,y*1000),c) for c in pen.contours)!=0
            assert shape.contains(p)==expected,(chr(cp),x,y)
            mask_checks+=1
    if cp==ord('B'):assert len(shape.interiors)==2
report={'supportedCodepoints':len(entries),'uniqueGlyphs':len(glyph_records),'totalPackageBytes':manifest['bytes'],'allGlyphPayloadsValidated':True,'fontMaskSamplesPassed':mask_checks,'requiredWords':required,'BHasTwoHoles':True,'worstCodepoint':worst[0],'worstCharacter':chr(worst[0]),'worstGlyphContourVertices':worst[1],'maximum12CharacterVertices':worst[1]*6*12,'maximum72WorstWordVertices':worst[1]*6*12*72,'scope':'Offline full payload integrity and required glyph winding mask checks; not native input, live API or multiplayer.'}
dest=ROOT/'artifacts/thick-word-r5';dest.mkdir(parents=True,exist_ok=True);(dest/'font-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
