"""YangTools external MCP bridge.

The process speaks MCP over stdio and discovers the currently running Revit
YangTools gateway from its per-process instance file. It never assumes port
8081 and always sends the per-session bearer token.

Do not write diagnostic text to stdout: stdout is reserved for MCP stdio.
"""

from __future__ import annotations

import asyncio
import json
import os
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from mcp.server.fastmcp import FastMCP

mcp = FastMCP("yangtools-revit")


@dataclass(frozen=True)
class GatewayEndpoint:
    url: str
    token: str
    instance_file: str


def _instance_directory() -> Path:
    local = os.environ.get("LOCALAPPDATA")
    if not local:
        local = str(Path.home() / "AppData" / "Local")
    return Path(local) / "YangTools" / "MCP" / "instances"


def _read_instance(path: Path) -> GatewayEndpoint | None:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
        url = str(payload.get("url") or "").strip()
        token = str(payload.get("token") or "").strip()
        if not url or not token:
            return None
        return GatewayEndpoint(url=url, token=token, instance_file=str(path))
    except (OSError, ValueError, TypeError):
        return None


def _status_request(endpoint: GatewayEndpoint, timeout: int = 2) -> bool:
    status_url = endpoint.url.rstrip("/") + "/status"
    request = urllib.request.Request(
        status_url,
        method="GET",
        headers={"Authorization": f"Bearer {endpoint.token}"},
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return 200 <= int(response.status) < 300
    except Exception:
        return False


def _discover_gateway() -> GatewayEndpoint:
    explicit_url = os.environ.get("REVIT_MCP_URL", "").strip()
    explicit_token = os.environ.get("REVIT_MCP_TOKEN", "").strip()
    explicit_instance = (
        os.environ.get("YANGTOOLS_MCP_INSTANCE", "").strip()
        or os.environ.get("REVIT_MCP_INSTANCE", "").strip()
    )

    if explicit_url:
        if not explicit_token:
            raise RuntimeError("已设置 REVIT_MCP_URL，但缺少 REVIT_MCP_TOKEN。")
        return GatewayEndpoint(explicit_url, explicit_token, explicit_instance)

    if explicit_instance:
        endpoint = _read_instance(Path(explicit_instance))
        if endpoint is None:
            raise RuntimeError(f"MCP 实例文件无效：{explicit_instance}")
        if not _status_request(endpoint):
            raise RuntimeError(f"MCP 实例不可连接：{explicit_instance}")
        return endpoint

    directory = _instance_directory()
    if not directory.exists():
        raise RuntimeError(
            "未发现 YangTools MCP 实例。请在 Revit 的“系统设置 → MCP与执行服务”中启动网关。"
        )

    try:
        candidates = sorted(
            directory.glob("*.json"),
            key=lambda item: item.stat().st_mtime,
            reverse=True,
        )
    except OSError as exc:
        raise RuntimeError(f"无法读取 MCP 实例目录：{exc}") from exc

    for candidate in candidates:
        endpoint = _read_instance(candidate)
        if endpoint is not None and _status_request(endpoint):
            return endpoint

    raise RuntimeError(
        "发现 MCP 实例文件，但没有可连接的 Revit 网关。请删除失效实例或在 Revit 中重新启动网关。"
    )


def _send_sync(command_name: str, args: dict[str, Any], timeout: int) -> str:
    endpoint = _discover_gateway()
    payload = json.dumps(
        {"command": command_name, "args": args},
        ensure_ascii=False,
    ).encode("utf-8")
    request = urllib.request.Request(
        endpoint.url,
        data=payload,
        method="POST",
        headers={
            "Content-Type": "application/json; charset=utf-8",
            "Authorization": f"Bearer {endpoint.token}",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Revit MCP HTTP {exc.code}: {body or exc.reason}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"无法连接 Revit MCP 网关：{exc.reason}") from exc
    except TimeoutError as exc:
        raise RuntimeError("等待 Revit 主线程执行超时。") from exc

    try:
        result = json.loads(body)
    except ValueError:
        return body

    if not result.get("success", False):
        raise RuntimeError(str(result.get("error") or "Revit MCP 执行失败。"))

    value = result.get("result", "")
    if isinstance(value, str):
        try:
            decoded = json.loads(value)
            return json.dumps(decoded, ensure_ascii=False, indent=2)
        except ValueError:
            return value
    return json.dumps(value, ensure_ascii=False, indent=2)


async def _send_to_revit(
    command_name: str,
    args: dict[str, Any],
    timeout: int = 70,
) -> str:
    return await asyncio.to_thread(_send_sync, command_name, args, timeout)


@mcp.tool()
async def get_revit_status() -> str:
    """获取 Revit 版本、活动文档和 YangTools 执行器状态。"""
    return await _send_to_revit("get_revit_status", {})


@mcp.tool()
async def get_active_document() -> str:
    """读取当前活动 Revit 文档的标题、路径、工作共享和云模型状态。"""
    return await _send_to_revit("get_active_document", {})


@mcp.tool()
async def get_selection() -> str:
    """读取当前 Revit 选择集，最多返回 100 个图元摘要。"""
    return await _send_to_revit("get_selection", {})


@mcp.tool()
async def get_element_parameters(element_ids: list[int]) -> str:
    """按 ElementId 读取图元摘要和可显示参数，最多读取 100 个。"""
    return await _send_to_revit(
        "get_element_parameters",
        {"elementIds": element_ids},
    )


@mcp.tool()
async def query_elements(
    category: str = "",
    name_contains: str = "",
    max_results: int = 100,
) -> str:
    """在当前文档中按类别名称和图元名称进行只读查询。"""
    bounded_max = max(1, min(int(max_results), 500))
    return await _send_to_revit(
        "query_elements",
        {
            "category": category,
            "nameContains": name_contains,
            "maxResults": bounded_max,
        },
    )


@mcp.tool()
async def inspect_rebar(element_ids: list[int] | None = None) -> str:
    """只读诊断钢筋：识别 Shape Driven / Free Form、宿主、排布、约束、Hook 和样本中心线。

    传入 element_ids 时检查指定钢筋；不传时检查 Revit 当前选择集中的钢筋。
    单次最多 500 个，不修改模型。
    """
    return await _send_to_revit(
        "inspect_rebar",
        {"elementIds": element_ids or []},
    )


@mcp.tool()
async def list_rebar_types() -> str:
    """列出当前文档可用的 RebarBarType，最多 200 个。"""
    return await _send_to_revit("list_rebar_types", {})


@mcp.tool()
async def create_shape_rebar(
    host_id: int,
    start_mm: list[float],
    end_mm: list[float],
    normal: list[float] | None = None,
    bar_type_id: int | None = None,
    layout_mode: str = "Single",
    number_of_bar_positions: int = 1,
    spacing_mm: float = 200.0,
    bars_on_normal_side: bool = True,
    include_first_bar: bool = True,
    include_last_bar: bool = True,
    dry_run: bool = True,
) -> str:
    """R01 最小 Shape Driven 创建器。

    仅支持直线中心线与 Single / NumberWithSpacing。坐标单位为 mm。
    dry_run=True 时不启动 Revit Transaction，不修改模型。
    """
    if len(start_mm) < 3 or len(end_mm) < 3:
        raise ValueError("start_mm / end_mm 必须包含 3 个坐标值。")
    if normal is not None and len(normal) < 3:
        raise ValueError("normal 必须包含 3 个分量。")

    return await _send_to_revit(
        "create_shape_rebar",
        {
            "hostId": int(host_id),
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
            "startMm": [float(start_mm[0]), float(start_mm[1]), float(start_mm[2])],
            "endMm": [float(end_mm[0]), float(end_mm[1]), float(end_mm[2])],
            "normal": [
                float((normal or [0.0, 0.0, 1.0])[0]),
                float((normal or [0.0, 0.0, 1.0])[1]),
                float((normal or [0.0, 0.0, 1.0])[2]),
            ],
            "layoutMode": layout_mode,
            "numberOfBarPositions": int(number_of_bar_positions),
            "spacingMm": float(spacing_mm),
            "barsOnNormalSide": bool(bars_on_normal_side),
            "includeFirstBar": bool(include_first_bar),
            "includeLastBar": bool(include_last_bar),
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def create_free_form_rebar(
    host_id: int,
    bars: list[list[list[float]]],
    bar_type_id: int | None = None,
    rebar_style: str = "Standard",
    dry_run: bool = True,
) -> str:
    """R07 最小 Free Form 创建器。

    bars = [bar1_points, bar2_points, ...]，每个点为 [x,y,z] mm。
    当前创建 unconstrained Free Form；dry_run 默认 True。
    """
    if not bars:
        raise ValueError("bars 至少包含一根钢筋路径")
    for bar in bars:
        if len(bar) < 2:
            raise ValueError("每根 bar 至少需要两个点")
        for p in bar:
            if len(p) < 3:
                raise ValueError("每个点必须包含 3 个坐标")
    return await _send_to_revit(
        "create_free_form_rebar",
        {
            "hostId": int(host_id),
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
            "rebarStyle": rebar_style,
            "bars": [[[float(v) for v in p[:3]] for p in bar] for bar in bars],
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def modify_free_form_rebar(
    rebar_id: int,
    bars: list[list[list[float]]],
    expected_unique_id: str = "",
    dry_run: bool = True,
) -> str:
    """R08 原位修改 unconstrained Free Form Rebar 曲线。

    bars = [bar1_points, bar2_points, ...]，每个点为 [x,y,z] mm。
    只接受 raw-curve/unconstrained Free Form；server-driven/constrained Free Form 会被拒绝。
    dry_run 默认 True。
    """
    if not bars:
        raise ValueError("bars 至少包含一根钢筋路径")
    for bar in bars:
        if len(bar) < 2:
            raise ValueError("每根 bar 至少需要两个点")
        for p in bar:
            if len(p) < 3:
                raise ValueError("每个点必须包含 3 个坐标")
    return await _send_to_revit(
        "modify_free_form_rebar",
        {
            "rebarId": int(rebar_id),
            "expectedUniqueId": expected_unique_id,
            "bars": [[[float(v) for v in p[:3]] for p in bar] for bar in bars],
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def probe_gradient_rebar_capabilities() -> str:
    """GR01-P0 只读探针：报告当前 Revit 版本的 Free Form Native API/server 能力表面。

    只证明 API surface / RebarUpdateService server registration，不等于 Surface/Aligned Runtime 可用于 GR01。
    """
    return await _send_to_revit("probe_gradient_rebar_capabilities", {}, timeout=30)


@mcp.tool()
async def preview_gradient_spacing_plan(
    distribution_length_mm: float,
    zones: list[dict],
    spacing_step_mm: float = 5.0,
    min_tail_ratio: float = 0.75,
    length_tolerance_mm: float = 1.0,
    station_merge_tolerance_mm: float = 0.5,
    max_physical_bars: int = 1000,
) -> str:
    """GR01 纯计算预览：自动模式按 Zone 最大间距均匀分配（实际间距 <= 最大间距）并生成全局 Station。

    spacing_step_mm / min_tail_ratio 为旧接口兼容参数，V1.0.4 自动模式不再参与计算。
    zone 形如：
    {"label":"普通","length_mm":4250,"max_spacing_mm":200,"manual_actual_spacing_mm":None}
    不修改 Revit 模型。
    """
    if distribution_length_mm <= 0:
        raise ValueError("distribution_length_mm 必须大于 0")
    if not zones:
        raise ValueError("zones 不能为空")
    payload_zones = []
    for i, zone in enumerate(zones):
        if not isinstance(zone, dict):
            raise ValueError(f"zones[{i}] 必须是对象")
        length = float(zone.get("length_mm", zone.get("lengthMm", 0.0)))
        max_spacing = float(zone.get("max_spacing_mm", zone.get("maxSpacingMm", 0.0)))
        manual = zone.get("manual_actual_spacing_mm", zone.get("manualActualSpacingMm"))
        if length <= 0 or max_spacing <= 0:
            raise ValueError(f"zones[{i}] length/max_spacing 必须大于 0")
        payload_zones.append(
            {
                "label": str(zone.get("label", "")),
                "lengthMm": length,
                "maxSpacingMm": max_spacing,
                "manualActualSpacingMm": float(manual) if manual is not None else None,
            }
        )
    return await _send_to_revit(
        "preview_gradient_spacing_plan",
        {
            "distributionLengthMm": float(distribution_length_mm),
            "zones": payload_zones,
            "spacingStepMm": float(spacing_step_mm),
            "minTailRatio": float(min_tail_ratio),
            "lengthToleranceMm": float(length_tolerance_mm),
            "stationMergeToleranceMm": float(station_merge_tolerance_mm),
            "maxPhysicalBars": int(max_physical_bars),
        },
        timeout=30,
    )


@mcp.tool()
async def probe_gradient_server_create(
    host_id: int,
    mode: str = "Surface",
    bar_type_id: int | None = None,
) -> str:
    """GR01-P0 最小 server-driven Free Form 骨架创建探针。

    mode = Surface / Aligned。内部启动 Transaction 后强制 RollBack，ChangedModel 最终必须为 false。
    只验证 built-in server GUID + CreateFreeForm(serverGUID) 的最小创建链，不代表 Native Engine 已通过 GR01 验收。
    仅在一次性测试文档中运行。
    """
    if host_id <= 0:
        raise ValueError("host_id 必须大于 0")
    normalized = mode.strip().lower()
    if normalized not in ("surface", "aligned"):
        raise ValueError("mode 仅支持 Surface / Aligned")
    return await _send_to_revit(
        "probe_gradient_server_create",
        {
            "hostId": int(host_id),
            "mode": "Aligned" if normalized == "aligned" else "Surface",
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
        },
        timeout=60,
    )


@mcp.tool()
async def preview_gradient_rebar_set(
    start_control_rebar_id: int,
    end_control_rebar_id: int,
    distribution_path: dict,
    zones: list[dict],
    expected_start_control_unique_id: str = "",
    expected_end_control_unique_id: str = "",
    spacing_step_mm: float = 5.0,
    min_tail_ratio: float = 0.75,
    length_tolerance_mm: float = 1.0,
    station_merge_tolerance_mm: float = 0.5,
    path_projection_tolerance_mm: float = 10.0,
    curve_continuity_tolerance_mm: float = 1.0,
    geometry_readback_tolerance_mm: float = 1.0,
    max_physical_bars: int = 1000,
    max_curves_per_bar: int = 100,
) -> str:
    """GR01 V1 只读预览：两根控制 Rebar + 全局 Z 轴/直线 ModelLine/Edge + Zone 生成 Explicit ManagedFreeForm 计划。

    distribution_path:
      ZAxis:     {"kind":"ZAxis"}
      ModelLine: {"kind":"ModelLine", "element_id":123}
      Edge:      {"kind":"Edge", "stable_reference":"..."}

    zones 可将一项设为 {"auto_length": true}，其长度会自动取有效分布长度扣除其余固定区段后的剩余值。

    控制筋必须 single-position、同 Host/BarType/RebarStyle，且当前 V1 不接受 Hook/End Treatment/Coupler。
    自动间距采用 UniformWithinMax：intervals=ceil(length/max_spacing)，实际间距=length/intervals；
    spacing_step_mm / min_tail_ratio 为旧接口兼容参数。返回 PlanFingerprint、N 个 Station、首/中/末几何样本；不启动 Transaction。
    """
    return await _gradient_rebar_set_call(
        "preview_gradient_rebar_set",
        start_control_rebar_id,
        end_control_rebar_id,
        distribution_path,
        zones,
        expected_start_control_unique_id,
        expected_end_control_unique_id,
        spacing_step_mm,
        min_tail_ratio,
        length_tolerance_mm,
        station_merge_tolerance_mm,
        path_projection_tolerance_mm,
        curve_continuity_tolerance_mm,
        geometry_readback_tolerance_mm,
        max_physical_bars,
        max_curves_per_bar,
        "",
        True,
    )


@mcp.tool()
async def create_gradient_rebar_set(
    start_control_rebar_id: int,
    end_control_rebar_id: int,
    distribution_path: dict,
    zones: list[dict],
    expected_plan_fingerprint: str = "",
    expected_start_control_unique_id: str = "",
    expected_end_control_unique_id: str = "",
    spacing_step_mm: float = 5.0,
    min_tail_ratio: float = 0.75,
    length_tolerance_mm: float = 1.0,
    station_merge_tolerance_mm: float = 0.5,
    path_projection_tolerance_mm: float = 10.0,
    curve_continuity_tolerance_mm: float = 1.0,
    geometry_readback_tolerance_mm: float = 1.0,
    max_physical_bars: int = 1000,
    max_curves_per_bar: int = 100,
    dry_run: bool = True,
) -> str:
    """GR01 V1 创建一个完整 Explicit ManagedFreeForm Rebar Set。

    默认 dry_run=True。正式创建建议先调用 preview_gradient_rebar_set，再把返回的 PlanFingerprint
    传入 expected_plan_fingerprint；Create 会重新读取控制筋/路径/Zone 重新计算并拒绝陈旧 Preview。
    成功时只创建 1 个 Rebar Element，physical bar 数等于 Preview BarCount；一次 Revit Undo 应删除整组。
    """
    return await _gradient_rebar_set_call(
        "create_gradient_rebar_set",
        start_control_rebar_id,
        end_control_rebar_id,
        distribution_path,
        zones,
        expected_start_control_unique_id,
        expected_end_control_unique_id,
        spacing_step_mm,
        min_tail_ratio,
        length_tolerance_mm,
        station_merge_tolerance_mm,
        path_projection_tolerance_mm,
        curve_continuity_tolerance_mm,
        geometry_readback_tolerance_mm,
        max_physical_bars,
        max_curves_per_bar,
        expected_plan_fingerprint,
        dry_run,
    )


async def _gradient_rebar_set_call(
    command: str,
    start_control_rebar_id: int,
    end_control_rebar_id: int,
    distribution_path: dict,
    zones: list[dict],
    expected_start_control_unique_id: str,
    expected_end_control_unique_id: str,
    spacing_step_mm: float,
    min_tail_ratio: float,
    length_tolerance_mm: float,
    station_merge_tolerance_mm: float,
    path_projection_tolerance_mm: float,
    curve_continuity_tolerance_mm: float,
    geometry_readback_tolerance_mm: float,
    max_physical_bars: int,
    max_curves_per_bar: int,
    expected_plan_fingerprint: str,
    dry_run: bool,
) -> str:
    if start_control_rebar_id <= 0 or end_control_rebar_id <= 0:
        raise ValueError("start_control_rebar_id / end_control_rebar_id 必须大于 0")
    if start_control_rebar_id == end_control_rebar_id:
        raise ValueError("两根控制筋不能相同")
    if not isinstance(distribution_path, dict):
        raise ValueError("distribution_path 必须是对象")
    kind = str(distribution_path.get("kind", "ZAxis"))
    normalized_kind = kind.strip().lower()
    if normalized_kind not in ("zaxis", "modelline", "edge"):
        raise ValueError("distribution_path.kind 仅支持 ZAxis / ModelLine / Edge")
    path_payload = {
        "kind": "ZAxis" if normalized_kind == "zaxis" else ("Edge" if normalized_kind == "edge" else "ModelLine"),
        "elementId": int(distribution_path.get("element_id", distribution_path.get("elementId")))
        if distribution_path.get("element_id", distribution_path.get("elementId")) is not None else None,
        "stableReference": str(distribution_path.get("stable_reference", distribution_path.get("stableReference", ""))),
    }
    if normalized_kind == "modelline" and (path_payload["elementId"] is None or path_payload["elementId"] <= 0):
        raise ValueError("ModelLine path 必须提供 element_id")
    if normalized_kind == "edge" and not path_payload["stableReference"].strip():
        raise ValueError("Edge path 必须提供 stable_reference")
    if not zones:
        raise ValueError("zones 不能为空")
    payload_zones = []
    auto_length_count = 0
    for i, zone in enumerate(zones):
        if not isinstance(zone, dict):
            raise ValueError(f"zones[{i}] 必须是对象")
        auto_length = bool(zone.get("auto_length", zone.get("autoLength", False)))
        if auto_length:
            auto_length_count += 1
        length = float(zone.get("length_mm", zone.get("lengthMm", 0.0)))
        max_spacing = float(zone.get("max_spacing_mm", zone.get("maxSpacingMm", 0.0)))
        manual = zone.get("manual_actual_spacing_mm", zone.get("manualActualSpacingMm"))
        if (not auto_length and length <= 0) or max_spacing <= 0:
            raise ValueError(f"zones[{i}] 固定长度/max_spacing 必须大于 0；auto_length=true 时长度可省略")
        payload_zones.append({
            "label": str(zone.get("label", "")),
            "autoLength": auto_length,
            "lengthMm": length,
            "maxSpacingMm": max_spacing,
            "manualActualSpacingMm": float(manual) if manual is not None else None,
        })
    if auto_length_count > 1:
        raise ValueError("zones 最多只能有一个 auto_length=true 的自动剩余区段")
    return await _send_to_revit(
        command,
        {
            "startControlRebarId": int(start_control_rebar_id),
            "expectedStartControlUniqueId": expected_start_control_unique_id,
            "endControlRebarId": int(end_control_rebar_id),
            "expectedEndControlUniqueId": expected_end_control_unique_id,
            "distributionPath": path_payload,
            "zones": payload_zones,
            "spacingStepMm": float(spacing_step_mm),
            "minTailRatio": float(min_tail_ratio),
            "lengthToleranceMm": float(length_tolerance_mm),
            "stationMergeToleranceMm": float(station_merge_tolerance_mm),
            "pathProjectionToleranceMm": float(path_projection_tolerance_mm),
            "curveContinuityToleranceMm": float(curve_continuity_tolerance_mm),
            "geometryReadbackToleranceMm": float(geometry_readback_tolerance_mm),
            "maxPhysicalBars": int(max_physical_bars),
            "maxCurvesPerBar": int(max_curves_per_bar),
            "expectedPlanFingerprint": expected_plan_fingerprint,
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def analyze_rebar_clearance(
    rebar_ids: list[int],
    minimum_clearance_mm: float = 25.0,
    include_same_element: bool = False,
    max_results: int = 200,
) -> str:
    """R09 只读钢筋碰撞/净距筛查。

    surface gap = 中心线最小距离 - 两根钢筋半径之和。
    gap < 0 为 Clash；0 <= gap < minimum_clearance_mm 为 ClearanceViolation。
    直线段精确计算；Arc/其他曲线使用 tessellation，仅作为筛查，不替代规范级验算。
    """
    if not rebar_ids:
        raise ValueError("rebar_ids 不能为空")
    bounded_results = max(1, min(int(max_results), 1000))
    return await _send_to_revit(
        "analyze_rebar_clearance",
        {
            "rebarIds": [int(value) for value in rebar_ids],
            "minimumClearanceMm": float(minimum_clearance_mm),
            "includeSameElement": bool(include_same_element),
            "maxResults": bounded_results,
        },
        timeout=100,
    )


@mcp.tool()
async def inspect_rebar_host_faces(host_id: int) -> str:
    """R02 只读探针：列出指定 Rebar Host 的 exposed planar faces、stable reference、法向和 Cover。

    返回的 stable reference 可直接传给 create_shape_rebar_on_face。不会修改模型。
    """
    return await _send_to_revit(
        "inspect_rebar_host_faces",
        {"hostId": int(host_id)},
    )


@mcp.tool()
async def create_shape_rebar_on_face(
    host_id: int,
    face_stable_reference: str,
    start_on_face_mm: list[float],
    end_on_face_mm: list[float],
    bar_type_id: int | None = None,
    layout_mode: str = "Single",
    number_of_bar_positions: int = 1,
    spacing_mm: float = 200.0,
    bars_on_normal_side: bool = True,
    include_first_bar: bool = True,
    include_last_bar: bool = True,
    dry_run: bool = True,
) -> str:
    """R02 PlanarFace + Host Cover + ToCover Shape Driven 创建器。

    start/end 坐标单位为 mm；服务会先投影到指定 bounded PlanarFace，
    再按 Cover + bar model radius 生成初始中心线，并强制验证 RebarPlane -> ToCover。
    dry_run=True 时不启动 Revit Transaction。
    """
    if not face_stable_reference.strip():
        raise ValueError("face_stable_reference 不能为空。")
    if len(start_on_face_mm) < 3 or len(end_on_face_mm) < 3:
        raise ValueError("start_on_face_mm / end_on_face_mm 必须包含 3 个坐标值。")

    return await _send_to_revit(
        "create_shape_rebar_on_face",
        {
            "hostId": int(host_id),
            "faceStableReference": face_stable_reference,
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
            "startOnFaceMm": [float(start_on_face_mm[0]), float(start_on_face_mm[1]), float(start_on_face_mm[2])],
            "endOnFaceMm": [float(end_on_face_mm[0]), float(end_on_face_mm[1]), float(end_on_face_mm[2])],
            "layoutMode": layout_mode,
            "numberOfBarPositions": int(number_of_bar_positions),
            "spacingMm": float(spacing_mm),
            "barsOnNormalSide": bool(bars_on_normal_side),
            "includeFirstBar": bool(include_first_bar),
            "includeLastBar": bool(include_last_bar),
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def create_shape_rebar_varying_on_face(
    host_id: int,
    cover_face_stable_reference: str,
    start_boundary_face_stable_reference: str,
    end_boundary_face_stable_reference: str,
    seed_start_on_cover_face_mm: list[float],
    seed_end_on_cover_face_mm: list[float],
    bar_type_id: int | None = None,
    number_of_bar_positions: int = 5,
    spacing_mm: float = 200.0,
    bars_on_normal_side: bool = True,
    include_first_bar: bool = True,
    include_last_bar: bool = True,
    enable_varying: bool = True,
    dry_run: bool = True,
) -> str:
    """R03 Shape Driven 表面分布 / Varying 创建器。

    三个 stable reference 必须来自同一 Rebar Host 的 exposed PlanarFace：
    - cover_face：钢筋所在保护层面；
    - start/end boundary：钢筋两端约束面。

    seed 两点只定义第一根钢筋的大致方向，坐标单位 mm。服务会把钢筋主平面旋转为
    “沿宿主面分布”的方向，用 Edge -> ToCover 控制保护层，用 Start/End -> ToCover
    控制长度；enable_varying=True 时由 Revit 约束求解每根钢筋。
    dry_run=True 不启动 Transaction。
    """
    for name, ref in [
        ("cover_face_stable_reference", cover_face_stable_reference),
        ("start_boundary_face_stable_reference", start_boundary_face_stable_reference),
        ("end_boundary_face_stable_reference", end_boundary_face_stable_reference),
    ]:
        if not ref.strip():
            raise ValueError(f"{name} 不能为空。")
    if len(seed_start_on_cover_face_mm) < 3 or len(seed_end_on_cover_face_mm) < 3:
        raise ValueError("seed_start/end_on_cover_face_mm 必须包含 3 个坐标值。")

    return await _send_to_revit(
        "create_shape_rebar_varying_on_face",
        {
            "hostId": int(host_id),
            "coverFaceStableReference": cover_face_stable_reference,
            "startBoundaryFaceStableReference": start_boundary_face_stable_reference,
            "endBoundaryFaceStableReference": end_boundary_face_stable_reference,
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
            "seedStartOnCoverFaceMm": [float(seed_start_on_cover_face_mm[0]), float(seed_start_on_cover_face_mm[1]), float(seed_start_on_cover_face_mm[2])],
            "seedEndOnCoverFaceMm": [float(seed_end_on_cover_face_mm[0]), float(seed_end_on_cover_face_mm[1]), float(seed_end_on_cover_face_mm[2])],
            "numberOfBarPositions": int(number_of_bar_positions),
            "spacingMm": float(spacing_mm),
            "barsOnNormalSide": bool(bars_on_normal_side),
            "includeFirstBar": bool(include_first_bar),
            "includeLastBar": bool(include_last_bar),
            "enableVarying": bool(enable_varying),
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def rebind_shape_rebar_constraints(
    rebar_id: int,
    expected_unique_id: str = "",
    edge_face_stable_reference: str = "",
    start_face_stable_reference: str = "",
    end_face_stable_reference: str = "",
    require_planar_face: bool = True,
    require_exposed_face: bool = True,
    preserve_unretargeted_constraints: bool = True,
    dry_run: bool = True,
) -> str:
    """R05 Shape Driven Rebar 约束原位重绑。

    将既有 Edge / StartOfBar / EndOfBar handle 重定向到同一 Rebar Host 的指定
    exposed face，并要求新的 preferred constraint 为 ToCover。默认 dry_run=True。
    R05 保持 ElementId / UniqueId / Host / varying mode，且默认不允许未请求约束发生变化。
    """
    if rebar_id <= 0:
        raise ValueError("rebar_id 必须大于 0。")
    if not any([
        edge_face_stable_reference.strip(),
        start_face_stable_reference.strip(),
        end_face_stable_reference.strip(),
    ]):
        raise ValueError("至少提供一个 edge/start/end face stable reference。")

    return await _send_to_revit(
        "rebind_shape_rebar_constraints",
        {
            "rebarId": int(rebar_id),
            "expectedUniqueId": expected_unique_id,
            "edgeFaceStableReference": edge_face_stable_reference,
            "startFaceStableReference": start_face_stable_reference,
            "endFaceStableReference": end_face_stable_reference,
            "requirePlanarFace": bool(require_planar_face),
            "requireExposedFace": bool(require_exposed_face),
            "preserveUnretargetedConstraints": bool(preserve_unretargeted_constraints),
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def modify_shape_rebar(
    rebar_id: int,
    expected_unique_id: str = "",
    bar_type_id: int | None = None,
    number_of_bar_positions: int | None = None,
    spacing_mm: float | None = None,
    bars_on_normal_side: bool = True,
    include_first_bar: bool = True,
    include_last_bar: bool = True,
    require_constraint_preservation: bool = True,
    dry_run: bool = True,
) -> str:
    """R04 Shape Driven Rebar 原位修改。

    支持可选 BarType 切换与 NumberWithSpacing 更新。服务在提交前验证 ElementId、
    UniqueId、Host 和既有 Constraint 目标保持不变；dry_run=True 默认不写模型。
    Free Form / 约束重定向 / 拓扑重建不在 R04。
    """
    if rebar_id <= 0:
        raise ValueError("rebar_id 必须大于 0。")
    if number_of_bar_positions is not None and not (1 <= number_of_bar_positions <= 1002):
        raise ValueError("number_of_bar_positions 必须在 1..1002。")
    if spacing_mm is not None and spacing_mm <= 0:
        raise ValueError("spacing_mm 必须大于 0。")

    return await _send_to_revit(
        "modify_shape_rebar",
        {
            "rebarId": int(rebar_id),
            "expectedUniqueId": expected_unique_id,
            "barTypeId": int(bar_type_id) if bar_type_id is not None else None,
            "numberOfBarPositions": int(number_of_bar_positions) if number_of_bar_positions is not None else None,
            "spacingMm": float(spacing_mm) if spacing_mm is not None else None,
            "barsOnNormalSide": bool(bars_on_normal_side),
            "includeFirstBar": bool(include_first_bar),
            "includeLastBar": bool(include_last_bar),
            "requireConstraintPreservation": bool(require_constraint_preservation),
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def list_rebar_hook_types() -> str:
    """列出当前 Revit 文档中的 RebarHookType（只读）。"""
    return await _send_to_revit("list_rebar_hook_types", {}, timeout=30)


@mcp.tool()
async def constrain_rebar_to_rebar(
    source_rebar_id: int,
    target_rebar_id: int,
    expected_source_unique_id: str = "",
    expected_target_unique_id: str = "",
    source_handle_type: str = "Edge",
    source_handle_ordinal: int | None = None,
    target_kind: str = "Any",
    target_index: int | None = None,
    distance_mm: float | None = None,
    dry_run: bool = True,
) -> str:
    """R06：将既有 Shape Driven Rebar handle 约束到另一根 Shape Driven Rebar。

    只使用 Revit 原生 ToOtherRebar candidate，不人工伪造几何关系。若同类 handle/candidate
    多于一个，服务会拒绝猜测并要求 source_handle_ordinal / target_kind / target_index。
    默认 dry_run=True。target_kind 可用 Any / Edge / BarBend / HookBend。
    """
    if source_rebar_id <= 0 or target_rebar_id <= 0:
        raise ValueError("source_rebar_id / target_rebar_id 必须大于 0。")
    if source_rebar_id == target_rebar_id:
        raise ValueError("source_rebar_id / target_rebar_id 不能相同。")
    if source_handle_ordinal is not None and source_handle_ordinal < 0:
        raise ValueError("source_handle_ordinal 不能小于 0。")
    if distance_mm is not None and abs(distance_mm) > 9144000:
        raise ValueError("distance_mm 超出 R06 安全范围。")
    return await _send_to_revit(
        "constrain_rebar_to_rebar",
        {
            "sourceRebarId": int(source_rebar_id),
            "expectedSourceUniqueId": expected_source_unique_id,
            "targetRebarId": int(target_rebar_id),
            "expectedTargetUniqueId": expected_target_unique_id,
            "sourceHandleType": source_handle_type,
            "sourceHandleOrdinal": int(source_handle_ordinal) if source_handle_ordinal is not None else None,
            "targetKind": target_kind,
            "targetIndex": int(target_index) if target_index is not None else None,
            "distanceMm": float(distance_mm) if distance_mm is not None else None,
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def modify_rebar_hooks(
    rebar_id: int,
    expected_unique_id: str = "",
    start_hook_type_id: int | None = None,
    end_hook_type_id: int | None = None,
    dry_run: bool = True,
) -> str:
    """R06：原位修改 Shape Driven Rebar 的 start/end Hook Type。

    Hook Type Id 通过 list_rebar_hook_types 获取；传 -1 表示移除该端 Hook；None 表示不改。
    默认 dry_run=True。无效 shape/hook 组合由 Revit 拒绝并回滚。
    """
    if rebar_id <= 0:
        raise ValueError("rebar_id 必须大于 0。")
    if start_hook_type_id is None and end_hook_type_id is None:
        raise ValueError("至少指定 start_hook_type_id 或 end_hook_type_id；-1 表示移除。")
    return await _send_to_revit(
        "modify_rebar_hooks",
        {
            "rebarId": int(rebar_id),
            "expectedUniqueId": expected_unique_id,
            "startHookTypeId": int(start_hook_type_id) if start_hook_type_id is not None else None,
            "endHookTypeId": int(end_hook_type_id) if end_hook_type_id is not None else None,
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def list_rebar_coupler_types() -> str:
    """R10：列出当前项目可用的 Rebar Coupler Family Type（只读）。"""
    return await _send_to_revit("list_rebar_coupler_types", {}, timeout=30)


@mcp.tool()
async def create_rebar_coupler(
    first_rebar_id: int,
    first_end: int,
    second_rebar_id: int | None = None,
    second_end: int = 0,
    coupler_type_id: int | None = None,
    expected_first_unique_id: str = "",
    expected_second_unique_id: str = "",
    operation_id: str = "",
    dry_run: bool = True,
) -> str:
    """R10：使用 Revit 原生 RebarCoupler 创建钢筋机械连接。

    - second_rebar_id 有值：Two-Bar Coupler，first_end / second_end 必须为 0 或 1；
    - second_rebar_id 为空：Single-End Cap，只使用 first_rebar_id / first_end；
    - coupler_type_id 为空时优先使用项目默认 Coupler Type；
    - dry_run=True 只做身份、端点、已占用、Hook、布局和类型预检，不启动 Transaction；
    - apply 时才调用 RebarCoupler.Create，原生 RebarCouplerError 决定几何/直径兼容性。
    """
    if first_rebar_id <= 0:
        raise ValueError("first_rebar_id 必须大于 0。")
    if first_end not in (0, 1):
        raise ValueError("first_end 只能是 0 或 1。")
    if second_rebar_id is not None:
        if second_rebar_id <= 0:
            raise ValueError("second_rebar_id 有值时必须大于 0。")
        if second_rebar_id == first_rebar_id:
            raise ValueError("Two-Bar 模式下 first_rebar_id / second_rebar_id 不能相同。")
        if second_end not in (0, 1):
            raise ValueError("second_end 只能是 0 或 1。")
    if coupler_type_id is not None and coupler_type_id <= 0:
        raise ValueError("coupler_type_id 有值时必须大于 0。")

    return await _send_to_revit(
        "create_rebar_coupler",
        {
            "operationId": operation_id,
            "couplerTypeId": int(coupler_type_id) if coupler_type_id is not None else None,
            "firstRebarId": int(first_rebar_id),
            "firstEnd": int(first_end),
            "expectedFirstUniqueId": expected_first_unique_id,
            "secondRebarId": int(second_rebar_id) if second_rebar_id is not None else None,
            "secondEnd": int(second_end),
            "expectedSecondUniqueId": expected_second_unique_id,
            "dryRun": bool(dry_run),
        },
        timeout=100,
    )


@mcp.tool()
async def execute_revit_python(script: str) -> str:
    """在 Revit 主线程执行 IronPython；网关设置必须显式允许外部 Python。"""
    if not script.strip():
        raise ValueError("Python 脚本不能为空。")
    return await _send_to_revit(
        "execute_revit_python",
        {"script": script},
        timeout=100,
    )


def main() -> None:
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
