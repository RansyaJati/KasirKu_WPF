using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Emulator ESC/POS ringan: mengubah byte stream printer thermal menjadi
    /// gambar (System.Drawing.Bitmap) supaya struk bisa dipratinjau di layar
    /// tanpa harus mencetak ke kertas.
    ///
    /// Mendukung perintah yang dipakai KasirKu:
    ///   ESC @ (init), ESC a (align), ESC E (bold), ESC d (feed),
    ///   GS ! (ukuran huruf), GS v 0 (raster logo), GS V (potong kertas).
    /// </summary>
    public static class StrukRenderer
    {
        private const int CellWidth = 12;   // 48 kolom x 12 dot = 576 dot (80mm)
        private const int LineHeight = 26;

        /// <summary>Merender struk sebuah transaksi sesuai pengaturan toko.</summary>
        public static Bitmap RenderStruk(Transaction t, StoreSettings settings)
        {
            int dots;
            if (settings.PaperWidthMm == 76 || (settings.PrinterName ?? "").ToUpperInvariant().Contains("U220"))
                dots = 480; // 40 kolom x 12 dot
            else if (settings.PaperWidthMm <= 58)
                dots = 384;
            else
                dots = 576;

            byte[] bytes = ThermalPrintService.BuildStrukBytes(t, settings);
            return RenderEscPos(bytes, dots);
        }

        /// <summary>Menyimpan hasil render struk sebagai PNG.</summary>
        public static string SimpanStrukPng(Transaction t, StoreSettings settings, string? filePath = null)
        {
            string target = filePath ?? Path.Combine(ExportService.StrukDir, $"Struk-{SafeId(t.Id)}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var bmp = RenderStruk(t, settings);
            bmp.Save(target, ImageFormat.Png);
            return target;
        }

        private static string SafeId(string id)
            => string.Concat((id ?? "struk").Split(Path.GetInvalidFileNameChars()));

        /// <summary>Merender byte ESC/POS menjadi bitmap selebar kertas thermal.</summary>
        public static Bitmap RenderEscPos(byte[] data, int dots = 576)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch { /* abaikan: encoding opsional */ }

            const int maxHeight = 8000;

            using var canvas = new Bitmap(dots, maxHeight, PixelFormat.Format24bppRgb);
            int usedHeight;

            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;

                var fontReg = new Font("Consolas", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
                var fontBold = new Font("Consolas", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
                var brush = Brushes.Black;

                int y = 8;
                bool bold = false;
                int align = 0;      // 0 kiri, 1 tengah, 2 kanan
                int widthScale = 1, heightScale = 1;
                var line = new StringBuilder();

                void FlushLine()
                {
                    if (line.Length == 0) return;
                    string text = line.ToString();
                    line.Clear();

                    int cellW = CellWidth * widthScale;
                    int totalW = text.Length * cellW;
                    int startX = align switch
                    {
                        1 => Math.Max(0, (dots - totalW) / 2),
                        2 => Math.Max(0, dots - totalW),
                        _ => 0
                    };

                    var font = bold ? fontBold : fontReg;

                    for (int i = 0; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c == ' ') continue;

                        float gx = startX + i * cellW;
                        float gy = y;

                        var state = g.Save();
                        g.TranslateTransform(gx, gy);
                        g.ScaleTransform(widthScale, heightScale);
                        g.DrawString(c.ToString(), font, brush, 0, 0);
                        g.Restore(state);
                    }

                    y += LineHeight * heightScale;
                }

                int p = 0;
                while (p < data.Length && y < maxHeight - 200)
                {
                    byte b = data[p];

                    // ── ESC ────────────────────────────────────────────
                    if (b == 0x1B && p + 1 < data.Length)
                    {
                        byte cmd = data[p + 1];

                        if (cmd == 0x40) { bold = false; align = 0; widthScale = heightScale = 1; p += 2; continue; }
                        if (cmd == 0x61 && p + 2 < data.Length) { FlushLine(); align = data[p + 2]; p += 3; continue; }
                        if (cmd == 0x45 && p + 2 < data.Length) { bold = data[p + 2] != 0; p += 3; continue; }
                        if (cmd == 0x64 && p + 2 < data.Length) { FlushLine(); y += LineHeight * data[p + 2]; p += 3; continue; }
                        if (cmd == 0x70) { p += Math.Min(5, data.Length - p); continue; } // ESC p (kick drawer)

                        p += 2;
                        continue;
                    }

                    // ── GS ─────────────────────────────────────────────
                    if (b == 0x1D && p + 1 < data.Length)
                    {
                        byte cmd = data[p + 1];

                        if (cmd == 0x21 && p + 2 < data.Length)
                        {
                            byte n = data[p + 2];
                            widthScale = (n & 0x10) != 0 || (n & 0x20) != 0 ? 2 : 1;
                            heightScale = (n & 0x01) != 0 || (n & 0x02) != 0 ? 2 : 1;
                            p += 3;
                            continue;
                        }

                        if (cmd == 0x56) // potong kertas
                        {
                            FlushLine();
                            using (var pen = new Pen(Color.FromArgb(170, 170, 170), 2) { DashStyle = DashStyle.Dash })
                                g.DrawLine(pen, 6, y, dots - 6, y);
                            y += 14;
                            p += (p + 2 < data.Length && data[p + 2] == 0x42) ? 4 : 3;
                            continue;
                        }

                        if (cmd == 0x76 && p + 7 < data.Length && data[p + 2] == 0x30)
                        {
                            FlushLine();
                            int xL = data[p + 4], xH = data[p + 5], yL = data[p + 6], yH = data[p + 7];
                            int wBytes = xL + (xH << 8);
                            int hDots = yL + (yH << 8);
                            int len = wBytes * hDots;
                            int src = p + 8;
                            if (src + len > data.Length) break;

                            int imgW = wBytes * 8;
                            using var img = new Bitmap(imgW, hDots, PixelFormat.Format1bppIndexed);

                            var pal = img.Palette;
                            pal.Entries[0] = Color.Black;
                            pal.Entries[1] = Color.White;
                            img.Palette = pal;

                            var rect = new Rectangle(0, 0, imgW, hDots);
                            var bits = img.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
                            try
                            {
                                byte[] rowBytes = new byte[bits.Stride];
                                for (int row = 0; row < hDots; row++)
                                {
                                    Array.Clear(rowBytes, 0, rowBytes.Length);
                                    for (int col = 0; col < wBytes; col++)
                                    {
                                        byte srcByte = data[src + row * wBytes + col];
                                        // ESC/POS: bit 1 = hitam. GDI 1bpp: bit 1 = putih → balik.
                                        rowBytes[col] = (byte)~srcByte;
                                    }
                                    System.Runtime.InteropServices.Marshal.Copy(
                                        rowBytes, 0, IntPtr.Add(bits.Scan0, row * bits.Stride), rowBytes.Length);
                                }
                            }
                            finally
                            {
                                img.UnlockBits(bits);
                            }

                            int xOff = align switch
                            {
                                1 => Math.Max(0, (dots - imgW) / 2),
                                2 => Math.Max(0, dots - imgW),
                                _ => 0
                            };

                            g.DrawImage(img, new Rectangle(xOff, y, imgW, hDots));
                            y += hDots + 4;
                            p = src + len;
                            continue;
                        }

                        p += 2;
                        continue;
                    }

                    // ── Line feed ──────────────────────────────────────
                    if (b == 0x0A)
                    {
                        FlushLine();
                        p++;
                        continue;
                    }

                    // ── Teks biasa ─────────────────────────────────────
                    line.Append((char)b);
                    p++;
                }

                FlushLine();
                usedHeight = Math.Max(40, Math.Min(maxHeight, y + 24));
            }

            // Pangkas sisa kanvas kosong di bawah struk
            var cropped = new Bitmap(dots, usedHeight, PixelFormat.Format24bppRgb);
            using (var g2 = Graphics.FromImage(cropped))
            {
                g2.DrawImage(canvas,
                    new Rectangle(0, 0, dots, usedHeight),
                    new Rectangle(0, 0, dots, usedHeight),
                    GraphicsUnit.Pixel);
            }
            return cropped;
        }
    }
}
