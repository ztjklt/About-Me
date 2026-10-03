import pytest

@pytest.fixture(autouse=True)
def isolated_crest_cache(tmp_path, monkeypatch):
    monkeypatch.setenv('UNIVERSITY_CREST_CACHE', str(tmp_path / 'crests'))
