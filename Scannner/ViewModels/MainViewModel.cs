using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Scanner.Core.Models;
using Scanner.Core.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Scanner.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DirectoryScanner _scanner;
    private CancellationTokenSource? _cts;
    private string _selectedPath = string.Empty;
    private string _statusText = "Готов к работе";
    private bool _isScanning;
    private readonly Dictionary<string, NodeViewModel> _nodeMap = new();

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ICommand BrowseCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel()
    {
        _scanner = new DirectoryScanner(maxWorkers: Environment.ProcessorCount);
        _scanner.NodeDiscovered += OnNodeDiscovered;
        
        BrowseCommand = new RelayCommand(Browse);
        ScanCommand = new RelayCommand(StartScan);
        CancelCommand = new RelayCommand(CancelScan);
    }

    private void Browse(object? parameter)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _selectedPath = dialog.SelectedPath;
            StatusText = $"Выбрана папка: {_selectedPath}";
        }
    }

    private void OnNodeDiscovered(DirectoryNode node)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (node.FullPath == _selectedPath)
            {
                if (Nodes.Count == 0)
                {
                    var rootVm = new NodeViewModel(node, isRoot: true);
                    Nodes.Add(rootVm);
                    _nodeMap[node.FullPath] = rootVm;
                }
                return;
            }
            
            var parentPath = Path.GetDirectoryName(node.FullPath);
            
            if (!string.IsNullOrEmpty(parentPath) && _nodeMap.TryGetValue(parentPath, out var parentVm))
            {
                var vm = new NodeViewModel(node, isRoot: false);
                parentVm.AddChild(vm);
                _nodeMap[node.FullPath] = vm;
                StatusText = $"Сканируется... Найдено: {_nodeMap.Count} элементов";
            }
        });
    }

    private void StartScan(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(_selectedPath))
        {
            MessageBox.Show("Выберите папку для сканирования.", "Предупреждение", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(_selectedPath))
        {
            MessageBox.Show("Папка не существует.", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CancelScan(parameter);
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Nodes.Clear();
            _nodeMap.Clear();
        });
        
        _cts = new CancellationTokenSource();
        _isScanning = true;
        StatusText = "Сканирование запущено...";

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var result = _scanner.Scan(_selectedPath, _cts.Token);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (result != null && _nodeMap.TryGetValue(_selectedPath, out var rootVm))
                    {
                        rootVm.UpdateSize(result.Size);
                        StatusText = $"Сканирование завершено. Всего: {FormatSize(result.Size)}, элементов: {_nodeMap.Count}";
                        
                        UpdatePercentagesRecursive(rootVm, result.Size);
                    }
                    _isScanning = false;
                });
            }
            catch (OperationCanceledException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = $"Сканирование отменено. Найдено: {_nodeMap.Count} элементов";
                    _isScanning = false;
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = "Ошибка при сканировании";
                    _isScanning = false;
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });
    }
    
    private void UpdatePercentagesRecursive(NodeViewModel node, long parentSize)
    {
        foreach (var child in node.Children)
        {
            if (parentSize > 0)
            {
                var percentage = (child.Size * 100.0) / parentSize;
                child.UpdatePercentage(percentage);
            }
            UpdatePercentagesRecursive(child, child.Size);
        }
    }

    private void CancelScan(object? parameter)
    {
        if (_cts != null && !_cts.IsCancellationRequested && _isScanning)
        {
            _cts.Cancel();
            StatusText = "Отмена сканирования...";
        }
    }

    private static string FormatSize(long bytes)
    {
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}