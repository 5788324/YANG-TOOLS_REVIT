using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using MediaColor = System.Windows.Media.Color;

namespace YangTools.Revit.UI
{
    public partial class BooleanGeometryWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        
        public BooleanGeometryState State { get; private set; }
        public bool IsExecute { get; private set; } = false;
        public string ActionToPerform { get; private set; } = null;

        public BooleanGeometryWindow(UIApplication uiapp, BooleanGeometryState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            State = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ChkDeleteTargetInst.IsChecked = State.DeleteTargetInst;
            ChkDeleteTargetFam.IsChecked = State.DeleteTargetFam;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            SetSelectionLabel(TxtMain, State.MainInst);
            SetSelectionLabel(TxtUnion, State.UnionInsts);
            SetSelectionLabel(TxtCut, State.CutInsts);
            SetSelectionLabel(TxtJoin, State.JoinInsts);
        }

        private static void SetSelectionLabel(System.Windows.Controls.TextBlock label, IList<FamilyInstance> selected)
        {
            string full = selected.Count == 0 ? "未选择" :
                string.Join(", ", selected.Select(x => $"{x.Name} (ID: {x.Id})"));
            label.Text = selected.Count == 0 ? "未选择" : $"已选择 {selected.Count} 个实例";
            label.ToolTip = full;
            label.TextTrimming = System.Windows.TextTrimming.CharacterEllipsis;
        }

        private void TriggerPick(string action)
        {
            CaptureAndPersistOptions();
            ActionToPerform = action;
            Close();
        }

        private void BtnSelectMain_Click(object sender, RoutedEventArgs e) => TriggerPick("PickMain");
        private void BtnSelectUnion_Click(object sender, RoutedEventArgs e) => TriggerPick("PickUnion");
        private void BtnSelectCut_Click(object sender, RoutedEventArgs e) => TriggerPick("PickCut");
        private void BtnSelectJoin_Click(object sender, RoutedEventArgs e) => TriggerPick("PickJoin");

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CaptureAndPersistOptions();
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (State.MainInst.Count == 0)
            {
                TaskDialog.Show("提示", "请先选择主体实例。");
                return;
            }

            CaptureAndPersistOptions();
            IsExecute = true;
            Close();
        }

        private void CaptureAndPersistOptions()
        {
            State.DeleteTargetInst = ChkDeleteTargetInst.IsChecked == true;
            State.DeleteTargetFam = ChkDeleteTargetFam.IsChecked == true;

            UserSettings settings = UserSettings.Load();
            settings.BooleanDeleteTargetInst = State.DeleteTargetInst;
            settings.BooleanDeleteTargetFam = State.DeleteTargetFam;
            settings.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            CaptureAndPersistOptions();
            base.OnClosed(e);
        }
    }
}
