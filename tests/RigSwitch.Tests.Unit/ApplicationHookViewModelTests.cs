namespace RigSwitch.Tests.Unit;

using RigSwitch.App.ViewModels;
using RigSwitch.Core.Models;
using Xunit;

public sealed class ApplicationHookViewModelTests
{
    [Fact]
    public void ApplicationHookItemViewModel_Properties_BindCorrectly()
    {
        var hook = new PresetApplicationHook
        {
            Id = "hook-123",
            ExecutablePath = @"C:\Tools\PitHouse.exe",
            Arguments = "-silent",
            CloseOnSwitchAway = true
        };

        var vm = new ApplicationHookItemViewModel(hook);

        Assert.Equal("hook-123", vm.Id);
        Assert.Equal(@"C:\Tools\PitHouse.exe", vm.ExecutablePath);
        Assert.Equal("-silent", vm.Arguments);
        Assert.True(vm.CloseOnSwitchAway);
        Assert.Equal("PitHouse.exe", vm.DisplayName);
    }

    [Fact]
    public void ApplicationHookItemViewModel_RemoveCommand_InvokesCallback()
    {
        var hook = new PresetApplicationHook { Id = "hook-del", ExecutablePath = @"C:\Tools\SimHub.exe" };
        ApplicationHookItemViewModel? removedItem = null;
        var vm = new ApplicationHookItemViewModel(hook, item => removedItem = item);

        vm.RemoveCommand.Execute(null);

        Assert.Same(vm, removedItem);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_InitializesApplicationHooksFromPreset()
    {
        var preset = new WorkstationPreset
        {
            ApplicationHooks =
            [
                new PresetApplicationHook { Id = "h1", ExecutablePath = @"C:\Apps\App1.exe" },
                new PresetApplicationHook { Id = "h2", ExecutablePath = @"C:\Apps\App2.exe" }
            ]
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "GroupA", true);

        Assert.Equal(2, vm.ApplicationHooks.Count);
        Assert.Equal("App1.exe", vm.ApplicationHooks[0].DisplayName);
        Assert.Equal("App2.exe", vm.ApplicationHooks[1].DisplayName);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_AddApplicationHookCommand_AddsItem()
    {
        var preset = new WorkstationPreset();
        var vm = new PresetConfigurationItemViewModel(
            preset,
            0,
            "GroupA",
            true,
            selectFileAction: () => @"C:\Custom\Tool.exe");

        vm.AddApplicationHookCommand.Execute(null);

        Assert.Single(vm.ApplicationHooks);
        Assert.Equal(@"C:\Custom\Tool.exe", vm.ApplicationHooks[0].ExecutablePath);
        Assert.Equal("Tool.exe", vm.ApplicationHooks[0].DisplayName);
        Assert.True(vm.ApplicationHooks[0].CloseOnSwitchAway);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_ApplyTo_PersistsHooksBackToPreset()
    {
        var preset = new WorkstationPreset();
        var vm = new PresetConfigurationItemViewModel(
            preset,
            0,
            "GroupA",
            true,
            selectFileAction: () => @"C:\Games\iRacingSim64DX11.exe");

        vm.AddApplicationHookCommand.Execute(null);

        var target = new WorkstationPreset();
        vm.ApplyTo(target);

        Assert.Single(target.ApplicationHooks);
        Assert.Equal(@"C:\Games\iRacingSim64DX11.exe", target.ApplicationHooks[0].ExecutablePath);
        Assert.True(target.ApplicationHooks[0].CloseOnSwitchAway);
    }

    [Fact]
    public void PresetConfigurationItemViewModel_RemoveHook_RemovesFromCollection()
    {
        var preset = new WorkstationPreset
        {
            ApplicationHooks =
            [
                new PresetApplicationHook { Id = "h1", ExecutablePath = @"C:\Apps\App1.exe" }
            ]
        };

        var vm = new PresetConfigurationItemViewModel(preset, 0, "GroupA", true);
        Assert.Single(vm.ApplicationHooks);

        vm.ApplicationHooks[0].RemoveCommand.Execute(null);

        Assert.Empty(vm.ApplicationHooks);
    }
}
