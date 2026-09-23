"""Deterministic cosmetic unlocks derived from the player's own introduction."""
import re


DEEPSEEK_LIKE = re.compile(r'(?:喜欢|喜歡)\s*deep\s*seek(?![A-Za-z])', re.I)
CLAUSE_BREAK = re.compile(r'[，,。；;！!？?\n]|但是|不过|然而|可是|却|但')
NEGATIVE_PREFIX = re.compile(r'(?:不|没|未|无|别|讨厌|反感|拒绝|假装|并非|不是|谈不上).{0,4}$')
UNCERTAIN_PREFIX = re.compile(r'(?:如果|假如|可能|也许|或许|据说|听说|假设).{0,8}$')
OTHER_SUBJECT = re.compile(r'(?:朋友|同学|室友|别人|其他人|他们|她们|他|她|有人|用户)')
SELF_SUBJECT = re.compile(r'(?:我|本人|自己)')
SHARED_SUBJECT = re.compile(r'(?:我|本人|自己)(?:和|跟|与)(?:朋友|同学|室友|别人)')


def likes_deepseek(introduction: str) -> bool:
    """Accept an explicit positive like; the latest first-person mention wins."""
    if not isinstance(introduction, str):
        return False
    stance = False
    for match in DEEPSEEK_LIKE.finditer(introduction):
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
        stance = not bool(NEGATIVE_PREFIX.search(clause))
    return stance
