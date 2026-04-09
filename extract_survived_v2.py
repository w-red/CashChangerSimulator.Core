import json
import os

def find_json_in_html(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
        search_str = ".report ="
        start_idx = content.find(search_str)
        if start_idx == -1: return None
        start_idx = content.find('{', start_idx)
        count = 0
        json_chars = []
        for i in range(start_idx, len(content)):
            char = content[i]
            if char == '{': count += 1
            elif char == '}': count -= 1
            json_chars.append(char)
            if count == 0: break
        return json.loads("".join(json_chars))

base_path = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
latest_dir = sorted([d for d in os.listdir(base_path) if os.path.isdir(os.path.join(base_path, d))])[-1]
report_path = os.path.join(base_path, latest_dir, "reports", "mutation-report.html")

data = find_json_in_html(report_path)
if data:
    for f, file_data in data.get('files', {}).items():
        if "DepositController.cs" in f:
            print(f"File: {f}")
            mutants = file_data.get('mutants', [])
            statuses = [m.get('status') for m in mutants]
            from collections import Counter
            counts = Counter(statuses)
            print(f"Counts: {counts}")
            for m in mutants:
                if m.get('status') == 'Survived':
                    loc = m['location']['start']
                    print(f"  Survived: Line {loc['line']} | {m['mutatorName']} -> {m['replacement']}")
else:
    print("No data found.")
