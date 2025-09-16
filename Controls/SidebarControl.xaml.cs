using System.Windows;
using System.Windows.Controls;

namespace VirtualCorkboard.Controls
{
    public partial class SidebarControl : System.Windows.Controls.UserControl
    {
        public SidebarControl()
        {
            InitializeComponent();
        }

        public event EventHandler? AddTextNoteRequested;

        private void Quit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var app = System.Windows.Application.Current;
            if (app != null && app.MainWindow != null)
            {
                // Optionally prompt the user to confirm exit or save work here
                app.MainWindow.Close();
            }
            else
            {
                app?.Shutdown();
            }
        }

        private void AddTextNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddTextNoteRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = this.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

    }
}