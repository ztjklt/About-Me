"""Grounded, catalog-bound text Personas. No model-controlled combat parameters."""
import json
import re
import uuid
from pathlib import Path
from server.roast_safety import safe_public_text

CATALOG = json.loads((Path(__file__).resolve().parents[1] / 'shared/trait-abilities.json').read_text())
VERSION = CATALOG['version']
FONT_SUPPORT = json.loads((Path(__file__).resolve().parents[1] / 'shared/trait-word-supported-codepoints.json').read_text())
if FONT_SUPPORT['schema'] != 'aboutme-thick-word-font-v2' or FONT_SUPPORT['maxCodepoints'] != 12:
    raise ValueError('文字字体支持目录版本不匹配')
SUPPORTED_CODEPOINTS = frozenset(FONT_SUPPORT['codepoints']) | {32}  # Space is layout advance, not a contour.
ABILITIES = {a['id'] for a in CATALOG['abilities']}
CATEGORIES = {'personality', 'mood', 'mbti', 'custom'}
WORDS = CATALOG['words'] + CATALOG['mbti']
PALETTE = {'flame-fan':'#FFAB69', 'bubble-lift':'#FFAEC8', 'front-shield':'#FFD773',
           'calm-dome':'#9ACDEA', 'tractor-loose':'#79CFC5', 'spring-pad':'#C3AFE9'}
SELF = re.compile(r'我|本人|自己|\b(?:I|my|myself)\b|\bMBTI\s*(?:是|为|:|：|is)', re.I)
BLOCK = re.compile(r'不|没|不是|并非|不太|不够|不喜欢|不爱|不想|没有|没那么|不算|不冷|不热|不温|不理|不专|不友|不自|不开心|不认真|不惊|假如|假设|如果|要是|例如|比如|希望成为|想变成|朋友|同学|他|她|他们|她们|\b(?:not|never|no|if|suppose|wish|friend|he|she|they)\b', re.I)


def word_error(text, sensitive=lambda _: False):
    if not isinstance(text, str) or not 1 <= len(text) <= 12:
        return '文字最多12个字符，请精简后重试'
    han = r'\u3400-\u9fff\uf900-\ufaff\U00020000-\U000323af'
    if not re.fullmatch('['+han+r'A-Za-z]['+han+r'A-Za-z0-9 ·-]*', text):
        return '文字仅支持汉字、英文字母数字、空格、中点和连字符，请修改后重试'
    if not safe_public_text(text) or sensitive(text):
        return '文字含不能公开的内容，请修改后重试'
    if any(ord(char) not in SUPPORTED_CODEPOINTS for char in text):
        return '文字包含当前字体未支持的字符，请改字后重试'
    return ''


def safe_word(text, sensitive=lambda _: False):
    return not word_error(text, sensitive)


def self_spans(introduction):
    """Carry a self subject across enumeration, but never across another person's clause."""
    spans, inherited = [], False
    for match in re.finditer(r'[^。；;!！?？\n\r，,]+[。；;!！?？\n\r，,]?', introduction):
        raw = match.group(0)
        clause = raw.rstrip('。；;!！?？\n\r，,').strip()
        own = bool(SELF.search(clause))
        # An explicit third person does not inherit the preceding subject.
        blocked = bool(BLOCK.search(clause))
        if (own or inherited) and not blocked:
            spans.append(clause)
        if re.search(r'[。；;!！?？\n\r]$', raw):
            inherited = False
        elif own or blocked:
            inherited = own and not blocked
    return spans


def contains_word(source, word):
    if re.fullmatch(r'[A-Za-z ]+', word):
        return bool(re.search(r'(?<![A-Za-z])'+re.escape(word)+r'(?![A-Za-z])', source, re.I))
    return word in source


def canonical(text, category=None):
    for row in WORDS:
        if category not in (None, 'custom', row['category']):
            continue
        if text.casefold() in [v.casefold() for v in [row['text'], *row.get('aliases', [])]]:
            return row
    return None


def identity(row):
    return row['category'], row['text'].casefold()


