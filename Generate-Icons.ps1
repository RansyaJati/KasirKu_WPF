Add-Type -AssemblyName System.Drawing

$generatorScript = @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

public static class IconBuilder {
    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius) {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        // Top-left
        path.AddArc(arc, 180, 90);
        // Top-right
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom-right
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom-left
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    public static Bitmap RenderPosIcon(int size) {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            float scale = size / 256f;

            // 1. Drop shadow
            using (var shadowPath = CreateRoundedRectangle(new RectangleF(12 * scale, 16 * scale, 232 * scale, 232 * scale), 48 * scale))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) {
                g.FillPath(shadowBrush, shadowPath);
            }

            // 2. Base Squircle (Gradient Blue / Cyan)
            RectangleF baseRect = new RectangleF(12 * scale, 12 * scale, 232 * scale, 232 * scale);
            using (var basePath = CreateRoundedRectangle(baseRect, 48 * scale)) {
                using (var lgb = new LinearGradientBrush(
                    new PointF(baseRect.Left, baseRect.Top),
                    new PointF(baseRect.Right, baseRect.Bottom),
                    Color.FromArgb(255, 14, 165, 233),   // Sky 500
                    Color.FromArgb(255, 30, 58, 138)))    // Blue 900
                {
                    g.FillPath(lgb, basePath);
                }

                // Inner highlight border
                using (var borderPen = new Pen(Color.FromArgb(140, 255, 255, 255), 2.5f * scale)) {
                    g.DrawPath(borderPen, basePath);
                }
            }

            // 3. Cash Register & POS illustration
            // Register Base / Drawer
            RectangleF drawerRect = new RectangleF(50 * scale, 138 * scale, 156 * scale, 48 * scale);
            using (var drawerPath = CreateRoundedRectangle(drawerRect, 10 * scale)) {
                using (var brush = new SolidBrush(Color.FromArgb(248, 250, 252))) {
                    g.FillPath(brush, drawerPath);
                }
                using (var pen = new Pen(Color.FromArgb(203, 213, 225), 2f * scale)) {
                    g.DrawPath(pen, drawerPath);
                }
            }

            // Drawer Key / Handle slot
            using (var slotBrush = new SolidBrush(Color.FromArgb(148, 163, 184))) {
                g.FillEllipse(slotBrush, new RectangleF(122 * scale, 158 * scale, 12 * scale, 12 * scale));
                g.FillRectangle(slotBrush, new RectangleF(70 * scale, 161 * scale, 36 * scale, 6 * scale));
                g.FillRectangle(slotBrush, new RectangleF(150 * scale, 161 * scale, 36 * scale, 6 * scale));
            }

            // Register Main Body (slanted keyboard deck)
            PointF[] bodyPoints = new PointF[] {
                new PointF(66 * scale, 84 * scale),
                new PointF(190 * scale, 84 * scale),
                new PointF(198 * scale, 138 * scale),
                new PointF(58 * scale, 138 * scale)
            };
            using (var bodyBrush = new SolidBrush(Color.FromArgb(226, 232, 240))) {
                g.FillPolygon(bodyBrush, bodyPoints);
            }

            // Register Display Screen
            RectangleF screenRect = new RectangleF(78 * scale, 42 * scale, 100 * scale, 42 * scale);
            using (var screenPath = CreateRoundedRectangle(screenRect, 6 * scale)) {
                using (var screenBg = new SolidBrush(Color.FromArgb(15, 23, 42))) { // Dark navy
                    g.FillPath(screenBg, screenPath);
                }
                using (var screenBorder = new Pen(Color.FromArgb(56, 189, 248), 2f * scale)) {
                    g.DrawPath(screenBorder, screenPath);
                }

                // Green LED dot
                using (var ledBrush = new SolidBrush(Color.FromArgb(34, 197, 94))) {
                    g.FillEllipse(ledBrush, new RectangleF(84 * scale, 48 * scale, 6 * scale, 6 * scale));
                }

                // Text on screen: "KASIR"
                if (size >= 32) {
                    using (var font = new Font("Segoe UI Black", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var textBrush = new SolidBrush(Color.FromArgb(56, 189, 248))) {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("KASIR", font, textBrush, new RectangleF(80 * scale, 42 * scale, 98 * scale, 42 * scale), sf);
                    }
                }
            }

            // Receipt paper rolling out from top of register
            PointF[] receiptPoints = new PointF[] {
                new PointF(142 * scale, 36 * scale),
                new PointF(170 * scale, 36 * scale),
                new PointF(170 * scale, 75 * scale),
                new PointF(142 * scale, 75 * scale)
            };
            using (var receiptBrush = new SolidBrush(Color.White)) {
                g.FillPolygon(receiptBrush, receiptPoints);
            }
            // Receipt lines
            using (var linePen = new Pen(Color.FromArgb(148, 163, 184), 2f * scale)) {
                g.DrawLine(linePen, 146 * scale, 44 * scale, 166 * scale, 44 * scale);
                g.DrawLine(linePen, 146 * scale, 52 * scale, 166 * scale, 52 * scale);
                g.DrawLine(linePen, 146 * scale, 60 * scale, 160 * scale, 60 * scale);
            }

            // Keypad buttons on slanted body
            using (var keyBrush = new SolidBrush(Color.FromArgb(37, 99, 235))) {
                for (int r = 0; r < 2; r++) {
                    for (int c = 0; c < 4; c++) {
                        float kx = (74 + c * 26) * scale;
                        float ky = (94 + r * 18) * scale;
                        g.FillRectangle(keyBrush, kx, ky, 18 * scale, 11 * scale);
                    }
                }
            }
            // Big Enter Key (Green)
            using (var enterBrush = new SolidBrush(Color.FromArgb(34, 197, 94))) {
                g.FillRectangle(enterBrush, 152 * scale, 112 * scale, 24 * scale, 11 * scale);
            }

            // Bottom Ribbon Badge: "POS"
            RectangleF badgeRect = new RectangleF(58 * scale, 194 * scale, 140 * scale, 34 * scale);
            using (var badgePath = CreateRoundedRectangle(badgeRect, 10 * scale)) {
                using (var badgeBg = new SolidBrush(Color.FromArgb(245, 15, 23, 42))) { // Deep Slate
                    g.FillPath(badgeBg, badgePath);
                }
                using (var badgeBorder = new Pen(Color.FromArgb(56, 189, 248), 2f * scale)) {
                    g.DrawPath(badgeBorder, badgePath);
                }

                if (size >= 32) {
                    using (var font = new Font("Segoe UI Black", 14 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var textBrush = new SolidBrush(Color.White)) {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("KASIRKU POS", font, textBrush, badgeRect, sf);
                    }
                }
            }
        }
        return bmp;
    }

    public static Bitmap RenderManagerIcon(int size) {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            float scale = size / 256f;

            // 1. Drop shadow
            using (var shadowPath = CreateRoundedRectangle(new RectangleF(12 * scale, 16 * scale, 232 * scale, 232 * scale), 48 * scale))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) {
                g.FillPath(shadowBrush, shadowPath);
            }

            // 2. Base Squircle (Gradient Deep Indigo / Purple / Violet)
            RectangleF baseRect = new RectangleF(12 * scale, 12 * scale, 232 * scale, 232 * scale);
            using (var basePath = CreateRoundedRectangle(baseRect, 48 * scale)) {
                using (var lgb = new LinearGradientBrush(
                    new PointF(baseRect.Left, baseRect.Top),
                    new PointF(baseRect.Right, baseRect.Bottom),
                    Color.FromArgb(255, 99, 102, 241),   // Indigo 500
                    Color.FromArgb(255, 30, 27, 75)))     // Indigo 950
                {
                    g.FillPath(lgb, basePath);
                }

                // Inner highlight border
                using (var borderPen = new Pen(Color.FromArgb(140, 255, 255, 255), 2.5f * scale)) {
                    g.DrawPath(borderPen, basePath);
                }
            }

            // 3. Manager illustration: Dashboard Card with Analytics Chart & Gear/Crown
            RectangleF cardRect = new RectangleF(44 * scale, 44 * scale, 168 * scale, 138 * scale);
            using (var cardPath = CreateRoundedRectangle(cardRect, 14 * scale)) {
                using (var cardBrush = new SolidBrush(Color.FromArgb(255, 255, 255))) {
                    g.FillPath(cardBrush, cardPath);
                }
                using (var cardBorder = new Pen(Color.FromArgb(199, 210, 254), 2f * scale)) {
                    g.DrawPath(cardBorder, cardPath);
                }
            }

            // Card Header Bar (Dark slate blue)
            RectangleF headerRect = new RectangleF(44 * scale, 44 * scale, 168 * scale, 28 * scale);
            using (var headerPath = CreateRoundedRectangle(headerRect, 12 * scale)) {
                using (var hb = new SolidBrush(Color.FromArgb(30, 41, 59))) {
                    g.FillPath(hb, headerPath);
                }
                // Header window controls (3 dots)
                using (var dotRed = new SolidBrush(Color.FromArgb(239, 68, 68)))
                using (var dotYellow = new SolidBrush(Color.FromArgb(245, 158, 11)))
                using (var dotGreen = new SolidBrush(Color.FromArgb(34, 197, 94))) {
                    g.FillEllipse(dotRed, 56 * scale, 54 * scale, 8 * scale, 8 * scale);
                    g.FillEllipse(dotYellow, 70 * scale, 54 * scale, 8 * scale, 8 * scale);
                    g.FillEllipse(dotGreen, 84 * scale, 54 * scale, 8 * scale, 8 * scale);
                }
            }

            // Analytics Bar Chart (3 ascending bars)
            // Bar 1 (Cyan)
            using (var bar1 = new SolidBrush(Color.FromArgb(56, 189, 248))) {
                var r = new RectangleF(66 * scale, 122 * scale, 22 * scale, 44 * scale);
                using (var bp = CreateRoundedRectangle(r, 4 * scale)) g.FillPath(bar1, bp);
            }
            // Bar 2 (Indigo)
            using (var bar2 = new SolidBrush(Color.FromArgb(99, 102, 241))) {
                var r = new RectangleF(98 * scale, 98 * scale, 22 * scale, 68 * scale);
                using (var bp = CreateRoundedRectangle(r, 4 * scale)) g.FillPath(bar2, bp);
            }
            // Bar 3 (Emerald / Gold growth)
            using (var bar3 = new SolidBrush(Color.FromArgb(16, 185, 129))) {
                var r = new RectangleF(130 * scale, 80 * scale, 22 * scale, 86 * scale);
                using (var bp = CreateRoundedRectangle(r, 4 * scale)) g.FillPath(bar3, bp);
            }

            // Trend line overlay
            using (var trendPen = new Pen(Color.FromArgb(245, 158, 11), 3.5f * scale)) {
                trendPen.StartCap = LineCap.Round;
                trendPen.EndCap = LineCap.Round;
                PointF p1 = new PointF(77 * scale, 116 * scale);
                PointF p2 = new PointF(109 * scale, 94 * scale);
                PointF p3 = new PointF(141 * scale, 76 * scale);
                PointF p4 = new PointF(175 * scale, 62 * scale);
                g.DrawLines(trendPen, new PointF[] { p1, p2, p3, p4 });

                // Dots on points
                using (var dotB = new SolidBrush(Color.FromArgb(245, 158, 11))) {
                    g.FillEllipse(dotB, p1.X - 4f * scale, p1.Y - 4f * scale, 8 * scale, 8 * scale);
                    g.FillEllipse(dotB, p2.X - 4f * scale, p2.Y - 4f * scale, 8 * scale, 8 * scale);
                    g.FillEllipse(dotB, p3.X - 4f * scale, p3.Y - 4f * scale, 8 * scale, 8 * scale);
                    g.FillEllipse(dotB, p4.X - 4.5f * scale, p4.Y - 4.5f * scale, 9 * scale, 9 * scale);
                }
            }

            // Admin Badge Pill: "MANAGER"
            RectangleF badgeRect = new RectangleF(50 * scale, 194 * scale, 156 * scale, 34 * scale);
            using (var badgePath = CreateRoundedRectangle(badgeRect, 10 * scale)) {
                using (var badgeBg = new SolidBrush(Color.FromArgb(245, 15, 23, 42))) { // Deep Slate
                    g.FillPath(badgeBg, badgePath);
                }
                using (var badgeBorder = new Pen(Color.FromArgb(129, 140, 248), 2f * scale)) {
                    g.DrawPath(badgeBorder, badgePath);
                }

                if (size >= 32) {
                    using (var font = new Font("Segoe UI Black", 14 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var textBrush = new SolidBrush(Color.FromArgb(248, 250, 252))) {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("KASIRKU ADMIN", font, textBrush, badgeRect, sf);
                    }
                }
            }
        }
        return bmp;
    }

    public static void SaveIco(Func<int, Bitmap> renderer, string outputPath) {
        int[] sizes = new int[] { 256, 128, 64, 48, 32, 16 };
        Bitmap[] bitmaps = new Bitmap[sizes.Length];
        for (int i = 0; i < sizes.Length; i++) {
            bitmaps[i] = renderer(sizes[i]);
        }

        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }

        using (var fs = new FileStream(outputPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs)) {
            bw.Write((ushort)0); // reserved
            bw.Write((ushort)1); // type = ICO
            bw.Write((ushort)bitmaps.Length);

            byte[][] pngData = new byte[bitmaps.Length][];
            for (int i = 0; i < bitmaps.Length; i++) {
                using (var ms = new MemoryStream()) {
                    bitmaps[i].Save(ms, ImageFormat.Png);
                    pngData[i] = ms.ToArray();
                }
            }

            int offset = 6 + (16 * bitmaps.Length);
            for (int i = 0; i < bitmaps.Length; i++) {
                int w = bitmaps[i].Width >= 256 ? 0 : bitmaps[i].Width;
                int h = bitmaps[i].Height >= 256 ? 0 : bitmaps[i].Height;
                bw.Write((byte)w);
                bw.Write((byte)h);
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((ushort)1); // planes
                bw.Write((ushort)32); // bpp
                bw.Write((uint)pngData[i].Length);
                bw.Write((uint)offset);
                offset += pngData[i].Length;
            }

            for (int i = 0; i < pngData.Length; i++) {
                bw.Write(pngData[i]);
                bitmaps[i].Dispose();
            }
        }
    }
}
'@

