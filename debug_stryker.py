import json
import os

def find_json_in_html(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
        search_str = ".report ="
        start_idx = content.find(search_str)
        if start_idx == -1: 
            search_str = "const report ="
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

print(f"Analyzing: {report_path}")
data = find_json_in_html(report_path)
if data:
    files = data.get('files', {})
    print(f"Total files in report: {len(files)}")
    for f in files.keys():
        stats = {}
        for m in files[f].get('mutants', []):
            s = m.get('status')
            stats[s] = stats.get(s, 0) + 1
        print(f"  {f} -> {stats}")
else:
    print("No data found.")
