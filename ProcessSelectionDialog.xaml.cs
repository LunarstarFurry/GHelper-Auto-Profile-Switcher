using System.Collections.Generic;
using System.Windows;
using System.Linq;

namespace GHelperAutoProfileSwitcher
{
    public partial class ProcessSelectionDialog : Window
    {
        public string SelectedProcess { get; private set; } = string.Empty;

        public ProcessSelectionDialog(List<ProcessInfo> processes)
        {
            InitializeComponent();
            ProcessListBox.ItemsSource = processes;
            ProcessListBox.DisplayMemberPath = "DisplayName";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListBox.SelectedItem is ProcessInfo info)
            {
                SelectedProcess = info.ProcessName;
                DialogResult = true;
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a process.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ProcessListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ProcessListBox.SelectedItem is ProcessInfo)
            {
                Ok_Click(sender, e);
            }
        }
    }

    public class ProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string DisplayName => $"{ProcessName} - {WindowTitle}";
    }
}