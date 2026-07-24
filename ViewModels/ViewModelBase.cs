using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XNote.ViewModels;

/// <summary>
/// Minimal INotifyPropertyChanged base, written by hand rather than pulling
/// in a full MVVM framework (CommunityToolkit.Mvvm etc.) — this app is small
/// enough that a hand-rolled base class keeps the dependency list short and
/// makes the binding plumbing explicit.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
