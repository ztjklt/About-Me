"""Conservative speaker and polarity checks for quoted Persona source spans.

The model may return only a short noun, so attribution is read from the original
introduction around every occurrence of that exact span, not from source alone.
"""
import re


STRONG_BREAK = re.compile(r'[。；;！!？?\n]')
CLAUSE_BREAK = re.compile(r'[，,。；;！!？?\n]|但是|不过|然而|可是|却|但')
PEOPLE = (r'(?:其他人|男朋友|女朋友|妹妹|姐姐|哥哥|弟弟|朋友|同学|室友|老师|'
          r'父母|爸爸|妈妈|家人|亲戚|伴侣|同事|学长|学姐|他们|她们|'
          r'(?<![吉其])(?:他|她)|'
          r'别人|有人|(?<![A-Za-z])TA(?![A-Za-z]))')
SELF = r'(?:我|本人|自己|我们|咱们)'
JOINT = rf'(?:{SELF}(?:和|跟|与|同|及){PEOPLE}|(?:我(?:的)?{PEOPLE}|{PEOPLE})(?:和|跟|与|同|及){SELF})'
SUBJECT = re.compile(rf'(?P<joint>{JOINT})|(?P<other>(?:我(?:的)?|我的)?{PEOPLE})|(?P<self>{SELF})', re.I)
PREDICATE = re.compile(r'喜欢|热爱|擅长|学习|就读于|毕业于|来自|讨厌|反感|拒绝|爱|会|踢|玩|用|弹|拍|写|做|养|有|学|读|是')
NEGATIVE_PREFIX = re.compile(r'(?:不再|从不|绝不|并非|不是|不|没|未|无|别)(?:太|再|会|能|想|愿意|很)?\s*$')
UNCERTAIN = re.compile(r'如果|假如|假设|也许|可能|或许|希望|假装|听说|据说|朋友说')
QUESTION_TAIL = re.compile(r'^\s*(?:吗|么|是否|是不是)')
CURRENT = re.compile(r'现在|目前|最近|如今|已经|不再')
PAST = re.compile(r'以前|曾经|过去|从前|原来')
ELLIPTIC_CURRENT = re.compile(
    r'(?:(?:现在|目前|最近|如今|已经)\s*(?P<negative>不再|不|没|未|无)?|(?P<not_again>不再))'
    r'.{0,3}(?:喜欢|爱|踢|玩|用|弹|拍|做|学|养|有)(?:了|啦)?\s*$')


def _parts(introduction):
    start = 0
    for boundary in CLAUSE_BREAK.finditer(introduction):
        if start < boundary.start():
            yield start, boundary.start()
        start = boundary.end()
    if start < len(introduction):
        yield start, len(introduction)


def _sentence_start(introduction, position):
    breaks = list(STRONG_BREAK.finditer(introduction, 0, position))
    return breaks[-1].end() if breaks else 0


def _speaker(prefix):
    state = 'self'  # A bare introductory phrase such as “喜欢摄影” speaks for the player.
    explicit = False
    for match in SUBJECT.finditer(prefix):
        explicit = True
        if match.lastgroup in ('self', 'joint'):
            state = 'self'
        else:
            # “跟朋友拍照” is a companion phrase; “妹妹喜欢...” is a new subject.
            preceding = prefix[match.start()-1:match.start()]
            following = prefix[match.end():]
            companion = (preceding in ('跟', '和', '与', '同') and state == 'self'
                         and not re.match(r'(?:也|都)?(?:喜欢|热爱|爱|会|擅长)', following))
            if not companion:
                state = 'other'
    if not explicit and prefix.lstrip().startswith(('也', '还', '又')):
        # A fresh sentence starting with an anaphor has no safe subject to inherit.
        return 'unknown'
    return state


def _polarity(clause_prefix):
    predicates = list(PREDICATE.finditer(clause_prefix))
    if not predicates:
        return True, 'unspecified'
    predicate = predicates[-1]
    verb = predicate.group()
    if verb in ('讨厌', '反感', '拒绝'):
        return False, 'negative'
    before = clause_prefix[:predicate.start()]
    if NEGATIVE_PREFIX.search(before) and not re.search(r'不(?:仅|只|但|光|止)\s*$', before):
        return False, 'ownership' if verb == '有' else 'negative'
    return True, 'preference' if verb in ('喜欢', '热爱', '爱') else 'other'


def _claim(introduction, part_start, part_end, at):
    sentence_start = _sentence_start(introduction, part_start)
    sentence_prefix = introduction[sentence_start:at]
    clause_prefix = introduction[part_start:at]
    if _speaker(sentence_prefix) != 'self' or UNCERTAIN.search(clause_prefix):
        return None
    if (QUESTION_TAIL.match(introduction[at:part_end]) or
        re.search(r'(?:吗|么|是否|是不是)\s*$', clause_prefix) or
        (part_end < len(introduction) and introduction[part_end] in '？?')):
        return None
    positive, kind = _polarity(clause_prefix)
    temporal = 'current' if CURRENT.search(clause_prefix) else 'past' if PAST.search(clause_prefix) else 'plain'
    return (positive, kind, temporal)


def _later_elliptic_update(introduction, end):
    """Read an object-free current-state update in this or the next sentence."""
    this_end = STRONG_BREAK.search(introduction, max(0, end - 1))
    next_end = STRONG_BREAK.search(introduction, this_end.end()) if this_end else None
    limit = next_end.start() if next_end else len(introduction)
    updates = []
    for a, b in _parts(introduction):
        if a < end or a >= limit:
            continue
        end_of_part = min(b, limit)
        match = ELLIPTIC_CURRENT.search(introduction[a:end_of_part])
        if match and _speaker(introduction[_sentence_start(introduction, a):end_of_part]) == 'self':
            positive = not (match.group('negative') or match.group('not_again'))
            updates.append((positive, 'negative' if not positive else 'preference', 'current'))
    return updates[-1] if updates else None


def source_is_own_positive_fact(introduction: str, source: str) -> bool:
    """Require an affirmative first-person assertion around an exact source span."""
    if not isinstance(introduction, str) or not isinstance(source, str) or not source:
        return False
    assertions = []
    offset = 0
    parts = list(_parts(introduction))
    while (start := introduction.find(source, offset)) >= 0:
        end = start + len(source)
        offset = start + 1
        overlapping = [(a, b) for a, b in parts if a < end and b > start]
        if not overlapping:
            continue
        claims = [_claim(introduction, a, b, min(end, b)) for a, b in overlapping]
        if any(claim is None for claim in claims):
            continue
        # A model source spanning multiple claims must not hide a negative one.
        for claim in claims:
            assertions.append(claim)
        if update := _later_elliptic_update(introduction, end):
            assertions.append(update)
    if not assertions:
        return False
    current = [claim for claim in assertions if claim[2] == 'current']
    if current:
        return current[-1][0]
    positive = [claim for claim in assertions if claim[0]]
    negatives = [claim for claim in assertions if not claim[0] and claim[1] != 'ownership']
    return bool(positive) and not negatives
