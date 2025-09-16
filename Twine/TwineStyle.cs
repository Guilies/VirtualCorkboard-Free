using System.Windows.Media;

namespace VirtualCorkboard.Twine
{
    public enum TwineTextureType
    {
        Solid,
        Dotted,
        Dashed,
        // Extendable for future textures
    }

    public class TwineStyle
    {
        // Default Twine Color
        public System.Windows.Media.Color TwineColor { get; set; } = Colors.Red;
        // Default Twine Texture
        public TwineTextureType Texture { get; set; } = TwineTextureType.Solid;
        // Default Twine Thickness
        public double Thickness { get; set; } = 5.0;
        // Default Highlight Color
        public System.Windows.Media.Color HighlightColor { get; set; } = Colors.CornflowerBlue;
    }
}