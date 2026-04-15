"""
analyze_survivors.py
====================
Stryker.NET の mutation-report.html から生存変異 (Survived mutants) を抽出し、
ファイル別・行番号別の詳細 Markdown レポートを生成します。

使い方:
    # 最新レポートを自動検出し、コンソールに出力
    python scripts/analyze_survivors.py

    # 特定のレポートを指定
    python scripts/analyze_survivors.py path/to/mutation-report.html

    # Markdown ファイルとして保存（--output オプション）
    python scripts/analyze_survivors.py --output report.md

    # 特定のファイル名だけを対象にフィルタリング
    python scripts/analyze_survivors.py --filter DepositController.cs
"""

import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path


def _safe_print(text: str) -> None:
    """CP932 等の環境でも安全にコンソール出力する。"""
    enc = sys.stdout.encoding or "utf-8"
    print(text.encode(enc, errors="replace").decode(enc, errors="replace"))


# ---------------------------------------------------------------------------
# 最新レポートの探索
# ---------------------------------------------------------------------------

def find_latest_report(base_dir: Path) -> Path | None:
    """StrykerOutput フォルダから最新の mutation-report.html を探索する。"""
    patterns = [
        "src/*/StrykerOutput/*/reports/mutation-report.html",
        "StrykerOutput/*/reports/mutation-report.html",
        "**/StrykerOutput/*/reports/mutation-report.html",
    ]
    for pattern in patterns:
        report_files = list(base_dir.glob(pattern))
        if report_files:
            report_files.sort(key=lambda x: x.stat().st_mtime, reverse=True)
            return report_files[0]
    return None


# ---------------------------------------------------------------------------
# HTML からの JSON データ抽出
# ---------------------------------------------------------------------------

def extract_json(html_file: Path) -> dict | None:
    """mutation-report.html から report JSON を抽出してパースする。"""
    content = html_file.read_text(encoding="utf-8")

    # Stryker レポートの JSON は <script> タグ内に埋め込まれている
    # "schemaVersion" をアンカーにして JSON ブロックを特定する
    token = '"schemaVersion"'
    token_idx = content.find(token)
    if token_idx == -1:
        return None

    potential_start = content.rfind("<script>", 0, token_idx)
    if potential_start == -1:
        potential_start = 0

    json_start = content.find("{", potential_start)
    if json_start == -1 or json_start > token_idx:
        json_start = content.rfind("{", 0, token_idx)
    if json_start == -1:
        return None

    # 括弧の対応で終端を検出
    brace_count = 0
    end_idx = -1
    in_string = False
    escape_next = False
    for i in range(json_start, len(content)):
        c = content[i]
        if escape_next:
            escape_next = False
            continue
        if c == "\\" and in_string:
            escape_next = True
            continue
        if c == '"':
            in_string = not in_string
            continue
        if in_string:
            continue
        if c == "{":
            brace_count += 1
        elif c == "}":
            brace_count -= 1
            if brace_count == 0:
                end_idx = i + 1
                break

    if end_idx == -1:
        return None

    try:
        return json.loads(content[json_start:end_idx])
    except json.JSONDecodeError:
        return None


# ---------------------------------------------------------------------------
# データ解析
# ---------------------------------------------------------------------------

PRIORITY_MUTATORS = {
    "Null coalescing mutation":           "🔴 High",
    "Block removal mutation":             "🔴 High",
    "Logical mutation":                   "🔴 High",
    "Equality mutation":                  "🟡 Mid",
    "Negate expression":                  "🟡 Mid",
    "LogicalNotExpression":               "🟡 Mid",
    "Conditional (true) mutation":        "🟡 Mid",
    "Conditional (false) mutation":       "🟡 Mid",
    "Statement mutation":                 "🟢 Low",
    "Boolean mutation":                   "🟢 Low",
    "String mutation":                    "🟢 Low",
    "Arithmetic mutation":                "🟡 Mid",
    "Linq method mutation":               "🔴 High",
    "PostIncrement":                      "🟢 Low",
}


def get_priority(mutator_name: str) -> str:
    for key, priority in PRIORITY_MUTATORS.items():
        if key in mutator_name:
            return priority
    return "🟢 Low"


