using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PersonalComputers;

/// <summary>
/// Mail between colonists. Mailboxes belong to names, so whoever logs in anywhere with a card reads their own
/// mail, and a stolen card reads someone else's. The Company welcomes everyone at their first login, and
/// spammers from the ad maze fill inboxes now and then.
/// </summary>
public sealed partial class CMUMailSystem : EntitySystem
{
    private static readonly ProtoId<LocalizedDatasetPrototype> Spam = "CMUDeepAdSlogans";
    private static readonly TimeSpan SpamInterval = TimeSpan.FromMinutes(7);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<string, List<CMUMailMessage>> _boxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, TimeSpan> _nextSend = new();
    private int _nextId = 1;
    private TimeSpan _nextSpam;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _boxes.Clear();
            _nextSend.Clear();
            _nextId = 1;
            _nextSpam = TimeSpan.Zero;
        });
        Subs.BuiEvents<CMUPersonalComputerComponent>(CMUPersonalComputerUiKey.Key, subs =>
        {
            subs.Event<CMUMailRequestMessage>(OnRequest);
            subs.Event<CMUMailSendMessage>(OnSend);
        });
    }

    private bool Enabled => _cfg.GetCVar(CMUPersonalComputerCVars.Enabled) && _cfg.GetCVar(CMUPersonalComputerCVars.Mail);

    /// <summary>A mailbox, made with a welcome from the Company the first time its owner looks.</summary>
    private List<CMUMailMessage> Box(string name)
    {
        if (_boxes.TryGetValue(name, out var box))
            return box;

        _boxes[name] = box = new List<CMUMailMessage>();
        box.Add(new CMUMailMessage(_nextId++, Loc.GetString("cmu-mail-company-from"), name,
            Loc.GetString("cmu-mail-welcome-subject"), Loc.GetString("cmu-mail-welcome-body", ("name", name)), _timing.CurTime));
        return box;
    }

    /// <summary>A name's mailbox, newest first.</summary>
    public IReadOnlyList<CMUMailMessage> Inbox(string name)
    {
        return Box(name);
    }

    /// <summary>Delivers a mail, from a player or from the colony's automated senders.</summary>
    public void Deliver(string from, string to, string subject, string body)
    {
        var box = Box(to);
        box.Insert(0, new CMUMailMessage(_nextId++, from, to, subject, body, _timing.CurTime));
        if (box.Count > CMUMailLimits.PerBox)
            box.RemoveAt(box.Count - 1);
    }

    private void OnRequest(Entity<CMUPersonalComputerComponent> ent, ref CMUMailRequestMessage args)
    {
        if (!Enabled || ent.Comp.SessionUser is not { } user)
            return;

        SendState(ent, args.Actor, user);
    }

    private void OnSend(Entity<CMUPersonalComputerComponent> ent, ref CMUMailSendMessage args)
    {
        if (!Enabled || ent.Comp.SessionUser is not { } user)
            return;

        var now = _timing.CurTime;
        if (_nextSend.TryGetValue(args.Actor, out var ready) && now < ready)
            return;

        var to = Clean(args.To, 64);
        var subject = Clean(args.Subject, CMUMailLimits.SubjectLength);
        var body = args.Body.Trim();
        if (body.Length > CMUMailLimits.BodyLength)
            body = body[..CMUMailLimits.BodyLength];

        if (to.Length == 0 || body.Length == 0)
            return;

        _nextSend[args.Actor] = now + CMUMailLimits.Cooldown;
        Deliver(user, to, subject.Length == 0 ? Loc.GetString("cmu-mail-no-subject") : subject, body);
        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} mailed {to} as {user} from terminal #{ent.Comp.TerminalId}: {subject}: {body}");
        SendState(ent, args.Actor, user);
    }

    private void SendState(EntityUid computer, EntityUid actor, string user)
    {
        var inbox = new List<CMUMailMessage>(Box(user));
        var sent = _boxes.Values.SelectMany(b => b).Where(m => m.From.Equals(user, StringComparison.OrdinalIgnoreCase)).ToList();
        _ui.ServerSendUiMessage(computer, CMUPersonalComputerUiKey.Key, new CMUMailStateMessage(inbox, sent, Directory()), actor);
    }

    /// <summary>Everyone with an ID card in the colony, so mail can be addressed without guessing spellings.</summary>
    private List<string> Directory()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out _, out var card))
        {
            if (card.OriginalOwner != null && !string.IsNullOrWhiteSpace(card.FullName))
                names.Add(card.FullName);
        }

        return names.ToList();
    }

    private static string Clean(string text, int max)
    {
        text = text.Trim().Replace('\n', ' ');
        return text.Length > max ? text[..max] : text;
    }

    /// <summary>Spam from the ad maze, now and then, into a mailbox someone has opened.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!Enabled || !_cfg.GetCVar(CMUPersonalComputerCVars.DeepWeb) || _boxes.Count == 0)
            return;

        var now = _timing.CurTime;
        if (_nextSpam == TimeSpan.Zero)
            _nextSpam = now + SpamInterval;

        if (now < _nextSpam)
            return;

        _nextSpam = now + SpamInterval;
        var slogans = _prototype.Index(Spam).Values;
        var to = _random.Pick(_boxes.Keys.ToList());
        Deliver(Loc.GetString("cmu-mail-spam-from"), to, Loc.GetString(slogans[_random.Next(slogans.Count)]),
            Loc.GetString("cmu-mail-spam-body"));
    }
}
