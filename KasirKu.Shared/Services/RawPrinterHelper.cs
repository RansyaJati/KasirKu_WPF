using System.Runtime.InteropServices;
using System.Text;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Helper untuk mengirim data mentah (raw bytes / ESC-POS) langsung ke printer Windows
    /// tanpa library eksternal (P/Invoke winspool.drv).
    /// </summary>
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName = "KasirKu Receipt";
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pOutputFile = null;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDataType = "RAW";
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumPrinters(
            int flags,
            string? name,
            int level,
            IntPtr pPrinterEnum,
            int cbBuf,
            out int pcbNeeded,
            out int pcReturned);

        private const int PRINTER_ENUM_LOCAL = 0x00000002;
        private const int PRINTER_ENUM_CONNECTIONS = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PRINTER_INFO_4
        {
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pPrinterName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pServerName;
            public int Attributes;
        }

        /// <summary>
        /// Mengambil daftar printer lokal dan network yang terpasang di Windows.
        /// </summary>
        public static List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            int cbNeeded = 0;
            int cReturned = 0;
            int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;

            EnumPrinters(flags, null, 4, IntPtr.Zero, 0, out cbNeeded, out cReturned);
            if (cbNeeded <= 0) return printers;

            IntPtr pBuf = Marshal.AllocHGlobal(cbNeeded);
            try
            {
                if (EnumPrinters(flags, null, 4, pBuf, cbNeeded, out cbNeeded, out cReturned))
                {
                    int structSize = Marshal.SizeOf(typeof(PRINTER_INFO_4));
                    for (int i = 0; i < cReturned; i++)
                    {
                        IntPtr pCur = new IntPtr(pBuf.ToInt64() + (i * structSize));
                        var info = Marshal.PtrToStructure<PRINTER_INFO_4>(pCur);
                        if (!string.IsNullOrWhiteSpace(info.pPrinterName))
                        {
                            printers.Add(info.pPrinterName);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pBuf);
            }

            return printers;
        }

        /// <summary>
        /// Mengirimkan byte mentah (RAW) langsung ke printer yang ditentukan.
        /// </summary>
        public static bool SendBytesToPrinter(string szPrinterName, byte[] bytes, string docName = "Struk KasirKu")
        {
            if (string.IsNullOrWhiteSpace(szPrinterName) || bytes == null || bytes.Length == 0)
                return false;

            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

            bool success = false;
            IntPtr hPrinter = IntPtr.Zero;
            var di = new DOCINFOA { pDocName = docName, pDataType = "RAW" };

            try
            {
                if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
                {
                    if (StartDocPrinter(hPrinter, 1, di))
                    {
                        if (StartPagePrinter(hPrinter))
                        {
                            success = WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out _);
                            EndPagePrinter(hPrinter);
                        }
                        EndDocPrinter(hPrinter);
                    }
                    ClosePrinter(hPrinter);
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }

            return success;
        }
    }
}
