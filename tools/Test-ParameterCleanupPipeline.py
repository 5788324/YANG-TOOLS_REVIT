#!/usr/bin/env python3
"""Pure-logic and source-contract checks for the duplicate-parameter pipeline.

This does not invoke Revit. It verifies the selection workflow, adaptive batch-delete
algorithm, value-integrity/uniqueness checks, and source contracts before Windows/Revit testing.
"""
from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, List, Sequence, Set, Tuple

ROOT = Path(__file__).resolve().parents[1]


@dataclass
class FakeDeleteMetrics:
    attempts: int = 0
    commits: int = 0
    splits: int = 0
    singles: int = 0
    rollbacks: int = 0


def adaptive_delete(
    ids: Sequence[int],
    fake_delete: Callable[[Sequence[int]], Set[int]],
    metrics: FakeDeleteMetrics,
) -> Tuple[List[int], List[int]]:
    """Python mirror of ParameterDeleteService.DeleteBatchAdaptive."""
    if not ids:
        return [], []
    metrics.attempts += 1
    if len(ids) == 1:
        metrics.singles += 1

    requested = set(ids)
    allowed = requested | {item + 10_000_000 for item in ids}
    try:
        deleted = fake_delete(ids)
        valid = requested.issubset(deleted) and deleted.issubset(allowed)
    except Exception:
        deleted = set()
        valid = False

    if valid:
        metrics.commits += 1
        return list(ids), []

    metrics.rollbacks += 1
    if len(ids) == 1:
        return [], list(ids)

    metrics.splits += 1
    middle = len(ids) // 2
    ok1, bad1 = adaptive_delete(ids[:middle], fake_delete, metrics)
    ok2, bad2 = adaptive_delete(ids[middle:], fake_delete, metrics)
    return ok1 + ok2, bad1 + bad2


def run_delete_scenario(total: int, blocked: Iterable[int], batch_size: int = 250) -> dict:
    blocked_set = set(blocked)
    metrics = FakeDeleteMetrics()
    deleted: List[int] = []
    rejected: List[int] = []

    def fake_delete(ids: Sequence[int]) -> Set[int]:
        result = set(ids)
        result.update(item + 10_000_000 for item in ids)  # normal Revit dependent children
        if any(item in blocked_set for item in ids):
            result.add(999_999_999)  # simulate an unexpected dependent element
        return result

    values = list(range(1, total + 1))
    for offset in range(0, total, batch_size):
        ok, bad = adaptive_delete(values[offset : offset + batch_size], fake_delete, metrics)
        deleted.extend(ok)
        rejected.extend(bad)

    return {
        "total": total,
        "blocked": sorted(blocked_set),
        "deleted": len(deleted),
        "rejected": sorted(rejected),
        "metrics": metrics.__dict__,
        "naive_single_calls": total,
        "call_reduction_ratio": round(1 - metrics.attempts / total, 4) if total else 0,
    }


def select_up_to_ten_sources(source_counts: Sequence[int]) -> Tuple[List[int], int]:
    selected: List[int] = []
    remaining = 10
    for index, count in enumerate(source_counts):
        if count <= 0 or count > remaining:
            continue
        selected.append(index)
        remaining -= count
        if remaining == 0:
            break
    return selected, 10 - remaining