def analyze_report(data: dict, filter_file: str | None = None) -> dict:
    """レポート JSON から生存変異を構造化して返す。"""
    files_data = data.get("files", {})
    result = {
        "files": {},
        "total_survived": 0,
        "total_killed": 0,
        "total_timeout": 0,
        "total_no_coverage": 0,
    }

    for filepath, file_info in files_data.items():
        display_name = os.path.basename(filepath)
        if filter_file and display_name != filter_file:
            continue

        mutants = file_info.get("mutants", [])
        survived = [m for m in mutants if m.get("status") == "Survived"]
        killed   = [m for m in mutants if m.get("status") == "Killed"]
        timeout  = [m for m in mutants if m.get("status") == "Timeout"]
        no_cov   = [m for m in mutants if m.get("status") == "NoCoverage"]

        if not survived:
            continue

        result["total_survived"] += len(survived)
        result["total_killed"]   += len(killed)
        result["total_timeout"]  += len(timeout)
        result["total_no_coverage"] += len(no_cov)

        survivors_detailed = []
        for m in survived:
            loc = m.get("location", {})
            start = loc.get("start", {})
            end   = loc.get("end", {})
            mutator = m.get("mutatorName", "Unknown")
            survivors_detailed.append({
                "id":          m.get("id"),
                "mutator":     mutator,
                "replacement": m.get("replacement", ""),
                "line_start":  start.get("line"),
                "line_end":    end.get("line"),
                "col_start":   start.get("column"),
                "priority":    get_priority(mutator),
            })

        survivors_detailed.sort(key=lambda x: (x["line_start"] or 0))

        result["files"][filepath] = {
            "display_name": display_name,
            "survived":     survivors_detailed,
            "killed_count": len(killed),
            "timeout_count": len(timeout),
        }

    return result


# ---------------------------------------------------------------------------
# Markdown レポート生成
# ---------------------------------------------------------------------------

CATEGORY_PATTERNS = {
    "Null合体・デフォルト値":     ["Null coalescing"],
    "ブロック除去":               ["Block removal"],
    "論理・等値条件":             ["Logical mutation", "Equality mutation",
                                   "Negate expression", "LogicalNotExpression",
                                   "Conditional"],
    "文字列":                     ["String mutation"],
    "算術・LINQ":                 ["Arithmetic mutation", "Linq method mutation",
                                   "PostIncrement"],
    "Statement（文の削除）":      ["Statement mutation"],
    "Boolean反転":                ["Boolean mutation"],
}


def categorize(mutator: str) -> str:
    for category, patterns in CATEGORY_PATTERNS.items():
        if any(p in mutator for p in patterns):
            return category
    return "その他"


def build_markdown(result: dict, report_path: Path, project_root: Path) -> str:
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    total_s = result["total_survived"]
    total_k = result["total_killed"]
    total_t = result["total_timeout"]
    total   = total_s + total_k + total_t
    score   = round(total_k / total * 100, 2) if total > 0 else 0.0

    try:
        rel_path = report_path.relative_to(project_root)
    except ValueError:
        rel_path = report_path

    lines = [
        f"# Stryker 生存変異 詳細分析レポート",
        f"",
        f"- **生成日時**: {now}",
        f"- **対象レポート**: `{rel_path}`",
        f"- **スコア**: {score}% (Killed: {total_k}, Survived: {total_s}, Timeout: {total_t})",
        f"",
        f"---",
        f"",
        f"## ファイル別サマリ",
        f"",
        f"| ファイル | Survived | Killed | Timeout | ファイルスコア |",
        f"|---------|----------|--------|---------|-------------|",
    ]

    sorted_files = sorted(result["files"].items(),
                          key=lambda x: len(x[1]["survived"]), reverse=True)
    for _, fd in sorted_files:
        s = len(fd["survived"])
        k = fd["killed_count"]
        t = fd["timeout_count"]
        tot = s + k + t
        pct = round(k / tot * 100, 1) if tot > 0 else 0.0
        lines.append(f"| **{fd['display_name']}** | **{s}** | {k} | {t} | {pct}% |")

    lines += ["", "---", ""]

    # ファイルごとの詳細
    for _, fd in sorted_files:
        display = fd["display_name"]
        survivors = fd["survived"]
        total_in_file = len(survivors) + fd["killed_count"] + fd["timeout_count"]

        lines += [
            f"## {display} — {len(survivors)}件の生存変異",
            f"",
        ]

        # カテゴリ別集計
        categories: dict[str, list] = {}
        for s in survivors:
            cat = categorize(s["mutator"])
            categories.setdefault(cat, []).append(s)

        lines += [
            "### カテゴリ別集計",
            "",
            "| カテゴリ | 件数 |",
            "|---------|------|",
        ]
        for cat, items in sorted(categories.items(), key=lambda x: -len(x[1])):
            lines.append(f"| {cat} | {len(items)} |")

        lines += ["", "### 行番号別一覧", ""]
        lines += [
            "| 優先度 | 行 | 変異タイプ | 変異後の値 |",
            "|--------|-----|-----------|-----------|",
        ]
        for s in survivors:
            line_info = f"L{s['line_start']}" if s["line_start"] else "?"
            if s["line_end"] and s["line_end"] != s["line_start"]:
                line_info += f"-{s['line_end']}"
            replacement = s["replacement"][:40].replace("|", "\\|") if s["replacement"] else ""
            lines.append(
                f"| {s['priority']} | {line_info} | {s['mutator']} | `{replacement}` |"
            )

        lines += ["", "---", ""]

    # 優先度マトリクス（全ファイル横断）
    all_survivors = []
    for _, fd in result["files"].items():
        for s in fd["survived"]:
            all_survivors.append((fd["display_name"], s))

    high   = [(f, s) for f, s in all_survivors if s["priority"] == "🔴 High"]
    mid    = [(f, s) for f, s in all_survivors if s["priority"] == "🟡 Mid"]
    low    = [(f, s) for f, s in all_survivors if s["priority"] == "🟢 Low"]

    lines += [
        "## 優先度マトリクス",
        "",
        f"| 優先度 | 件数 | 推定効果 |",
        f"|--------|------|---------|",
        f"| 🔴 High | {len(high)} | コアロジックの変異 — テスト追加でスコアが大きく伸びる |",
        f"| 🟡 Mid  | {len(mid)} | 条件分岐・算術 — エッジケーステストで対応可能 |",
        f"| 🟢 Low  | {len(low)} | 文字列・Statement — 例外メッセージ検証等で対応 |",
        "",
    ]

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# コンソール出力
# ---------------------------------------------------------------------------

