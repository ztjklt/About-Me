"""Package only reviewed DS renderer/assets against the client integration base."""
from pathlib import Path
import difflib, hashlib, json, os, shutil, subprocess, tempfile, zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/ds-girl-face-v2'
DEL = OUT / 'delivery'
BASE = 'abc89c4'
def git(*args, **kwargs):
    return subprocess.check_output(['git', *args], cwd=ROOT, **kwargs)
def sha(data):
    return hashlib.sha256(data).hexdigest()
def copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

DEL.mkdir(parents=True, exist_ok=True)
modified = ['client/Assets/AboutMe/Runtime/CampusDoll.cs', 'client/Assets/AboutMe/Runtime/DollSkin.cs']
patch, modified_info = [], []
for path in modified:
    before, after = git('show', BASE + ':' + path), (ROOT/path).read_bytes()
    patch.extend(difflib.unified_diff(before.decode().splitlines(True), after.decode().splitlines(True), fromfile='a/'+path, tofile='b/'+path))
    modified_info.append({'path':path, 'baselineSha256':sha(before), 'resultSha256':sha(after)})
patch_path = DEL / 'integration-abc89c4.patch'
patch_path.write_text(''.join(patch))
# Validate the patch against a disposable Git index; never stage the inherited working tree.
with tempfile.TemporaryDirectory(prefix='ds-delivery-check-') as temp:
    env = dict(os.environ, GIT_INDEX_FILE=str(Path(temp)/'index'))
    git('read-tree', BASE, env=env)
    git('apply', '--cached', '--check', str(patch_path), env=env)

new = [
    'client/Assets/AboutMe/Art/DSGirlModular.meta',
    'client/Assets/AboutMe/Resources/CampusDoll/DSGirlModular.prefab',
    'client/Assets/AboutMe/Resources/CampusDoll/DSGirlModular.prefab.meta',
    'docs/DS_GIRL_MODULAR.md', 'docs/DS_GIRL_FACE_V2.md',
    'scripts/ds-girl-modular-assets.py', 'scripts/ds-girl-modular-source.py', 'scripts/package-ds-girl-face.py',
]
for path in ['Runtime/DSGirlOutfit.cs','Runtime/DSGirlFaceDetails.cs','Editor/DSGirlModularTools.cs','Editor/DSGirlFaceReview.cs']:
    new += ['client/Assets/AboutMe/'+path, 'client/Assets/AboutMe/'+path+'.meta']
new += sorted(str(p.relative_to(ROOT)) for p in (ROOT/'client/Assets/AboutMe/Art/DSGirlModular').rglob('*') if p.is_file())
new_info = []
for path in new:
    source = ROOT/path
    copy(source, DEL/'new-files'/path)
    new_info.append({'path':path, 'sha256':sha(source.read_bytes())})

unchanged = json.loads((OUT/'unchanged-costume-hashes.json').read_text())
assert all(sha((ROOT/path).read_bytes()) == expected for path, expected in unchanged.items())
evidence = ['face-validation.json','pose-parity.json','unchanged-costume-hashes.json','front.png','three-quarter.png','face-close.png','blink.png','action-eat.png','action-eat-side.png','action-grab.png','before-front.png','before-three-quarter.png']
evidence += sorted(p.name for p in OUT.glob('expression-*.png'))
for path in evidence:
    copy(OUT/path, DEL/'source-and-evidence/current-face'/path)
for path in ['DSGirl_Modular.blend','base-neutral.json','ds-base-neutral.json','accessories.json','mesh-statistics.json','review/side.png','review/back.png','review/comparison.png']:
    copy(ROOT/'artifacts/ds-girl-modular'/path, DEL/'source-and-evidence/modular-source'/path)
manifest = {
    'baselineCommit':git('rev-parse',BASE).decode().strip(), 'patchCheckPassed':True,
    'modified':modified_info, 'newFiles':new_info, 'costumeHashesUnchanged':True,
    'faceParameters':json.loads((OUT/'face-validation.json').read_text()),
    'pending':'Eye position / glasses clarification from PM; eye and brow drop stay zero.',
    'prerequisite':'Client owns AppearanceData.character, SameAs comparison, normalization, trusted unlock mapping and save/UI/network integration. Not standalone buildable on clean abc89c4.'
}
(DEL/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
(DEL/'README.md').write_text('''# DS modular outfit + face details candidate

Read new-files/docs/DS_GIRL_FACE_V2.md first. This is a scoped asset/rendering delivery, not a whole-project snapshot.

Apply integration-abc89c4.patch after checking it against the client branch, and copy the whitelisted new-files/ paths. Keep the client-owned AppearanceData.character and unlock/save integration. Do not use the old whole-model adapter or hide the original body Skin.

The patch was checked against abc89c4 in a disposable Git index. Runtime compilation and visual/pose checks were performed in the DS task workspace; the client must build and validate the combined feature. Eye position is pending PM clarification; no glasses were added. Current macOS/WebGL builds predate these face changes.

Editable Blender and mesh sources are in source-and-evidence/modular-source/. Current face/pose checks are in source-and-evidence/current-face/. Earlier macOS/performance evidence remains separately archived in artifacts/ds-girl-modular and is not represented as current face runtime evidence.
''')
with zipfile.ZipFile(OUT/'DSGirl-FaceV2-Integration.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(DEL.rglob('*')):
        if p.is_file(): z.write(p,str(p.relative_to(DEL)))
print(json.dumps({'modifiedFiles':len(modified),'newFiles':len(new),'patchCheckPassed':True,'costumeHashesUnchanged':True,'archiveBytes':(OUT/'DSGirl-FaceV2-Integration.zip').stat().st_size}))
