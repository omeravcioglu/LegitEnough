using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum FaceShape { Oval, Round, Square, Long, Heart }

    public enum HairStyle { Bald, Buzz, Short, SidePart, Curly, Afro, Long, Ponytail, Bun, Receding }

    public enum FacialHair { None, Stubble, Mustache, Goatee, Beard }

    public enum GlassesStyle { None, Round, Square, Thick, Sunglasses }

    public enum HatStyle { None, Beanie, Cap, Fedora }

    public enum TopStyle { TShirt, Shirt, Hoodie, Jacket, Sweater, Coat, Suit }

    /// <summary>
    /// Modular look of a customer (design doc §7). Pure data: palette indices and style
    /// choices. Renderers (placeholder vector art now, pixel art later) interpret it.
    /// </summary>
    public sealed class Appearance
    {
        public int SkinTone;
        public FaceShape Face;
        /// <summary>Face width / face height.</summary>
        public float FaceWidth = 0.78f;
        public HairStyle Hair;
        public int HairColor;
        public FacialHair FacialHair;
        public EyeColor Eyes;
        public float EyeSize = 1f;
        public float NoseSize = 1f;
        public float MouthWidth = 1f;
        public float BrowThickness = 1f;
        public bool Freckles;
        public int Age = 30;
        /// <summary>Body width multiplier.</summary>
        public float Build = 1f;
        public TopStyle Top;
        public int TopColor;
        public int InnerColor;
        public bool Scarf;
        public int ScarfColor;
        public bool Earrings;
        public HatStyle Hat;
        /// <summary>Glasses worn, innermost first. [Square, Sunglasses] = sunglasses over regular glasses.</summary>
        public List<GlassesStyle> Glasses = new List<GlassesStyle>();

        public bool IsGray => Age >= 55;

        public Appearance Clone()
        {
            var c = (Appearance)MemberwiseClone();
            c.Glasses = new List<GlassesStyle>(Glasses);
            return c;
        }
    }

    /// <summary>Palette sizes shared by generator and renderers.</summary>
    public static class AppearancePalette
    {
        public const int SkinTones = 8;
        public const int HairColors = 8;
        public const int ClothColors = 14;
    }
}
