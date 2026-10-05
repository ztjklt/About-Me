#!/usr/bin/env python3
"""Copy completed, reviewed model deliveries and record provenance without signed URLs."""
import hashlib,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[1];art=root/'artifacts/animal-persona';rows=[]
for name,id,label,color in [('cat','cat','猫','#F4AE62'),('dog','animal-dog','狗','#E5C58D'),('rabbit','animal-rabbit','兔子','#FFF3EA'),('hamster','animal-hamster','仓鼠','#E9BE79')]:
 result=json.loads((art/f'{name}-result.json').read_text());source=art/f'{name}-source'
 target=root/'client/Assets/AboutMe/Art/TripoAnimals'/id;target.mkdir(parents=True,exist_ok=True)
 shutil.copy2(root/result['model_file'],target/'model.fbx');shutil.copy2(source/'model.glb',target/'model.glb')
 shutil.copy2(source/'preview.png',target/'preview.png')
 provenance={k:result[k] for k in ['task_id','source_task_id','credits_consumed','credits_breakdown']};provenance.update(provider='Tripo',model='P1-20260311',animal=name)
 provenance['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir() if p.is_file() and p.suffix!='.meta'}
 (target/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n');rows.append(provenance)
 shutil.copy2(source/'preview.png',root/f'client/Assets/AboutMe/Resources/PersonaThumbs/{id}.png')
 p=root/'shared/persona-visuals.json';data=json.loads(p.read_text());data['items']=[x for x in data['items'] if x['visualId']!=id]
 data['items'].append(dict(visualId=id,label=label,templateId='Summon',resourcePath='PersonaProps/'+id,source='Tripo P1-20260311',license='Generated asset; Tripo account terms',color=color,scale=1,colliderRadius=.45,swallowScale=1))
 text=json.dumps(data,ensure_ascii=False,indent=2)+'\n';p.write_text(text);(root/'client/Assets/AboutMe/Resources/persona-visuals.json').write_text(text)
(art/'model-deliveries.json').write_text(json.dumps(rows,indent=2)+'\n')
print('Copied 4 completed model deliveries, 200 credits total')
