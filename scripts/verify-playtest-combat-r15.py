#!/usr/bin/env python3
"""Verify the exact local bundle before launch; occupied ports are never reused."""
import argparse
import hashlib
import json
import subprocess
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('manifest', type=Path)
parser.add_argument('--api-pid', type=int)
parser.add_argument('--authority-pid', type=int)
args = parser.parse_args()
manifest = json.loads(args.manifest.read_text())
root = args.manifest.resolve().parents[2]

def verify_files(base, files):
    for name, expected in files.items():
        path = base / name
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise SystemExit('版本文件缺失或哈希不匹配：' + str(path))

app = root / manifest['appPath']
service = root / manifest['servicePath']
verify_files(app, manifest['clientFiles'])
verify_files(service, manifest['serviceFiles'])
verify_files(root / manifest['webPath'], manifest['webFiles'])
assembly=(app / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll').read_bytes()
if manifest.get('eaterRoastVersion') != 3: raise SystemExit('吞噬者调侃交付身份缺失')
if any(manifest.get(key)!=value for key,value in {'mouthPresentationVersion':1,'maxRoastVisibleCharacters':7,'roastBubbleWidth':152,'roastBubbleHeight':42}.items()):
    raise SystemExit('R15 嘴型或短气泡交付身份缺失')
if not {'ai-idle-no-mouth','normal-idle-closed-smile','short-sharp-roasts-5-to-7','overhead-small-single-line-bubbles'}.issubset(manifest['requiredFeatures']):
    raise SystemExit('R15 嘴型或短梗功能门槛缺失')
if b'AuditMouthStyle' not in assembly: raise SystemExit('R15 嘴型客户端缺失')
for symbol in ('EatingRoastAttempt', 'SayEatingRoast', 'PrepareEatingRoasts', 'EaterRoastPresentation'):
    if symbol.encode() not in assembly: raise SystemExit('吞噬者调侃客户端缺失：'+symbol)
for symbol in manifest['requiredAssemblySymbols']:
    if symbol.encode() not in assembly: raise SystemExit('此前交付功能缺失：'+symbol)
legacy_features={'horizontal-persona-results','questionnaire-free-answer-13','free-introduction-preserved','player-settings','zh-en-language','catalog-274','compact-introduction-studio','saved-avatar-persona-preview','compact-wardrobe-grid','english-ai-preference-recognition'}
if not legacy_features.issubset(manifest['requiredFeatures']): raise SystemExit('完整 R2 功能门槛缺失')
for symbol in ('PersonaResultScrollRect','OpenIntroductionQuestionnaire','IntroductionQuestions','GameLocalization','SetGameLanguage','ApplyCameraLook','AuditPlayerSettings','IntroductionUIAudit'):
    if symbol.encode() not in assembly: raise SystemExit('完整整合功能缺失：'+symbol)
if manifest['settingsSourceSnapshot'] != '2530bcdd73597917a0043780164b1942eb8986efa968bb9f0ab745fda35ff640': raise SystemExit('设置 V2 基线不匹配')
expected_snapshot=hashlib.sha256(json.dumps(manifest['sourceFiles'],sort_keys=True).encode()).hexdigest()
if manifest['sourceSnapshotSha256'] != expected_snapshot or manifest['apiSourceSnapshot'] != expected_snapshot: raise SystemExit('完整源码清单不匹配')
prior=args.manifest.resolve().parent/'prior-release-manifest.json'
if not prior.is_file() or hashlib.sha256(prior.read_bytes()).hexdigest()!=manifest['priorReleaseManifestSha256']: raise SystemExit('R6继承交付身份不匹配')
if 'animal-skeletal-walk-4' not in manifest['requiredFeatures'] or len(manifest.get('animationDeliveries',[]))!=4: raise SystemExit('四种骨骼走动交付缺失')
for row in manifest['animationDeliveries']:
    species={'dog':'animal-dog','rabbit':'animal-rabbit','hamster':'animal-hamster'}.get(row['animal'],row['animal'])
    key='client/Assets/AboutMe/Art/TripoAnimals/'+species+'/Animation/walk.fbx'
    if manifest['sourceFiles'].get(key)!=row['fbx_sha256']: raise SystemExit('走动FBX来源与冻结源码不一致：'+species)
if not {'configurable-solo-bots-1-to-6','gentler-practice-reactions'}.issubset(manifest['requiredFeatures']): raise SystemExit('可调人机交付缺失')
if manifest.get('flameDamagePerPulse')!=60 or manifest.get('aiPreferenceCount')!=10 or manifest.get('questionnaireCount')!=16: raise SystemExit('R13机制或题目身份不匹配')
if manifest['sourceFiles']['shared/persona-mechanics.json']!=manifest['sourceFiles']['client/Assets/AboutMe/Resources/persona-mechanics.json']: raise SystemExit('机制共享目录不一致')
mechanics=json.loads((service/'shared/persona-mechanics.json').read_text())
if next(d for d in mechanics['items'] if d['id']=='flame-fan')['damage']!=60: raise SystemExit('喷火伤害不匹配')
if manifest.get('maxPersonas')!=20: raise SystemExit('Persona 上限不匹配')
if manifest.get('protocolVersion')!=5: raise SystemExit('动物与单次投掷协议版本不匹配')
for symbol in ('ThickWordVisual','TraitAbilityCatalog','BorrowWord','AISkinRegistry','AnimalPersonaCatalog','TickAnimalPersona','AnimalPersonaMotion'):
    if symbol.encode() not in assembly: raise SystemExit('R6及继承功能缺失：'+symbol)
if len(json.loads((service/'shared/trait-abilities.json').read_text())['mbti'])!=16: raise SystemExit('MBTI目录未保留')
traits=json.loads((service/'shared/trait-abilities.json').read_text())
if traits['version']!=2 or len(traits['abilities'])!=16 or len({w['abilityId'] for w in traits['mbti']})!=16: raise SystemExit('16个独立MBTI机制不完整')
if manifest.get('traitAbilityCount')!=16 or manifest.get('traitWordVersion')!=2: raise SystemExit('MBTI机制发布身份不匹配')
if manifest['sourceFiles']['shared/trait-abilities.json']!=manifest['sourceFiles']['client/Assets/AboutMe/Resources/trait-abilities.json']: raise SystemExit('MBTI共享目录不一致')
if len(json.loads((service/'shared/ai-skins.json').read_text())['skins'])!=10: raise SystemExit('AI角色目录未完整整合')
if manifest['sourceFiles']['shared/ai-skins.json']!=manifest['sourceFiles']['client/Assets/AboutMe/Resources/ai-skins.json']: raise SystemExit('AI角色共享目录不一致')
if manifest['sourceFiles']['shared/trait-word-supported-codepoints.json']!=manifest['sourceFiles']['client/Assets/AboutMe/Resources/ThickWords/supported-codepoints.json']: raise SystemExit('API字形支持表不一致')
if len(json.loads((service/'shared/catalog.json').read_text())['items']) != 277: raise SystemExit('277目录不匹配')

if manifest['clientBaseCommit'] != '78208762df19afa2bea556858690ec6b141b4edd':
    raise SystemExit('未核对的客户端源码版本')
if manifest['apiSourceCommit'] != manifest['clientSourceCommit']:
    raise SystemExit('API 源码基线不匹配')

def listener(port, expected_pid):
    result = subprocess.run(['lsof', '-nP', '-iTCP:' + str(port), '-sTCP:LISTEN', '-Fp'],
                            text=True, capture_output=True)
    pids = {int(line[1:]) for line in result.stdout.splitlines() if line.startswith('p')}
    if pids != {expected_pid}:
        raise SystemExit('端口监听进程与本轮启动进程不匹配：' + str(port))

if args.api_pid is not None:
    listener(manifest['apiPort'], args.api_pid)
    command = subprocess.check_output(['ps', '-p', str(args.api_pid), '-o', 'command='], text=True)
    if str(service) not in command or 'server.release_entry:app' not in command:
        raise SystemExit('API 进程路径不匹配')
    url = 'http://127.0.0.1:' + str(manifest['apiPort'])
    with urllib.request.urlopen(url + '/local-release-version', timeout=2) as response:
        version = json.load(response)
    if any(version.get(key) != manifest[key] for key in ('releaseId', 'apiSourceCommit', 'apiSourceSnapshot', 'serviceTreeSha256')):
        raise SystemExit('运行 API 交付标识不匹配')
    if version.get('pid') != args.api_pid or version.get('serviceRoot') != str(service):
        raise SystemExit('运行 API 进程身份不匹配')
    with urllib.request.urlopen(url + '/health', timeout=2) as response:
        health = json.load(response)
    if not health.get('preparedRoasts'): raise SystemExit('吞噬者调侃API缺失')
    if health.get('maxPersonas')!=20: raise SystemExit('运行 API Persona 上限不匹配')
    if not health.get('traitWordPersonas') or health.get('traitWordVersion')!=2: raise SystemExit('文字能力API版本不匹配')
    if health.get('playerSettingsLanguage') != 'settings-language-v2': raise SystemExit('设置服务版本不匹配')
    if health.get('personaCatalogCount') != 277 or health.get('partyMechanicsCount') != manifest['mechanicCount']:
        raise SystemExit('运行 API 的目录/机制数量不匹配')
if args.authority_pid is not None:
    listener(manifest['gamePort'], args.authority_pid)
    command = subprocess.check_output(['ps', '-p', str(args.authority_pid), '-o', 'command='], text=True)
    if str(root / manifest['executablePath']) not in command or '--aboutme-server' not in command:
        raise SystemExit('权威服务器运行路径不匹配')
if manifest.get('combatTuningVersion')!=1 or b'HitContactHazard' not in assembly: raise SystemExit('R15伤害规则未整合')
if any(manifest.get(k)!=v for k,v in {'lightMeleeDamage':60,'utilityDamage':100,'hazardContactDamage':100}.items()): raise SystemExit('R15伤害配置身份不匹配')
if any(d['damage']>0 and d['damage']!=(60 if d['id']=='flame-fan' else 100) for d in mechanics['items']): raise SystemExit('攻击机制伤害目录不匹配')
if any(next(d for d in mechanics['items'] if d['id']==k)['damage']!=100 for k in ('wet-trail','sand-sink','sticky-step','frost-lock')): raise SystemExit('踩踏场地伤害未整合')

print('已核对完整候选版 ' + manifest['releaseId'] + ' · 客户端/权威同包 · 完整源码快照 + 设置 V2 · 277 道具 / '+str(manifest['mechanicCount'])+' 机制')
