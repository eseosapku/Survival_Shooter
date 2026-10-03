using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ricochet.EditorTools
{
    /// <summary>
    /// Draws the project's textures in code and saves them as PNGs:
    /// the floor texture with the player's full name, the wall grid, the reticle and the UI sprites.
    /// </summary>
    public static class TextureFactory
    {
        // 5x7 pixel font ("#" = lit). Blocky letters suit the neon sci-fi look.
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
            ['C'] = new[] { " ####", "#    ", "#    ", "#    ", "#    ", "#    ", " ####" },
            ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
            ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
            ['G'] = new[] { " ####", "#    ", "#    ", "#  ##", "#   #", "#   #", " ####" },
            ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['I'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" },
            ['J'] = new[] { "  ###", "   # ", "   # ", "   # ", "#  # ", "#  # ", " ##  " },
            ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
            ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
            ['M'] = new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" },
            ['N'] = new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
            ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            ['Q'] = new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
            ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
            ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
            ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
            ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #" },
            ['X'] = new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
            ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
            ['-'] = new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
            [' '] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
        };

        class PixelCanvas
        {
            public readonly int W, H;
            public readonly Color[] Px;

            public PixelCanvas(int w, int h, Color fill)
            {
                W = w;
                H = h;
                Px = new Color[w * h];
                for (int i = 0; i < Px.Length; i++) Px[i] = fill;
            }

            /// <summary>Alpha-blends a colour onto a pixel.</summary>
            public void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0f) return;
                int i = y * W + x;
                Color d = Px[i];
                float a = c.a + d.a * (1f - c.a);
                if (a <= 0f) return;
                Color o = (c * c.a + d * d.a * (1f - c.a)) / a;
                o.a = a;
                Px[i] = o;
            }

            public void Rect(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++)
                        Blend(x, y, c);
            }

            public Texture2D ToTexture()
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                t.SetPixels(Px);
                t.Apply();
                return t;
            }
        }

        static void Save(PixelCanvas c, string path)
        {
            var tex = c.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ---------------- Floor: full name ----------------

        public static void FloorName(string path, string fullName)
        {
            const int size = 1024;
            var c = new PixelCanvas(size, size, new Color(0f, 0.85f, 1f, 0.10f));
            Color line = new Color(0.2f, 1f, 1f, 0.35f);
            Color edge = new Color(0.2f, 1f, 1f, 0.85f);

            // Fine grid every 128 px, strong edge so tiles form a 50 cm grid on the floor.
            for (int i = 0; i < size; i += 128)
            {
                c.Rect(i, 0, 2, size, line);
                c.Rect(0, i, size, 2, line);
            }
            c.Rect(0, 0, 5, size, edge);
            c.Rect(size - 5, 0, 5, size, edge);
            c.Rect(0, 0, size, 5, edge);
            c.Rect(0, size - 5, size, 5, edge);

            // Corner brackets in magenta.
            Color mag = new Color(1f, 0.2f, 0.85f, 0.9f);
            foreach (var (x, y, sx, sy) in new[] { (24, 24, 1, 1), (size - 24, 24, -1, 1), (24, size - 24, 1, -1), (size - 24, size - 24, -1, -1) })
            {
                c.Rect(sx > 0 ? x : x - 80, sy > 0 ? y : y - 8, 80, 8, mag);
                c.Rect(sx > 0 ? x : x - 8, sy > 0 ? y : y - 80, 8, 80, mag);
            }

            // The name, split into lines that fit the tile.
            var lines = SplitName(fullName.ToUpperInvariant(), 11);
            const int scale = 9;
            const int lineGap = 30;
            int blockH = lines.Count * 7 * scale + (lines.Count - 1) * lineGap;
            int top = (size + blockH) / 2;

            // Dark band behind the name so it stays readable on bright floors.
            c.Rect(60, top - blockH - 50, size - 120, blockH + 100, new Color(0.02f, 0.03f, 0.08f, 0.7f));
            c.Rect(60, top + 46, size - 120, 4, new Color(1f, 0.2f, 0.85f, 0.9f));
            c.Rect(60, top - blockH - 50, size - 120, 4, new Color(1f, 0.2f, 0.85f, 0.9f));

            for (int l = 0; l < lines.Count; l++)
            {
                string text = lines[l];
                int textW = (text.Length * 6 - 1) * scale;
                int x0 = (size - textW) / 2;
                int y0 = top - (l + 1) * 7 * scale - l * lineGap;
                DrawText(c, text, x0, y0, scale, new Color(1f, 0.2f, 0.85f, 0.6f), 4); // glow
                DrawText(c, text, x0, y0, scale, new Color(0.4f, 1f, 1f, 1f), 1);     // core
            }

            // Small caption.
            DrawText(c, "RICOCHET", (size - (8 * 6 - 1) * 4) / 2, 120, 4, new Color(0.2f, 1f, 1f, 0.6f), 0);
            Save(c, path);
        }

        static List<string> SplitName(string name, int maxChars)
        {
            var result = new List<string>();
            string current = "";
            foreach (var word in name.Split(' '))
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > maxChars)
                {
                    result.Add(current);
                    current = word;
                }
                else current = current.Length == 0 ? word : current + " " + word;
            }
            if (current.Length > 0) result.Add(current);
            return result;
        }

        static void DrawText(PixelCanvas c, string text, int x0, int y0, int scale, Color color, int grow)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out var g)) continue;
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (g[row][col] != '#') continue;
                        int px = x0 + (i * 6 + col) * scale;
                        int py = y0 + (6 - row) * scale; // row 0 is the top
                        c.Rect(px - grow, py - grow, scale + grow * 2, scale + grow * 2, color);
                    }
            }
        }

        // ---------------- Walls, reticle ----------------

        public static void WallGrid(string path)
        {
            const int size = 256;
            var c = new PixelCanvas(size, size, new Color(0.2f, 1f, 1f, 0.07f));
            Color line = new Color(0.3f, 1f, 1f, 0.75f);
            c.Rect(0, 0, 3, size, line);
            c.Rect(0, 0, size, 3, line);
            c.Rect(size / 2 - 1, 0, 2, size, new Color(0.3f, 1f, 1f, 0.3f));
            c.Rect(0, size / 2 - 1, size, 2, new Color(0.3f, 1f, 1f, 0.3f));
            Save(c, path);
        }

        public static void Reticle(string path)
        {
            const int size = 256;
            var c = new PixelCanvas(size, size, Color.clear);
            float mid = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(mid, mid));
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 112f) / 6f);
                    float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 70f) / 2.5f) * 0.6f;
                    float dot = Mathf.Clamp01((14f - d) / 2f);
                    float a = Mathf.Max(ring, Mathf.Max(inner, dot));
                    c.Blend(x, y, new Color(0.3f, 1f, 1f, a));
                }
            // Ticks
            Color tick = new Color(1f, 0.25f, 0.85f, 1f);
            c.Rect((int)mid - 3, 0, 6, 28, tick);
            c.Rect((int)mid - 3, size - 28, 6, 28, tick);
            c.Rect(0, (int)mid - 3, 28, 6, tick);
            c.Rect(size - 28, (int)mid - 3, 28, 6, tick);
            Save(c, path);
        }

        // ---------------- UI sprites ----------------

        public static void RoundedRect(string path, int size, float radius)
        {
            var c = new PixelCanvas(size, size, Color.clear);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    c.Blend(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
            Save(c, path);
        }

        public static void Circle(string path, int size, float ringThickness)
        {
            var c = new PixelCanvas(size, size, Color.clear);
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ringThickness > 0f) a *= Mathf.Clamp01(d - (r - ringThickness) + 0.5f);
                    c.Blend(x, y, new Color(1f, 1f, 1f, a));
                }
            Save(c, path);
        }

        /// <summary>Transparent centre, opaque edges (tinted red by the HUD for damage).</summary>
        public static void Vignette(string path)
        {
            const int size = 256;
            var c = new PixelCanvas(size, size, Color.clear);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float d = p.magnitude;
                    c.Blend(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f))));
                }
            Save(c, path);
        }

        /// <summary>Soft round glow used by particles and the muzzle flash.</summary>
        public static void Glow(string path)
        {
            const int size = 64;
            var c = new PixelCanvas(size, size, Color.clear);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    c.Blend(x, y, new Color(1f, 1f, 1f, a * a));
                }
            Save(c, path);
        }
    }
}
