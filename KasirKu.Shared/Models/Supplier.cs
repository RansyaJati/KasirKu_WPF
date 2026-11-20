using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KasirKu.Shared.Models;

public class Supplier : INotifyPropertyChanged
{
    private int _id;
    private string _nama = "";
    private string _noHp = "";
    private string _alamat = "";
    private string _email = "";

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

    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
