using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Scanner.Core.Models;

namespace Scanner.ViewModels;

public class NodeViewModel : INotifyPropertyChanged
{
    private readonly DirectoryNode _node;
    private readonly ObservableCollection<NodeViewModel> _children = new();
    private bool _isRoot;

    public NodeViewModel(DirectoryNode node, bool isRoot = false)
    {
        _node = node;
        _isRoot = isRoot;
        
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                _children.Add(new NodeViewModel(child));
            }
        }
    }

    public string Name => _node.Name;
    public long Size => _node.Size;
    public double Percentage => _node.Percentage;
    
    public string DisplayText
    {
        get
        {
            if (_isRoot)
            {
                return $"📁 {Name} ({FormatSize(Size)})";
            }
            else
            {
                string icon = _node.IsDirectory ? "📁" : "📄";
                return $"{icon} {Name} ({FormatSize(Size)}, {Percentage:F1}%)";
            }
        }
    }
    
    public ObservableCollection<NodeViewModel> Children => _children;
    
    public void AddChild(NodeViewModel child)
    {
        _children.Add(child);
        OnPropertyChanged(nameof(Children));
    }
    
    public void UpdateSize(long newSize)
    {
        _node.Size = newSize;
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(Size));
    }
    
    public void UpdatePercentage(double newPercentage)
    {
        _node.Percentage = newPercentage;
        if (!_isRoot)
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(Percentage));
        }
    }
    
    private static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 Б";
        string[] sizes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}