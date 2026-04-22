import json
import re
import os

report_path = r'test/StrykerOutput/2026-04-08.21-18-14/reports/mutation-report.html'

if not os.path.exists(report_path):
    print(f"Error: {report_path} not found.")
    exit(1)

print(f"Reading {report_path}...")
with open(report_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 巨大なreport = {...}; の部分を抽出
match = re.search(r'report\s*=\s*({.*?});</script>', content, re.DOTALL)
if not match:
    print("Error: Could not find 'report =' in the HTML.")
    exit(1)

json_str = match.group(1)
try:
    data = json.loads(json_str)
except Exception as e:
    print(f"Error parsing JSON: {e}")
    # ファイルに書き出してデバッグ
    with open('debug_json.txt', 'w', encoding='utf-8') as df:
        df.write(json_str[:2000])
    exit(1)

survivors = []
for file_name, file_data in data.get('files', {}).items():
    for mutant in file_data.get('mutants', []):
        if mutant.get('status') == 'Survived':
            survivors.append({
                'id': mutant.get('id'),
                'mutator': mutant.get('mutatorName'),
                'replacement': mutant.get('replacement'),
                'location': mutant.get('location'),
                'file': file_name
            })

print(f"Found {len(survivors)} survivors.")
print(json.dumps(survivors, indent=2))
