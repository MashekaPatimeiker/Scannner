namespace Scanner.Core.Models;

public class DirectoryNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public double Percentage { get; set; }
    public bool IsDirectory { get; set; }
    public List<DirectoryNode> Children { get; set; } = new();
}