#nullable enable

using System.Collections.Generic;
using UnityEngine;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Presentation.Cards
{
    /// <summary>
    /// Procedurally paints readable UNO card faces (rank + suit) onto textures.
    /// Avoids TextMeshPro/font asset failures so ranks stay visible in play mode.
    /// </summary>
    public static class CardFaceBaker
    {
        private const int Width = 256;
        private const int Height = 384;

        private static readonly Dictionary<string, Texture2D> FaceCache = new Dictionary<string, Texture2D>(128);
        private static Texture2D? _backTexture;

        // 5x7 pixel glyphs for digits / letters used on cards.
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
            ['3'] = new[] { "01110", "10001", "00001", "00110", "00001", "10001", "01110" },
            ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
            ['+'] = new[] { "00100", "00100", "00100", "11111", "00100", "00100", "00100" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
            ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
            ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
            ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            ['D'] = new[] { "11100", "10010", "10001", "10001", "10001", "10010", "11100" },
            ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
        };

        public static Texture2D GetFaceTexture(Card card)
        {
            string key = $"{card.Color}_{card.Type}_{card.Value}";
            if (FaceCache.TryGetValue(key, out Texture2D? cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = BakeFace(card);
            FaceCache[key] = tex;
            return tex;
        }

        public static Texture2D GetBackTexture()
        {
            if (_backTexture != null)
            {
                return _backTexture;
            }

            _backTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "UNO_CardBack",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color border = new Color(0.95f, 0.95f, 0.97f, 1f);
            Color body = new Color(0.72f, 0.08f, 0.14f, 1f);
            Color oval = new Color(0.12f, 0.12f, 0.16f, 1f);
            Color ink = new Color(1f, 0.85f, 0.15f, 1f);

            Fill(_backTexture, body);
            FillRect(_backTexture, 8, 8, Width - 16, Height - 16, border);
            FillRect(_backTexture, 18, 18, Width - 36, Height - 36, body);
            FillEllipse(_backTexture, Width / 2, Height / 2, 78, 110, oval);
            DrawString(_backTexture, "UNO", Width / 2, Height / 2, 5, ink, centered: true);
            _backTexture.Apply(false, false);
            return _backTexture;
        }

        public static string GetShortLabel(Card card)
        {
            return card.Type switch
            {
                CardType.Number => card.Value.ToString(),
                CardType.Skip => "SKIP",
                CardType.Reverse => "REV",
                CardType.DrawTwo => "+2",
                CardType.Wild => "WILD",
                CardType.WildDrawFour => "+4",
                _ => "?"
            };
        }

        public static string GetDisplayName(Card card)
        {
            return card.Type switch
            {
                CardType.Number => $"{card.Color.ToString().ToUpperInvariant()} {card.Value}",
                CardType.Skip => $"{card.Color.ToString().ToUpperInvariant()} SKIP",
                CardType.Reverse => $"{card.Color.ToString().ToUpperInvariant()} REV",
                CardType.DrawTwo => $"{card.Color.ToString().ToUpperInvariant()} +2",
                CardType.Wild => "WILD",
                CardType.WildDrawFour => "WILD +4",
                _ => "CARD"
            };
        }

        public static Color GetSuitColor(CardColor color)
        {
            return color switch
            {
                CardColor.Red => new Color(0.906f, 0.114f, 0.212f, 1f),
                CardColor.Blue => new Color(0.05f, 0.42f, 0.95f, 1f),
                CardColor.Green => new Color(0.05f, 0.72f, 0.38f, 1f),
                CardColor.Yellow => new Color(1f, 0.84f, 0.08f, 1f),
                CardColor.Wild => new Color(0.14f, 0.14f, 0.16f, 1f),
                _ => Color.gray
            };
        }

        private static Texture2D BakeFace(Card card)
        {
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = $"UNO_Face_{card.Color}_{card.Type}_{card.Value}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color suit = GetSuitColor(card.Color);
            Color border = Color.white;
            Color oval = Color.white;
            Color ink = card.Color == CardColor.Yellow
                ? new Color(0.12f, 0.1f, 0.05f, 1f)
                : suit;

            if (card.Color == CardColor.Wild)
            {
                // Four-color wild face.
                FillQuadWild(tex);
                FillRect(tex, 10, 10, Width - 20, Height - 20, new Color(1f, 1f, 1f, 0.15f));
                FillEllipse(tex, Width / 2, Height / 2, 86, 120, new Color(0.1f, 0.1f, 0.12f, 1f));
                ink = Color.white;
            }
            else
            {
                Fill(tex, suit);
                FillRect(tex, 8, 8, Width - 16, Height - 16, border);
                FillRect(tex, 18, 18, Width - 36, Height - 36, suit);
                FillEllipse(tex, Width / 2, Height / 2, 82, 116, oval);
            }

            string label = GetShortLabel(card);
            int centerScale = label.Length <= 2 ? 7 : label.Length <= 3 ? 5 : 4;
            DrawString(tex, label, Width / 2, Height / 2, centerScale, ink, centered: true);

            // Corner ranks for quick reading when cards overlap in a fan.
            string corner = label.Length <= 2 ? label : label.Substring(0, Mathf.Min(2, label.Length));
            DrawString(tex, corner, 40, Height - 48, 3, Color.white, centered: true);
            DrawString(tex, corner, Width - 40, 48, 3, Color.white, centered: true, flipped: true);

            tex.Apply(false, false);
            return tex;
        }

        private static void FillQuadWild(Texture2D tex)
        {
            Color red = GetSuitColor(CardColor.Red);
            Color blue = GetSuitColor(CardColor.Blue);
            Color green = GetSuitColor(CardColor.Green);
            Color yellow = GetSuitColor(CardColor.Yellow);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool left = x < Width / 2;
                    bool top = y >= Height / 2;
                    Color c = (left, top) switch
                    {
                        (true, true) => red,
                        (false, true) => blue,
                        (true, false) => yellow,
                        _ => green
                    };
                    tex.SetPixel(x, y, c);
                }
            }
        }

        private static void Fill(Texture2D tex, Color color)
        {
            Color[] pixels = new Color[Width * Height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            int xMax = Mathf.Min(Width, x + w);
            int yMax = Mathf.Min(Height, y + h);
            for (int py = Mathf.Max(0, y); py < yMax; py++)
            {
                for (int px = Mathf.Max(0, x); px < xMax; px++)
                {
                    tex.SetPixel(px, py, color);
                }
            }
        }

        private static void FillEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= Width)
                    {
                        continue;
                    }

                    float nx = (x - cx) / (float)rx;
                    float ny = (y - cy) / (float)ry;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void DrawString(
            Texture2D tex,
            string text,
            int cx,
            int cy,
            int scale,
            Color color,
            bool centered,
            bool flipped = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int glyphW = 5 * scale;
            int glyphH = 7 * scale;
            int gap = scale;
            int totalW = text.Length * glyphW + (text.Length - 1) * gap;
            int startX = centered ? cx - totalW / 2 : cx;
            int startY = centered ? cy - glyphH / 2 : cy;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = char.ToUpperInvariant(text[i]);
                int gx = startX + i * (glyphW + gap);
                DrawGlyph(tex, ch, gx, startY, scale, color, flipped);
            }
        }

        private static void DrawGlyph(Texture2D tex, char ch, int x, int y, int scale, Color color, bool flipped)
        {
            if (!Glyphs.TryGetValue(ch, out string[]? rows) || rows == null)
            {
                return;
            }

            for (int row = 0; row < 7; row++)
            {
                string line = rows[row];
                for (int col = 0; col < 5; col++)
                {
                    if (line[col] != '1')
                    {
                        continue;
                    }

                    int px = flipped ? x + (4 - col) * scale : x + col * scale;
                    int py = flipped ? y + (6 - row) * scale : y + (6 - row) * scale;
                    FillRect(tex, px, py, scale, scale, color);
                }
            }
        }
    }
}
