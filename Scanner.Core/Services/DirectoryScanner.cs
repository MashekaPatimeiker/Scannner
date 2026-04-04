using System.Collections.Concurrent;
using System.IO;
using Scanner.Core.Models;

namespace Scanner.Core.Services;

public class DirectoryScanner
{
    private readonly int _maxWorkers;
    private readonly SemaphoreSlim _semaphore;
    
    public event Action<DirectoryNode>? NodeDiscovered;

    public DirectoryScanner(int maxWorkers = 8)
    {
        _maxWorkers = maxWorkers;
        _semaphore = new SemaphoreSlim(maxWorkers, maxWorkers);
    }

    public DirectoryNode Scan(string rootPath, CancellationToken token)
    {
        var root = new DirectoryNode
        {
            Name = Path.GetFileName(rootPath),
            FullPath = rootPath,
            IsDirectory = true,
            Children = new List<DirectoryNode>()
        };

        NodeDiscovered?.Invoke(root);

        var nodeMap = new ConcurrentDictionary<string, DirectoryNode>();
        nodeMap[rootPath] = root;

        var queue = new ConcurrentQueue<string>();
        queue.Enqueue(rootPath);

        using var countdown = new CountdownEvent(1);
        
        while (!token.IsCancellationRequested)
        {
            if (queue.IsEmpty && countdown.CurrentCount == 1)
                break;

            if (!queue.TryDequeue(out var currentPath))
            {
                Thread.Sleep(10);
                continue;
            }

            countdown.AddCount();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _semaphore.Wait(token);
                    try
                    {
                        ProcessDirectory(currentPath, nodeMap, queue, token);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        countdown.Signal();
        countdown.Wait(30000);

        CalculateSizes(root);
        
        CalculatePercentages(root);

        NodeDiscovered?.Invoke(root);

        return root;
    }

  private void ProcessDirectory(
    string path, 
    ConcurrentDictionary<string, DirectoryNode> nodeMap,
    ConcurrentQueue<string> queue, 
    CancellationToken token)
{
    if (token.IsCancellationRequested)
        return;

    if (!nodeMap.TryGetValue(path, out var currentNode))
        return;

    try
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) 
            return;

        FileSystemInfo[] entries;
        try
        {
            entries = dir.GetFileSystemInfos();
        }
        catch (UnauthorizedAccessException)
        {
            var accessDeniedNode = new DirectoryNode
            {
                Name = $"{Path.GetFileName(path)} (Нет доступа)",
                FullPath = path,
                IsDirectory = true,
                Size = 0,
                Percentage = 0,
                Children = new List<DirectoryNode>()
            };
            
            lock (currentNode.Children)
            {
                var index = currentNode.Children.FindIndex(c => c.FullPath == path);
                if (index >= 0)
                {
                    currentNode.Children[index] = accessDeniedNode;
                }
                else
                {
                    currentNode.Children.Add(accessDeniedNode);
                }
            }
            
            nodeMap[path] = accessDeniedNode;
            NodeDiscovered?.Invoke(accessDeniedNode);
            return;
        }

        foreach (var entry in entries)
        {
            if (token.IsCancellationRequested)
                return;

            if (entry.LinkTarget != null)
                continue;

            if (entry is FileInfo file)
            {
                var fileNode = new DirectoryNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Size = file.Length,
                    Children = new List<DirectoryNode>()
                };
                
                lock (currentNode.Children)
                {
                    currentNode.Children.Add(fileNode);
                }
                
                NodeDiscovered?.Invoke(fileNode);
            }
            else if (entry is DirectoryInfo subDir)
            {
                var dirNode = new DirectoryNode
                {
                    Name = subDir.Name,
                    FullPath = subDir.FullName,
                    IsDirectory = true,
                    Children = new List<DirectoryNode>()
                };
                
                lock (currentNode.Children)
                {
                    currentNode.Children.Add(dirNode);
                }
                
                nodeMap[subDir.FullName] = dirNode;
                queue.Enqueue(subDir.FullName);
                NodeDiscovered?.Invoke(dirNode);
            }
        }
    }
    catch (UnauthorizedAccessException)
    {
        
    }
    catch (IOException ex)
    {
        System.Diagnostics.Debug.WriteLine($"IO Error: {ex.Message}");
    }
}

    private long CalculateSizes(DirectoryNode node)
    {
        if (!node.IsDirectory)
            return node.Size;

        long totalSize = 0;
        
        lock (node.Children)
        {
            foreach (var child in node.Children)
            {
                totalSize += CalculateSizes(child);
            }
        }
        
        node.Size = totalSize;
        return totalSize;
    }

    private void CalculatePercentages(DirectoryNode node)
    {
        lock (node.Children)
        {
            foreach (var child in node.Children)
            {
                child.Percentage = node.Size == 0 ? 0 : (child.Size * 100.0) / node.Size;
                if (child.IsDirectory)
                {
                    CalculatePercentages(child);
                }
            }
        }
    }
}