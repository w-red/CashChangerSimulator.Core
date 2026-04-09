import json
import os
import re

def extract_stryker_json(html_path):
    with open(html_path, 'r', encoding='utf-8') as f:
        content = f.read()
    match = re.search(r'report\s*=\s*(.*?});', content, re.DOTALL)
    if match:
        return json.loads(match.group(1))
    return None

report_path = r"test\StrykerOutput\2026-04-08.21-18-14\reports\mutation-report.html"
data = extract_stryker_json(report_path)

if data:
    files = data.get('files', {})
    found = False
    for path, info in files.items():
        if "DepositController.cs" in path:
            found = True
            mutants = info.get('mutants', [])
            survived = [m for m in mutants if m.get('status') == 'Survived']
            print(f"\n--- Survived Mutants in {os.path.basename(path)} ({len(survived)}) ---")
            for m in survived:
                loc = m.get('location', {}).get('start', {})
                print(f"ID: {m.get('id')}, Line: {loc.get('line')}, Mutator: {m.get('mutatorName')}")
    if not found:
        print("DepositController.cs not found in report.")
else:
    print("Failed to extract data.")
