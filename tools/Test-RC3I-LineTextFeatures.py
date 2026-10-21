#!/usr/bin/env python3
"""RC3I source-only invariant contract. Does NOT compile or run Revit.
UI/geometry behavioral outcomes require Windows/DeepSeek evidence.
"""
from pathlib import Path
import re,json
from xml.etree import ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'src/YangTools.Revit'
read=lambda p:(S/p).read_text(encoding='utf-8')
a=read('Commands/MergeCollinearDetailLinesCommand.cs')
b=read('Commands/MergeTextColumnsCommand.cs')
c=read('Commands/MergeTextCommand.cs')
u=read('UI/MergeTextWindow.xaml')
layout=read('Core/TextLayoutMerger.cs')
tests={
 'line command ribbon': '[RibbonButton("模型修改区", "合并直线"' in a,
 'line selected only': 'Selection.GetElementIds()' in a and 'PickObjects(ObjectType.Element' in a,
 'detail straight only': 'DetailCurve curve' in a and 'GeometryCurve is Line' in a,
 'line no pinned/groups/foreign views': 'curve.Pinned' in a and 'curve.GroupId' in a and 'curve.OwnerViewId' in a,
 'line same style collinear endpoints': 'StyleId.GetIdValue()' in a and 'CrossProduct' in a and 'JoinToleranceFeet' in a,
 'line no curve below minimum': 'doc.Application.ShortCurveTolerance' in a,
 'line preview and one transaction': '合并直线预览' in a and 'new Transaction(doc, "合并共线详图线")' in a,
 'line rollback and delete': 'transaction.RollBack()' in a and 'doc.Delete(segment.Curve.Id)' in a,
 'columns ribbon': '[RibbonButton("文本工具区", "双列文字合并"' in b,
 'columns grouping': 'TryColumns' in b and 'TryColumns(View view' in layout,
 'columns two explicit output choices': 'TaskDialogCommandLinkId.CommandLink1' in b and 'TaskDialogCommandLinkId.CommandLink2' in b,
 'columns only same text type': 'GetTypeId().GetIdValue()' in b,
 'columns reject rotated text': 'BaseDirection.Normalize()' in b,
 'columns remove all old in transaction': 'new Transaction(doc, "双列文字合并")' in b and 'doc.Delete(original.Id)' in b,
 'columns use unwrapped 2021 TextNote.Create': 'TextNote.Create(doc, view.Id, anchor.Coord, finalTexts[i], options)' in b,
 'existing text command reading mode': 'case MergeSortMode.ReadingOrder:' in c and 'ReadingText(doc.ActiveView' in c,
 'existing text command keeps old modes': all(('MergeSortMode.'+x) in c for x in ('SelectionOrder','XAsc','XDesc','YAsc','YDesc')) if 'MergeSortMode.SelectionOrder' in c else all(('MergeSortMode.'+x) in c for x in ('XAsc','XDesc','YAsc','YDesc')),
 'existing text cleanup': 'CleanWhitespace' in c and 'Regex.Replace' in layout,
 'existing text preview': '文本合并预览' in c,
 'view-relative 2d reading': 'view.RightDirection' in layout and 'view.UpDirection' in layout,
 'row grouping respects View.Scale': 'size * Math.Max(1, view.Scale)' in layout,
 'image size pairs': all((S/'Icons'/f'{name}_{dim}.png').exists() for name in ('merge_lines','merge_columns') for dim in (16,32)),
 'new ribbon count': sum(p.read_text(encoding='utf8').count('[RibbonButton') for p in (S/'Commands').glob('*.cs'))==34,
 'modified UI valid XML': ET.parse(S/'UI/MergeTextWindow.xaml') is not None,
 'old text state includes cleanup': 'CleanWhitespace { get; set; } = true;' in read('Models/TextToolStates.cs'),
 'safe user viewing docs present': (ROOT/'docs/RC3I_LINE_TEXT_FEATURES_20261021.md').exists(),
}
failed=[k for k,v in tests.items() if not v]
print(json.dumps({'suite':'RC3I line/text SOURCE-ONLY','total':len(tests),'passed':len(tests)-len(failed),'failed':failed,'runtime':'NOT_TESTED'},ensure_ascii=False,indent=2))
if failed:raise SystemExit(1)
