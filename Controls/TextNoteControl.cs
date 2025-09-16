using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using TextBox = System.Windows.Controls.TextBox;

namespace VirtualCorkboard.Controls
{
    public class TextNoteControl : BaseNoteControl
    {
        public static readonly DependencyProperty NoteTextProperty =
            DependencyProperty.Register(nameof(NoteText), typeof(string), typeof(TextNoteControl), new PropertyMetadata(""));

        public string NoteText
        {
            get => (string)GetValue(NoteTextProperty);
            set => SetValue(NoteTextProperty, value);
        }

        private readonly TextBox _textBox;

        public TextNoteControl()
        {
            _textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                AcceptsReturn = true,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 30,
                Padding = new Thickness(4),
                IsReadOnly = true,
                IsHitTestVisible = false,
                Text = NoteText
            };

            _textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding(nameof(NoteText))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });

            _textBox.MouseDoubleClick += (s, e) =>
            {
                _textBox.IsReadOnly = false;
                _textBox.IsHitTestVisible = true;
                _textBox.Focus();
                _textBox.SelectAll();
            };

            _textBox.LostFocus += (s, e) =>
            {
                // If focus left because user clicked elsewhere, ensure we exit cleanly.
                ExitEditMode();
            };

            // Forward drag events to BaseNoteControl when not editing
            _textBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (_textBox.IsReadOnly)
                    this.OnMouseLeftButtonDown(e);
            };
            _textBox.PreviewMouseMove += (s, e) =>
            {
                if (_textBox.IsReadOnly)
                    this.OnMouseMove(e);
            };
            _textBox.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_textBox.IsReadOnly)
                    this.OnMouseLeftButtonUp(e);
            };

            this.Content = _textBox;
        }

        protected override void EnterEditMode()
        {
            _textBox.IsReadOnly = false;
            _textBox.IsHitTestVisible = true;
            _textBox.Focus();
            _textBox.SelectAll();
        }

        protected override void ExitEditMode()
        {
            // Make the textbox non-editable and non-interactive
            _textBox.IsReadOnly = true;
            _textBox.IsHitTestVisible = false;

            // Collapse selection and remove keyboard focus so the caret disappears
            _textBox.SelectionLength = 0;
            _textBox.SelectionStart = _textBox.CaretIndex; // keep caret position but hide it
            if (_textBox.IsKeyboardFocusWithin)
            {
                Keyboard.ClearFocus(); // moves focus off the TextBox, hides the caret
            }

            base.ExitEditMode();
            Debug.WriteLine("[TextNoteControl] Exiting edit mode, caret hidden and focus cleared.");
        }
    }
}