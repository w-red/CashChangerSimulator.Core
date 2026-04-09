import json
import os

def find_json_in_html(file_path):
    try:
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
    except: return None

base_path = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
dirs = sorted([d for d in os.listdir(base_path) if os.path.isdir(os.path.join(base_path, d))])

for d in dirs[-5:]:
    report_path = os.path.join(base_path, d, "reports", "mutation-report.html")
    if os.path.exists(report_path):
        data = find_json_in_html(report_path)
        if data:
            print(f"Session: {d}")
            for f, file_data in data.get('files', {}).items():
                if "DepositController.cs" in f:
                    from collections import Counter
                    counts = Counter([m.get('status') for m in file_data.get('mutants', [])])
                    print(f"  {f} -> {counts}")
