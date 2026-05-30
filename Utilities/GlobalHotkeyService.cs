using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TimeTrack.Data;
using System.Globalization;
using System.Threading.Tasks;

namespace TimeTrack.Utilities
{
    public sealed class GlobalHotkeyService : IDisposable
    {
        private readonly Window _window;
        private HwndSource? _source;
        private const int HOTKEY_ID = 0x5454; // TT

        public GlobalHotkeyService(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void Initialize()
        {
            _window.SourceInitialized += OnSourceInitialized;
            _window.Closed += (s, e) => Dispose();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(_window);
            _source = HwndSource.FromHwnd(helper.Handle);
            if (_source == null) return;
            _source.AddHook(HwndHook);

            // Default: Ctrl+Shift+E (customizable via SettingsManager later if desired)
            var shortcut = SettingsManager.GetShortcut("GlobalExport") ?? new KeyboardShortcut
            {
                Key = Key.E,
                Modifiers = ModifierKeys.Control | ModifierKeys.Shift
            };

            Register(shortcut);
        }

        private void Register(KeyboardShortcut sc)
        {
            var helper = new WindowInteropHelper(_window);
            var mods = 0u;
            if (sc.Modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
            if (sc.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
            if (sc.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
            if (sc.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

            var vk = (uint)KeyInterop.VirtualKeyFromKey(sc.Key);
            NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID, mods, vk);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _ = HandleGlobalExportAsync();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private async Task HandleGlobalExportAsync()
        {
            try
            {
                await ExportAndMarkRecordedAsync();
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle("Global export hotkey failed.", ex);
            }
        }

        private async Task ExportAndMarkRecordedAsync()
        {
            if (_window is not TimeTrack.Views.MainWindow mw) return;
            var vm = mw.DataContext as TimeTrack.ViewModels.TimeKeeperViewModel;
            var entries = vm?.Entries;
            if (vm == null || entries == null || entries.Count == 0) return;

            // Show initial status
            mw.ShowStatus("Global export hotkey triggered - finding entry...");

            // Prefer currently selected, else next unrecorded
            TimeEntry? entry = vm.SelectedItem ?? entries.FirstOrDefault(e => !e.Recorded && e.StartTime != null && e.EndTime != null);
            if (entry == null) entry = entries.FirstOrDefault(e => e.StartTime != null && e.EndTime != null);
            if (entry == null)
            {
                mw.ShowStatus("No valid entry found to export.");
                return;
            }

            mw.ShowStatus("Entry found - building export data...");

            var start = entry.StartTime!.Value;
            var end = entry.EndTime!.Value;
            if (end < start)
                throw new InvalidOperationException("Cannot export a negative time duration");

            // Build tokens
            var activity = new DateTime(entry.Date.Year, entry.Date.Month, entry.Date.Day, start.Hour, start.Minute, start.Second);
            string activityDate = activity.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

            var ts = (TimeSpan)(end - start);
            int hoursWorked = ts.Hours;
            double minutesWorked = ts.Minutes;
            double timeWorked = hoursWorked + (Math.Ceiling((minutesWorked / 60) * 10) / 10);
            string units = timeWorked.ToString(CultureInfo.InvariantCulture);
            string notes = entry.Notes ?? string.Empty;

            mw.ShowStatus("Data built - marking entry recorded...");

            // Mark recorded and persist
            entry.Recorded = true;
            TimeTrack.Data.Database.Update(entries);

            mw.ShowStatus("Entry marked recorded - data copied to clipboard.");
        }

        public void Dispose()
        {
            try
            {
                if (_source != null)
                    _source.RemoveHook(HwndHook);

                var helper = new WindowInteropHelper(_window);
                NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID);
            }
            catch { }
        }
    }
}
