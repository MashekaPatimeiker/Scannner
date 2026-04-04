using Scanner.Core.Models;
using Scanner.ViewModels;
using Xunit;

namespace Scanner.Tests;

public class NodeViewModelTests
{
    [Fact]
    public void Constructor_CreatesChildren_ForExistingNodes()
    {
        var child = new DirectoryNode 
        { 
            Name = "Child", 
            IsDirectory = false, 
            Size = 100,
            Children = new List<DirectoryNode>()
        };
        var parent = new DirectoryNode 
        { 
            Name = "Parent", 
            IsDirectory = true, 
            Children = new List<DirectoryNode> { child } 
        };
        
        var vm = new NodeViewModel(parent);
        
        Assert.Single(vm.Children);
        Assert.Equal("Child", vm.Children[0].Name);
    }
    
    [Fact]
    public void Constructor_WithNullChildren_HandlesGracefully()
    {
        var node = new DirectoryNode 
        { 
            Name = "Node", 
            IsDirectory = true, 
            Children = null 
        };
        
        var vm = new NodeViewModel(node);
        Assert.Empty(vm.Children);
    }
    
    [Fact]
    public void Constructor_WithEmptyChildren_CreatesEmptyCollection()
    {
        var node = new DirectoryNode 
        { 
            Name = "Node", 
            IsDirectory = true, 
            Children = new List<DirectoryNode>() 
        };
        
        var vm = new NodeViewModel(node);
        
        Assert.Empty(vm.Children);
    }
    
    [Fact]
    public void AddChild_AddsToChildrenCollection()
    {
        var parent = new DirectoryNode { Name = "Parent", IsDirectory = true, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(parent);
        var childNode = new DirectoryNode { Name = "Child", IsDirectory = false, Children = new List<DirectoryNode>() };
        var child = new NodeViewModel(childNode);
        
        vm.AddChild(child);
        
        Assert.Contains(child, vm.Children);
    }
    
    [Fact]
    public void DisplayText_ForRoot_ShowsOnlySize()
    {
        var node = new DirectoryNode { Name = "Root", IsDirectory = true, Size = 1000, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node, isRoot: true);
        
        var displayText = vm.DisplayText;
        
        
        Assert.Contains("📁 Root", displayText);
        Assert.Contains("1000", displayText);
        Assert.DoesNotContain("%", displayText);
    }
    
    [Fact]
    public void DisplayText_ForNonRootDirectory_ShowsSizeAndPercentage()
    {
        var node = new DirectoryNode 
        { 
            Name = "Folder", 
            IsDirectory = true, 
            Size = 500, 
            Percentage = 50,
            Children = new List<DirectoryNode>()
        };
        var vm = new NodeViewModel(node, isRoot: false);
        
        var displayText = vm.DisplayText;
        
        Assert.Contains("📁 Folder", displayText);
        Assert.Contains("500", displayText);
        Assert.Contains("50", displayText);
        Assert.Contains("%", displayText);
    }
    
    [Fact]
    public void DisplayText_ForFile_ShowsFileIcon()
    {
        var node = new DirectoryNode 
        { 
            Name = "file.txt", 
            IsDirectory = false, 
            Size = 100, 
            Percentage = 10,
            Children = new List<DirectoryNode>()
        };
        var vm = new NodeViewModel(node, isRoot: false);
        
        var displayText = vm.DisplayText;
        
        Assert.Contains("📄 file.txt", displayText);
        Assert.Contains("100", displayText);
        Assert.Contains("10", displayText);
        Assert.Contains("%", displayText);
    }
    
    [Fact]
    public void FormatSize_ZeroBytes_ReturnsZero()
    {
        var node = new DirectoryNode { Name = "Empty", IsDirectory = false, Size = 0, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node);
        
        var displayText = vm.DisplayText;
        
        Assert.Contains("0 Б", displayText);
    }
    
    [Fact]
    public void FormatSize_ConvertsToKilobytes()
    {
        var node = new DirectoryNode { Name = "File", IsDirectory = false, Size = 2048, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node);
        
        var displayText = vm.DisplayText;
        
        Assert.Contains("2 КБ", displayText);
    }
    
    [Fact]
    public void FormatSize_ConvertsToMegabytes()
    {
        var node = new DirectoryNode { Name = "File", IsDirectory = false, Size = 2_097_152, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node);
        
        var displayText = vm.DisplayText;
        
        Assert.Contains("2 МБ", displayText);
    }
    
    [Fact]
    public void UpdateSize_UpdatesDisplayText()
    {
        var node = new DirectoryNode { Name = "Folder", IsDirectory = true, Size = 100, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node, isRoot: true);
        var oldDisplayText = vm.DisplayText;
        
        vm.UpdateSize(200);
        
        Assert.NotEqual(oldDisplayText, vm.DisplayText);
        Assert.Contains("200", vm.DisplayText);
    }
    
    [Fact]
    public void UpdatePercentage_ForNonRoot_UpdatesDisplayText()
    {
        var node = new DirectoryNode { Name = "Folder", IsDirectory = true, Size = 100, Percentage = 10, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node, isRoot: false);
        var oldDisplayText = vm.DisplayText;
        
        vm.UpdatePercentage(20);
        
        Assert.NotEqual(oldDisplayText, vm.DisplayText);
        Assert.Contains("20", vm.DisplayText);
    }
    
    [Fact]
    public void UpdatePercentage_ForRoot_DoesNotShowPercentage()
    {
        var node = new DirectoryNode { Name = "Root", IsDirectory = true, Size = 100, Percentage = 10, Children = new List<DirectoryNode>() };
        var vm = new NodeViewModel(node, isRoot: true);
        
        vm.UpdatePercentage(20);
        
        Assert.DoesNotContain("%", vm.DisplayText);
        Assert.DoesNotContain("20", vm.DisplayText);
    }
    
    [Fact]
    public void RecursiveChildren_CreatedCorrectly()
    {
        var grandChild = new DirectoryNode { Name = "GrandChild", IsDirectory = false, Size = 25, Children = new List<DirectoryNode>() };
        var child = new DirectoryNode { Name = "Child", IsDirectory = true, Size = 50, Children = new List<DirectoryNode> { grandChild } };
        var parent = new DirectoryNode { Name = "Parent", IsDirectory = true, Children = new List<DirectoryNode> { child } };
        
        var vm = new NodeViewModel(parent);
        
        Assert.Single(vm.Children);
        Assert.Single(vm.Children[0].Children);
        Assert.Equal("GrandChild", vm.Children[0].Children[0].Name);
    }
}