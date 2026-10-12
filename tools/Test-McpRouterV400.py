#!/usr/bin/env python3
"""RC1A static contract for the 3.1.38 -> 4.0.0 MCP routing extraction.

Source-only checks: these are not an assertion that the plugin builds/loads in Revit.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src' / 'YangTools.Revit'
HANDLERS = SRC / 'Mcp' / 'Handlers'
read = lambda p: p.read_text(encoding='utf-8-sig')
checks: list[tuple[str, bool]] = []
def check(label: str, ok: bool) -> None:
    checks.append((label, bool(ok)))

broker = read(SRC / 'Mcp' / 'RevitExecutionBroker.cs')
router = read(HANDLERS / 'RevitToolRouter.cs')
core = read(HANDLERS / 'CoreDocumentToolHandler.cs')
rebar = read(HANDLERS / 'RebarToolHandler.cs')
python = read(HANDLERS / 'PythonToolHandler.cs')
gateway = read(SRC / 'Mcp' / 'McpGateway.cs')
fixture = json.loads(read(ROOT / 'tools/fixtures/YANGTOOLS_MCP_COMMANDS_V4.json'))

checks_expected = {x['name']: x['domain'] for x in fixture['commands']}
parsed = re.findall(r'case "([^"]+)":\s+return ([^;]+);', router)
check('baseline 27 distinct commands', len(checks_expected)==27 and len(parsed)==27 and len(set(n for n,_ in parsed))==27)
check('wire names unchanged', set(n for n,_ in parsed) == set(checks_expected))
for name, body in parsed:
    domain=checks_expected.get(name)
    expected = 'CoreDocumentToolHandler.' if domain=='core' else 'RebarToolHandler.' if domain=='rebar' else '_python.'
    check(f'command routed to expected domain: {name}', expected in body)
    if domain in ('core','rebar'):
        method=re.search(r'(?:CoreDocumentToolHandler|RebarToolHandler)\.(\w+)\(',body)
        handler=core if domain=='core' else rebar
        check(f'public-to-router adapter exists: {name}', bool(method and re.search(r'\binternal static \w+ '+re.escape(method.group(1))+r'\s*\(',handler)))

check('ExternalEvent remains the only broker entry', 'ExternalEvent.Create(_handler)' in broker and '_externalEvent.Raise()' in broker)
check('main thread owns dispatch', 'RevitExecutionResult result = _router.Dispatch(uiapp, request.Command, request.Args);' in broker)
check('queue cancellation/timeout preserved', all(t in broker for t in ('ConcurrentQueue<RevitExecutionRequest>','Task.WhenAny(request.Completion.Task, delay)','CancellationToken.IsCancellationRequested','TryCompleteFailure')))
check('broker no longer embeds business handlers', all(t not in broker for t in ('FilteredElementCollector','TransactionGroup(', 'RebarGradientSetService.Execute', 'ScriptEngine?', 'switch (command)')))
check('core read-only adapter uses no transaction', all(t not in core for t in ('Transaction(', 'TransactionGroup(')))
check('domain services remain wired', all(t in rebar for t in ('RebarInspectionService.Inspect','RebarGradientSetService.Execute','RebarCouplerService.Execute','RebarShapeDrivenCreateService.Execute')))
check('python editor runs only via router', '_python.ExecutePython(uiapp, args)' in router and 'ExecutePython(uiapp, args)' not in broker)
check('python writes transaction/rollback unchanged', all(t in python for t in ('TransactionGroup(document, "YangTools MCP Python")','group.Assimilate()','group.RollBack()')))
check('python status available through broker', all(t in broker for t in ('_pythonHandler.PythonAvailable','_pythonHandler.PythonInitialized','_pythonHandler.PythonError')))
check('python gateway permission gate unchanged', all(t in gateway for t in ('command.Equals("execute_revit_python", StringComparison.Ordinal)', '!settings.AllowPythonExecution','TimeSpan.FromSeconds(60)')))
check('MCP endpoint command/args and reply unchanged', all(t in gateway for t in ('"/mcp"','body["command"]','body["args"]','["success"] = result.Success','["result"] = result.Result','["error"] = result.Error','["durationMs"] = result.DurationMilliseconds')))
check('MCP unknown command remains failure', 'return Failure("不支持的 Revit 执行命令：" + command);' in router)
check('no version-specific DLL explosion', len(list(SRC.rglob('*.csproj')))==1)
project = ElementTree.fromstring(read(SRC/'YangTools.Revit.csproj'))
props = {x.tag: x.text for pg in project.findall('PropertyGroup') for x in pg if x.tag in ('Version','AssemblyVersion','FileVersion','InformationalVersion')}
check('version tags correct', props=={'Version':'4.0.0','AssemblyVersion':'4.0.0.0','FileVersion':'4.0.0.0','InformationalVersion':'4.0.0'})
check('UI version visible', 'Text="4.0.0"' in read(SRC/'UI/SystemSettingsWindow.xaml'))
check('Revit 2021-2027 targets left intact', '2021;2022;2023;2024;2025;2026;2027' in read(SRC/'YangTools.Revit.csproj'))
check('package excludes artifacts and caches', all(s in read(ROOT/'tools/Package-Source.ps1') for s in ("'artifacts'","'obj'","'bin'","'__pycache__'")))

failed=[n for n,ok in checks if not ok]
print(f'V4 MCP routing compatibility/static checks: {len(checks)-len(failed)}/{len(checks)} PASS')
for n in failed:print('FAIL:',n)
if failed:sys.exit(1)
