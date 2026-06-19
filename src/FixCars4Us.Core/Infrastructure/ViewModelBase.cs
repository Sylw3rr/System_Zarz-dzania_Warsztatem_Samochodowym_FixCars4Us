// Wzorzec: Observer (INotifyPropertyChanged) — podstawa MVVM.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FixCars4Us.Core.Infrastructure;

/// <summary>Bazowa klasa ViewModel z implementacją INotifyPropertyChanged.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Ustawia pole i powiadamia UI tylko jeśli wartość się zmieniła.</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
