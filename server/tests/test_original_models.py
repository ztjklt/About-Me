import json
from pathlib import Path
import pytest
from server import app as module

ROOT=Path(__file__).resolve().parents[2]
RECIPES=json.loads((ROOT/'shared/persona-original-models.json').read_text())['items']

@pytest.mark.parametrize('recipe',RECIPES,ids=[r['id'] for r in RECIPES])
def test_original_model_has_grounded_catalog_and_negative_rejection(recipe):
    source='我喜欢'+recipe['label']
    raw={'personas':[dict(name=recipe['label'],source=source,templateId=recipe['templateId'],visualId=recipe['id'])]}
    assert module.validate_personas(raw,source)[0]['visualId']==recipe['id']
    source='我不喜欢'+recipe['label'];raw['personas'][0]['source']=source
    with pytest.raises(ValueError):module.validate_personas(raw,source)

def test_original_recipes_and_client_copy_are_exact_and_unique():
    assert (ROOT/'shared/persona-original-models.json').read_bytes()==(ROOT/'client/Assets/AboutMe/Resources/persona-original-models.json').read_bytes()
    assert len({r['id'] for r in RECIPES})==len(RECIPES)==98
    geometry=[json.dumps(r['parts'],sort_keys=True) for r in RECIPES]
    assert len(set(geometry))==98
    assert all(r['license']=='project-original' and r['parts'] and r['stage'] in ('model_with_existing_template','model_with_authoritative_mechanic') for r in RECIPES)

def test_live_party_mechanic_registry_is_packaged_and_bound_to_real_models():
    path=ROOT/'shared/persona-mechanics.json'
    assert path.read_bytes()==(ROOT/'client/Assets/AboutMe/Resources/persona-mechanics.json').read_bytes()
    mechanics=json.loads(path.read_text())['items']
    assert len(mechanics)==len({m['id'] for m in mechanics})==30
    assert len({m['visualId'] for m in mechanics})==30
    recipes={r['id']:r for r in RECIPES}
    planned={m['id'] for m in json.loads((ROOT/'shared/persona-expansion-200.json').read_text())['mechanics']}
    for m in mechanics:
        assert m['id'] in planned and m['visualId'] in recipes
        assert recipes[m['visualId']]['stage']=='model_with_authoritative_mechanic'
        assert recipes[m['visualId']]['mechanicId']==m['id']
        assert m['hint'] and .2<=m['windup']<=.4 and 4<=m['cooldown']<=16
        assert 0<=m['force']<=8 and 0<m['radius']<=8
