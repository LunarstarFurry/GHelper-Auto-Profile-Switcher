using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GHelperAutoProfileSwitcher
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<AppProfile> _profiles;
        private DispatcherTimer _timer;
        private NotifyIcon _notifyIcon;
        private TargetMode _currentMode = TargetMode.Balanced;
        private IntPtr _currentIconHandle = IntPtr.Zero;

        private bool _isPaused = false;
        private DateTime? _pauseUntil = null;
        private ToolStripMenuItem? _pauseMenuItem;
        private ToolStripMenuItem? _resumeMenuItem;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

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
                _notifyIcon.Text = $"G-Helper - {_currentMode}";

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

            _profiles = new ObservableCollection<AppProfile>(ConfigManager.LoadConfig());
            ProfilesGrid.ItemsSource = _profiles;

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
                _notifyIcon.Visible = false;
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

            if (_profiles.Count == 0) return;

            var runningProcesses = Process.GetProcesses().Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            TargetMode targetMode = TargetMode.Balanced;
            bool found = false;

            foreach (var profile in _profiles)
            {
                if (runningProcesses.Contains(profile.ProcessName))
                {
                    targetMode = profile.Mode;
                    found = true;
                    if (targetMode == TargetMode.Turbo) break;
                }
            }

            if (_currentMode != targetMode || (!found && _currentMode != TargetMode.Balanced))
            {
                _currentMode = targetMode;
                CurrentModeText.Text = _currentMode.ToString();
                GHelperHotkeys.SetMode(_currentMode);
                UpdateTrayIcon();
            }
        }

        private void AddCurrentApp_Click(object sender, RoutedEventArgs e)
        {
            var runningApps = Process.GetProcesses()
                                     .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                                     .Select(p => new ProcessInfo 
                                     { 
                                         ProcessName = p.ProcessName, 
                                         WindowTitle = p.MainWindowTitle 
                                     })
                                     .GroupBy(p => p.ProcessName)
                                     .Select(g => g.First())
                                     .OrderBy(p => p.ProcessName)
                                     .ToList();

            var dialog = new ProcessSelectionDialog(runningApps);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                string selectedProcess = dialog.SelectedProcess;
                if (!string.IsNullOrEmpty(selectedProcess) && !_profiles.Any(p => p.ProcessName.Equals(selectedProcess, StringComparison.OrdinalIgnoreCase)))
                {
                    _profiles.Add(new AppProfile { ProcessName = selectedProcess, Mode = TargetMode.Turbo });
                    ConfigManager.SaveConfig(_profiles.ToList());
                }
            }
        }

        private void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is AppProfile profile)
            {
                _profiles.Remove(profile);
                ConfigManager.SaveConfig(_profiles.ToList());
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.SaveConfig(_profiles.ToList());
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
                        string path = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
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

        private int _lastSelectedPauseIndex = 4; // Default to 'Indefinitely'

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
            PauseDurationComboBox.SelectedIndex = _lastSelectedPauseIndex;
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
    }
}