"""Deterministic cosmetic unlocks derived from the player's own introduction."""
import re


LIKE_BRANDS = re.compile(
    r'(?:喜欢|喜歡)\s*(?P<first>deep\s*seek|claude)(?![A-Za-z])'
    r'(?:\s*(?:和|跟|与|及|、)\s*(?P<second>deep\s*seek|claude)(?![A-Za-z]))?', re.I)
CLAUSE_BREAK = re.compile(r'[，,。；;！!？?\n]|但是|不过|然而|可是|却|但')
NEGATIVE_PREFIX = re.compile(r'(?:不|没|未|无|别|讨厌|反感|拒绝|假装|并非|不是|谈不上).{0,4}$')
UNCERTAIN_PREFIX = re.compile(r'(?:如果|假如|可能|也许|或许|据说|听说|假设).{0,8}$')
OTHER_SUBJECT = re.compile(r'(?:朋友|同学|室友|别人|其他人|他们|她们|他|她|有人|用户)')
SELF_SUBJECT = re.compile(r'(?:我|本人|自己)')
SHARED_SUBJECT = re.compile(r'(?:我|本人|自己)(?:和|跟|与)(?:朋友|同学|室友|别人)')

# Exact first-person statements emitted by the optional English questionnaire.
ENGLISH_LIKES = re.compile(
    r"\bI\s+(?P<negative>do\s+not\s+|don't\s+|never\s+)?"
    r"(?P<verb>like|love|prefer|use|dislike|hate)\s+"
    r"(?P<first>deep\s*seek|claude)(?![A-Za-z])"
    r"(?:\s*(?:and|,)\s*(?P<second>deep\s*seek|claude)(?![A-Za-z]))?", re.I)
ENGLISH_UNCERTAIN = re.compile(r"\b(?:if|perhaps|maybe|suppose|supposing)\s*$", re.I)


def liked_brands(introduction: str) -> dict[str, bool]:
    """Accept explicit positive likes; each brand's latest mention wins."""
    stance = {'deepseek': False, 'claude': False}
    if not isinstance(introduction, str):
        return stance
    events = []
    for match in LIKE_BRANDS.finditer(introduction):
        clause = CLAUSE_BREAK.split(introduction[:match.start()])[-1]
        suffix = introduction[match.end():]
        tail = re.split(r'[，,。；;！!？?\n]', suffix, maxsplit=1)[0]
        other = list(OTHER_SUBJECT.finditer(clause))
        self_mentions = list(SELF_SUBJECT.finditer(clause))
        if other and (not self_mentions or
                      (other[-1].start() > self_mentions[-1].start() and not SHARED_SUBJECT.search(clause))):
            continue
        if (UNCERTAIN_PREFIX.search(clause) or re.match(r'\s*(?:吗|麼|么|是不是|是否)', tail)
                or re.match(r'\s*[？?]', suffix)):
            continue
        positive = not bool(NEGATIVE_PREFIX.search(clause))
        for brand in (match.group('first'), match.group('second')):
            if brand:
                events.append((match.start(), re.sub(r'\s+', '', brand).lower(), positive))
    for match in ENGLISH_LIKES.finditer(introduction):
        prefix = introduction[:match.start()]
        suffix = introduction[match.end():]
        # Conditional/quoted mentions and questions are not preferences.
        if ENGLISH_UNCERTAIN.search(prefix) or (prefix.rstrip().endswith(('"', '“', "'"))) or re.match(r'\s*[?？]', suffix):
            continue
        positive = not match.group('negative') and match.group('verb').lower() not in ('dislike', 'hate')
        for brand in (match.group('first'), match.group('second')):
            if brand:
                events.append((match.start(), re.sub(r'\s+', '', brand).lower(), bool(positive)))
    for _, brand, positive in sorted(events):
        stance[brand] = positive
    return stance


def likes_deepseek(introduction: str) -> bool:
    return liked_brands(introduction)['deepseek']


def likes_claude(introduction: str) -> bool:
    return liked_brands(introduction)['claude']
