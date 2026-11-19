using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class FamilyInstanceCoordinatesWindow : Window
    {
        private readonly Document _document;
        private readonly Transform[] _fromInternal;

        public FamilyInstanceCoordinatesWindow(Document document, IReadOnlyList<FamilyInstance> instances)
        {
            _document = document;
            _fromInternal = new[]
            {
                CreateSurveyTransform(document.ActiveProjectLocation),
                Transform.CreateTranslation(-BasePoint.GetProjectBasePoint(document).Position),
                Transform.Identity
            };

            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            InstanceBox.ItemsSource = instances.Select(instance => new InstanceOption(
                instance, $"{instance.Symbol.FamilyName} · {instance.Name}  ({instance.Id.GetIdValue()})")).ToList();
            InstanceBox.SelectedIndex = 0;
        }

        private static Transform CreateSurveyTransform(ProjectLocation location)
        {
            // GetProjectPosition reports shared coordinates. Derive its east/north
            // rotation from an internal X baseline, independently of API angle conventions.
            var origin = SharedPosition(location, XYZ.Zero);
            const double baseline = 1024;
            var east = SharedPosition(location, new XYZ(baseline, 0, 0)) - origin;
            var transform = Transform.Identity;
            transform.Origin = origin;
            transform.BasisX = new XYZ(east.X, east.Y, 0).Normalize();
            transform.BasisY = XYZ.BasisZ.CrossProduct(transform.BasisX);
            return transform;
        }

        private static XYZ SharedPosition(ProjectLocation location, XYZ point)
        {
            var position = location.GetProjectPosition(point);
            return new XYZ(position.EastWest, position.NorthSouth, position.Elevation);
        }

        private void InstanceChanged(object sender, SelectionChangedEventArgs e) => RefreshCoordinates();

        private void RefreshCoordinates()
        {
            if (InstanceBox.SelectedItem is not InstanceOption selectedInstance) return;
            var selected = CoordinateGrid.SelectedIndex;
            var instance = selectedInstance.Instance;
            var point = ((LocationPoint)instance.Location).Point;

            CoordinateGrid.ItemsSource = new[]
            {
                new CoordinateRow("测量坐标", _fromInternal[0].OfPoint(point)),
                new CoordinateRow("项目基点坐标", _fromInternal[1].OfPoint(point)),
                new CoordinateRow("内部原点坐标", point)
            };
            CoordinateGrid.SelectedIndex = selected < 0 ? 1 : selected;

            bool locked = instance.Pinned || instance.GroupId != ElementId.InvalidElementId || _document.IsReadOnly;
            ApplyButton.IsEnabled = !locked;
            StatusText.Text = locked ? "该实例已固定、在组内或项目只读，仅可查看。" : string.Empty;
        }

        private void CoordinateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CoordinateGrid.SelectedItem is not CoordinateRow row) return;
            TargetLabel.Text = $"目标位置 · {row.Label}（mm）";
            TargetX.Text = row.X;
            TargetY.Text = row.Y;
            TargetZ.Text = row.Z;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var row = (CoordinateRow)CoordinateGrid.SelectedItem;
            if (!TryCoordinate(TargetX.Text, row.X, row.Point.X, out double x) ||
                !TryCoordinate(TargetY.Text, row.Y, row.Point.Y, out double y) ||
                !TryCoordinate(TargetZ.Text, row.Z, row.Point.Z, out double z))
            {
                StatusText.Text = "请输入有效的 X、Y、Z 数值，单位为毫米。";
                return;
            }

            var instance = ((InstanceOption)InstanceBox.SelectedItem).Instance;
            var current = ((LocationPoint)instance.Location).Point;
            var target = _fromInternal[CoordinateGrid.SelectedIndex].Inverse.OfPoint(new XYZ(x, y, z));
            var displacement = target - current;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.001, UnitTypeId.Millimeters);
            if (displacement.GetLength() <= tolerance)
            {
                StatusText.Text = "位置没有变化。";
                return;
            }

            try
            {
                using (var transaction = new Transaction(_document, "移动族实例到坐标"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("Revit 无法开始移动。");

                    ElementTransformUtils.MoveElement(_document, instance.Id, displacement);
                    _document.Regenerate();
                    if (instance.Location is not LocationPoint actual || actual.Point.DistanceTo(target) > tolerance)
                    {
                        transaction.RollBack();
                        StatusText.Text = "实例受到宿主或模型约束，无法移动到指定位置。";
                        return;
                    }

                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit 未提交移动，请检查模型。");
                }

                RefreshCoordinates();
                StatusText.Text = "位置已更新，三套坐标已刷新。可按 Ctrl+Z 撤销。";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private static bool TryCoordinate(string input, string displayed, double original, out double result)
        {
            result = original;
            if (input.Trim() == displayed) return true;
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out double mm) ||
                double.IsNaN(mm) || double.IsInfinity(mm)) return false;
            result = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
            return true;
        }

        private sealed class InstanceOption
        {
            public FamilyInstance Instance { get; }
            public string Label { get; }

            public InstanceOption(FamilyInstance instance, string label)
            {
                Instance = instance;
                Label = label;
            }
        }

        private sealed class CoordinateRow
        {
            public string Label { get; }
            public XYZ Point { get; }
            public string X => Format(Point.X);
            public string Y => Format(Point.Y);
            public string Z => Format(Point.Z);

            public CoordinateRow(string label, XYZ point)
            {
                Label = label;
                Point = point;
            }

            private static string Format(double value) =>
                UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)
                    .ToString("0.###", CultureInfo.CurrentCulture);
        }
    }
}
