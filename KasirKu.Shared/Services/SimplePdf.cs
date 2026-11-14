using System.IO;
using System.Text;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Gambar untuk disisipkan ke PDF (data RGB 24-bit, tanpa kompresi).
    /// </summary>
    public sealed class PdfImage
    {
        public byte[] Rgb { get; set; } = Array.Empty<byte>();
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        /// <summary>Lebar gambar di halaman PDF (dalam point).</summary>
        public float DrawWidth { get; set; } = 120f;

        public float DrawHeight =>
            PixelWidth <= 0 ? DrawWidth : DrawWidth * PixelHeight / PixelWidth;
    }

    /// <summary>
    /// Satu baris teks untuk PDF. Bold memakai Helvetica-Bold.
    /// </summary>
    public sealed class PdfLine
    {
        public string Text { get; set; } = "";
        public float Size { get; set; } = 10;
        public bool Bold { get; set; }
        public bool Center { get; set; }

        /// <summary>Jika diisi, baris ini mencetak gambar (bukan teks).</summary>
        public PdfImage? Image { get; set; }
    }

    /// <summary>
    /// Penulis PDF minimal TANPA library eksternal (tanpa NuGet).
    /// Memakai font standar PDF (Helvetica / Helvetica-Bold) sehingga
    /// tidak perlu embed font. Mendukung gambar RGB sederhana (logo struk).
    /// </summary>
    public static class SimplePdf
    {
        public static void WriteLines(string filePath, List<PdfLine> lines, float pageWidth = 595, float pageHeight = 842, float margin = 40)
        {
            var pages = Paginate(lines, pageHeight, margin);

            // Nomor objek: 1=Catalog, 2=Pages, lalu per halaman: Page + Content,
            // lalu 2 font, lalu objek gambar.
            int pageCount = pages.Count;
            var pageObjNums = new List<int>();
            var contentObjNums = new List<int>();
            int next = 3;
            for (int i = 0; i < pageCount; i++)
            {
                pageObjNums.Add(next++);
                contentObjNums.Add(next++);
            }
            int fontReg = next++;
            int fontBold = next++;

            // Kumpulkan gambar unik
            var imageIds = new Dictionary<PdfImage, int>(new RefComparer());
            var imageList = new List<PdfImage>();
            foreach (var page in pages)
                foreach (var l in page)
                    if (l.Image != null && !imageIds.ContainsKey(l.Image))
                    {
                        imageIds[l.Image] = imageList.Count;
                        imageList.Add(l.Image);
                    }

            var imageObjNums = new int[imageList.Count];
            for (int i = 0; i < imageList.Count; i++) imageObjNums[i] = next++;
            int totalObjects = next - 1;

            string kids = string.Join(" ", pageObjNums.Select(n => $"{n} 0 R"));
            var objMap = new Dictionary<int, byte[]>
            {
                [1] = Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
                [2] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>")
            };

            for (int i = 0; i < pageCount; i++)
            {
                // Nama XObject yang dipakai halaman ini
                var used = new List<(string Name, int Obj)>();
                foreach (var l in pages[i])
                {
                    if (l.Image == null) continue;
                    int idx = imageIds[l.Image];
                    string name = "Im" + idx;
                    if (!used.Any(u => u.Name == name))
                        used.Add((name, imageObjNums[idx]));
                }
                string xobj = used.Count == 0
                    ? ""
                    : " /XObject << " + string.Join(" ", used.Select(u => $"/{u.Name} {u.Obj} 0 R")) + " >>";

                string pageObj = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] " +
                                 $"/Resources << /Font << /F1 {fontReg} 0 R /F2 {fontBold} 0 R >>{xobj} >> /Contents {contentObjNums[i]} 0 R >>";
                objMap[pageObjNums[i]] = Encoding.ASCII.GetBytes(pageObj);

                string stream = BuildContentStream(pages[i], pageWidth, pageHeight, margin, imageIds);
                byte[] streamBytes = Encoding.ASCII.GetBytes(stream);
                byte[] header = Encoding.ASCII.GetBytes($"<< /Length {streamBytes.Length} >>\nstream\n");
                byte[] footer = Encoding.ASCII.GetBytes("\nendstream");
                byte[] full = new byte[header.Length + streamBytes.Length + footer.Length];
                Buffer.BlockCopy(header, 0, full, 0, header.Length);
                Buffer.BlockCopy(streamBytes, 0, full, header.Length, streamBytes.Length);
                Buffer.BlockCopy(footer, 0, full, header.Length + streamBytes.Length, footer.Length);
                objMap[contentObjNums[i]] = full;
            }

            objMap[fontReg] = Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objMap[fontBold] = Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

            for (int i = 0; i < imageList.Count; i++)
            {
                var img = imageList[i];
                byte[] head = Encoding.ASCII.GetBytes(
                    $"<< /Type /XObject /Subtype /Image /Width {img.PixelWidth} /Height {img.PixelHeight} " +
                    $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {img.Rgb.Length} >>\nstream\n");
                byte[] tail = Encoding.ASCII.GetBytes("\nendstream");
                byte[] full = new byte[head.Length + img.Rgb.Length + tail.Length];
                Buffer.BlockCopy(head, 0, full, 0, head.Length);
                Buffer.BlockCopy(img.Rgb, 0, full, head.Length, img.Rgb.Length);
                Buffer.BlockCopy(tail, 0, full, head.Length + img.Rgb.Length, tail.Length);
                objMap[imageObjNums[i]] = full;
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            var offsets = new long[totalObjects + 1];
            WriteAscii(fs, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

            for (int n = 1; n <= totalObjects; n++)
            {
                offsets[n] = fs.Position;
                WriteAscii(fs, $"{n} 0 obj\n");
                byte[] body = objMap[n];
                fs.Write(body, 0, body.Length);
                WriteAscii(fs, "\nendobj\n");
            }

            long xrefPos = fs.Position;
            WriteAscii(fs, $"xref\n0 {totalObjects + 1}\n");
            WriteAscii(fs, "0000000000 65535 f \n");
            for (int n = 1; n <= totalObjects; n++)
                WriteAscii(fs, $"{offsets[n]:D10} 00000 n \n");
            WriteAscii(fs, $"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");
        }

        private sealed class RefComparer : IEqualityComparer<PdfImage>
        {
            public bool Equals(PdfImage? x, PdfImage? y) => ReferenceEquals(x, y);
            public int GetHashCode(PdfImage obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private static List<List<PdfLine>> Paginate(List<PdfLine> lines, float pageHeight, float margin)
        {
            var pages = new List<List<PdfLine>>();
            var cur = new List<PdfLine>();

            float usable = pageHeight - margin * 2;
            float y = 0;

            foreach (var l in lines)
            {
                float h = LineHeight(l);
                if (y + h > usable && cur.Count > 0)
                {
                    pages.Add(cur);
                    cur = new List<PdfLine>();
                    y = 0;
                }
                cur.Add(l);
                y += h;
            }
            if (cur.Count > 0) pages.Add(cur);
            if (pages.Count == 0) pages.Add(new List<PdfLine> { new PdfLine { Text = "-", Size = 10 } });
            return pages;
        }

        private static float Leading(float size) => Math.Max(13, size * 1.4f);

        private static float LineHeight(PdfLine l)
            => l.Image != null ? l.Image.DrawHeight + 8f : Leading(l.Size);

        private static string BuildContentStream(List<PdfLine> lines, float pageWidth, float pageHeight, float margin,
            Dictionary<PdfImage, int> imageIds)
        {
            var sb = new StringBuilder();
            float y = pageHeight - margin;

            foreach (var l in lines)
            {
                if (l.Image != null)
                {
                    float dh = l.Image.DrawHeight;
                    float dw = l.Image.DrawWidth;
                    float x = (pageWidth - dw) / 2f;
                    if (x < margin) x = margin;
                    y -= dh + 4f;
                    int idx = imageIds[l.Image];
                    sb.Append($"q {dw:0.##} 0 0 {dh:0.##} {x:0.##} {y:0.##} cm /Im{idx} Do Q\n");
                    y -= 4f;
                    continue;
                }

                y -= Leading(l.Size);
                string font = l.Bold ? "/F2" : "/F1";
                string text = Sanitize(l.Text);

                float xLine = margin;
                if (l.Center)
                {
                    float est = text.Length * l.Size * 0.55f;
                    xLine = Math.Max(margin, (pageWidth - est) / 2);
                }
                sb.Append($"BT {font} {l.Size:0.#} Tf {xLine:0.#} {y:0.#} Td ({Escape(text)}) Tj ET\n");
            }
            return sb.ToString();
        }

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        /// <summary>Font standar PDF hanya WinAnsi. Buang emoji &amp; petakan simbol umum.</summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '•': sb.Append('-'); break;
                    case '–':
                    case '—': sb.Append('-'); break;
                    case '‘':
                    case '’': sb.Append('\''); break;
                    case '“':
                    case '”': sb.Append('"'); break;
                    case '…': sb.Append("..."); break;
                    default:
                        if (c >= 32 && c <= 126) sb.Append(c);
                        else if (c >= 160 && c <= 255) sb.Append(c);
                        else if (c == '\t') sb.Append("    ");
                        break;
                }
            }
            string out_ = sb.ToString();
            if (out_.Length > 200) out_ = out_[..200];
            return out_;
        }

        private static void WriteAscii(FileStream fs, string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            fs.Write(b, 0, b.Length);
        }
    }
}
