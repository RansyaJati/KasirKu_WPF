using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Mengelola logo toko untuk dicetak di kepala struk (thermal ESC/POS raster)
    /// dan disisipkan ke struk PDF.
    ///
    /// Urutan sumber logo:
    ///   1. StoreSettings.LogoPath (dipilih lewat menu Pengaturan)
    ///   2. Documents/KasirKu/logo.png  (taruh file logo sendiri di sini)
    ///   3. Logo bawaan yang dibuat otomatis (wordmark BANYU FROZEN FOOD)
    /// </summary>
    public static class LogoService
    {
        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu");

        public static string UserLogoPath => Path.Combine(DataDir, "logo.png");
        public static string DefaultLogoPath => Path.Combine(DataDir, "logo_default.png");

        // ── Resolusi path ────────────────────────────────────────────────

        public static string? ResolveLogoPath(StoreSettings? settings)
        {
            if (settings != null && !string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
                return settings.LogoPath;

            if (File.Exists(UserLogoPath)) return UserLogoPath;

            if (!File.Exists(DefaultLogoPath))
            {
                try { EnsureDefaultLogoFile(); } catch { }
            }

            return File.Exists(DefaultLogoPath) ? DefaultLogoPath : null;
        }

        // ── ESC/POS raster ───────────────────────────────────────────────

        /// <summary>
        /// Membuat perintah ESC/POS <c>GS v 0</c> (raster bit image) untuk logo,
        /// sudah dipusatkan (centered) sesuai lebar kertas printer.
        /// </summary>
        public static byte[] BuildEscPosLogo(StoreSettings settings)
        {
            try
            {
                string? path = ResolveLogoPath(settings);
                if (path == null) return Array.Empty<byte>();

                int paperDots = settings.PaperWidthMm <= 58 ? 384 : 576;
                int maxHeight = settings.PaperWidthMm <= 58 ? 140 : 190;

                using var src = LoadBitmap(path);
                if (src == null) return Array.Empty<byte>();

                var raster = RenderRaster(src!, paperDots, maxHeight);
                if (raster.Height <= 0) return Array.Empty<byte>();

                return BuildRasterCommand(raster.Data, raster.WidthBytes, raster.Height, paperDots);
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private static Bitmap? LoadBitmap(string path)
        {
            try
            {
                // Salin ke memory agar file tidak terkunci
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }

        private sealed class Raster
        {
            public byte[] Data = Array.Empty<byte>();
            public int WidthBytes;
            public int Height;
        }

        /// <summary>
        /// Mengubah gambar menjadi bitmap 1-bit (hitam = cetak) berukuran
        /// selebar kertas, dengan latar putih. Logo berlatar gelap otomatis
        /// di-invers supaya hemat kertas thermal.
        /// </summary>
        private static Raster RenderRaster(Bitmap src, int canvasWidth, int maxHeight)
        {
            src = CropToContent(src) ?? src;

            bool invert = IsDarkBackground(src);

            int widthBytes = (canvasWidth + 7) / 8;

            // Skala agar pas lebar kertas (sisakan margin kiri-kanan yang lega)
            int usable = Math.Max(64, (int)(canvasWidth * 0.82));
            double scale = Math.Min(1.0, (double)usable / src.Width);
            int drawW = Math.Max(1, (int)Math.Round(src.Width * scale));
            int drawH = Math.Max(1, (int)Math.Round(src.Height * scale));

            if (drawH > maxHeight)
            {
                double s2 = (double)maxHeight / drawH;
                drawW = Math.Max(1, (int)Math.Round(drawW * s2));
                drawH = maxHeight;
            }

            int offX = (canvasWidth - drawW) / 2;
            int offY = 0;

            using var canvas = new Bitmap(canvasWidth, drawH, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                if (invert)
                {
                    // Logo berlatar gelap: invers warna saat menggambar, sehingga
                    // margin putih di kanvas tetap putih (tidak jadi bar hitam).
                    using var ia = CreateInvertAttributes();
                    g.DrawImage(src, new Rectangle(offX, offY, drawW, drawH),
                        0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                }
                else
                {
                    g.DrawImage(src, new Rectangle(offX, offY, drawW, drawH));
                }
            }

            var raster = new Raster { WidthBytes = widthBytes, Height = drawH };
            raster.Data = new byte[widthBytes * drawH];

            var bmpData = canvas.LockBits(new Rectangle(0, 0, canvasWidth, drawH),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = bmpData.Stride;
                byte[] buffer = new byte[stride * drawH];
                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < drawH; y++)
                {
                    int rowStart = y * stride;
                    int outRow = y * widthBytes;

                    for (int x = 0; x < canvasWidth; x++)
                    {
                        int i = rowStart + x * 3;
                        int b = buffer[i];
                        int g2 = buffer[i + 1];
                        int r = buffer[i + 2];

                        // Luminance
                        int lum = (r * 299 + g2 * 587 + b * 114) / 1000;

                        // Threshold dengan sedikit ambang agar teks tipis tetap jelas
                        bool black = lum < 150;
                        if (black)
                        {
                            int byteIndex = outRow + (x >> 3);
                            raster.Data[byteIndex] |= (byte)(0x80 >> (x & 7));
                        }
                    }
                }
            }
            finally
            {
                canvas.UnlockBits(bmpData);
            }

            return raster;
        }

        /// <summary>
        /// Memangkas area kosong (warna latar) di sekeliling logo, supaya
        /// logo tercetak rapat dan tidak menyisakan ruang kosong besar.
        /// </summary>
        private static Bitmap? CropToContent(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            if (w < 8 || h < 8) return null;

            Color c1 = src.GetPixel(0, 0);
            Color c2 = src.GetPixel(w - 1, 0);
            Color c3 = src.GetPixel(0, h - 1);
            Color c4 = src.GetPixel(w - 1, h - 1);
            int bgR = (c1.R + c2.R + c3.R + c4.R) / 4;
            int bgG = (c1.G + c2.G + c3.G + c4.G) / 4;
            int bgB = (c1.B + c2.B + c3.B + c4.B) / 4;

            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = src.GetPixel(x, y);
                    int d = Math.Abs(c.R - bgR) + Math.Abs(c.G - bgG) + Math.Abs(c.B - bgB);
                    if (d > 60)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0) return null;

            int padX = Math.Max(2, w / 60);
            int padY = Math.Max(2, h / 60);

            minX = Math.Max(0, minX - padX);
            minY = Math.Max(0, minY - padY);
            maxX = Math.Min(w - 1, maxX + padX);
            maxY = Math.Min(h - 1, maxY + padY);

            int rw = maxX - minX + 1;
            int rh = maxY - minY + 1;

            if (rw >= w && rh >= h) return null;

            var rect = new Rectangle(minX, minY, rw, rh);
            var cropped = new Bitmap(rw, rh, PixelFormat.Format24bppRgb);

            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(src, new Rectangle(0, 0, rw, rh), rect, GraphicsUnit.Pixel);
            }

            return cropped;
        }

        /// <summary>Atribut gambar dengan warna ter-invers (RGB dibalik).</summary>
        private static ImageAttributes CreateInvertAttributes()
        {
            var matrix = new ColorMatrix(new[]
            {
                new float[] { -1,  0,  0, 0, 0 },
                new float[] {  0, -1,  0, 0, 0 },
                new float[] {  0,  0, -1, 0, 0 },
                new float[] {  0,  0,  0, 1, 0 },
                new float[] {  1,  1,  1, 0, 1 }
            });

            var ia = new ImageAttributes();
            ia.SetColorMatrix(matrix);
            return ia;
        }

        /// <summary>
        /// Deteksi apakah logo berlatar gelap (perlu di-invers untuk thermal).
        /// Sampling titik di tepi gambar.
        /// </summary>
        private static bool IsDarkBackground(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            if (w < 3 || h < 3) return false;

            long sum = 0;
            int count = 0;

            void Sample(int x, int y)
            {
                Color c = src.GetPixel(x, y);
                sum += (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                count++;
            }

            int stepX = Math.Max(1, w / 20);
            int stepY = Math.Max(1, h / 10);

            for (int x = 0; x < w; x += stepX)
            {
                Sample(x, 0);
                Sample(x, h - 1);
            }
            for (int y = 0; y < h; y += stepY)
            {
                Sample(0, y);
                Sample(w - 1, y);
            }

            if (count == 0) return false;
            return sum / count < 120;
        }

        /// <summary>Menyusun perintah ESC/POS GS v 0 dari data raster 1-bit.</summary>
        private static byte[] BuildRasterCommand(byte[] data, int widthBytes, int height, int paperDots)
        {
            using var ms = new MemoryStream();

            // ESC a 1 (center) — sebagian printer mengabaikan untuk raster,
            // jadi padding kiri sudah dibuat 0 (putih) pada kanvas.
            ms.Write(new byte[] { 0x1B, 0x61, 0x01 }, 0, 3);

            // GS v 0 0 xL xH yL yH
            ms.WriteByte(0x1D);
            ms.WriteByte(0x76);
            ms.WriteByte(0x30);
            ms.WriteByte(0x00);
            ms.WriteByte((byte)(widthBytes & 0xFF));
            ms.WriteByte((byte)((widthBytes >> 8) & 0xFF));
            ms.WriteByte((byte)(height & 0xFF));
            ms.WriteByte((byte)((height >> 8) & 0xFF));
            ms.Write(data, 0, data.Length);

            // Kembali rata kiri + line feed
            ms.Write(new byte[] { 0x1B, 0x61, 0x00 }, 0, 3);
            return ms.ToArray();
        }

        // ── Untuk PDF ────────────────────────────────────────────────────

        /// <summary>
        /// Menghasilkan data RGB 24-bit (latar putih, sudah diperkecil) untuk
        /// disisipkan sebagai gambar di struk PDF.
        /// </summary>
        public static (byte[] Rgb, int Width, int Height)? LoadRgbForPdf(StoreSettings? settings, int targetWidthPx = 480)
        {
            try
            {
                string? path = ResolveLogoPath(settings);
                if (path == null) return null;

                using var src = LoadBitmap(path);
                if (src == null) return null;

                var cropped = CropToContent(src);
                Bitmap use = cropped ?? src;

                double scale = Math.Min(1.0, (double)targetWidthPx / use.Width);
                int w = Math.Max(1, (int)Math.Round(use.Width * scale));
                int h = Math.Max(1, (int)Math.Round(use.Height * scale));

                using var canvas = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    if (IsDarkBackground(use))
                    {
                        using var ia = CreateInvertAttributes();
                        g.DrawImage(use, new Rectangle(0, 0, w, h),
                            0, 0, use.Width, use.Height, GraphicsUnit.Pixel, ia);
                    }
                    else
                    {
                        g.DrawImage(use, new Rectangle(0, 0, w, h));
                    }
                }

                if (!ReferenceEquals(use, src)) use.Dispose();

                var bmpData = canvas.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                byte[] rgb = new byte[w * h * 3];
                try
                {
                    byte[] row = new byte[bmpData.Stride];
                    for (int y = 0; y < h; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            IntPtr.Add(bmpData.Scan0, y * bmpData.Stride), row, 0, bmpData.Stride);

                        for (int x = 0; x < w; x++)
                        {
                            int si = x * 3;
                            int di = (y * w + x) * 3;
                            rgb[di] = row[si + 2];     // R
                            rgb[di + 1] = row[si + 1]; // G
                            rgb[di + 2] = row[si];     // B
                        }
                    }
                }
                finally
                {
                    canvas.UnlockBits(bmpData);
                }

                return (rgb, w, h);
            }
            catch
            {
                return null;
            }
        }

        // ── Logo bawaan ──────────────────────────────────────────────────

        /// <summary>
        /// Membuat file logo bawaan (wordmark BANYU FROZEN FOOD) bila belum ada.
        /// File ini bisa ditimpa dengan logo asli toko.
        /// </summary>
        public static string EnsureDefaultLogoFile()
        {
            Directory.CreateDirectory(DataDir);

            if (File.Exists(DefaultLogoPath))
                return DefaultLogoPath;

            int w = 960, h = 300;
            using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Ikon monogram (gaya pita "B"): dua busur tebal
                using var penAccent = new Pen(Color.FromArgb(86, 190, 235), 26)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                using var penAccent2 = new Pen(Color.FromArgb(86, 190, 235), 26)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.DrawArc(penAccent, new Rectangle(40, 60, 150, 110), 200, 250);
                g.DrawArc(penAccent2, new Rectangle(40, 130, 150, 110), 160, 220);

                // Teks utama
                using var fBanyu = new Font("Arial Black", 76, FontStyle.Bold, GraphicsUnit.Pixel);
                using var fSub = new Font("Arial", 34, FontStyle.Bold, GraphicsUnit.Pixel);
                using var white = new SolidBrush(Color.White);

                g.DrawString("BANYU", fBanyu, white, 210, 45);
                g.DrawString("F R O Z E N   F O O D", fSub, white, 214, 165);
            }

            bmp.Save(DefaultLogoPath, ImageFormat.Png);
            return DefaultLogoPath;
        }
    }
}
