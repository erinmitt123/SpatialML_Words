"""Builds Assets/Data/vocabulary.json from the vocabulary sheet, Assets/Languages/vocabulary.csv.

The sheet is the source of truth: every word, article, phrase, sentence, pinyin and meaning is
written out in it by hand. Nothing is generated here; this script only checks the sheet and
converts it. One row per YOLOX class (0-79), one column per field.

  Level 1 (word):     <lang>_word        la silla        / zh_pinyin, zh_characters
  Level 2 (phrase):   <lang>_phrase      una silla       / zh_phrase + zh_phrasePinyin
  Level 3 (sentence): <lang>_sentence    Siéntate en la silla, por favor. / zh_sentence
  Sentence meaning:   <lang>_sentenceMeaning  the example in the learner's own language (English
                      for zh/es/fr/de, Chinese for en), shown under the example on the word card

Editing: open vocabulary.csv in a spreadsheet, change cells, save as "CSV UTF-8" (keeps the
accents, tone marks and characters), then run
  py -3 Assets/Languages/build_vocab.py Assets/Data/vocabulary.json
Set "reviewed" to TRUE once a native speaker has checked a row.
"""
import csv, json, os, sys

SHEET = os.path.join(os.path.dirname(os.path.abspath(__file__)), "vocabulary.csv")

LATIN = ["word", "article", "noun", "gender", "plural", "phrase", "sentence", "sentenceMeaning", "meaning", "alternatives"]
ZH = ["characters", "pinyin", "measureWord", "measureWordPinyin", "phrase", "phrasePinyin",
      "sentence", "sentencePinyin", "sentenceMeaning", "meaning"]
BOOLS = {"enabled", "reviewed", "plural"}
# Cells that may be empty: English has no article or gender, and most words have no alternative.
OPTIONAL = {"article", "noun", "gender", "alternatives"}
# Needed only on enabled rows: disabled classes are never shown, so their meanings are not written yet.
ENABLED_ONLY = {"sentenceMeaning"}


def parse_bool(value, where):
    v = value.strip().upper()
    if v in ("TRUE", "1", "YES"):
        return True
    if v in ("FALSE", "0", "NO", ""):
        return False
    raise ValueError(f"{where}: expected TRUE or FALSE, got '{value}'")


def read_block(row, lang, fields, number, enabled):
    block = {}
    for field in fields:
        column = f"{lang}_{field}"
        if column not in row:
            raise ValueError(f"sheet is missing column {column}")
        value = row[column].strip()
        where = f"row {number} ({row['cocoLabel']}) {column}"
        if field in BOOLS:
            block[field] = parse_bool(value, where)
            continue
        if not value and field not in OPTIONAL and (enabled or field not in ENABLED_ONLY):
            raise ValueError(f"{where} is empty; every word, phrase and sentence must be written in the sheet")
        block[field] = value
    return block


def main(out):
    with open(SHEET, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    entries, seen = [], set()
    for number, row in enumerate(rows, start=2):
        index = int(row["classIndex"])
        if index in seen or not 0 <= index <= 79:
            raise ValueError(f"row {number}: classIndex {index} is duplicated or out of range")
        seen.add(index)
        enabled = parse_bool(row["enabled"], f"row {number} enabled")
        entries.append({
            "classIndex": index,  # YOLOX output index (0-79)
            "cocoLabel": row["cocoLabel"].strip(),
            "category": row["category"].strip(),
            "roomFit": row["roomFit"].strip(),
            "enabled": enabled,
            "reviewed": parse_bool(row["reviewed"], f"row {number} reviewed"),
            "en": read_block(row, "en", LATIN, number, enabled),
            "zh": read_block(row, "zh", ZH, number, enabled),
            "es": read_block(row, "es", LATIN, number, enabled),
            "fr": read_block(row, "fr", LATIN, number, enabled),
            "de": read_block(row, "de", LATIN, number, enabled),
        })
    entries.sort(key=lambda e: e["classIndex"])
    db = {
        "version": 1,
        "model": "YOLOX-M (COCO 80 classes)",
        "notes": "Built from Assets/Languages/vocabulary.csv; edit the sheet, not this file. "
                 "classIndex matches the YOLOX output index (0-79). Levels: 1 = word, 2 = phrase, 3 = sentence. "
                 "Mandarin levels use pinyin -> characters with pinyin -> characters only. "
                 "Spanish is Mexican Spanish; the Spain word, where different, is in 'alternatives'. All entries need native-speaker review.",
        "entries": entries,
    }
    with open(out, "w", encoding="utf-8") as f:
        json.dump(db, f, ensure_ascii=False, indent=2)
    reviewed = sum(e["reviewed"] for e in entries)
    print(f"wrote {len(entries)} entries, {sum(e['enabled'] for e in entries)} enabled, {reviewed} reviewed")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(SHEET), "..", "Data", "vocabulary.json"))
