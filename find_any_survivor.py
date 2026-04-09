import json
import os

def find_survived(path, label):
    try:
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
                    if survived:
                        print(f"[{label}] Found {len(survived)} survived mutants in {f}")
                        for m in survived:
                            print(f"  Line {m['location']['start']['line']}: {m['mutatorName']} -> {m['replacement']}")
                        return True
    except: pass
    return False

base = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
dirs = sorted([d for d in os.listdir(base) if os.path.isdir(os.path.join(base, d))], reverse=True)

for d in dirs:
    path = os.path.join(base, d, "reports", "mutation-report.html")
    if os.path.exists(path):
        if find_survived(path, d):
            # break # Found the latest one with results
            pass
