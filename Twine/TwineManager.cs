using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using VirtualCorkboard.Controls;
using Cursors = System.Windows.Input.Cursors;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace VirtualCorkboard.Twine
{
    public class TwineManager
    {
        private readonly Canvas _twineCanvas;
        private readonly Dictionary<TwineConnection, Line> _twineLines = new();
        private readonly Dictionary<Line, TwineConnection> _lineToConnection = new();
        private readonly HashSet<PinControl> _pinsToUpdate = new();
        private readonly HashSet<TwineConnection> _connectionsToUpdate = new();

        // Selection state
        private readonly HashSet<TwineConnection> _selectedConnections = new();
        private readonly Dictionary<TwineConnection, Line> _selectionOverlays = new();

        private readonly DispatcherTimer _updateTimer;
        private Line? _ghostLine;
        private PinControl? _dragSourcePin;

        public bool HasSelectedConnections => _selectedConnections.Count > 0;

        // Add this event to notify the MainWindow when a twine is selected
        public event EventHandler TwineSelectionChanged;

        public TwineManager(Canvas twineCanvas)
        {
            _twineCanvas = twineCanvas;

            // Clear selection when clicking empty twine canvas space
            _twineCanvas.MouseLeftButtonDown += (s, e) =>
            {
                if (ReferenceEquals(e.Source, _twineCanvas))
                {
                    ClearSelection();
                }
            };

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(11) // ~90 FPS
            };
            _updateTimer.Tick += (s, e) => ProcessBatchedUpdates();
            _updateTimer.Start();
        }

        public void StartTwineConnection(PinControl sourcePin, Point _)
        {
            Debug.WriteLine($"[TwineManager] StartTwineConnection: SourcePin={sourcePin}");
            _dragSourcePin = sourcePin;

            var start = GetPinPositionOnTwineCanvas(sourcePin);
            _ghostLine = CreateTwineLine(start, start, new TwineStyle());
            _ghostLine.IsHitTestVisible = false; // don't interfere with clicks
            _twineCanvas.Children.Add(_ghostLine);
            Debug.WriteLine("[TwineManager] Ghost line added to canvas (TwineCanvas space).");
        }

        public void UpdateGhostLine(Point currentPoint)
        {
            // currentPoint should be in TwineCanvas coords
            if (_ghostLine != null)
            {
                _ghostLine.X2 = currentPoint.X;
                _ghostLine.Y2 = currentPoint.Y;
            }
        }

        public void CompleteConnection(PinControl targetPin)
        {
            if (_dragSourcePin == null || _ghostLine == null)
            {
                Debug.WriteLine("[TwineManager] CompleteConnection: Drag source or ghost line is null, aborting.");
                return;
            }

            Debug.WriteLine($"[TwineManager] CompleteConnection: SourcePin={_dragSourcePin}, TargetPin={targetPin}");
            var connection = new TwineConnection(_dragSourcePin, targetPin, new TwineStyle());
            _dragSourcePin.OutgoingConnections.Add(connection);
            targetPin.IncomingConnections.Add(connection);

            var start = GetPinPositionOnTwineCanvas(connection.SourcePin);
            var end = GetPinPositionOnTwineCanvas(connection.TargetPin);
            var line = CreateTwineLine(start, end, connection.Style);

            // Hook selection handlers
            line.MouseLeftButtonDown += OnLineMouseLeftButtonDown;
            line.Cursor = Cursors.Hand;

            _twineLines.Add(connection, line);
            _lineToConnection[line] = connection;
            _twineCanvas.Children.Add(line);
            Debug.WriteLine("[TwineManager] Twine line added to canvas (TwineCanvas space).");

            // Clean up ghost line
            _twineCanvas.Children.Remove(_ghostLine);
            _ghostLine = null;
            _dragSourcePin = null;
            Debug.WriteLine("[TwineManager] Ghost line removed, connection complete.");
        }

        public void CancelConnection()
        {
            if (_ghostLine != null)
            {
                _twineCanvas.Children.Remove(_ghostLine);
                _ghostLine = null;
                _dragSourcePin = null;
                Debug.WriteLine("[TwineManager] Connection cancelled, ghost line removed.");
            }
        }

        public void UpdateConnectionPosition(TwineConnection connection)
        {
            if (_twineLines.TryGetValue(connection, out var line))
            {
                var sourcePos = GetPinPositionOnTwineCanvas(connection.SourcePin);
                var targetPos = GetPinPositionOnTwineCanvas(connection.TargetPin);

                line.X1 = sourcePos.X;
                line.Y1 = sourcePos.Y;
                line.X2 = targetPos.X;
                line.Y2 = targetPos.Y;

                if (_selectionOverlays.TryGetValue(connection, out var overlay))
                {
                    overlay.X1 = sourcePos.X;
                    overlay.Y1 = sourcePos.Y;
                    overlay.X2 = targetPos.X;
                    overlay.Y2 = targetPos.Y;
                }
            }
        }

        public void AddConnection(TwineConnection connection)
        {
            Debug.WriteLine($"[TwineManager] AddConnection: SourcePin={connection.SourcePin}, TargetPin={connection.TargetPin}");

            var start = GetPinPositionOnTwineCanvas(connection.SourcePin);
            var end = GetPinPositionOnTwineCanvas(connection.TargetPin);

            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(connection.Style.TwineColor),
                StrokeThickness = connection.Style.Thickness,
                StrokeDashArray = connection.Style.Texture switch
                {
                    TwineTextureType.Dotted => new DoubleCollection { 2, 2 },
                    TwineTextureType.Dashed => new DoubleCollection { 6, 2 },
                    _ => null
                },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            // Hook selection handlers
            line.MouseLeftButtonDown += OnLineMouseLeftButtonDown;
            line.Cursor = Cursors.Hand;

            _twineLines[connection] = line;
            _lineToConnection[line] = connection;
            _twineCanvas.Children.Add(line);
            Debug.WriteLine("[TwineManager] Twine line added to canvas via AddConnection (TwineCanvas space).");
        }

        public void UpdateAllConnectionsForPin(PinControl pin)
        {
            foreach (var conn in pin.OutgoingConnections)
                UpdateConnectionPosition(conn);
            foreach (var conn in pin.IncomingConnections)
                UpdateConnectionPosition(conn);
        }

        public void RequestUpdateForPin(PinControl pin)
        {
            _pinsToUpdate.Add(pin);
        }

        public void RemoveAllConnectionsForPin(PinControl pin)
        {
            foreach (var conn in pin.OutgoingConnections.ToList())
                RemoveConnection(conn);
            foreach (var conn in pin.IncomingConnections.ToList())
                RemoveConnection(conn);
        }

        public void RemoveConnection(TwineConnection connection)
        {
            // Remove from pins
            connection.SourcePin.OutgoingConnections.Remove(connection);
            connection.TargetPin.IncomingConnections.Remove(connection);

            // Deselect if selected and remove overlay
            if (_selectedConnections.Contains(connection))
                DeselectConnection(connection);

            // Remove line from canvas and reverse map
            if (_twineLines.TryGetValue(connection, out var line))
            {
                _twineCanvas.Children.Remove(line);
                _twineLines.Remove(connection);
                _lineToConnection.Remove(line);
            }
        }

        public void DeleteSelectedConnections()
        {
            if (_selectedConnections.Count == 0) return;

            var toDelete = _selectedConnections.ToList();
            ClearSelection(); // removes overlays
            foreach (var conn in toDelete)
            {
                RemoveConnection(conn);
            }
        }

        private void ProcessBatchedUpdates()
        {
            foreach (var pin in _pinsToUpdate)
            {
                foreach (var conn in pin.OutgoingConnections)
                    _connectionsToUpdate.Add(conn);
                foreach (var conn in pin.IncomingConnections)
                    _connectionsToUpdate.Add(conn);
            }

            foreach (var conn in _connectionsToUpdate)
                UpdateConnectionPosition(conn);

            _pinsToUpdate.Clear();
            _connectionsToUpdate.Clear();
        }

        private Line CreateTwineLine(Point start, Point end, TwineStyle style)
        {
            return new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(style.TwineColor),
                StrokeThickness = style.Thickness,
                StrokeDashArray = style.Texture switch
                {
                    TwineTextureType.Dotted => new DoubleCollection { 2, 2 },
                    TwineTextureType.Dashed => new DoubleCollection { 6, 2 },
                    _ => null
                },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
        }

        // Selection handlers/visuals
        private void OnLineMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("[TwineManager] Twine Click Detected!");
            if (sender is not Line line) return;
            if (!_lineToConnection.TryGetValue(line, out var connection)) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                ToggleSelection(connection);
            }
            else
            {
                ClearSelection();
                SelectConnection(connection);
            }

            // Notify listeners that a twine was selected
            TwineSelectionChanged?.Invoke(this, EventArgs.Empty);

            // Ensure Delete key goes somewhere useful
            Keyboard.Focus(_twineCanvas);
            e.Handled = true;
        }

        private void ToggleSelection(TwineConnection connection)
        {
            if (_selectedConnections.Contains(connection))
                DeselectConnection(connection);
            else
                SelectConnection(connection);
        }

        private void SelectConnection(TwineConnection connection)
        {
            Debug.WriteLine($"[TwineManager] SelectConnection: {connection}");
            if (_selectedConnections.Contains(connection)) return;

            _selectedConnections.Add(connection);

            if (!_twineLines.TryGetValue(connection, out var main)) return;

            var start = new Point(main.X1, main.Y1);
            var end = new Point(main.X2, main.Y2);

            // Outline overlay behind the main line
            var overlay = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(connection.Style.HighlightColor),
                StrokeThickness = main.StrokeThickness + 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false // do not intercept clicks
            };

            // Insert behind the main line
            _twineCanvas.Children.Add(overlay);
            int mainZ = Panel.GetZIndex(main);
            Panel.SetZIndex(overlay, mainZ - 1);

            _selectionOverlays[connection] = overlay;
        }

        private void DeselectConnection(TwineConnection connection)
        {
            if (!_selectedConnections.Remove(connection)) return;

            if (_selectionOverlays.TryGetValue(connection, out var overlay))
            {
                _twineCanvas.Children.Remove(overlay);
                _selectionOverlays.Remove(connection);
            }
        }

        // Make this method public so MainWindow can call it
        public void ClearSelection()
        {
            if (_selectedConnections.Count == 0) return;
            
            foreach (var kvp in _selectionOverlays.ToList())
            {
                _twineCanvas.Children.Remove(kvp.Value);
            }
            _selectionOverlays.Clear();
            _selectedConnections.Clear();
        }

        // Centralized coordinate conversion: pin center -> TwineCanvas space
        private Point GetPinPositionOnTwineCanvas(PinControl pin)
        {
            try
            {
                var centerOnPin = new Point(pin.ActualWidth / 2, pin.ActualHeight / 2);
                return pin.TransformToVisual(_twineCanvas).Transform(centerOnPin);
            }
            catch
            {
                return new Point(0, 0);
            }
        }
    }
}