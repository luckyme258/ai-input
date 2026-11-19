#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
HSK 词库去重 / 合规过滤（complete.json 缺失时，从预过滤 TSV 备份重建）。

输入 TSV 列：word<TAB>pinyin<TAB>initials<TAB>baseFreq
规则：
  - 命中剔除表即跳过；
  - 同词去重，保留首个读音形态；
  - 校验四列、拼音/首字母非空。

用法：
  python tools/Convert-HskDict.py --source hsk-basic.prefilter.tsv.bak \
      --out hsk-basic.tsv --blocklist blocklist-political.txt
"""
import argparse
from pathlib import Path


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--blocklist")
    args = ap.parse_args()

    blocklist: set[str] = set()
    if args.blocklist:
        for line in Path(args.blocklist).read_text(encoding="utf-8").splitlines():
            w = line.strip()
            if w and not w.startswith("#"):
                blocklist.add(w)

    seen: set[str] = set()
    rows: list[str] = []
    skipped_block = skipped_bad = skipped_dup = 0

    for raw in Path(args.source).read_text(encoding="utf-8").splitlines():
        if not raw:
            continue
        p = raw.split("\t")
        if len(p) < 4:
            skipped_bad += 1
            continue
        word, pinyin, initials, freq = p[0], p[1], p[2], p[3]
        if not word or not pinyin or not initials:
            skipped_bad += 1
            continue
        if word in blocklist:
            skipped_block += 1
            continue
        if word in seen:
            skipped_dup += 1
            continue
        seen.add(word)
        rows.append(f"{word}\t{pinyin}\t{initials}\t{freq}")

    Path(args.out).write_text("\n".join(rows) + "\n", encoding="utf-8")
    print(f"written: {len(rows)} rows; skipped(blocklist): {skipped_block}; "
          f"skipped(dup): {skipped_dup}; skipped(bad): {skipped_bad} -> {args.out}")


if __name__ == "__main__":
    main()
