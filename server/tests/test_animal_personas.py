import json
from pathlib import Path
import pytest
from server import app as api

ROOT=Path(__file__).resolve().parents[2]
ANIMALS=[('cat','猫','kittens'),('animal-dog','狗','puppies'),('animal-rabbit','兔子','bunnies'),('animal-hamster','仓鼠','hamsters')]
@pytest.mark.parametrize('visual,label,english',ANIMALS)
def test_liking_animal_maps_only_to_catalog_model(visual,label,english):
    for source in ['我喜欢'+label,'I love '+english]:
        result=api.validate_personas({'personas':[dict(name=label,source=source,templateId='Summon',visualId=visual)]},source)
        assert len(result)==1 and result[0]['visualId']==visual
    for source in ['我不喜欢'+label,'I dislike '+english,'我喜欢摄影']:
        with pytest.raises(api.NoUsablePersonaError):
            api.validate_personas({'personas':[dict(name=label,source=source,templateId='Summon',visualId=visual)]},source)

def test_multiple_animals_from_one_real_introduction_remain_distinct():
    source='我喜欢猫、狗、兔子和仓鼠'
    raw={'terms':[dict(term=label,source=source) for _,label,_ in ANIMALS],
         'personas':[dict(name=label,term=label,source=source,templateId='Summon',visualId=visual) for visual,label,_ in ANIMALS]}
    result=api.validate_personas(raw,source)
    assert {x['visualId'] for x in result}=={a[0] for a in ANIMALS}

def test_animals_do_not_match_unrelated_english_substrings_or_fake_combat():
    assert not api.catalog_supports_fact(api.CATALOG_BY_PAIR[('Summon','cat')],'I love education')
    with pytest.raises(ValueError):
        api.validate_personas({'personas':[dict(name='狗',source='我喜欢狗',templateId='ExecuteCode',visualId='animal-dog')]},'我喜欢狗')

def test_shared_catalog_and_questionnaire_contract():
    assert (ROOT/'shared/catalog.json').read_bytes()==(ROOT/'client/Assets/AboutMe/Resources/catalog.json').read_bytes()
    rows=json.loads((ROOT/'shared/catalog.json').read_text())['items']
    assert len(rows)==277
    assert all(any(x['visualId']==visual and x['templateId']=='Summon' for x in rows) for visual,_,_ in ANIMALS)
