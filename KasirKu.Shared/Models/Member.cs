using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KasirKu.Shared.Models;

public class Member : INotifyPropertyChanged
{
    private int _id;
    private string _nama = "";
    private string _noHp = "";
    private string _alamat = "";
    private int _poin = 0;
    private DateTime _tanggalDaftar = DateTime.Now;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Nama
    {
        get => _nama;
        set { _nama = value; OnPropertyChanged(); }
    }

    public string NoHp
    {
        get => _noHp;
        set { _noHp = value; OnPropertyChanged(); }
    }

    public string Alamat
    {
        get => _alamat;
        set { _alamat = value; OnPropertyChanged(); }
    }

    public int Poin
    {
        get => _poin;
        set { _poin = value; OnPropertyChanged(); OnPropertyChanged(nameof(PoinStr)); }
    }

    public DateTime TanggalDaftar
    {
        get => _tanggalDaftar;
        set { _tanggalDaftar = value; OnPropertyChanged(); OnPropertyChanged(nameof(TanggalDaftarStr)); }
    }

    public string PoinStr => $"{Poin:N0} Poin";
    public string TanggalDaftarStr => TanggalDaftar.ToString("dd/MM/yyyy");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