def source_contract_checks() -> List[str]:
    files = {
        "window_xaml": ROOT / "src/YangTools.Revit/UI/DuplicateProjectParameterCleanupWindow.xaml",
        "window_code": ROOT / "src/YangTools.Revit/UI/DuplicateProjectParameterCleanupWindow.xaml.cs",
        "merge": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/ParameterMergeService.cs",
        "value_index": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/ParameterValueIndexService.cs",
        "delete": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/ParameterDeleteService.cs",
        "value": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/ParameterValueUtil.cs",
        "verify": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/ParameterCleanupVerificationService.cs",
        "report": ROOT / "src/YangTools.Revit/Core/ParameterCleanup/CsvReportWriter.cs",
    }
    text = {name: path.read_text(encoding="utf-8") for name, path in files.items()}
    failures: List[str] = []

    required = {
        "window_xaml": ["全选可处理项", "全不选", "反选", "测试：最多 10 个来源参数"],
        "window_code": ["ApplySelection", "remainingSources = 10", "UpdateSelectionSummary", "snapshot.ValueIndex", "DispatcherPriority.Render"],
        "merge": ["MergeValuesIndexed", "operationsByElementId", "TryGetParameterByIdentity", "ParameterValueUtil.TrySetFrom", "快速值索引", "IdentityFailureAbortThreshold", "值引擎提前停止"],
        "value_index": ["WhereElementIsNotElementType", "WhereElementIsElementType", "GetElementIds", "HasGlobalParameterAssociation"],
        "delete": ["DefaultBatchSize = 250", "GetDependentElements(null)", "allowedDeletedValues", "DeleteBatchAdaptive", "_document.Delete(requestedIds)", "SplitCount", "subTransaction.RollBack()"],
        "value": ["AsDouble()", "AsInteger()", "AsString()", "AsElementId()", "target.Set(source.AsDouble())"],
        "verify": ["CaptureBefore", "VerifyAfter", "StoredValuesEqual", "FullyCleanedGroupsUniquePassed", "原有非空内部值在清理后不存在"],
        "report": ["08_参数值完整性复核.csv", "09_已选参数唯一性复核.csv", "10_运行诊断.csv"],
    }
    for name, tokens in required.items():
        for token in tokens:
            if token not in text[name]:
                failures.append(f"{name}: missing {token}")

    if "LookupParameter(" in text["merge"]:
        failures.append("merge: must not use LookupParameter(name)")
    if "Task.Run(" in text["window_code"]:
        failures.append("window: Revit API must not be moved to Task.Run")
    if re.search(r"Set\s*\(\s*source\.AsValueString", text["value"]):
        failures.append("value: display strings must not be used for Set")
    if "_document.Delete(record.Element.Id)" in text["delete"]:
        failures.append("delete: old per-item delete loop is still present")
    if "AsValueString()" in text["verify"]:
        failures.append("verify: must compare raw internal values, not display strings")
    return failures




def simulate_indexed_merge(source_values: Sequence[str], initial_keeper: str | None = None) -> dict:
    """Mirror the fixed rule: first source fills an empty keeper; later differing sources become conflicts."""
    keeper = initial_keeper
    copied = 0
    equal = 0
    conflicts: List[str] = []
    for source in source_values:
        if keeper is None:
            keeper = source
            copied += 1
        elif keeper == source:
            equal += 1
        else:
            conflicts.append(source)
    return {"keeper": keeper, "copied": copied, "equal": equal, "conflicts": conflicts}

def verify_value_preservation(before_values: Sequence[Set[str]], after_values: Sequence[Set[str]]) -> dict:
    """Pure-logic mirror: every distinct non-empty value before must still exist after."""
    missing = []
    verified = 0
    for index, expected in enumerate(before_values):
        current = after_values[index] if index < len(after_values) else set()
        for value in expected:
            if value in current:
                verified += 1
            else:
                missing.append({"element_group": index, "value": value})
    return {"verified": verified, "missing": missing, "passed": not missing}


