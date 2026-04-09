import json
import re
import sys
import os

def parse_stryker_report(file_path):
    if not os.path.exists(file_path):
        print(f"File not found: {file_path}")
        return

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            html_content = f.read()
    except Exception as e:
        print(f"Error reading file: {e}")
        return

    # Stryker reports usually contain the JSON in a script tag
    match = re.search(r'\{[^{]*"schemaVersion"[^{]*"files":.*\}', html_content, re.DOTALL)
    if not match:
        # Try finding everything between { and } that looks like a report
        match = re.search(r'(\{.*"files".*\})', html_content, re.DOTALL)
        if not match:
            print("Could not find JSON report in HTML.")
            return

    try:
        content = match.group(1)
        # Find the end of the JSON by balancing braces
        count = 0
        json_str = ""
        for i, char in enumerate(content):
            if char == '{': count += 1
            elif char == '}': count -= 1
            json_str += char
            if count == 0 and json_str: break
        
        data = json.loads(json_str)
    except Exception as e:
        print(f"Error parsing JSON: {e}")
        return

    target_files = ["DepositController.cs", "DispenseController.cs"]
    
    print(f"Surviving mutants in targeted files:")
    found_any = False
    files_dict = data.get('files', {})
    for rel_path in files_dict:
        if any(target in rel_path for target in target_files):
            file_data = files_dict[rel_path]
            mutants = file_data.get('mutants', [])
            survived_mutants = [m for m in mutants if m.get('status') == 'Survived']
            
            if survived_mutants:
                print(f"\nFile: {rel_path}")
                found_any = True
                for m in survived_mutants:
                    line = m.get('location', {}).get('start', {}).get('line', '?')
                    col = m.get('location', {}).get('start', {}).get('column', '?')
                    mutator = m.get('mutatorName', 'Unknown')
                    replacement = m.get('replacement', '')
                    print(f"  Line {line}:{col} | Mutator: {mutator} | Replacement: {replacement}")
    
    if not found_any:
        print("\nNo surviving mutants found for targeted files in the report.")

if __name__ == "__main__":
    # Get all subdirectories in StrykerOutput
    base_path = "c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput"
    if os.path.exists(base_path):
        dirs = [d for d in os.listdir(base_path) if os.path.isdir(os.path.join(base_path, d))]
        if dirs:
            latest_dir = sorted(dirs)[-1]
            path = os.path.join(base_path, latest_dir, "reports", "mutation-report.html")
            print(f"Analyzing most recent report: {path}")
            parse_stryker_report(path)
        else:
            print("No Stryker output directories found.")
    else:
        print("StrykerOutput directory not found.")
