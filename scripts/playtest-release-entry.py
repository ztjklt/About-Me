"""Local release diagnostics; the 049312f API and generation protocol remain unchanged."""
import hashlib
import json
import os
from pathlib import Path
from server.app import app

_root = Path(__file__).resolve().parents[1]
_manifest = json.loads(Path(os.environ['ABOUTME_RELEASE_MANIFEST']).read_text())
_files = {name: hashlib.sha256((_root / name).read_bytes()).hexdigest()
          for name in _manifest['serviceFiles']}
_tree = hashlib.sha256(json.dumps(_files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
if _tree != _manifest['serviceTreeSha256']:
    raise RuntimeError('API release source hash mismatch')

@app.get('/local-release-version')
def local_release_version():
    return {'releaseId': _manifest['releaseId'], 'apiSourceCommit': _manifest['apiSourceCommit'],
            'serviceTreeSha256': _tree, 'pid': os.getpid(), 'serviceRoot': str(_root)}
