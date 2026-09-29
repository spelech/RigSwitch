namespace RigSwitch.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using RigSwitch.App.Views;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

public partial class MainSettingsViewModel
{
    public ObservableCollection<DisplayMonitorTileViewModel> MonitorTiles { get; } = [];

    public ICommand IdentifyMonitorsCommand { get; }

    public void IdentifyMonitors()
    {
        try
        {
            var screens = Screen.AllScreens;
            for (int i = 0; i < MonitorTiles.Count; i++)
            {
                var tile = MonitorTiles[i];
                var screen = (i < screens.Length) ? screens[i] : Screen.PrimaryScreen;
                if (screen != null)
                {
                    var window = new IdentifyWindow(
                        tile.DisplayNumber,
                        tile.FriendlyName,
                        screen.Bounds.Left,
                        screen.Bounds.Top,
                        screen.Bounds.Width,
                        screen.Bounds.Height);
                    window.Show();
                }
            }
            StatusMessage = "Identifying monitors...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Identify error: {ex.Message}";
        }
    }

    public void PopulateMonitorTiles(IReadOnlyList<DisplayDeviceInfo> displays, UserSettings settings)
    {
        MonitorTiles.Clear();

        var deskPreset = settings.GetActivePreset(ProfileMode.Desk);
        var rigPreset = settings.GetActivePreset(ProfileMode.SimRig);

        var deskMonitorIds = deskPreset.TargetMonitorIds;
        var rigMonitorIds = rigPreset.TargetMonitorIds;

        int number = 1;
        foreach (var display in displays)
        {
            var pile = MonitorPileAssignment.Unassigned;
            if (deskMonitorIds.Contains(display.MonitorId, StringComparer.OrdinalIgnoreCase))
            {
                pile = MonitorPileAssignment.Desk;
            }
            else if (rigMonitorIds.Contains(display.MonitorId, StringComparer.OrdinalIgnoreCase))
            {
                pile = MonitorPileAssignment.SimRig;
            }

            var tile = new DisplayMonitorTileViewModel(
                displayNumber: number++,
                monitorId: display.MonitorId,
                friendlyName: display.FriendlyName,
                pile: pile,
                isPrimary: display.IsPrimary,
                movePileAction: OnTilePileMoved);

            MonitorTiles.Add(tile);
        }
    }

    private void OnTilePileMoved(DisplayMonitorTileViewModel tile, MonitorPileAssignment newPile)
    {
        tile.Pile = newPile;

        SyncMonitorTilesToActivePresets();
    }

    public void SyncMonitorTilesToActivePresets()
    {
        var deskPresetVm = DeskPresets.FirstOrDefault(p => p.IsActive) ?? DeskPresets.FirstOrDefault();
        var rigPresetVm = RigPresets.FirstOrDefault(p => p.IsActive) ?? RigPresets.FirstOrDefault();

        var deskMonitorIds = MonitorTiles
            .Where(t => t.Pile == MonitorPileAssignment.Desk)
            .Select(t => t.MonitorId)
            .ToList();

        var rigMonitorIds = MonitorTiles
            .Where(t => t.Pile == MonitorPileAssignment.SimRig)
            .Select(t => t.MonitorId)
            .ToList();

        if (deskPresetVm != null)
        {
            deskPresetVm.TargetMonitorId = deskMonitorIds.Count > 0 ? deskMonitorIds[0] : string.Empty;
            deskPresetVm.TargetMonitorIds = deskMonitorIds;
        }

        if (rigPresetVm != null)
        {
            rigPresetVm.TargetMonitorId = rigMonitorIds.Count > 0 ? rigMonitorIds[0] : string.Empty;
            rigPresetVm.TargetMonitorIds = rigMonitorIds;
        }

        if (_settings != null)
        {
            var activeDeskPreset = _settings.GetActivePreset(ProfileMode.Desk);
            var activeRigPreset = _settings.GetActivePreset(ProfileMode.SimRig);
            activeDeskPreset.TargetMonitorIds = deskMonitorIds;
            activeRigPreset.TargetMonitorIds = rigMonitorIds;
        }
    }
}
