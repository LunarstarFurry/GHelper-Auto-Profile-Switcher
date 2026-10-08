using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GHelperAutoProfileSwitcher
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<AppProfile> _profiles = new();
        private TargetMode _defaultMode = TargetMode.Balanced;
        private DispatcherTimer _timer = null!;
        private NotifyIcon _notifyIcon = null!;
        private TargetMode _currentMode = TargetMode.Balanced;
        private TargetMode _lastTargetMode = TargetMode.Balanced;
        private IntPtr _currentIconHandle = IntPtr.Zero;

        private bool _isPaused = false;
        private DateTime? _pauseUntil = null;
        private ToolStripMenuItem? _pauseMenuItem;
        private ToolStripMenuItem? _resumeMenuItem;

        private bool _isSwitching = false;
        private int _retryCount = 0;
        private const int MaxRetries = 3;
        private int _lastSelectedPauseIndex = 4; // Default to 'Indefinitely'

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private void UpdateTrayIcon()
        {
            int width = 16;
            int height = 16;
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    System.Drawing.Color color = System.Drawing.Color.White;
                    if (_isPaused)
                    {
                        color = System.Drawing.Color.Gray;
                    }
                    else
                    {
                        switch (_currentMode)
                        {
                            case TargetMode.Silent: color = System.Drawing.Color.DeepSkyBlue; break;
                            case TargetMode.Balanced: color = System.Drawing.Color.White; break;
                            case TargetMode.Turbo: color = System.Drawing.Color.Red; break;
                        }
                    }
                    using (Brush brush = new SolidBrush(color))
                    {
                        g.FillEllipse(brush, 0, 0, 15, 15);
                    }
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold))
                    {
                        string text = _isPaused ? "P" : _currentMode.ToString().Substring(0, 1);
                        using (Brush textBrush = new SolidBrush(System.Drawing.Color.Black))
                        {
                            StringFormat sf = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString(text, font, textBrush, new RectangleF(0, 0, 16, 16), sf);
                        }
                    }
                }
                
                IntPtr hIcon = bitmap.GetHicon();
                System.Drawing.Icon newIcon = System.Drawing.Icon.FromHandle(hIcon);

                var oldIcon = _notifyIcon.Icon;
                _notifyIcon.Icon = newIcon;
                string adminSuffix = IsAdministrator() ? " (Admin)" : string.Empty;
                _notifyIcon.Text = $"G-Helper - {_currentMode}{adminSuffix}";

                if (_currentIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_currentIconHandle);
                }
                if (oldIcon != null && oldIcon != SystemIcons.Application)
                {
                    oldIcon.Dispose();
                }
                _currentIconHandle = hIcon;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            
            ModeColumn.ItemsSource = Enum.GetValues(typeof(TargetMode));

            var config = ConfigManager.LoadConfig();
            _defaultMode = config.DefaultMode;
            _profiles = new ObservableCollection<AppProfile>(config.Profiles);
            ProfilesGrid.ItemsSource = _profiles;
            ProfilesGrid.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(SaveConfig));

            DefaultModeComboBox.ItemsSource = Enum.GetValues(typeof(TargetMode));
            DefaultModeComboBox.SelectedItem = _defaultMode;

            // Sync initial state with G-Helper if readable
            var initialGHelperMode = GHelperStatus.GetActiveGHelperMode();
            if (initialGHelperMode.HasValue)
            {
                _currentMode = initialGHelperMode.Value;
                _lastTargetMode = _currentMode;
            }
            else
            {
                _currentMode = _defaultMode;
                _lastTargetMode = _defaultMode;
            }
            CurrentModeText.Text = _currentMode.ToString();

            if (IsAdministrator())
            {
                Title += " [Administrator]";
                ElevateButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                ElevateButton.Visibility = Visibility.Visible;
            }

            SetupTrayIcon();
            CheckStartWithWindows();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = true
            };
            UpdateTrayIcon();
            
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => 
            {
                Show();
                WindowState = WindowState.Normal;
            });

            if (!IsAdministrator())
            {
                contextMenu.Items.Add("Restart as Administrator", null, (s, e) => RestartAsAdmin());
            }

            _pauseMenuItem = new ToolStripMenuItem("Pause Agent");
            _pauseMenuItem.DropDownItems.Add("1 Hour", null, (s, e) => PauseAgent(1));
            _pauseMenuItem.DropDownItems.Add("4 Hours", null, (s, e) => PauseAgent(4));
            _pauseMenuItem.DropDownItems.Add("8 Hours", null, (s, e) => PauseAgent(8));
            _pauseMenuItem.DropDownItems.Add("24 Hours", null, (s, e) => PauseAgent(24));
            _pauseMenuItem.DropDownItems.Add("Indefinitely", null, (s, e) => PauseAgent(0));
            contextMenu.Items.Add(_pauseMenuItem);

            _resumeMenuItem = new ToolStripMenuItem("Resume Agent");
            _resumeMenuItem.Click += (s, e) => ResumeAgent();
            _resumeMenuItem.Visible = false;
            contextMenu.Items.Add(_resumeMenuItem);

            contextMenu.Items.Add("Exit", null, (s, e) => 
            {
                _timer.Stop();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                if (_currentIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_currentIconHandle);
                    _currentIconHandle = IntPtr.Zero;
                }
                Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isPaused)
            {
                if (_pauseUntil.HasValue && DateTime.Now >= _pauseUntil.Value)
                {
                    ResumeAgent();
                }
                else
                {
                    if (_pauseUntil.HasValue)
                    {
                        var remaining = _pauseUntil.Value - DateTime.Now;
                        PauseDurationComboBox.Text = $"{(int)remaining.TotalHours:D2}h {remaining.Minutes:D2}m";
                    }
                    return;
                }
            }

            // Get running processes safely and dispose process handles
            HashSet<string> runningProcesses;
            var processes = Process.GetProcesses();
            try
            {
                runningProcesses = processes.Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
            
            TargetMode targetMode = _defaultMode;
            foreach (var profile in _profiles)
            {
                if (runningProcesses.Contains(profile.ProcessName))
                {
                    targetMode = profile.Mode;
                    if (targetMode == TargetMode.Turbo) break;
                }
            }

            var actualGHelperMode = GHelperStatus.GetActiveGHelperMode();

            // Scenario 1: Target mode changed (e.g. app launched or closed)
            if (targetMode != _lastTargetMode)
            {
                bool isAppExit = (targetMode == _defaultMode);
                _lastTargetMode = targetMode;
                _retryCount = 0;
                _ = ApplyModeChangeAsync(targetMode, isAppExit);
            }
            // Scenario 2: Target mode hasn't changed, but G-Helper is desynced (e.g. key was missed on app exit)
            else if (actualGHelperMode.HasValue && actualGHelperMode.Value != targetMode)
            {
                if (_retryCount < MaxRetries)
                {
                    _retryCount++;
                    _ = ApplyModeChangeAsync(targetMode, isAppExit: false);
                }
            }
            // Scenario 3: G-Helper matches target mode
            else if (actualGHelperMode.HasValue && actualGHelperMode.Value == targetMode)
            {
                _retryCount = 0;
                if (_currentMode != targetMode)
                {
                    _currentMode = targetMode;
                    CurrentModeText.Text = _currentMode.ToString();
                    UpdateTrayIcon();
                }
            }
        }

        private async Task ApplyModeChangeAsync(TargetMode targetMode, bool isAppExit)
        {
            if (_isSwitching) return;
            _isSwitching = true;

            try
            {
                // When an app exits, wait 300ms for DirectX / exclusive fullscreen / resolution switch to settle
                if (isAppExit)
                {
                    await Task.Delay(300);
                }

                await GHelperHotkeys.SetModeAsync(targetMode);

                // Give G-Helper a brief window to process the hotkey and write state
                await Task.Delay(700);

                var actual = GHelperStatus.GetActiveGHelperMode();
                if (actual.HasValue)
                {
                    if (actual.Value == targetMode)
                    {
                        _currentMode = targetMode;
                        CurrentModeText.Text = _currentMode.ToString();
                        UpdateTrayIcon();
                        _retryCount = 0;
                    }
                    else if (_retryCount < MaxRetries)
                    {
                        // Retry sending the hotkey if G-Helper didn't catch the first pulse
                        _retryCount++;
                        await GHelperHotkeys.SetModeAsync(targetMode);
                        await Task.Delay(700);

                        var retryActual = GHelperStatus.GetActiveGHelperMode();
                        if (retryActual.HasValue && retryActual.Value == targetMode)
                        {
                            _currentMode = targetMode;
                            CurrentModeText.Text = _currentMode.ToString();
                            UpdateTrayIcon();
                            _retryCount = 0;
                        }
                    }
                }
                else
                {
                    // Fallback when config cannot be read directly
                    _currentMode = targetMode;
                    CurrentModeText.Text = _currentMode.ToString();
                    UpdateTrayIcon();

                    if (isAppExit)
                    {
                        // Send a confirmation pulse 1s later to guarantee focus transition didn't swallow it
                        await Task.Delay(1000);
                        await GHelperHotkeys.SetModeAsync(targetMode);
                    }
                }
            }
            finally
            {
                _isSwitching = false;
            }
        }

        private void DefaultModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DefaultModeComboBox.SelectedItem is TargetMode mode)
            {
                _defaultMode = mode;
                SaveConfig();
                Timer_Tick(null, EventArgs.Empty);
            }
        }

        private void SaveConfig()
        {
            var config = new AppConfig
            {
                DefaultMode = _defaultMode,
                Profiles = _profiles.ToList()
            };
            ConfigManager.SaveConfig(config);
        }

        private void AddCurrentApp_Click(object sender, RoutedEventArgs e)
        {
            var currentAppExe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
            var processes = Process.GetProcesses();
            List<ProcessInfo> runningApps;

            try
            {
                runningApps = processes
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                    .Where(p => !p.ProcessName.Equals(currentAppExe, StringComparison.OrdinalIgnoreCase) &&
                                !p.ProcessName.Equals("GHelper", StringComparison.OrdinalIgnoreCase))
                    .Where(p => !_profiles.Any(prof => prof.ProcessName.Equals(p.ProcessName, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => new ProcessInfo 
                    { 
                        ProcessName = p.ProcessName, 
                        WindowTitle = p.MainWindowTitle 
                    })
                    .GroupBy(p => p.ProcessName)
                    .Select(g => g.First())
                    .OrderBy(p => p.ProcessName)
                    .ToList();
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }

            var dialog = new ProcessSelectionDialog(runningApps);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                string selectedProcess = dialog.SelectedProcess;
                if (!string.IsNullOrEmpty(selectedProcess) && !_profiles.Any(p => p.ProcessName.Equals(selectedProcess, StringComparison.OrdinalIgnoreCase)))
                {
                    _profiles.Add(new AppProfile { ProcessName = selectedProcess, Mode = TargetMode.Turbo });
                    SaveConfig();
                    Timer_Tick(null, EventArgs.Empty);
                }
            }
        }

        private void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is AppProfile profile)
            {
                _profiles.Remove(profile);
                SaveConfig();
                Timer_Tick(null, EventArgs.Empty);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            MessageBox.Show("Configuration saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void CheckStartWithWindows()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key != null)
                {
                    StartWithWindowsCheckBox.IsChecked = key.GetValue("GHelperAutoProfileSwitcher") != null;
                }
            }
        }

        private void StartWithWindowsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    if (StartWithWindowsCheckBox.IsChecked == true)
                    {
                        string path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            key.SetValue("GHelperAutoProfileSwitcher", $"\"{path}\" -hidden");
                        }
                    }
                    else
                    {
                        key.DeleteValue("GHelperAutoProfileSwitcher", false);
                    }
                }
            }
        }

        private void PauseAgent(double hours)
        {
            _lastSelectedPauseIndex = PauseDurationComboBox.SelectedIndex;
            _isPaused = true;
            if (hours > 0)
                _pauseUntil = DateTime.Now.AddHours(hours);
            else
                _pauseUntil = null;
            
            UpdatePauseUI();
            UpdateTrayIcon();
        }

        private void ResumeAgent()
        {
            _isPaused = false;
            _pauseUntil = null;
            
            UpdatePauseUI();
            UpdateTrayIcon();

            if (_lastSelectedPauseIndex >= 0 && _lastSelectedPauseIndex < PauseDurationComboBox.Items.Count)
            {
                PauseDurationComboBox.SelectedIndex = _lastSelectedPauseIndex;
            }
            else
            {
                PauseDurationComboBox.SelectedIndex = 4;
            }

            Timer_Tick(null, EventArgs.Empty);
        }

        private void UpdatePauseUI()
        {
            if (_pauseMenuItem != null) _pauseMenuItem.Visible = !_isPaused;
            if (_resumeMenuItem != null) _resumeMenuItem.Visible = _isPaused;
            
            if (_isPaused)
            {
                PauseResumeButton.Content = "Resume";
                PauseDurationComboBox.IsEditable = true;
                PauseDurationComboBox.IsReadOnly = true;
                if (_pauseUntil.HasValue)
                {
                    var remaining = _pauseUntil.Value - DateTime.Now;
                    PauseDurationComboBox.Text = $"{(int)remaining.TotalHours:D2}h {remaining.Minutes:D2}m";
                }
                else
                {
                    PauseDurationComboBox.Text = "Indefinite";
                }
            }
            else
            {
                PauseResumeButton.Content = "Pause";
                PauseDurationComboBox.IsEditable = false;
                PauseDurationComboBox.IsReadOnly = false;
            }
        }

        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPaused)
            {
                ResumeAgent();
            }
            else
            {
                if (PauseDurationComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && 
                    item.Tag != null && double.TryParse(item.Tag.ToString(), out double hours))
                {
                    PauseAgent(hours);
                }
                else
                {
                    PauseAgent(0);
                }
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void RestartAsAdmin()
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = (WindowState == WindowState.Minimized || !IsVisible) ? "-hidden" : string.Empty
            };

            try
            {
                Process.Start(startInfo);
                _timer.Stop();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                if (_currentIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_currentIconHandle);
                    _currentIconHandle = IntPtr.Zero;
                }
                Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC prompt
            }
        }

        private void ElevateButton_Click(object sender, RoutedEventArgs e)
        {
            RestartAsAdmin();
        }
    }
}