def verify_group_uniqueness(before_counts: Sequence[int], after_counts: Sequence[int], deleted_sources: Sequence[int]) -> dict:
    failures = []
    unique = 0
    for index, before_count in enumerate(before_counts):
        after_count = after_counts[index]
        deleted = deleted_sources[index]
        fully_cleaned = before_count > 1 and deleted == before_count - 1
        if after_count == 1:
            unique += 1
        if fully_cleaned and after_count != 1:
            failures.append(index)
    return {"unique": unique, "fully_cleaned_failures": failures, "passed": not failures}

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="artifacts/verification/parameter-cleanup-pipeline-report.json")
    args = parser.parse_args()

    selection_cases = [
        [1] * 20,
        [3, 4, 6, 2, 1],
        [11, 5, 5, 2],
        [2, 2, 2, 2, 2, 2],
    ]
    selection_results = []
    for counts in selection_cases:
        indexes, selected_sources = select_up_to_ten_sources(counts)
        selection_results.append({
            "source_counts": counts,
            "selected_indexes": indexes,
            "selected_sources": selected_sources,
            "within_limit": selected_sources <= 10,
        })

    scenarios = {
        "ten_all_safe": run_delete_scenario(10, []),
        "606_all_safe": run_delete_scenario(606, []),
        "606_one_cascade": run_delete_scenario(606, [317]),
        "606_four_cascades": run_delete_scenario(606, [1, 25, 317, 606]),
    }

    value_scenarios = {
        "preserved": verify_value_preservation([{"3500", "0"}, {"A"}], [{"3500", "0"}, {"A"}]),
        "lost_conflict_value": verify_value_preservation([{"3500", "4200"}], [{"3500"}]),
    }
    indexed_merge_scenarios = {
        "first_source_then_conflict": simulate_indexed_merge(["3500", "4200"]),
        "keeper_already_set": simulate_indexed_merge(["3500", "4200"], initial_keeper="3500"),
    }
    uniqueness_scenarios = {
        "cleaned_to_one": verify_group_uniqueness([3, 2], [1, 1], [2, 1]),
        "reported_deleted_but_still_duplicate": verify_group_uniqueness([3], [2], [2]),
        "protected_source_remains": verify_group_uniqueness([3], [2], [1]),
    }

    failures = source_contract_checks()
    for case in selection_results:
        if not case["within_limit"]:
            failures.append("selection: exceeded 10 source parameters")
    if scenarios["606_all_safe"]["metrics"]["attempts"] != math.ceil(606 / 250):
        failures.append("delete simulation: all-safe case did not use one call per batch")
    if scenarios["606_one_cascade"]["rejected"] != [317]:
        failures.append("delete simulation: adaptive split did not isolate one cascade")
    if scenarios["606_four_cascades"]["rejected"] != [1, 25, 317, 606]:
        failures.append("delete simulation: adaptive split did not isolate multiple cascades")
    if not value_scenarios["preserved"]["passed"]:
        failures.append("verification simulation: preserved values were reported missing")
    if value_scenarios["lost_conflict_value"]["passed"]:
        failures.append("verification simulation: lost value was not detected")
    if not uniqueness_scenarios["cleaned_to_one"]["passed"]:
        failures.append("uniqueness simulation: valid cleanup did not pass")
    if uniqueness_scenarios["reported_deleted_but_still_duplicate"]["passed"]:
        failures.append("uniqueness simulation: fully deleted group remaining duplicate was not detected")
    if not uniqueness_scenarios["protected_source_remains"]["passed"]:
        failures.append("uniqueness simulation: protected source should be reported incomplete, not hard failure")
    if indexed_merge_scenarios["first_source_then_conflict"] != {"keeper": "3500", "copied": 1, "equal": 0, "conflicts": ["4200"]}:
        failures.append("indexed merge: later source overwrote or failed to report a conflict")

    report = {
        "ok": not failures,
        "scope": "pure logic + source contract; no Revit runtime",
        "selection_results": selection_results,
        "delete_scenarios": scenarios,
        "value_integrity_scenarios": value_scenarios,
        "indexed_merge_scenarios": indexed_merge_scenarios,
        "uniqueness_scenarios": uniqueness_scenarios,
        "failures": failures,
    }
    output = ROOT / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print("重复项目参数清理链路模拟检查")
    print(f"- source_contract_failures: {len(failures)}")
    print(f"- 606 all-safe API calls: {scenarios['606_all_safe']['metrics']['attempts']} (old strict path: recursive/per-item)")
    print(f"- 606 one-cascade API calls: {scenarios['606_one_cascade']['metrics']['attempts']}")
    print(f"- value-loss detection: {'pass' if not value_scenarios['lost_conflict_value']['passed'] else 'fail'}")
    print(f"- indexed conflict preservation: {'pass' if indexed_merge_scenarios['first_source_then_conflict']['keeper'] == '3500' else 'fail'}")
    print(f"- uniqueness failure detection: {'pass' if not uniqueness_scenarios['reported_deleted_but_still_duplicate']['passed'] else 'fail'}")
    print(f"- report: {output}")
    if failures:
        for failure in failures:
            print("ERROR", failure)
        return 1
    print("检查通过。实际 Revit 事务仍需 Windows/Revit 实机验收。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
