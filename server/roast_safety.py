"""Deterministic minimum safety gate for publicly displayed roast text."""
import re


PRIVATE_PATTERNS = (
    re.compile(r'[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}', re.I),
    re.compile(r'(?:https?://|www\.)\S+', re.I),
    re.compile(r'(?<!\d)1[3-9]\d{9}(?!\d)'),
    re.compile(r'(?<!\d)\d{17}[\dXx](?!\d)'),
    re.compile(r'(?:微信(?:号|联系)?|QQ(?:号)?|手机号|电话号码|身份证(?:号)?|家庭住址|详细地址|银行卡(?:号)?)', re.I),
)
BLOCKED_TERMS = (
    '傻逼', '傻比', '操你', '妈的', '去死', '自杀', '杀光', '强奸', '色情',
    '纳粹', '种族灭绝', '人肉搜索', '开盒', '贱人', '婊子', '废物',
)


def safe_public_text(value):
    text = str(value or '').strip()
    if not text or any(ord(char) < 32 for char in text):
        return False
    if any(pattern.search(text) for pattern in PRIVATE_PATTERNS):
        return False
    return not any(term in text.casefold() for term in BLOCKED_TERMS)


def safe_roast(value):
    text = str(value or '').strip()
    visible = re.sub(r'\s+', '', text)
    if not 15 <= len(visible) <= 40 or not safe_public_text(text):
        return False
    # A single line of ordinary Chinese text: no handles, links, markup or digits.
    if re.search(r'[A-Za-z0-9@#<>\\/{}\[\]]', text):
        return False
    return text.count('。') + text.count('！') + text.count('？') <= 1
