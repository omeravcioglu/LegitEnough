using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// Colour palettes for appearance indices and game enums. The room stays muted; clothing,
    /// documents and screens carry the saturated colours (visual direction: "things you touch").
    /// </summary>
    public static class Looks
    {
        public static readonly Color[] Skin =
        {
            Ui.Hex("#f0c8a8"), Ui.Hex("#e0b08c"), Ui.Hex("#cc9670"), Ui.Hex("#b47c56"),
            Ui.Hex("#9a6544"), Ui.Hex("#7e5034"), Ui.Hex("#633d27"), Ui.Hex("#4a2c1c"),
        };

        public static readonly Color[] Hair =
        {
            Ui.Hex("#1a1512"), Ui.Hex("#3b281c"), Ui.Hex("#5c3b24"), Ui.Hex("#8a4a26"),
            Ui.Hex("#b58a4e"), Ui.Hex("#d8c08a"), Ui.Hex("#8a8580"), Ui.Hex("#d6d2c8"),
        };

        public static readonly Color[] Cloth =
        {
            Ui.Hex("#3a3d42"), Ui.Hex("#2b3a5a"), Ui.Hex("#4f5634"), Ui.Hex("#6a2731"), Ui.Hex("#b8923a"),
            Ui.Hex("#2f6262"), Ui.Hex("#b3a27e"), Ui.Hex("#3e5a78"), Ui.Hex("#1f1e22"), Ui.Hex("#6b4429"),
            Ui.Hex("#7a7d80"), Ui.Hex("#4a2a3a"), Ui.Hex("#2f4a35"), Ui.Hex("#b2382b"),
        };

        public static Color SkinOf(Appearance a) => Skin[Mathf.Clamp(a.SkinTone, 0, Skin.Length - 1)];
        public static Color HairOf(Appearance a) => Hair[Mathf.Clamp(a.HairColor, 0, Hair.Length - 1)];
        public static Color ClothOf(int i) => Cloth[((i % Cloth.Length) + Cloth.Length) % Cloth.Length];

        public static Color Iris(EyeColor e) => e switch
        {
            EyeColor.Brown => Ui.Hex("#6b4226"),
            EyeColor.DarkBrown => Ui.Hex("#3b2416"),
            EyeColor.Hazel => Ui.Hex("#7a6a35"),
            EyeColor.Green => Ui.Hex("#4f7a4a"),
            EyeColor.Blue => Ui.Hex("#4f78a8"),
            EyeColor.Gray => Ui.Hex("#7d8a93"),
            _ => Ui.Hex("#6b4226"),
        };

        public static Color Of(Backdrop b) => b switch
        {
            Backdrop.White => Ui.Hex("#ece8dc"),
            Backdrop.LightGray => Ui.Hex("#bfc2c2"),
            Backdrop.Blue => Ui.Hex("#86a3c4"),
            Backdrop.Red => Ui.Hex("#b0584f"),
            _ => Color.white,
        };

        public static Color Of(CoverColor c) => c switch
        {
            CoverColor.DarkRed => Ui.Hex("#6e1e28"),
            CoverColor.Navy => Ui.Hex("#1f2a4d"),
            CoverColor.Black => Ui.Hex("#1b1b1d"),
            CoverColor.Green => Ui.Hex("#24462e"),
            CoverColor.Brown => Ui.Hex("#5a3a24"),
            _ => Color.gray,
        };

        public static Color Of(RiskLevel r) => r switch
        {
            RiskLevel.Low => Ui.Hex("#7fae5a"),
            RiskLevel.Moderate => Ui.Hex("#e0a83a"),
            _ => Ui.Hex("#d0503f"),
        };

        public static Color Of(ClientStatus s) => s switch
        {
            ClientStatus.Success => Ui.Hex("#7fae5a"),
            ClientStatus.Failed => Ui.Hex("#d0503f"),
            ClientStatus.Missing => Ui.Hex("#9a7fd1"),
            ClientStatus.Returning => Ui.Hex("#5fa8d3"),
            _ => Ui.Hex("#a8a29a"),
        };
    }
}
