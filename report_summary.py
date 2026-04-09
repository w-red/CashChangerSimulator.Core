import json
import os

def find_json_in_html(file_path):
    if not os.path.exists(file_path):
        return None

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            start_idx = content.find('{"schemaVersion"')
            if start_idx == -1:
                return None
            
            count = 0
            json_chars = []
            for i in range(start_idx, len(content)):
                char = content[i]
                if char == '{': count += 1
                elif char == '}': count -= 1
                json_chars.append(char)
                if count == 0: break
            
            return json.loads("".join(json_chars))
    except Exception as e:
        print(f"Error: {e}")
        return None

def main():
    base_path = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
    dirs = sorted([d for d in os.listdir(base_path) if os.path.isdir(os.path.join(base_path, d))])
    if not dirs:
        return
    
    report_path = os.path.join(base_path, dirs[-1], "reports", "mutation-report.html")
    data = find_json_in_html(report_path)
    if not data:
        print("No data.")
        return
    
    print(f"Schema Version: {data.get('schemaVersion')}")
    print(f"Files in report: {len(data.get('files', {}))}")
    
    for rel_path, file_data in data.get('files', {}).items():
        mutants = file_data.get('mutants', [])
        stats = {}
        for m in mutants:
            st = m.get('status')
            stats[st] = stats.get(st, 0) + 1
        print(f"File: {rel_path} | Stats: {stats}")

if __name__ == "__main__":
    main()
