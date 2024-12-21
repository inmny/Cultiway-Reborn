import json
with open('locale.csv', 'r', encoding='gbk') as f:
    lines = f.readlines()
    keys = lines[0].strip().split(',')[1:]
    data = {}
    for key in keys:
        data[key] = {}
    for line in lines[1:]:
        values = line.strip().split(',')
        for i, key in enumerate(keys):
            data[key][values[0]] = values[i+1]
    for key in keys:
        with open(f'{key}.json', 'w', encoding='utf-8') as f:
            json.dump(data[key], f, ensure_ascii=False)