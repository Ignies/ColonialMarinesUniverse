using System.Numerics;
using Content.Shared.CMU14.PersonalComputers;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>Arcade games downloaded from the ExtraNet: their desktop icons and their windows.</summary>
public sealed partial class CMUComputerDesktop
{
    /// <summary>
    /// The downloaded games' icons, after the saved links. Hidden while the arcade is off, but kept, so the games are
    /// back when the arcade is.
    /// </summary>
    private void AddGameIcons()
    {
        if (_shortcuts == null || !_cfg.GetCVar(CMUPersonalComputerCVars.Arcade))
            return;

        var host = _shortcuts;
        foreach (var game in host.Downloads)
        {
            var name = Loc.GetString($"cmu-arcade-{game}");
            // the address only marks the icon as one that can be deleted, like a saved link
            var icon = new CMUDesktopIcon(_theme, CMUComputerApp.Game, name, CMUGameDownloads.Address(game), CMUGameIcons.For(game))
            {
                ToolTip = Loc.GetString("cmu-game-download-icon-tooltip", ("name", name)),
            };
            icon.OnSelect += SelectIcon;
            icon.OnOpen += _ => OpenGame(game);
            icon.OnRemove += _ =>
            {
                OnClicked?.Invoke();
                host.DeleteGame(game);
            };
            _icons.Add(icon);
            _iconGrid.AddChild(icon);
        }
    }

    /// <summary>What the downloaded games' icons show, so the desktop redraws them when it changes.</summary>
    private string GameIconsKey()
    {
        if (_shortcuts == null)
            return string.Empty;

        var key = _cfg.GetCVar(CMUPersonalComputerCVars.Arcade) ? "games:" : "no games:";
        foreach (var game in _shortcuts.Downloads)
            key += $"{game};";

        return key;
    }

    /// <summary>Opens a downloaded game in its own maximized window, or brings back the one already open.</summary>
    public void OpenGame(CMUArcadeGame game)
    {
        OnClicked?.Invoke();
        foreach (var window in _windows)
        {
            if (window.Content is not CMUArcadeWindow arcade || arcade.Game != game)
                continue;

            Restore(window);
            return;
        }

        var content = new CMUArcadeWindow(_theme, game, _timing, _shortcuts);
        AddWindow(CMUComputerApp.Game, Loc.GetString($"cmu-arcade-{game}"), content, new Vector2(680, 520));
    }
}
