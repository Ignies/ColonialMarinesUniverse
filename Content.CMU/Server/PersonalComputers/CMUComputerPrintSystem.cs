using Content.Server.Administration.Logs;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Desk terminals print notes from Files onto real paper, which drops out of the machine where it stands. Laptops
/// have no printer. Each printer needs a few seconds between sheets.
/// </summary>
public sealed class CMUComputerPrintSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId Paper = "CMPaper";
    private static readonly SoundPathSpecifier PrintSound = new("/Audio/Machines/printer.ogg");

    /// <summary>When each terminal's printer is ready again.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _ready = new();

    public override void Initialize()
    {
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUComputerPrintNoteMessage>(OnPrintNote);
        });
        SubscribeLocalEvent<CMUPersonalComputerComponent, ComponentShutdown>((ent, _, _) => _ready.Remove(ent));
    }

    /// <summary>Whether this terminal has a printer: its own casing, else its OS's, decides.</summary>
    public bool CanPrint(CMUPersonalComputerComponent computer)
    {
        var casing = computer.Casing
            ?? (_prototype.TryIndex(computer.Os, out var os) ? os.Casing : CMUComputerCasing.Seegson);
        return CMUComputerPrinting.CanPrint(casing);
    }

    private void OnPrintNote(Entity<CMUPersonalComputerComponent> ent, ref CMUComputerPrintNoteMessage args)
    {
        if (!_cfg.GetCVar(CMUPersonalComputerCVars.Notes)
            || args.Index < 0
            || args.Index >= ent.Comp.Notes.Count
            || !CanPrint(ent.Comp))
            return;

        var note = ent.Comp.Notes[args.Index];
        TryPrint(ent, args.Actor, $"{note.Name}.TXT", note.Text, note.Author);
    }

    /// <summary>
    /// Prints a document on the terminal's printer: a titled sheet of plain text, signed when there is an author.
    /// False with no printer, or while it is still busy with the last sheet.
    /// </summary>
    public bool TryPrint(Entity<CMUPersonalComputerComponent> ent, EntityUid actor, string name, string text, string author)
    {
        if (!CanPrint(ent.Comp))
            return false;

        var now = _timing.CurTime;
        if (_ready.TryGetValue(ent, out var ready) && now < ready)
            return false;

        _ready[ent] = now + CMUComputerPrinting.Cooldown;
        var content = $"[head=2]{FormattedMessage.EscapeText(name)}[/head]\n{FormattedMessage.EscapeText(text)}";
        if (author.Length > 0)
            content += "\n\n" + Loc.GetString("cmu-print-signed", ("author", FormattedMessage.EscapeText(author)));

        var paper = Spawn(Paper, Transform(ent).Coordinates);
        _metaData.SetEntityName(paper, Loc.GetString("cmu-print-name", ("name", name)));
        _paper.SetContent(paper, content);
        _audio.PlayPvs(PrintSound, ent, AudioParams.Default.WithVolume(-4f));
        _ui.ServerSendUiMessage(ent.Owner, CMUPersonalComputerUiKey.Key, new CMUComputerPrintedMessage(name), actor);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(actor):player} printed {name} on {ToPrettyString(ent):entity}");
        return true;
    }
}
