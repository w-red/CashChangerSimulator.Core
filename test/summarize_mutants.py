
import json
import re

html_path = 'c:/Users/ITI202301003_User/source/repos/w-red/CashChangerSimulator.Core/test/StrykerOutput/2026-04-09.01-46-17/reports/mutation-report.html'

with open(html_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find the report JSON
match = re.search(r'report = (\{.*?\});\n', content, re.DOTALL)
if not match:
    # Try different pattern if the above fails
    match = re.search(r'report = (\{.*?\})', content, re.DOTALL)

if match:
    report = json.loads(match.group(1))
    survived = []
    
    for file_path, file_data in report['files'].items():
        for mutant in file_data['mutants']:
            if mutant['status'] == 'Survived':
                survived.append({
                    'id': mutant['id'],
                    'mutator': mutant['mutatorName'],
                    'replacement': mutant.get('replacement', 'N/A'),
                    'line': mutant['location']['start']['line'],
                    'file': file_path.split('\\')[-1]
                })
    
    print(f"Total Survived: {len(survived)}")
    print("\nSample Survived Mutants:")
    for m in survived[:20]:
        print(f"Line {m['line']}: {m['mutator']} -> {m['replacement']}")
else:
    print("Could not find report JSON")
