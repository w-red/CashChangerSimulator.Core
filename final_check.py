import json
import os

def extract(path, label):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
        search_str = ".report ="
        start_idx = content.find(search_str)
        if start_idx == -1: return
        start_idx = content.find('{', start_idx)
        count = 0
        json_chars = []
        for i in range(start_idx, len(content)):
            char = content[i]
            if char == '{': count += 1
            elif char == '}': count -= 1
            json_chars.append(char)
            if count == 0: break
        data = json.loads("".join(json_chars))
        for f, file_data in data.get('files', {}).items():
            if "DepositController.cs" in f:
                survived = [m for m in file_data.get('mutants', []) if m.get('status') == 'Survived']
                print(f"[{label}] {f}: Survived={len(survived)}, Total={len(file_data.get('mutants', []))}")
                for m in survived[:10]:
                    print(f"  Line {m['location']['start']['line']}: {m['mutatorName']} -> {m['replacement']}")

base = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
extract(os.path.join(base, "2026-04-08.21-18-14", "reports", "mutation-report.html"), "21-18-14")
extract(os.path.join(base, "2026-04-08.21-11-59", "reports", "mutation-report.html"), "21-11-59")
extract(os.path.join(base, "2026-04-08.20-44-37", "reports", "mutation-report.html"), "20-44-37")