def print_console(result: dict) -> None:
    total_s = result["total_survived"]
    total_k = result["total_killed"]
    total_t = result["total_timeout"]
    total   = total_s + total_k + total_t
    score   = round(total_k / total * 100, 2) if total > 0 else 0.0

    print(f"\n=== Stryker Survived Mutant Analysis ===")
    print(f"Score: {score}% | Killed: {total_k} | Survived: {total_s} | Timeout: {total_t}")
    print()

    sorted_files = sorted(result["files"].items(),
                          key=lambda x: len(x[1]["survived"]), reverse=True)

    print("--- File Summary ---")
    for _, fd in sorted_files:
        s = len(fd["survived"])
        bar = "#" * (s // 3) + "-" * max(0, (20 - s // 3))
        print(f"  {s:>3} survived | {fd['display_name']:<35} |{bar}|")

    print()
    for _, fd in sorted_files:
        print(f"\n=== {fd['display_name']} ({len(fd['survived'])} survived) ===")
        high = [s for s in fd["survived"] if s["priority"] == "🔴 High"]
        mid  = [s for s in fd["survived"] if s["priority"] == "🟡 Mid"]
        low  = [s for s in fd["survived"] if s["priority"] == "🟢 Low"]

        for label, items in [("[HIGH]", high), ("[MID ]", mid), ("[LOW ]", low)]:
            if not items:
                continue
            print(f"  [{label}]")
            for s in items:
                line  = f"L{s['line_start']}" if s["line_start"] else "?"
                repl  = s["replacement"][:30] if s["replacement"] else ""
                out = f"    {line:<8} {s['mutator']:<40} -> {repl}"
                print(out.encode(sys.stdout.encoding or "utf-8", errors="replace").decode(
                    sys.stdout.encoding or "utf-8", errors="replace"))


# ---------------------------------------------------------------------------
# エントリーポイント
# ---------------------------------------------------------------------------

def main() -> None:
    args = sys.argv[1:]
    output_path: Path | None = None
    filter_file: str | None = None
    report_arg: str | None = None

    # 引数パース
    i = 0
    while i < len(args):
        if args[i] == "--output" and i + 1 < len(args):
            output_path = Path(args[i + 1])
            i += 2
        elif args[i] == "--filter" and i + 1 < len(args):
            filter_file = args[i + 1]
            i += 2
        else:
            report_arg = args[i]
            i += 1

    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent

    if report_arg:
        report_path = Path(report_arg).resolve()
    else:
        report_path = find_latest_report(project_root)

    if not report_path or not report_path.exists():
        print("Error: Stryker のレポートが見つかりません。")
        print("Usage: python analyze_survivors.py [report.html] [--output out.md] [--filter File.cs]")
        sys.exit(1)

    print(f"Analyzing: {report_path}")

    data = extract_json(report_path)
    if data is None:
        print("Error: HTML から JSON データを抽出できませんでした。")
        sys.exit(1)

    result = analyze_report(data, filter_file=filter_file)

    if not result["files"]:
        _safe_print("[OK] No survived mutants!" if not filter_file
              else f"[OK] No survived mutants in '{filter_file}'.")
        sys.exit(0)

    # コンソール出力
    print_console(result)

    # Markdown 出力
    md = build_markdown(result, report_path, project_root)

    if output_path:
        output_path.write_text(md, encoding="utf-8-sig")
        _safe_print(f"[OK] Markdown report saved: {output_path}")
    else:
        # デフォルト出力先: tmp/ フォルダ
        default_out = project_root / "tmp" / f"stryker_analysis_{datetime.now().strftime('%Y%m%d_%H%M%S')}.md"
        default_out.parent.mkdir(parents=True, exist_ok=True)
        default_out.write_text(md, encoding="utf-8-sig")
        _safe_print(f"[OK] Markdown report auto-saved: {default_out.relative_to(project_root)}")


if __name__ == "__main__":
    main()
