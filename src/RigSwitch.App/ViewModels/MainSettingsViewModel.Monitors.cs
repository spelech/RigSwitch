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
            var activeTiles = MonitorTiles.Where(t => t.IsActive).ToList();
            if (activeTiles.Count == 0)
            {
                StatusMessage = "No active monitors to identify.";
                return;
            }

            var primaryScreen = screens.FirstOrDefault(s => s.Primary) ?? Screen.PrimaryScreen;
            var nonPrimaryScreens = screens.Where(s => s != primaryScreen).ToList();
            int nonPrimaryScreenIdx = 0;

            foreach (var tile in activeTiles)
            {
                Screen? targetScreen = null;
                if (tile.IsPrimary && primaryScreen != null)
                {
                    targetScreen = primaryScreen;
                }
                else if (nonPrimaryScreenIdx < nonPrimaryScreens.Count)
                {
                    targetScreen = nonPrimaryScreens[nonPrimaryScreenIdx++];
                }
                else if (primaryScreen != null && screens.Length == 1)
                {
                    targetScreen = primaryScreen;
                }

                if (targetScreen != null)
                {
                    var window = new IdentifyWindow(
                        tile.DisplayNumber,
                        tile.FriendlyName,
                        targetScreen.Bounds.Left,
                        targetScreen.Bounds.Top,
                        targetScreen.Bounds.Width,
                        targetScreen.Bounds.Height);
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
                isActive: display.IsActive,
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
