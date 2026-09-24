"""Create a Claude-only handoff; never include the inherited working tree wholesale."""
from pathlib import Path
import difflib, hashlib, json, os, shutil, subprocess, tempfile, zipfile

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/claude-girl-modular';DEL=OUT/'delivery'
DEL.mkdir(exist_ok=True)
def sha(data):return hashlib.sha256(data).hexdigest()
def copy(a,b):
    b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)
def git(*args,**kwargs):return subprocess.check_output(['git',*args],cwd=ROOT,**kwargs)
modified=['client/Assets/AboutMe/Runtime/'+n for n in ['CampusDoll.cs','DollSkin.cs','AppearanceData.cs','BodyPhysicsRuntimeProbe.cs']]
diff=[];changes=[]
for path in modified:
    before=(OUT/'baseline'/path).read_bytes();after=(ROOT/path).read_bytes()
    diff.extend(difflib.unified_diff(before.decode().splitlines(True),after.decode().splitlines(True),fromfile='a/'+path,tofile='b/'+path))
    changes.append({'path':path,'baselineSha256':sha(before),'resultSha256':sha(after)})
patch=DEL/'claude-only.patch';patch.write_text(''.join(diff))
# Reconstruct the recorded baseline in a disposable index to validate the patch.
with tempfile.TemporaryDirectory(prefix='claude-patch-check-') as temp:
    env=dict(os.environ,GIT_INDEX_FILE=str(Path(temp)/'index'));git('read-tree','HEAD',env=env)
    for path in modified:
        blob=git('hash-object','-w','--stdin',input=(OUT/'baseline'/path).read_bytes()).decode().strip()
        git('update-index','--add','--cacheinfo','100644,'+blob+','+path,env=env)
    git('apply','--cached','--check',str(patch),env=env)
new=['client/Assets/AboutMe/Art/ClaudeGirlModular.meta','client/Assets/AboutMe/Resources/CampusDoll/ClaudeGirlModular.prefab','client/Assets/AboutMe/Resources/CampusDoll/ClaudeGirlModular.prefab.meta','docs/CLAUDE_GIRL_MODULAR.md','scripts/package-claude-girl.py','scripts/start-claude-girl-preview.command']
for path in ['Runtime/ClaudeGirlOutfit.cs','Runtime/ClaudeGirlFaceDetails.cs','Editor/ClaudeGirlModularTools.cs','Editor/ClaudeGirlFaceReview.cs']:
    new.extend(['client/Assets/AboutMe/'+path,'client/Assets/AboutMe/'+path+'.meta'])
new.extend('scripts/claude-girl-modular-'+name+'.py' for name in ['assets','source','video'])
new.extend(str(p.relative_to(ROOT)) for p in sorted((ROOT/'client/Assets/AboutMe/Art/ClaudeGirlModular').rglob('*')) if p.is_file())
new_info=[]
for path in new:
    copy(ROOT/path,DEL/'new-files'/path);new_info.append({'path':path,'sha256':sha((ROOT/path).read_bytes())})
evidence=['ClaudeGirl_Modular.blend','reference.jpg','base-neutral.json','ds-base-neutral.json','claude-base-neutral.json','accessories.json','mesh-statistics.json','pose-parity.json','ds-regression-parity.json','switch-validation.json','physics-parity.txt','physics-motion.mp4','mac-runtime.mp4','runtime-validation.json','perf-toy/metrics.json','perf-claude/metrics.json','runtime-capture/metrics.json']
evidence += [str(p.relative_to(OUT)) for folder in ['review','face-review'] for p in sorted((OUT/folder).glob('*')) if p.suffix in ['.png','.jpg','.json']]
evidence_info=[]
for path in evidence:
    source=OUT/path;copy(source,DEL/'source-and-evidence'/path);evidence_info.append({'path':path,'sha256':sha(source.read_bytes())})
inherited=json.loads((OUT/'inherited-state.json').read_text())
unintended=[p for p,h in inherited.items() if p not in modified and (not (ROOT/p).exists() or sha((ROOT/p).read_bytes())!=h)]
if unintended:raise RuntimeError('Inherited files changed outside the whitelist: '+str(unintended))
manifest={'branch':'codex/claude-girl-modular','headIsNotDeliveryCommit':True,'baseline':'DS modular FaceV2 working-tree snapshot; NOT clean 545b4ce','modified':changes,'newFiles':new_info,'evidence':evidence_info,'patchCheckPassed':True,'inheritedFilesChecked':len(inherited),'unintendedInheritedChanges':unintended,'prerequisite':'Existing DS modular renderer; client owns Claude unlock/select/save/server whitelist integration.'}
(DEL/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(DEL/'README.md').write_text('''# Claude girl modular delivery

Read new-files/docs/CLAUDE_GIRL_MODULAR.md. Apply claude-only.patch only after comparing the four baseline hashes in manifest.json. It targets the recorded DS modular FaceV2 working tree, not clean HEAD. Copy new-files/ with paths and Unity meta files preserved. Keep source-and-evidence/ under artifacts/claude-girl-modular/ for re-import and editable source.

The packet deliberately excludes inherited DS/other project changes and compiled Mac/Web builds. It supplies the Claude renderer/assets, not a new production unlock policy. Client integration must preserve its existing default character value, trusted unlock checks and DS/GPT behavior. Current builds and videos are local automatic-test evidence; mobile/WeChat/human/network validation remains open.
''')
archive=OUT/'ClaudeGirl-Modular-Delivery.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(DEL.rglob('*')):
        if p.is_file():z.write(p,str(p.relative_to(DEL)))
print(json.dumps({'modifiedFiles':len(modified),'newFiles':len(new),'patchCheckPassed':True,'unintendedInheritedChanges':unintended,'archiveBytes':archive.stat().st_size}))
