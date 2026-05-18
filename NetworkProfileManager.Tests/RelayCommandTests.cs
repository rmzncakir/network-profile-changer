using System;
using NetworkProfileManager.Commands;

namespace NetworkProfileManager.Tests;

public class RelayCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        bool called = false;
        var cmd = new RelayCommand(_ => called = true);
        cmd.Execute(null);
        Assert.True(called);
    }

    [Fact]
    public void Execute_PassesParameterThrough()
    {
        object? captured = null;
        var cmd = new RelayCommand(p => captured = p);
        cmd.Execute("hello");
        Assert.Equal("hello", captured);
    }

    [Fact]
    public void CanExecute_ReturnsTrue_WhenPredicateNull()
    {
        var cmd = new RelayCommand(_ => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_DelegatesToPredicate()
    {
        var cmd = new RelayCommand(_ => { }, p => p is int i && i > 0);
        Assert.True(cmd.CanExecute(5));
        Assert.False(cmd.CanExecute(-1));
        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void Constructor_ThrowsWhenExecuteNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
    }
}
