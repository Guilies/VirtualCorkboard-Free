using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using VirtualCorkboard.Twine;
using Point = System.Windows.Point;
using Application = System.Windows.Application;
using System.Diagnostics;

namespace VirtualCorkboard.Controls
{

    public partial class PinControl : System.Windows.Controls.Control
    {
        private Point _lastKnownPosition;
        private WeakReference<Window> _mainWindowRef;

        static PinControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PinControl), new FrameworkPropertyMetadata(typeof(PinControl)));
        }

        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register(nameof(PinColor), typeof(Brush), typeof(PinControl), new PropertyMetadata(Brushes.Red));

        public Brush PinColor
        {
            get => (Brush)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public static readonly DependencyProperty TwineColorProperty =
            DependencyProperty.Register(nameof(TwineColor), typeof(Brush), typeof(PinControl), new PropertyMetadata(Brushes.Red));

        public Brush TwineColor
        {
            get => (Brush)GetValue(TwineColorProperty);
            set => SetValue(TwineColorProperty, value);
        }

        // Event for starting a twine drag
        public event MouseButtonEventHandler? PinMouseDown;
        public event MouseButtonEventHandler? PinMiddleMouseDown;
        public event MouseButtonEventHandler? PinMiddleMouseUp;

        public PinControl()
        {
            Debug.WriteLine("[PinControl] Constructor called.");
            this.MouseDown += PinControl_MouseDown;
            this.MouseUp += PinControl_MouseUp;
            this.PreviewMouseDown += PinControl_PreviewMouseDown;
            this.PreviewMouseUp += PinControl_PreviewMouseUp;
        }

        private void PinControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine($"[PinControl] MouseDown: {e.ChangedButton}");
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseDown?.Invoke(this, e);
            }
        }

        private void PinControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine($"[PinControl] MouseUp: {e.ChangedButton}");
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseUp?.Invoke(this, e);
            }
        }

        private void PinControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine($"[PinControl] PreviewMouseDown: {e.ChangedButton}");
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseDown?.Invoke(this, e);
                e.Handled = true; 
            }
        }

        private void PinControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine($"[PinControl] PreviewMouseUp: {e.ChangedButton}");
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseUp?.Invoke(this, e);
                e.Handled = true; 
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var pinVisual = GetTemplateChild("PART_PinVisual") as UIElement;
            if (pinVisual != null)
            {
                pinVisual.MouseLeftButtonDown += (s, e) => PinMouseDown?.Invoke(this, e);
            }
        }

        public List<TwineConnection> OutgoingConnections { get; } = new();
        public List<TwineConnection> IncomingConnections { get; } = new();

        private Point? _cachedWorkspacePosition;

        // Utility to get pin position in workspace coordinates
        public Point GetPinPositionOnWorkspace()
        {
            if (_mainWindowRef == null || !_mainWindowRef.TryGetTarget(out var mainWindow))
            {
                mainWindow = Application.Current.MainWindow;
                _mainWindowRef = new WeakReference<Window>(mainWindow);
            }

            if (mainWindow == null || ActualWidth == 0 || ActualHeight == 0)
                return _lastKnownPosition;

            try
            {
                _lastKnownPosition = this.TransformToAncestor(mainWindow)
                    .Transform(new Point(ActualWidth / 2, ActualHeight / 2));
                return _lastKnownPosition;
            }
            catch
            {
                return _lastKnownPosition;
            }
        }

        public void InvalidatePosition()
        {
            _cachedWorkspacePosition = null;
        }
    }
}
