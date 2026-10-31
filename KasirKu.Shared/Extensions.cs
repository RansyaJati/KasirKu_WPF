namespace KasirKu.Shared
{
    /// <summary>Ekstensi format nomor 3 digit untuk ID TRX & counter.</summary>
    public static class IntExtensions
    {
        public static string D3(this int v) => v.ToString("D3");
    }
}
