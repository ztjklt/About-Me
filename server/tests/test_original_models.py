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
    assert len({r['id'] for r in RECIPES})==len(RECIPES)==28
    geometry=[json.dumps(r['parts'],sort_keys=True) for r in RECIPES]
    assert len(set(geometry))==28
    assert all(r['license']=='project-original' and r['parts'] and r['stage']=='model_with_existing_template' for r in RECIPES)
