using System;
using System.Collections.Generic;

namespace BinaryKits.Zpl.Viewer.Helpers
{
    using FontScaleDictionary = Dictionary<string, (int height, int width)>;

    internal static class FontScale
    {
        private static readonly FontScaleDictionary fontScales6mm = new()
        {
            ["A"] = (9, 5),
            ["B"] = (11, 7),
            ["C"] = (18, 10),
            ["D"] = (18, 10),
            ["E"] = (21, 10),
            ["F"] = (26, 13),
            ["G"] = (60, 40),
            ["H"] = (17, 11),
            ["GS"] = (24, 24),
            ["P"] = (20, 18),
            ["Q"] = (28, 24),
            ["R"] = (35, 31),
            ["S"] = (40, 35),
            ["T"] = (48, 42),
            ["U"] = (59, 53),
            ["V"] = (80, 71)
        };

        private static readonly FontScaleDictionary fontScales8mm = new()
        {
            ["A"] = (9, 5),
            ["B"] = (11, 7),
            ["C"] = (18, 10),
            ["D"] = (18, 10),
            ["E"] = (28, 15),
            ["F"] = (26, 13),
            ["G"] = (60, 40),
            ["H"] = (21, 13),
            ["GS"] = (24, 24),
            ["P"] = (20, 18),
            ["Q"] = (28, 24),
            ["R"] = (35, 31),
            ["S"] = (40, 35),
            ["T"] = (48, 42),
            ["U"] = (59, 53),
            ["V"] = (80, 71)
        };

        private static readonly FontScaleDictionary fontScales12mm = new()
        {
            ["A"] = (9, 5),
            ["B"] = (11, 7),
            ["C"] = (18, 10),
            ["D"] = (18, 10),
            ["E"] = (42, 20),
            ["F"] = (26, 13),
            ["G"] = (60, 40),
            ["H"] = (34, 22),
            ["GS"] = (24, 24),
            ["P"] = (20, 18),
            ["Q"] = (28, 24),
            ["R"] = (35, 31),
            ["S"] = (40, 35),
            ["T"] = (48, 42),
            ["U"] = (59, 53),
            ["V"] = (80, 71)
        };

        private static readonly FontScaleDictionary fontScales24mm = new()
        {
            ["A"] = (9, 5),
            ["B"] = (11, 7),
            ["C"] = (18, 10),
            ["D"] = (18, 10),
            ["E"] = (42, 20),
            ["F"] = (26, 13),
            ["G"] = (60, 40),
            ["H"] = (34, 22),
            ["GS"] = (24, 24),
            ["P"] = (20, 18),
            ["Q"] = (28, 24),
            ["R"] = (35, 31),
            ["S"] = (40, 35),
            ["T"] = (48, 42),
            ["U"] = (59, 53),
            ["V"] = (80, 71)
        };

        private static (int height, int width)? GetFontScale(string fontName, int printDensityDpmm)
        {
            FontScaleDictionary dict;
            switch (printDensityDpmm)
            {
                case 6:
                    dict = fontScales6mm;
                    break;
                case 8:
                    dict = fontScales8mm;
                    break;
                case 12:
                    dict = fontScales12mm;
                    break;
                case 24:
                    dict = fontScales24mm;
                    break;
                default:
                    return null;
            }

            if (dict.TryGetValue(fontName, out (int height, int width) value))
            {
                return value;
            }

            return null;
        }

        private static readonly (int height, int width) defaultScalingFontScale = (15, 12);

        // This is a corrective used to match labelary.com scale for text fields' font size
        // TODO: implement a proper solution based on font metrics
        private const float heightScale = 1.1f;

        public static float? GetBitmappedFontSize(string fontName, int scalingFactor, int printDensityDpmm)
        {
            return GetFontScale(fontName, printDensityDpmm)?.height * scalingFactor * heightScale;
        }

        /// <summary>
        /// Fonts P to V are fixed size variants of the scalable font 0 (CG Triumvirate Bold Condensed).
        /// Zebra printers and labelary.com render them with the font 0 typeface.
        /// </summary>
        public static bool IsFont0Variant(string fontName)
        {
            return fontName is "0" or "P" or "Q" or "R" or "S" or "T" or "U" or "V";
        }

        // Bitmapped fonts are magnified by integer factors; ZPL rounds half-way values up.
        private static int GetMagnification(int requested, int baseSize)
        {
            return (int)Math.Max(1, Math.Round((double)requested / baseSize, MidpointRounding.AwayFromZero));
        }

        public static (float fontSize, float scaleX) GetFontScaling(string fontName, int fontHeight, int fontWidth, int printDensityDpmm)
        {
            (int height, int width)? fontScale = GetFontScale(fontName, printDensityDpmm);

            if (fontScale != null && IsFont0Variant(fontName))
            {
                (int height, int width) = fontScale.Value;
                int heightRatio = fontHeight > 0 ? GetMagnification(fontHeight, height) : fontWidth > 0 ? GetMagnification(fontWidth, width) : 1;
                int widthRatio = fontWidth > 0 ? GetMagnification(fontWidth, width) : heightRatio;

                // The font 0 typeface is drawn with width == height, so the base cell ratio becomes the horizontal scale.
                float emSize = height * heightRatio;
                return (emSize, (float)(width * widthRatio) / emSize);
            }

            if (fontScale != null)
            {
                (int height, int width) = fontScale.Value;
                if (fontHeight > 0)
                {
                    int intHeightRatio = GetMagnification(fontHeight, height);
                    float emSize = height * intHeightRatio * heightScale;

                    if (fontWidth == 0)
                    {
                        return (emSize, 1.0f);
                    }

                    int intWidthRatio = GetMagnification(fontWidth, width);

                    return (emSize, (float)intWidthRatio / intHeightRatio);
                }
                else if (fontWidth > 0)
                {
                    int intWidthRatio = GetMagnification(fontWidth, width);

                    return (height * intWidthRatio * heightScale, 1.0f);
                }
                else
                {
                    return (height * heightScale, 1.0f);
                }
            }

            float fontSize = fontHeight > 0 ? fontHeight : fontWidth > 0 ? fontWidth : defaultScalingFontScale.height;
            float scaleX = 1.0f;

            if (fontWidth != 0 && fontWidth != fontSize)
            {
                scaleX = fontWidth / fontSize;
            }

            return (fontSize, scaleX);
        }
    }
}
