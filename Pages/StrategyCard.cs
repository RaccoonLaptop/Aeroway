using System.ComponentModel;

namespace ZapretUI.Pages;

public sealed class StrategyCard : INotifyPropertyChanged
{
    public required string FileName { get; init; }
    public required string Title { get; init; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
