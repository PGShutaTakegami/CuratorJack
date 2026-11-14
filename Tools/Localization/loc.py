"""言語テーブルの TSV（Tools/Localization/<テーブル名>.tsv）を編集する。

  python loc.py apply <patch.tsv>   パッチ（table, key, ja, en, zh-Hans）を取り込む。
                                    空の列は今の値を残す。キーが無ければテーブルの最後に足す。
                                    繁体字は簡体字から OpenCC（s2twp・台湾の言い回し）で作り直す。
  python loc.py check               4 言語のどれかが空の行を出す。
  python loc.py data <patch.tsv>    カード・アイテムの翻訳（asset, en名, en説明, zh-Hans名, zh-Hans説明）に
                                    繁体字を足して CardItemData.txt に書く（language_data_import.csx で取り込む）。

TSV は Unity の .uloop/csx/language_import.csx でテーブルへ取り込み、language_export.csx で書き出す。
改行は \\n、タブは \\t、バックスラッシュは \\\\ で書く。
"""

import os
import sys

from opencc import OpenCC

HERE = os.path.dirname(os.path.abspath(__file__))
HEADER = ["key", "ja", "en", "zh-Hans", "zh-Hant"]
to_traditional = OpenCC("s2twp")


def load(table):
    path = os.path.join(HERE, table + ".tsv")
    rows = []
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            lines = f.read().split("\n")
        for line in lines[1:]:
            if line:
                cols = line.split("\t")
                assert len(cols) == 5, (table, line)
                rows.append(cols)
    return rows


def save(table, rows):
    path = os.path.join(HERE, table + ".tsv")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(HEADER) + "\n")
        for cols in rows:
            f.write("\t".join(cols) + "\n")


def apply(patch_path):
    tables = {}
    with open(patch_path, encoding="utf-8") as f:
        for n, line in enumerate(f.read().split("\n"), 1):
            if not line.strip() or line.startswith("#"):
                continue
            cols = line.split("\t")
            if len(cols) != 5:
                sys.exit(f"{patch_path}:{n}: columns={len(cols)}: {line}")
            table, key, ja, en, zh = cols
            rows = tables.setdefault(table, load(table))
            row = next((r for r in rows if r[0] == key), None)
            if row is None:
                row = [key, "", "", "", ""]
                rows.append(row)
            for i, value in ((1, ja), (2, en), (3, zh)):
                if value:
                    row[i] = value
            if row[3]:
                row[4] = to_traditional.convert(row[3])
    for table, rows in tables.items():
        save(table, rows)
        print(f"{table}: {len(rows)}")


def check():
    missing = 0
    for name in sorted(os.listdir(HERE)):
        if not name.endswith(".tsv"):
            continue
        for cols in load(name[:-4]):
            empty = [HEADER[i] for i in range(1, 5) if not cols[i]]
            if empty:
                missing += 1
                print(f"{name[:-4]}\t{cols[0]}\tmissing {','.join(empty)}")
    print(f"missing rows: {missing}")


def data(patch_path):
    rows = []
    with open(patch_path, encoding="utf-8") as f:
        for n, line in enumerate(f.read().split("\n"), 1):
            if not line.strip() or line.startswith("#"):
                continue
            cols = line.split("\t")
            if len(cols) != 5 or not all(cols):
                sys.exit(f"{patch_path}:{n}: columns={len(cols)}: {line}")
            rows.append(cols + [to_traditional.convert(cols[3]), to_traditional.convert(cols[4])])
    with open(os.path.join(HERE, "CardItemData.txt"), "w", encoding="utf-8", newline="\n") as f:
        for cols in rows:
            f.write("\t".join(cols) + "\n")
    print(f"CardItemData: {len(rows)}")


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "apply":
        apply(sys.argv[2])
    elif len(sys.argv) >= 3 and sys.argv[1] == "data":
        data(sys.argv[2])
    elif len(sys.argv) >= 2 and sys.argv[1] == "check":
        check()
    else:
        print(__doc__)
