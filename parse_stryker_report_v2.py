import json
import os

def find_json_in_html(file_path):
    if not os.path.exists(file_path):
        print(f"File not found: {file_path}")
        return None

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Look for the start of the JSON object.
            # Stryker usually puts it after document.querySelector('mutation-test-report-app').report = 
            search_str = ".report ="
            start_idx = content.find(search_str)
            if start_idx == -1:
                # Try another common pattern
                search_str = "const report ="
                start_idx = content.find(search_str)
            
            if start_idx == -1:
                # Try finding where schemaVersion starts
                start_idx = content.find('{"schemaVersion"')
                if start_idx == -1:
                    return None
            else:
                start_idx = content.find('{', start_idx)
            
            # Extract JSON until balanced
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
    dirs = [d for d in os.listdir(base_path) if os.path.isdir(os.path.join(base_path, d))]
    if not dirs:
        print("No report directories.")
        return
    
    latest_dir = sorted(dirs)[-1]
    report_path = os.path.join(base_path, latest_dir, "reports", "mutation-report.html")
    print(f"Parsing: {report_path}")
    
    data = find_json_in_html(report_path)
    if not data:
        print("Failed to extract JSON.")
        return
    
    target_files = ["DepositController.cs", "DispenseController.cs"]
    for rel_path, file_data in data.get('files', {}).items():
        if any(t in rel_path for t in target_files):
            survived = [m for m in file_data.get('mutants', []) if m.get('status') == 'Survived']
            if survived:
                print(f"\n{rel_path}: {len(survived)} survived mutants")
                for m in survived:
                    msg = f"  Line {m['location']['start']['line']}:{m['location']['start']['column']} | {m['mutatorName']} -> {m['replacement']}"
                    print(msg)

if __name__ == "__main__":
    main()
