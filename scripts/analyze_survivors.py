import json
import re
import os
import sys
from pathlib import Path

def find_latest_report(base_dir: Path):
    """StrykerOutput フォルダから最新の mutation-report.html を探索する"""
    report_files = list(base_dir.glob("src/*/StrykerOutput/*/reports/mutation-report.html"))
    if not report_files:
        # 別の可能性のあるパス（プロジェクトルートが src 直下の場合など）
        report_files = list(base_dir.glob("StrykerOutput/*/reports/mutation-report.html"))
    
    if not report_files:
        return None
    
    # 更新日時でソートして最新を返す
    report_files.sort(key=lambda x: x.stat().st_mtime, reverse=True)
    return report_files[0]

def extract_json(html_file: Path):
    if not html_file.exists():
        print(f"File not found: {html_file}")
        return None
        
    with open(html_file, "r", encoding="utf-8") as f:
        content = f.read()
    
    # schemaVersion を目印に探す
    token = '"schemaVersion"'
    token_idx = content.find(token)
    if token_idx == -1:
        return None

    # 目印が含まれている「一番外側の { 」まで遡る
    found_start = -1
    # 実際にはファイルの先頭付近に projectRoot があるはずなので、
    # HTML内の最初の { から始めて、token_idx を含むものを探す
    # ただし Stryker レポートの JSON は通常 script タグ内にあるので、
    # token_idx より前にある最初の { を探すだけで十分な場合が多い
    
    # 確実に全体を拾うため、token_idx よりも前の、最も適切な開始位置を探す
    potential_start = content.rfind("<script>", 0, token_idx)
    if potential_start == -1: potential_start = 0
    
    json_start = content.find("{", potential_start)
    if json_start == -1 or json_start > token_idx:
        # フォールバック: 単純に token_idx から遡る
        json_start = content.rfind("{", 0, token_idx)

    if json_start == -1:
        return None

    # 括弧の対応関係を数えて JSON の終わりを特定する (Stack-based matching)
    brace_count = 0
    end_idx = -1
    for i in range(json_start, len(content)):
        char = content[i]
        if char == "{":
            brace_count += 1
        elif char == "}":
            brace_count -= 1
            if brace_count == 0:
                end_idx = i + 1
                break
                
    if end_idx == -1 or end_idx < token_idx:
        # まだ全体を拾えていない場合は探索範囲を広げる
        return None
        
    return content[json_start:end_idx]

def analyze():
    # プロジェクトルートを特定 (スクラッチフォルダの親)
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent
    
    target_report = None
    
    if len(sys.argv) > 1:
        # 引数があればそれを使用
        target_report = Path(sys.argv[1]).resolve()
    else:
        # 引数がない場合は自動探索
        target_report = find_latest_report(project_root)
        
    if not target_report or not target_report.exists():
        print("Error: Could not find any Stryker mutation report.")
        print("Usage: python analyze_survivors.py [path/to/mutation-report.html]")
        return

    print(f"Analyzing report: {target_report.relative_to(project_root)}")
    json_str = extract_json(target_report)
    
    if not json_str:
        print("Error: Could not extract mutation data from the HTML file.")
        return

    try:
        data = json.loads(json_str)
        files = data.get("files", {})
        
        all_survivors = []
        file_stats = {}
        
        print(f"DEBUG: Found {len(files)} files in report.")
        
        for filepath, file_data in files.items():
            mutants = file_data.get("mutants", [])
            # ステータスが Survived のものを抽出
            survived = [m for m in mutants if m.get("status") == "Survived"]
            
            if not survived:
                continue
            
            # 統計用の名前
            display_name = os.path.basename(filepath)
            file_stats[display_name] = file_stats.get(display_name, 0) + len(survived)
            
            for m in survived:
                all_survivors.append({
                    "file": filepath,
                    "display_name": display_name,
                    "line": m.get("location", {}).get("start", {}).get("line"),
                    "mutator": m.get("mutatorName"),
                    "replacement": m.get("replacement"),
                })

        # サマリー表示
        print("\n=== Survival Statistics ===")
        if not file_stats:
            print("Perfect score! No mutants survived.")
            return

        sorted_stats = sorted(file_stats.items(), key=lambda x: x[1], reverse=True)
        for fname, count in sorted_stats:
            print(f"{count:>3} survivors | {fname}")
        
        print("\n=== Detailed Survivor List ===")
        # ファイル -> 行番号でソート
        all_survivors.sort(key=lambda x: (x["file"], x["line"]))
        
        current_file = None
        for s in all_survivors:
            if s["file"] != current_file:
                current_file = s["file"]
                print(f"\n--- {current_file} ---")
            
            print(f"Line {s['line']:3} | {s['mutator']:25} | Result: {s['replacement']}")

    except Exception as e:
        print(f"Error parsing JSON: {e}")

if __name__ == "__main__":
    analyze()
