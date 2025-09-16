using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VirtualCorkboard.Twine;
using System.Collections.Generic;
using System.Linq;
using Panel = System.Windows.Controls.Panel;
using Application = System.Windows.Application;

namespace VirtualCorkboard.Controls
{
    public class BaseNoteControl : ContentControl
    {
        static BaseNoteControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BaseNoteControl),
                new FrameworkPropertyMetadata(typeof(BaseNoteControl)));
        }

        // global z-order counter so "bring to front" always wins
        private static int _zOrderCounter = 0;
        private static int NextZ() => ++_zOrderCounter;

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }
        
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(BaseNoteControl),
                new PropertyMetadata(false, OnIsSelectedChanged));

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseNoteControl note && e.NewValue is bool sel && sel)
            {
                // Whenever a note becomes selected, bring it to front
                note.BringToFront();

                // Optional cross-component selection clear (if MainWindow exposes NoteSelected)
                if (Application.Current?.MainWindow is VirtualCorkboard.MainWindow mw)
                {
                    mw.NoteSelected();
                }
            }
        }

        // Allow turning bounds clamping on/off (default on)
        public bool ClampToParentBounds
        {
            get => (bool)GetValue(ClampToParentBoundsProperty);
            set => SetValue(ClampToParentBoundsProperty, value);
        }

        public static readonly DependencyProperty ClampToParentBoundsProperty =
            DependencyProperty.Register(nameof(ClampToParentBounds), typeof(bool), typeof(BaseNoteControl),
                new PropertyMetadata(true));

        // Drag logic fields
        private bool _isDragging;
        private System.Windows.Point _dragStart;

        // Group move snapshot
        private readonly List<(BaseNoteControl Note, double Left, double Top)> _groupStart = new();

        private PinControl? _cachedPin;
        public PinControl? Pin
        {
            get
            {
                if (_cachedPin == null)
                {
                    this.ApplyTemplate();
                    _cachedPin = this.Template.FindName("Pin", this) as PinControl;
                }
                return _cachedPin;
            }
        }

        public BaseNoteControl()
        {
            // Make the note able to receive keyboard focus
            this.Focusable = true;

            // Mouse events for drag logic
            this.MouseLeftButtonDown += BaseNoteControl_MouseLeftButtonDown;
            this.MouseMove += BaseNoteControl_MouseMove;
            this.MouseLeftButtonUp += BaseNoteControl_MouseLeftButtonUp;
            this.MouseDoubleClick += BaseNoteControl_MouseDoubleClick;

            // Key events for shortcuts (e.g., delete)
            this.KeyDown += BaseNoteControl_DeleteKeyDown;
        }

        public TwineManager? TwineManager { get; set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Hook up resize thumb events
            HookupResizeThumb("TopThumb");
            HookupResizeThumb("BottomThumb");
            HookupResizeThumb("LeftThumb");
            HookupResizeThumb("RightThumb");
            HookupResizeThumb("TopLeftThumb");
            HookupResizeThumb("TopRightThumb");
            HookupResizeThumb("BottomLeftThumb");
            HookupResizeThumb("BottomRightThumb");
        }

        private void HookupResizeThumb(string thumbName)
        {
            if (Template.FindName(thumbName, this) is Thumb thumb)
            {
                thumb.DragDelta += ResizeThumb_DragDelta;
            }
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb) return;

            double minWidth = MinWidth > 0 ? MinWidth : 50;
            double minHeight = MinHeight > 0 ? MinHeight : 50;

            switch (thumb.Name)
            {
                case "TopThumb":
                    ResizeTop(e.VerticalChange, minHeight);
                    break;
                case "BottomThumb":
                    ResizeBottom(e.VerticalChange, minHeight);
                    break;
                case "LeftThumb":
                    ResizeLeft(e.HorizontalChange, minWidth);
                    break;
                case "RightThumb":
                    ResizeRight(e.HorizontalChange, minWidth);
                    break;
                case "TopLeftThumb":
                    ResizeTop(e.VerticalChange, minHeight);
                    ResizeLeft(e.HorizontalChange, minWidth);
                    break;
                case "TopRightThumb":
                    ResizeTop(e.VerticalChange, minHeight);
                    ResizeRight(e.HorizontalChange, minWidth);
                    break;
                case "BottomLeftThumb":
                    ResizeBottom(e.VerticalChange, minHeight);
                    ResizeLeft(e.HorizontalChange, minWidth);
                    break;
                case "BottomRightThumb":
                    ResizeRight(e.HorizontalChange, minWidth);
                    ResizeBottom(e.VerticalChange, minHeight);
                    break;
            }

            // Update twine positions after resize
            UpdateTwineConnections();
        }

        private void ResizeTop(double deltaY, double minHeight)
        {
            double newHeight = Height - deltaY;
            if (newHeight >= minHeight)
            {
                Height = newHeight;
                if (Parent is Canvas canvas)
                {
                    double currentTop = Canvas.GetTop(this);
                    Canvas.SetTop(this, currentTop + deltaY);
                }
            }
        }

        private void ResizeBottom(double deltaY, double minHeight)
        {
            double newHeight = Height + deltaY;
            if (newHeight >= minHeight)
            {
                Height = newHeight;
            }
        }

        private void ResizeLeft(double deltaX, double minWidth)
        {
            double newWidth = Width - deltaX;
            if (newWidth >= minWidth)
            {
                Width = newWidth;
                if (Parent is Canvas canvas)
                {
                    double currentLeft = Canvas.GetLeft(this);
                    Canvas.SetLeft(this, currentLeft + deltaX);
                }
            }
        }

        private void ResizeRight(double deltaX, double minWidth)
        {
            double newWidth = Width + deltaX;
            if (newWidth >= minWidth)
            {
                Width = newWidth;
            }
        }

        private void UpdateTwineConnections()
        {
            if (TwineManager != null && Pin != null)
            {
                Pin.InvalidatePosition();
                TwineManager.RequestUpdateForPin(Pin);
            }
        }

        private void UpdateTwineConnectionsFor(BaseNoteControl note)
        {
            if (note.TwineManager != null && note.Pin != null)
            {
                note.Pin.InvalidatePosition();
                note.TwineManager.RequestUpdateForPin(note.Pin);
            }
        }

        private IEnumerable<BaseNoteControl> EnumerateNotesInCanvas(Canvas canvas)
        {
            foreach (var child in canvas.Children)
                if (child is BaseNoteControl n) yield return n;
        }

        private void DeselectAllSiblings(Canvas canvas)
        {
            foreach (var n in EnumerateNotesInCanvas(canvas))
            {
                if (n == this) continue;
                n.ExitEditMode();
                n.IsSelected = false;
            }
        }

        private void SnapshotGroup(Canvas canvas)
        {
            _groupStart.Clear();
            // Move selected group if any selected; otherwise include self
            var group = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();
            if (group.Count == 0)
            {
                this.IsSelected = true;
                group.Add(this);
            }
            foreach (var n in group)
            {
                var l = Canvas.GetLeft(n);
                var t = Canvas.GetTop(n);
                _groupStart.Add((n, double.IsNaN(l) ? 0 : l, double.IsNaN(t) ? 0 : t));
            }
        }

        private void BringToFront()
        {
            Panel.SetZIndex(this, NextZ());
        }

        private void BringGroupToFront(Canvas canvas, BaseNoteControl preferredTop)
        {
            var group = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();
            // raise others first
            foreach (var n in group.Where(n => n != preferredTop))
                Panel.SetZIndex(n, NextZ());
            // clicked note last (on top)
            Panel.SetZIndex(preferredTop, NextZ());
        }

        private void MoveGroup(Canvas canvas, System.Windows.Point currentPos)
        {
            var dx = currentPos.X - _dragStart.X;
            var dy = currentPos.Y - _dragStart.Y;

            // Clamp delta so the entire group remains within canvas bounds
            if (ClampToParentBounds)
            {
                (dx, dy) = ClampDeltaToCanvas(canvas, dx, dy);
            }

            foreach (var entry in _groupStart)
            {
                Canvas.SetLeft(entry.Note, entry.Left + dx);
                Canvas.SetTop(entry.Note, entry.Top + dy);
                UpdateTwineConnectionsFor(entry.Note);
            }
        }

        private (double dx, double dy) ClampDeltaToCanvas(Canvas canvas, double dx, double dy)
        {
            double canvasW = canvas.ActualWidth;
            double canvasH = canvas.ActualHeight;

            // If canvas not measured yet, skip clamping
            if (double.IsNaN(canvasW) || double.IsNaN(canvasH) || canvasW <= 0 || canvasH <= 0)
                return (dx, dy);

            double minDx = double.NegativeInfinity, maxDx = double.PositiveInfinity;
            double minDy = double.NegativeInfinity, maxDy = double.PositiveInfinity;

            foreach (var entry in _groupStart)
            {
                var note = entry.Note;

                double w = (!double.IsNaN(note.Width) && note.Width > 0) ? note.Width : (note.ActualWidth > 0 ? note.ActualWidth : 0);
                double h = (!double.IsNaN(note.Height) && note.Height > 0) ? note.Height : (note.ActualHeight > 0 ? note.ActualHeight : 0);

                // Allowed dx/dy for this note to stay within [0, canvasW - w] and [0, canvasH - h]
                double noteMinDx = -entry.Left;
                double noteMaxDx = canvasW - (entry.Left + w);

                double noteMinDy = -entry.Top;
                double noteMaxDy = canvasH - (entry.Top + h);

                if (noteMinDx > minDx) minDx = noteMinDx;
                if (noteMaxDx < maxDx) maxDx = noteMaxDx;

                if (noteMinDy > minDy) minDy = noteMinDy;
                if (noteMaxDy < maxDy) maxDy = noteMaxDy;
            }

            // Clamp requested delta to the intersection of all notes' allowed ranges
            double clampedDx = Math.Clamp(dx, minDx, maxDx);
            double clampedDy = Math.Clamp(dy, minDy, maxDy);
            return (clampedDx, clampedDy);
        }

        private int CountSelectedNotes(Canvas canvas)
        {
            int count = 0;
            foreach (var n in EnumerateNotesInCanvas(canvas))
                if (n.IsSelected) count++;
            return count;
        }

        private void BaseNoteControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only start dragging if not clicking on a resize thumb
            if (e.OriginalSource is Thumb) return;
            if (e.ClickCount == 2) return; // Double-click handled separately

            if (Parent is Canvas canvas)
            {
                var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                if (ctrl)
                {
                    // Toggle selection without affecting others
                    IsSelected = !IsSelected;
                    // Ensure this note gets keyboard focus so Delete works
                    Keyboard.Focus(this);
                    e.Handled = true;
                    return;
                }

                int selectedCount = CountSelectedNotes(canvas);
                bool isPartOfExistingMulti = this.IsSelected && selectedCount > 1;

                if (isPartOfExistingMulti)
                {
                    // Keep current multi-selection; begin group drag
                    BringGroupToFront(canvas, this);
                    _dragStart = e.GetPosition(canvas);
                    SnapshotGroup(canvas);
                    _isDragging = true;
                    CaptureMouse();

                    // Focus this note so Delete works during/after drag
                    Keyboard.Focus(this);
                    e.Handled = true;
                    return;
                }

                // Single-selection: deselect others, select this
                DeselectAllSiblings(canvas);
                IsSelected = true;

                // Focus so Delete works
                Keyboard.Focus(this);

                _dragStart = e.GetPosition(canvas);
                SnapshotGroup(canvas);
                _isDragging = true;
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void BaseNoteControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed && Parent is Canvas canvas)
            {
                var pos = e.GetPosition(canvas);
                MoveGroup(canvas, pos);
            }
        }

        private void BaseNoteControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;

                // Final update for twine connections for the moved group
                if (Parent is Canvas canvas)
                {
                    foreach (var entry in _groupStart)
                    {
                        if (entry.Note.TwineManager != null && entry.Note.Pin != null)
                        {
                            entry.Note.Pin.InvalidatePosition();
                            entry.Note.TwineManager.UpdateAllConnectionsForPin(entry.Note.Pin);
                        }
                    }
                }

                _groupStart.Clear();
            }
        }

        private void BaseNoteControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EnterEditMode();
            e.Handled = true;
        }

        private void BaseNoteControl_DeleteKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && Parent is Canvas canvas)
            {
                var toRemove = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();
                foreach (var n in toRemove)
                {
                    // Remove associated twines (both directions)
                    if (n.TwineManager != null && n.Pin != null)
                    {
                        n.TwineManager.RemoveAllConnectionsForPin(n.Pin);
                    }
                    canvas.Children.Remove(n);
                }
                e.Handled = true;
            }
        }

        protected virtual void EnterEditMode()
        {
            // To be overridden by derived note types
        }

        protected virtual void ExitEditMode()
        {
            IsSelected = false;
        }
    }
}