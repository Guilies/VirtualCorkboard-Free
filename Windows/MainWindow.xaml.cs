using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging = false;
        private bool _isActive = true;
        private NotifyIcon? _trayIcon;
        private TwineManager _twineManager;

        private PinControl? _twineDragSourcePin;
        private bool _isTwineDragging = false;

        public MainWindow()
        {
            InitializeComponent();
            Sidebar.AddTextNoteRequested += Sidebar_AddTextNoteRequested;
            this.KeyDown += MainWindow_KeyDown;
            this.PreviewMouseUp += MainWindow_PreviewMouseUp; // handle middle mouse release anywhere

            _twineManager = new TwineManager(TwineCanvas);

            // Cross-component selection management
            _twineManager.TwineSelectionChanged += TwineManager_TwineSelectionChanged;

            // Background clicks on TwineCanvas (when not on a line) should deselect notes
            TwineCanvas.MouseLeftButtonDown += TwineCanvas_MouseLeftButtonDown;
        }

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            Sidebar.Visibility = Sidebar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Sidebar_AddTextNoteRequested(object? sender, EventArgs e)
        {
            var note = new TextNoteControl
            {
                Width = 200,
                Height = 150,
                NoteText = "New Note"
            };
            Canvas.SetLeft(note, 150);
            Canvas.SetTop(note, 80);
            NotesCanvas.Children.Add(note);

            note.ApplyTemplate();
            if (note.Template.FindName("Pin", note) is PinControl pin)
            {
                pin.PinMiddleMouseDown += Pin_PinMiddleMouseDown;
                // pin.PinMiddleMouseUp += Pin_PinMiddleMouseUp;
            }

            note.TwineManager = _twineManager;
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // New: Delete selected twine connections
            if (e.Key == Key.Delete && _twineManager != null && _twineManager.HasSelectedConnections)
            {
                _twineManager.DeleteSelectedConnections();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F1) // Or your preferred key
            {
                ToggleActiveInactive();
            }
        }

        private void ToggleActiveInactive()
        {
            if (_isActive)
            {
                // Going inactive
                DimmingOverlay.Visibility = Visibility.Collapsed;
                this.Hide();
                if (_trayIcon != null) _trayIcon.Visible = true;
                _isActive = false;
            }
            else
            {
                // Going active
                this.Show();
                DimmingOverlay.Visibility = Visibility.Visible;
                this.Topmost = true;
                this.Activate();
                if (_trayIcon != null) _trayIcon.Visible = false;
                _isActive = true;
            }
        }

        private void NotesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only when clicking the empty canvas area, not a note
            if (ReferenceEquals(e.Source, NotesCanvas))
            {
                foreach (var child in NotesCanvas.Children)
                {
                    if (child is BaseNoteControl note)
                    {
                        note.IsSelected = false;
                    }
                }

                // Also clear any selected twine connections
                _twineManager.ClearSelection();
            }
        }

        private void TwineCanvas_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            // Only when clicking empty space on TwineCanvas (not a Line etc.)
            if (ReferenceEquals(e.Source, TwineCanvas))
            {
                foreach (var child in NotesCanvas.Children)
                {
                    if (child is BaseNoteControl note)
                    {
                        note.IsSelected = false;
                    }
                }
                // TwineManager already clears twine selection on empty TwineCanvas click
            }
        }

        // Pin event handlers:
        // Start twine drag (guard against duplicate starts from Preview/Mouse events)
        private void Pin_PinMiddleMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (_isTwineDragging) return;
            if (sender is PinControl pin)
            {
                _twineDragSourcePin = pin;
                _isTwineDragging = true;
                var pos = pin.GetPinPositionOnWorkspace();
                _twineManager.StartTwineConnection(pin, pos);
                Mouse.Capture(this, CaptureMode.SubTree);
            }
        }

        // General-purpose middle mouse up anywhere (canvas, window, or pin)
        private void MainWindow_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            if (_isTwineDragging && _twineDragSourcePin != null)
            {
                var mousePos = e.GetPosition(this);
                PinControl? targetPin = FindClosestPin(mousePos, 50);

                if (targetPin != null && targetPin != _twineDragSourcePin)
                {
                    _twineManager.CompleteConnection(targetPin);
                }
                else
                {
                    _twineManager.CancelConnection();
                }

                _isTwineDragging = false;
                _twineDragSourcePin = null;
                Mouse.Capture(null);
                e.Handled = true;
            }
        }

        // Track mouse movement for ghost line
        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isTwineDragging && _twineDragSourcePin != null)
            {
                var pos = e.GetPosition(TwineCanvas);
                _twineManager.UpdateGhostLine(pos);
            }
        }

        private PinControl? FindClosestPin(System.Windows.Point mousePos, double maxDistance)
        {
            PinControl? closestPin = null;
            double closestDist = maxDistance;

            foreach (var child in NotesCanvas.Children)
            {
                if (child is BaseNoteControl note)
                {
                    note.ApplyTemplate();
                    if (note.Template.FindName("Pin", note) is PinControl pin)
                    {
                        // Transform pin position to MainWindow coordinates
                        var pinPos = pin.TransformToAncestor(this).Transform(new System.Windows.Point(pin.ActualWidth / 2, pin.ActualHeight / 2));
                        double dist = (pinPos - mousePos).Length;
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestPin = pin;
                        }
                    }
                }
            }
            return closestPin;
        }

        private void TwineManager_TwineSelectionChanged(object? sender, EventArgs e)
        {
            // Selecting a twine should deselect any notes
            foreach (var child in NotesCanvas.Children)
            {
                if (child is BaseNoteControl note)
                {
                    note.IsSelected = false;
                }
            }
        }

        // Called by BaseNoteControl when a note becomes selected
        public void NoteSelected()
        {
            // Selecting a note should clear twine selections
            _twineManager.ClearSelection();
        }
    }
}
