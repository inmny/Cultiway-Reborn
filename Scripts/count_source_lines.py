import glob
import argparse
import chardet

parser = argparse.ArgumentParser()
parser.add_argument('--source', type=str, required=True)
args = parser.parse_args()
source_path = args.source

total_lines = 0
total_files = 0

def check_encoding(file_path):
    with open(file_path, 'rb') as file:
        rawdata = file.read(4)
    result = chardet.detect(rawdata)
    return result['encoding']

for f in glob.glob(f'{source_path}/**/*.cs', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        total_lines += len(file.readlines())
        total_files += 1
    
    pass

print(f'Total lines: {total_lines}')
print(f'Total files: {total_files}')
    