def extract(introduction, selections=(), sensitive=lambda _: False):
    rows, seen = [], set()
    spans = self_spans(introduction)
    def add(text, category, source, ability='', term=None):
        known = canonical(text, category)
        if known:
            text, category = known['text'], known['category']
            ability = ability or known['abilityId']
        if not known:
            category = 'custom'
        if category not in CATEGORIES or sensitive(text) or sensitive(source):
            return
        key = (category, text.casefold())
        if key in seen:
            return
        seen.add(key)
        error = word_error(text, sensitive)
        keyword = term or text
        offset = max(0, source.casefold().find(keyword.casefold()) - 40)
        rows.append(dict(text=text, category=category, source=source[offset:offset+160],
                         abilityId=ability if not error else '', term=keyword, invalidReason=error))
    for source in spans:
        found = []
        for row in WORDS:
            for label in [row['text'], *row.get('aliases', [])]:
                if row['category'] == 'personality' and re.search(r'喜欢|热爱|欣赏|想要|喜欢的人|\b(?:like|love|prefer|admire)\b', source, re.I):
                    continue
                if row['category'] == 'mbti' and not re.search(r'(?:(?:我|本人)(?:的)?(?:MBTI)?(?:是|为|属于)|MBTI\s*(?:是|为|:|：)|\bI (?:am|identify as)|\bmy (?:MBTI|type) is)\s*'+re.escape(label), source, re.I):
                    continue
                if contains_word(source, label):
                    start = source.casefold().find(label.casefold())
                    found.append((len(label), start, row, label))
        occupied = []
        for length, start, row, label in sorted(found, key=lambda f: -f[0]):
            if any(start < end and start+length > begin for begin, end in occupied):
                continue
            occupied.append((start, start+length))
            add(row['text'], row['category'], source, row['abilityId'], label)
        # Explicit custom identity labels remain visible until the player chooses an ability.
        custom = re.search(r'(?:我的(?:性格|心情|表情)(?:是|为|：|:)|我喜欢的人物表情是|我(?:性格|心情)(?:是|为)|\bmy (?:personality|mood|(?:favorite character |preferred )?expression) is\s+)(.+)', source, re.I)
        if custom:
            for text in re.split(r'、|以及|并且|和|与|及|\band\b', custom.group(1), flags=re.I):
                text = text.strip()
                # Modifiers and conjunctions around a published trait do not create a shadow asset.
                if not any(contains_word(text, label) for row in WORDS for label in [row['text'], *row.get('aliases', [])]):
                    add(text, 'custom', source)
    # Confirmation is evidence-bound and cannot create facts missing from the introduction.
    for selection in selections:
        value = selection.model_dump() if hasattr(selection, 'model_dump') else dict(selection)
        text, category, ability = value.get('text', '').strip(), value.get('category', 'custom'), value.get('abilityId', '')
        source = next((s for s in spans if contains_word(s, text)), None)
        if not source or ability not in ABILITIES | {''} or not safe_word(text, sensitive):
            continue
        known = canonical(text, category)
        key = identity(known) if known else ('custom', text.casefold())
        existing = next((r for r in rows if identity(r) == key), None)
        if existing:
            if ability:
                existing['abilityId'] = ability
        else:
            add(text, category, source, ability)
    return rows


def augment(assets, terms, introduction, selections=(), sensitive=lambda _: False, maximum=12):
    """Replace only word rows, preserve immutable asset IDs, and keep all nonword candidates."""
    old_words = {(a.get('wordCategory'), a.get('wordText', '').casefold()): a for a in assets if a.get('visualId') == 'trait-word'}
    result = [dict(a) for a in assets if a.get('visualId') != 'trait-word'][:maximum]
    rows = extract(introduction, selections, sensitive)
    words = {(r['term'].casefold(), r['text'].casefold()) for r in rows}
    # Remove stale word result states (and canonical aliases) before rebuilding them.
    row_keys = {identity(r) for r in rows}
    def is_word_term(term):
        known = canonical(term.get('term', ''))
        return (term.get('kind') == 'trait-word' or
                any(term.get('term','').casefold() in keys for keys in words) or
                (known is not None and identity(known) in row_keys))
    new_terms = [dict(t) for t in terms if not is_word_term(t)]
    for row in rows:
        term = dict(term=row['term'], source=row['source'], kind='trait-word',
                    wordCategory=row['category'], wordText=row['text'], abilityId=row['abilityId'])
        if row.get('invalidReason'):
            term.update(status='failed', reason=row['invalidReason'], stage='文字不可用')
        elif row['abilityId'] and len(result) < maximum:
            old = old_words.get(identity(row), {})
            result.append(dict(assetId=old.get('assetId') or uuid.uuid4().hex, name=row['text'],
                               templateId='SpecialAbility', visualId='trait-word',
                               color=PALETTE[row['abilityId']], source=row['source'], generationTerm=row['term'],
                               wordCategory=row['category'], wordText=row['text'], abilityId=row['abilityId']))
        elif row['abilityId']:
            term.update(status='failed', reason='已达到12件 Persona 上限，请精简介绍', stage='超过上限')
        else:
            term.update(status='needs-confirmation', reason='请选择能力', stage='请选择能力')
        new_terms.append(term)
    return result, new_terms


def valid_asset(asset, introduction, sensitive=lambda _: False):
    return (asset.get('name') == asset.get('wordText') and asset.get('source') and asset['source'] in introduction and
            asset.get('templateId') == 'SpecialAbility' and asset.get('abilityId') in ABILITIES and
            asset.get('wordCategory') in CATEGORIES and safe_word(asset.get('wordText'), sensitive) and
            any(r['text'] == asset['wordText'] and r['category'] == asset['wordCategory'] for r in
                extract(introduction, [dict(text=asset['wordText'], category=asset['wordCategory'], abilityId=asset['abilityId'])], sensitive)))
