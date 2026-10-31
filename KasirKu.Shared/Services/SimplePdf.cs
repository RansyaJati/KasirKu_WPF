using System.IO;
using System.Text;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Satu baris teks untuk PDF. Bold memakai Helvetica-Bold.
    /// </summary>
    public sealed class PdfLine
    {
        public string Text { get; set; } = "";
        public float Size { get; set; } = 10;
        public bool Bold { get; set; }
        public bool Center { get; set; }
    }

    /// <summary>
    /// Penulis PDF minimal TANPA library eksternal (tanpa NuGet).
    /// Memakai font standar PDF (Helvetica / Helvetica-Bold) sehingga
    /// tidak perlu embed font. Cukup untuk struk &amp; laporan teks.
    /// </summary>
    public static class SimplePdf
    {
        public static void WriteLines(string filePath, List<PdfLine> lines, float pageWidth = 595, float pageHeight = 842, float margin = 40)
        {
            // Pecah baris menjadi halaman-halaman
            var pages = Paginate(lines, pageHeight, margin);

            // Urutan: 1=Catalog, 2=Pages, lalu per halaman: Page + Content, lalu 2 font di akhir.
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
            int totalObjects = next - 1;

            string kids = string.Join(" ", pageObjNums.Select(n => $"{n} 0 R"));
            string obj1 = "<< /Type /Catalog /Pages 2 0 R >>";
            string obj2 = $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>";

            var objMap = new Dictionary<int, byte[]>();
            objMap[1] = Encoding.ASCII.GetBytes(obj1);
            objMap[2] = Encoding.ASCII.GetBytes(obj2);

            for (int i = 0; i < pageCount; i++)
            {
                string pageObj = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] " +
                                 $"/Resources << /Font << /F1 {fontReg} 0 R /F2 {fontBold} 0 R >> >> /Contents {contentObjNums[i]} 0 R >>";
                objMap[pageObjNums[i]] = Encoding.ASCII.GetBytes(pageObj);

                string stream = BuildContentStream(pages[i], pageWidth, pageHeight, margin);
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

        private static List<List<PdfLine>> Paginate(List<PdfLine> lines, float pageHeight, float margin)
        {
            var pages = new List<List<PdfLine>>();
            var cur = new List<PdfLine>();
            float y = margin; // tinggi terpakai dari atas (diukur turun)

            float usable = pageHeight - margin * 2;
            foreach (var l in lines)
            {
                float h = Leading(l.Size);
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

        private static string BuildContentStream(List<PdfLine> lines, float pageWidth, float pageHeight, float margin)
        {
            var sb = new StringBuilder();
            float y = pageHeight - margin;
            foreach (var l in lines)
            {
                y -= Leading(l.Size);
                string font = l.Bold ? "/F2" : "/F1";
                string text = Sanitize(l.Text);
                // Perkiraan lebar untuk center: rata-rata 0.55 * size per karakter
                float x = margin;
                if (l.Center)
                {
                    float est = text.Length * l.Size * 0.55f;
                    x = Math.Max(margin, (pageWidth - est) / 2);
                }
                sb.Append($"BT {font} {l.Size:0.#} Tf {x:0.#} {y:0.#} Td ({Escape(text)}) Tj ET\n");
            }
            return sb.ToString();
        }

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        /// <summary>
        /// Font standar PDF hanya WinAnsi. Buang emoji &amp; petakan simbol umum.
        /// </summary>
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
                        else if (c >= 160 && c <= 255) sb.Append(c); // latin-1 lolos
                        else if (c == '\t') sb.Append("    ");
                        // emoji & simbol lain dibuang
                        break;
                }
            }
            string out_ = sb.ToString();
            // Hindari baris terlalu panjang untuk operator Tj
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
