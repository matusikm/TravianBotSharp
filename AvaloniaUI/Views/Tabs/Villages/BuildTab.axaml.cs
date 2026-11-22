using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using JobItem = MainCore.UI.Models.Output.ListBoxItem;
using MainCore.UI.ViewModels.Tabs.Villages;

using ReactiveUI;
using ReactiveUI.Avalonia;


namespace AvaloniaUI.Views.Tabs.Villages
{
    using ReactiveUI.Primitives;

    public class BuildTabBase : ReactiveUserControl<BuildViewModel>
    {
    }

    /// <summary>
    /// Code-behind for BuildTab.axaml
    /// </summary>
    public partial class BuildTab : BuildTabBase
    {
        // Pixels the pointer must travel with the button held before a press becomes a drag,
        // so a plain click still just selects.
        private const double DragThreshold = 4;

        private ListBoxItem? _dragItem;
        private Point _dragStart;
        private bool _dragging;
        private ListBoxItem? _dropMarker;

        public BuildTab()
        {
            InitializeComponent();

            // Tunnel + handledEventsToo: ListBoxItem marks presses as handled when it selects.
            JobsGrid.AddHandler(PointerPressedEvent, OnJobPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            JobsGrid.AddHandler(PointerMovedEvent, OnJobPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            JobsGrid.AddHandler(PointerReleasedEvent, OnJobPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            JobsGrid.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), RoutingStrategies.Bubble, handledEventsToo: true);

            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.Buildings.Items, v => v.BuildingsGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Buildings.SelectedItem, v => v.BuildingsGrid.SelectedItem).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Buildings.SelectedIndex, v => v.BuildingsGrid.SelectedIndex).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.Jobs.Items, v => v.JobsGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Jobs.SelectedItem, v => v.JobsGrid.SelectedItem).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Jobs.SelectedIndex, v => v.JobsGrid.SelectedIndex).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.Queue.Items, v => v.QueueGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Queue.SelectedItem, v => v.QueueGrid.SelectedItem).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.Queue.SelectedIndex, v => v.QueueGrid.SelectedIndex).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.UpgradeOneLevelCommand, v => v.UpgradeOneLevelButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.UpgradeMaxLevelCommand, v => v.UpgradeMaxLevelButton).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.ImportCommand, v => v.ImportButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.ExportCommand, v => v.ExportButton).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.UpCommand, v => v.UpButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DownCommand, v => v.DownButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.TopCommand, v => v.TopButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.BottomCommand, v => v.BottomButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteCommand, v => v.DeleteButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteAllCommand, v => v.DeleteAllButton).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.TopCommand, v => v.JobTopButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.UpCommand, v => v.JobUpButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DownCommand, v => v.JobDownButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.BottomCommand, v => v.JobBottomButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteCommand, v => v.JobDeleteButton).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.BuildNormalCommand, v => v.NormalBuild).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.NormalBuildInput.Buildings, v => v.NormalBuildings.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.NormalBuildInput.SelectedBuilding, v => v.NormalBuildings.SelectedItem).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.NormalBuildInput.Level, v => v.NormalLevel.Text).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.BuildResourceCommand, v => v.ResourceBuild).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.ResourceBuildInput.Plans, v => v.ResType.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.ResourceBuildInput.SelectedPlan, v => v.ResType.SelectedItem).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.ResourceBuildInput.Level, v => v.ResourceLevel.Text).DisposeWith(d);
            });
        }

        private ListBoxItem? ItemAt(PointerEventArgs e) =>
            (JobsGrid.InputHitTest(e.GetPosition(JobsGrid)) as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);

        private void OnJobPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(JobsGrid).Properties.IsLeftButtonPressed) return;
            _dragItem = ItemAt(e);
            _dragStart = e.GetPosition(JobsGrid);
            _dragging = false;
        }

        private void OnJobPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragItem is null) return;
            if (!e.GetCurrentPoint(JobsGrid).Properties.IsLeftButtonPressed)
            {
                EndDrag();
                return;
            }

            if (!_dragging)
            {
                var delta = e.GetPosition(JobsGrid) - _dragStart;
                if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;
                _dragging = true;
                e.Pointer.Capture(JobsGrid);
                JobsGrid.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
            }

            ShowDropMarker(DropIndex(e));
            e.Handled = true;
        }

        private void OnJobPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging || _dragItem is null)
            {
                EndDrag();
                return;
            }

            var from = JobsGrid.IndexFromContainer(_dragItem);
            var to = DropIndex(e);
            var job = _dragItem.DataContext as JobItem;
            EndDrag();
            e.Pointer.Capture(null);
            e.Handled = true;

            if (job is null || ViewModel is null || from < 0 || to < 0 || from == to) return;
            ViewModel.MoveToCommand.Execute(new JobMove(job.Id, to)).Subscribe();
        }

        /// <summary>Index the dragged job lands at: the row under the pointer, or the end when below the last row.</summary>
        private int DropIndex(PointerEventArgs e)
        {
            var count = JobsGrid.ItemCount;
            if (count == 0) return -1;
            var target = ItemAt(e);
            if (target is not null) return JobsGrid.IndexFromContainer(target);
            return e.GetPosition(JobsGrid).Y <= 0 ? 0 : count - 1;
        }

        private void ShowDropMarker(int to)
        {
            ClearDropMarker();
            if (_dragItem is null || to < 0) return;
            var from = JobsGrid.IndexFromContainer(_dragItem);
            if (from == to) return;
            if (JobsGrid.ContainerFromIndex(to) is not ListBoxItem target) return;

            // Moving down lands after the target row, moving up lands before it.
            target.Classes.Add(to > from ? "dropBelow" : "dropAbove");
            _dropMarker = target;
        }

        private void ClearDropMarker()
        {
            _dropMarker?.Classes.Remove("dropAbove");
            _dropMarker?.Classes.Remove("dropBelow");
            _dropMarker = null;
        }

        private void EndDrag()
        {
            ClearDropMarker();
            _dragItem = null;
            _dragging = false;
            JobsGrid.Cursor = null;
        }
    }
}
