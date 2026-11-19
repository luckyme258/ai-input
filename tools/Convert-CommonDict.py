#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
合成宽松协议通用词库（jieba 大词表 + mozillazg 字拼音表，均为 MIT）。

输入：
  -  jieba dict.txt.big：word <空格> freq <空格> tag
  -  pinyin.txt：U+XXXX: pinyin(,多音)  # 字
输出 TSV 列：word<TAB>pinyin(无声调全拼,空格分隔)<TAB>initials(首字母串)<TAB>baseFreq

规则：
  - 多音字取首读音；任一汉字查不到拼音则跳过该词；
  - 仅保留词频 >= min_freq、长度 <= max_len 的中文词；
  - 默认剔除含繁体字的词条（zhconv 转简体后与原词不一致即跳过）；
  - --blocklist 指定逐词一行的剔除表，命中即跳过（合规审核用，表文件不入库）；
  - 同词去重，保留最高词频；输出按词频降序。

依赖：简体过滤需要 zhconv（纯 Python）：pip install zhconv

用法：
  python tools/Convert-CommonDict.py --jieba dict.txt.big --pinyin pinyin.txt \
      --out common-mit.tsv [--min-freq 20] [--max-len 6] [--keep-traditional] \
      [--blocklist blocklist.txt]
"""

import argparse
import re
import unicodedata

TONE_MAP = {
    "ā": "a", "á": "a", "ǎ": "a", "à": "a",
    "ē": "e", "é": "e", "ě": "e", "è": "e",
    "ī": "i", "í": "i", "ǐ": "i", "ì": "i",
    "ō": "o", "ó": "o", "ǒ": "o", "ò": "o",
    "ū": "u", "ú": "u", "ǔ": "u", "ù": "u",
    "ǖ": "v", "ǘ": "v", "ǚ": "v", "ǜ": "v", "ü": "v",
}

HANZI_RE = re.compile(r"[\u4e00-\u9fff]")
PINYIN_LINE_RE = re.compile(r"^U\+([0-9A-Fa-f]+):\s*([^\s#]+)")


def strip_tone(syl: str) -> str:
    out = "".join(TONE_MAP.get(ch, ch) for ch in syl)
    return out.lower()


def load_char_pinyin(path: str) -> dict:
    table: dict[str, str] = {}
    with open(path, encoding="utf-8") as fh:
        for raw in fh:
            raw = raw.strip()
            if not raw or raw.startswith("#"):
                continue
            m = PINYIN_LINE_RE.match(raw)
            if not m:
                continue
            code = int(m.group(1), 16)
            if code > 0xFFFF:
                continue
            ch = chr(code)
            first = m.group(2).split(",")[0]
            table[ch] = strip_tone(first)
    return table


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--jieba", required=True)
    ap.add_argument("--pinyin", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--min-freq", type=float, default=20.0)
    ap.add_argument("--max-len", type=int, default=6)
    ap.add_argument("--keep-traditional", action="store_true",
                    help="保留繁体词条（默认剔除）")
    ap.add_argument("--blocklist", help="逐词一行的剔除表，命中即跳过（合规审核用）")
    args = ap.parse_args()

    char_py = load_char_pinyin(args.pinyin)

    blocklist: set[str] = set()
    if args.blocklist:
        with open(args.blocklist, encoding="utf-8") as fh:
            for line in fh:
                w = line.strip()
                if w and not w.startswith("#"):
                    blocklist.add(w)

    to_simplified = None
    if not args.keep_traditional:
        try:
            import zhconv
        except ImportError as exc:
            raise SystemExit("简体过滤需要 zhconv：pip install zhconv（或加 --keep-traditional 关闭过滤）") from exc
        to_simplified = lambda w: zhconv.convert(w, "zh-cn")

    # word -> (pinyin_spaced, initials, freq)
    by_word: dict[str, tuple[str, str, float]] = {}
    skipped = 0
    skipped_traditional = 0
    skipped_blocklist = 0

    with open(args.jieba, encoding="utf-8") as fh:
        for raw in fh:
            raw = raw.strip()
            if not raw:
                continue
            parts = raw.split()
            if len(parts) < 2:
                continue
            word = parts[0]
            if not (1 <= len(word) <= args.max_len):
                continue
            if not HANZI_RE.search(word):
                continue
            if to_simplified is not None and to_simplified(word) != word:
                skipped_traditional += 1
                continue
            if word in blocklist:
                skipped_blocklist += 1
                continue
            try:
                freq = float(parts[1])
            except ValueError:
                continue
            if freq < args.min_freq:
                continue

            syls: list[str] = []
            inits: list[str] = []
            ok = True
            for ch in word:
                py = char_py.get(ch)
                if py is None:
                    ok = False
                    break
                syls.append(py)
                inits.append(py[0])
            if not ok:
                skipped += 1
                continue

            row = (" ".join(syls), "".join(inits), freq)
            existing = by_word.get(word)
            if existing is None or freq > existing[2]:
                by_word[word] = row

    rows = sorted(by_word.items(), key=lambda kv: kv[1][2], reverse=True)

    with open(args.out, "w", encoding="utf-8", newline="\n") as fh:
        for word, (pinyin, initials, freq) in rows:
            fh.write(f"{word}\t{pinyin}\t{initials}\t{freq:.3g}\n")

    print(f"written: {len(rows)} rows; skipped(no pinyin): {skipped}; "
          f"skipped(traditional): {skipped_traditional}; "
          f"skipped(blocklist): {skipped_blocklist} -> {args.out}")


if __name__ == "__main__":
    main()
