using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeTrack.Data;
using TimeTrack.ViewModels;

namespace TimeTrack.Views.Dialogs
{
    public partial class ParkingLotDialog : Window
    {
        private readonly TimeKeeperViewModel _viewModel;

        public ParkingLotDialog(TimeKeeperViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            UpdateStatus();
            TxtInput.Focus();
        }

        private void ParkInput()
        {
            var text = TxtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            _viewModel.AddParkingLotItem(text);
            TxtInput.Clear();
            UpdateStatus();
        }

        private void BtnPark_Click(object sender, RoutedEventArgs e)
        {
            ParkInput();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ParkInput();
                e.Handled = true;
            }
        }

        private void BtnResolve_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ParkingLotItem item)
            {
                _viewModel.ResolveParkingLotItem(item);
                UpdateStatus();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ParkingLotItem item)
            {
                _viewModel.DeleteParkingLotItem(item);
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            var count = _viewModel.ParkingLotItems.Count;
            TxtStatus.Text = $"{count} item{(count == 1 ? "" : "s")} parked";
        }
    }
}
