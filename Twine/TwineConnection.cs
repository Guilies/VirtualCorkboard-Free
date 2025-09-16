using System;
using System.Windows;
using VirtualCorkboard.Controls;
using Point = System.Windows.Point;

namespace VirtualCorkboard.Twine
{
    public class TwineConnection
    {
        public PinControl SourcePin { get; }
        public PinControl TargetPin { get; }
        public TwineStyle Style { get; set; }

        public TwineConnection(PinControl source, PinControl target, TwineStyle style)
        {
            SourcePin = source ?? throw new ArgumentNullException(nameof(source));
            TargetPin = target ?? throw new ArgumentNullException(nameof(target));
            Style = style ?? throw new ArgumentNullException(nameof(style));
        }

        public Point GetSourcePosition()
        {
            return SourcePin.GetPinPositionOnWorkspace();

        }

        public Point GetTargetPosition()
        {
            return TargetPin.GetPinPositionOnWorkspace();
        }
    }
}