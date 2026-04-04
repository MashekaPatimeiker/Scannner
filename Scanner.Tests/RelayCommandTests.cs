using Scanner.ViewModels;
using Xunit;

namespace Scanner.Tests;

public class RelayCommandTests
{
    [Fact]
    public void Execute_CallsAction()
    {
        var executed = false;
        var command = new RelayCommand(_ => executed = true);
        
        command.Execute(null);
        
        Assert.True(executed);
    }
    
    [Fact]
    public void Execute_PassesParameter()
    {
        object? receivedParam = null;
        var command = new RelayCommand(param => receivedParam = param);
        
        command.Execute("test");
        
        Assert.Equal("test", receivedParam);
    }
    
    [Fact]
    public void CanExecute_ReturnsTrue_WhenNoPredicate()
    {
        var command = new RelayCommand(_ => { });
        
        Assert.True(command.CanExecute(null));
    }
    
    [Fact]
    public void CanExecute_ReturnsPredicateResult()
    {
        var command = new RelayCommand(_ => { }, _ => false);
        
        Assert.False(command.CanExecute(null));
    }
    
    [Fact]
    public void CanExecute_WithParameter_ReturnsPredicateResult()
    {
        var command = new RelayCommand(_ => { }, param => param?.ToString() == "allow");
        
        Assert.True(command.CanExecute("allow"));
        Assert.False(command.CanExecute("deny"));
        Assert.False(command.CanExecute(null));
    }
    
    [Fact]
    public void Execute_Throws_WhenActionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
    }
    
    [Fact]
    public void CanExecuteChanged_IsRaised()
    {
        var command = new RelayCommand(_ => { }, _ => true);
        var canExecuteChangedRaised = false;
        command.CanExecuteChanged += (s, e) => canExecuteChangedRaised = true;
        
        command.RaiseCanExecuteChanged();
        
        Assert.True(canExecuteChangedRaised);
    }
}