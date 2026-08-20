import csv
import json
import sys
from pathlib import Path


def parse_value(text):
    value = text.strip()
    lowered = value.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    try:
        return int(value)
    except ValueError:
        try:
            return float(value)
        except ValueError:
            return value


def convert_key_value_table(source):
    with source.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != ["parameter", "value"]:
            raise ValueError("平衡表必须使用 parameter,value 两列")
        data = {}
        for row in reader:
            key = row["parameter"].strip()
            if not key:
                continue
            if key in data:
                raise ValueError(f"平衡表参数重复: {key}")
            data[key] = parse_value(row["value"])

    project_root = Path(__file__).resolve().parent.parent
    output = project_root / "Content" / f"{source.stem}.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(output)


def convert_legacy_locale():
    with open("locale.csv", "r", encoding="gbk") as stream:
        lines = stream.readlines()
    keys = lines[0].strip().split(",")[1:]
    data = {key: {} for key in keys}
    for line in lines[1:]:
        values = line.strip().split(",")
        for index, key in enumerate(keys):
            data[key][values[0]] = values[index + 1]
    for key in keys:
        with open(f"{key}.json", "w", encoding="utf-8") as stream:
            json.dump(data[key], stream, ensure_ascii=False)


if __name__ == "__main__":
    if len(sys.argv) == 1:
        convert_legacy_locale()
    elif len(sys.argv) == 2:
        convert_key_value_table(Path(sys.argv[1]).resolve())
    else:
        raise SystemExit("用法: python Scripts/csv2json.py Tables/<file>.csv")
