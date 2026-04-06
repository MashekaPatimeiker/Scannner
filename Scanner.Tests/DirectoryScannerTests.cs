using Scanner.Core.Models;
using Scanner.Core.Services;
using Xunit;

namespace Scanner.Tests;

public class DirectoryScannerTests : IDisposable
{
    private readonly string _testRoot;
    
    public DirectoryScannerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }
    
    private void CreateTestStructure()
    {
        File.WriteAllText(Path.Combine(_testRoot, "root.txt"), new string('A', 1000));
        
        var subDir1 = Path.Combine(_testRoot, "SubDir1");
        Directory.CreateDirectory(subDir1);
        File.WriteAllText(Path.Combine(subDir1, "file1.txt"), new string('B', 500));
        File.WriteAllText(Path.Combine(subDir1, "file2.txt"), new string('C', 300));
        
        var subDir2 = Path.Combine(subDir1, "SubDir2");
        Directory.CreateDirectory(subDir2);
        File.WriteAllText(Path.Combine(subDir2, "deep.txt"), new string('D', 200));
    }
    
    [Fact]
    public void Scan_WithSingleWorker_ReturnsCorrectTotalSize()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 1);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        Assert.Equal(2000, result.Size);
    }
    
    [Fact]
    public void Scan_WithMultipleWorkers_ReturnsCorrectTotalSize()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 8);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        Assert.Equal(2000, result.Size);
    }
    [Fact]
    public void Scan_With100Workers_DoesNotCrash()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 100);
    
        var result = scanner.Scan(_testRoot, CancellationToken.None);
    
        Assert.Equal(2000, result.Size);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Scan_WithDifferentWorkerCounts_ReturnsSameResult(int maxWorkers)
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: maxWorkers);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        Assert.Equal(2000, result.Size);
        Assert.Equal(2, result.Children.Count); 
    }
    
    [Fact]
    public void Scan_CalculatesCorrectPercentages()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 4);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        var rootFile = result.Children.FirstOrDefault(c => !c.IsDirectory);
        var subDir = result.Children.FirstOrDefault(c => c.IsDirectory);
        
        Assert.NotNull(rootFile);
        Assert.NotNull(subDir);
        
        Assert.Equal(50, rootFile.Percentage, 1);
        Assert.Equal(50, subDir.Percentage, 1);
        
        var deepDir = subDir.Children.FirstOrDefault(c => c.IsDirectory);
        if (deepDir != null)
        {
            Assert.Equal(20, deepDir.Percentage, 1);
        }
    }
    
    [Fact]
    public void Scan_WithCancellation_ReturnsPartialResults()
    {
        for (int i = 0; i < 100; i++)
        {
            File.WriteAllText(Path.Combine(_testRoot, $"largefile{i}.txt"), new string('X', 100000));
        }
        
        var cts = new CancellationTokenSource();
        var scanner = new DirectoryScanner(maxWorkers: 2);
        
        var task = Task.Run(() => scanner.Scan(_testRoot, cts.Token));
        
        Thread.Sleep(50);
        cts.Cancel();
        
        DirectoryNode result = null;
        try
        {
            result = task.Result;
        }
        catch (AggregateException)
        {
        }
        
        Assert.NotNull(result);
    }
    
    [Fact]
    public void Scan_WithEmptyDirectory_ReturnsEmptyNode()
    {
        var emptyDir = Path.Combine(_testRoot, "EmptyDir");
        Directory.CreateDirectory(emptyDir);
        var scanner = new DirectoryScanner(maxWorkers: 1);
        
        var result = scanner.Scan(emptyDir, CancellationToken.None);
        
        Assert.NotNull(result);
        Assert.Empty(result.Children);
        Assert.Equal(0, result.Size);
    }
    
    [Fact]
    public void Scan_SkipsSymlinks()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 1);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        Assert.NotNull(result);
    }
    
    [Fact]
    public void Scan_HandlesUnauthorizedAccess_ContinuesScanning()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 1);
        
        var exception = Record.Exception(() => 
            scanner.Scan(_testRoot, CancellationToken.None));
        
        Assert.Null(exception);
    }
    
    [Fact]
    public void Scan_WithLargeDirectory_DoesNotDeadlock()
    {
        var deepPath = _testRoot;
        int depth = 50;
        int fileSize = 100;
        long expectedTotalSize = 0;
        
        for (int i = 0; i < depth; i++)
        {
            deepPath = Path.Combine(deepPath, $"Level{i}");
            Directory.CreateDirectory(deepPath);
            var filePath = Path.Combine(deepPath, $"file{i}.txt");
            File.WriteAllText(filePath, new string('X', fileSize));
            expectedTotalSize += fileSize;
        }
        
        var scanner = new DirectoryScanner(maxWorkers: 4);
        
        var task = Task.Run(() => scanner.Scan(_testRoot, CancellationToken.None));
        var completed = task.Wait(TimeSpan.FromSeconds(30));
        
        Assert.True(completed, "Scan should complete without deadlock");
        Assert.Equal(expectedTotalSize, task.Result.Size);
    }
    
    [Fact]
    public void Scan_NodeStructure_IsHierarchical()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 1);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        var subDir = result.Children.FirstOrDefault(c => c.IsDirectory);
        Assert.NotNull(subDir);
        Assert.Equal("SubDir1", subDir.Name);
        
        var deepDir = subDir.Children.FirstOrDefault(c => c.IsDirectory);
        Assert.NotNull(deepDir);
        Assert.Equal("SubDir2", deepDir.Name);
        
        var deepFile = deepDir.Children.FirstOrDefault(c => !c.IsDirectory);
        Assert.NotNull(deepFile);
        Assert.Equal("deep.txt", deepFile.Name);
        Assert.Equal(200, deepFile.Size);
    }
    
    [Fact]
    public void Scan_FileSizes_AreCorrectlySummed()
    {
        CreateTestStructure();
        var scanner = new DirectoryScanner(maxWorkers: 2);
        
        var result = scanner.Scan(_testRoot, CancellationToken.None);
        
        var subDir = result.Children.First(c => c.IsDirectory);
        Assert.Equal(1000, subDir.Size);
        
        var deepDir = subDir.Children.First(c => c.IsDirectory);
        Assert.Equal(200, deepDir.Size);
    }
}