Add-Type -TypeDefinition $generatorScript -ReferencedAssemblies System.Drawing

# 1. Generate POS Icon (.ico & .png)
[IconBuilder]::SaveIco([Func[int, System.Drawing.Bitmap]]{ param($sz) [IconBuilder]::RenderPosIcon($sz) }, "KasirKu.POS\Assets\app.ico")
$pBmp = [IconBuilder]::RenderPosIcon(256)
$pBmp.Save("KasirKu.POS\Assets\app.png", [System.Drawing.Imaging.ImageFormat]::Png)
$pBmp.Dispose()

# 2. Generate Manager Icon (.ico & .png)
[IconBuilder]::SaveIco([Func[int, System.Drawing.Bitmap]]{ param($sz) [IconBuilder]::RenderManagerIcon($sz) }, "KasirKu.Manager\Assets\app.ico")
$mBmp = [IconBuilder]::RenderManagerIcon(256)
$mBmp.Save("KasirKu.Manager\Assets\app.png", [System.Drawing.Imaging.ImageFormat]::Png)
$mBmp.Dispose()

# 3. Copy also to Deploy package Assets folder
$deployAssets = "C:\Users\Ransya Maulana Jati\Downloads\Banyu Deploy\Assets"
if (Test-Path $deployAssets) {
    Copy-Item "KasirKu.POS\Assets\app.ico" "$deployAssets\pos.ico" -Force
    Copy-Item "KasirKu.Manager\Assets\app.ico" "$deployAssets\manager.ico" -Force
    Copy-Item "KasirKu.POS\Assets\app.png" "$deployAssets\pos.png" -Force
    Copy-Item "KasirKu.Manager\Assets\app.png" "$deployAssets\manager.png" -Force
}

Write-Host "Icons generated successfully!"
