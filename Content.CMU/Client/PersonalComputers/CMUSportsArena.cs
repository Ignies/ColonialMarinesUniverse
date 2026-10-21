using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.Station;
using Content.Shared.CMU14.PersonalComputers;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// Plays a sportsbook match live with dressed dolls and real item sprites. Before kickoff the lineup waits on the
/// field; from kickoff everyone watches the same match, worked out from the seed and winner the server reveals.
/// Each sport draws its own arena; this base holds the dolls, the scoreboard text and the effects they share:
/// particles, blood decals that stay on the floor, screen shake, flashes and sounds.
/// </summary>
public abstract class CMUSportsArena : LayoutContainer
{
    public const float ArenaWidth = 520;
    public const float ArenaHeight = 240;
    protected const float Bar = 26;

    /// <summary>An event this long ago when the arena first sees it is history: it leaves its marks, but silently.</summary>
    private const float Fresh = 0.5f;

    private const int MaxDecals = 160;
    private const int MaxParticles = 600;

    private static readonly ResPath Splatter = new("/Textures/Fluids/splatter.rsi");
    private const int SplatterStates = 6;

    protected static readonly Color Blood = Color.FromHex("#9E0B0B");
    protected static readonly Color DarkBlood = Color.FromHex("#5E0606");

    [Dependency] protected IEntityManager Entities = default!;
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IResourceCache Cache = default!;

    /// <summary>For effects only. The match itself comes from its seed, never from this.</summary>
    protected readonly System.Random FxRandom = new();

    protected readonly Font BigFont;
    protected readonly Font SmallFont;

    private readonly List<Doll> _dolls = new();
    private readonly List<SpriteView> _items = new();
    private readonly List<(string Text, Vector2 Position, Color Colour, bool Big, bool Right)> _texts = new();
    private readonly List<Popup> _popups = new();
    private readonly List<Particle> _particles = new();
    private readonly List<Decal> _decals = new();
    private readonly Dictionary<string, Texture> _textures = new();
    private readonly FxLayer _fx;
    private readonly TextLayer _layer;
    private CMUBetMatch? _match;
    private CMUSportsPlay? _play;
    private TimeSpan _start;
    private bool _replay;
    private float _seen = float.NegativeInfinity;
    private bool _caughtUp;
    private float _shake;
    private float _shakeTime;
    private Vector2 _shakeOffset;
    private Color _flash;
    private float _flashLife;
    private float _flashAge;
    private string _header = string.Empty;

    /// <summary>The clock in the top right. Sports set their own after the base's default.</summary>
    protected string Clock = string.Empty;

    private sealed class Doll(SpriteView view, string kit)
    {
        public readonly SpriteView View = view;
        public readonly string Kit = kit;
        public EntityUid? Dummy;

        /// <summary>Whether <see cref="Pose"/> turned the doll's sprite, so an upright pose turns it back.</summary>
        public bool Turned;
    }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public Color Colour;
        public float Gravity;
        public float Drag;

        /// <summary>The floor this particle lands on, or NaN to fly until it fades.</summary>
        public float Floor;

        /// <summary>Lands as a small blood decal.</summary>
        public bool Stains;

        /// <summary>Drawn as a streak along its velocity, like a spark.</summary>
        public bool Streak;
    }

    private readonly record struct Decal(Vector2 Position, float Size, int State, Color Colour, float Rotation);

    private sealed class Popup
    {
        public required string Text;
        public Vector2 Position;
        public Color Colour;
        public bool Big;
        public float Age;
        public float Life;
        public float Rise;
    }

    protected CMUSportsArena()
    {
        IoCManager.InjectDependencies(this);
        SetSize = new Vector2(ArenaWidth, ArenaHeight);
        HorizontalAlignment = HAlignment.Left;
        RectClipContent = true;
        BigFont = Cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 18);
        SmallFont = Cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 10);
        _fx = new FxLayer(this) { SetSize = new Vector2(ArenaWidth, ArenaHeight) };
        _layer = new TextLayer(this) { SetSize = new Vector2(ArenaWidth, ArenaHeight) };
    }

    /// <summary>The arena for a sport.</summary>
    public static CMUSportsArena Create(CMUSport sport)
    {
        return sport switch
        {
            CMUSport.Sprint => new CMUSprintArena(),
            CMUSport.Boxing => new CMUBoxingArena(),
            CMUSport.Baseball => new CMUBaseballArena(),
            _ => new CMUZeroGArena(),
        };
    }

    public CMUBetMatch? Match => _match;

    /// <summary>Whether a replay is on rather than the live match.</summary>
    public bool Replaying => _replay;

    /// <summary>
    /// Shows a match, live from its kickoff, or from now when <paramref name="replay"/> is set. A replay can start
    /// <paramref name="from"/> seconds in, for highlights: what happened before that leaves its marks on the floor
    /// but plays no sound, as when a late viewer tunes in.
    /// </summary>
    public void Show(CMUBetMatch match, bool replay = false, float from = 0)
    {
        var known = _match != null && _match.Id == match.Id && _match.Winner == match.Winner;
        _match = match;
        if (known && !replay && !_replay)
            return;

        _replay = replay;
        _play = match.Winner >= 0 ? CMUSports.Play(match.Sport, match.Seed, match.Entrants.Count, match.Winner) : null;
        var skip = replay ? TimeSpan.FromSeconds(Math.Clamp(from, 0, CMUSports.Length(match.Sport))) : TimeSpan.Zero;
        _start = replay ? Timing.CurTime - skip : match.Kickoff;
        Rebuild();
    }

    private void Rebuild()
    {
        ClearDolls();
        RemoveAllChildren();
        _items.Clear();
        ResetEffects();
        if (_match is not { } match)
            return;

        Build(match);
        AddChild(_fx);
        AddChild(_layer);
        if (IsInsideTree)
            SpawnDolls();
    }

    /// <summary>Wipes the floor and forgets which events played, so they play again from the start.</summary>
    private void ResetEffects()
    {
        _particles.Clear();
        _decals.Clear();
        _popups.Clear();
        _seen = float.NegativeInfinity;
        _caughtUp = false;
        _shake = 0;
        _shakeTime = 0;
        _shakeOffset = Vector2.Zero;
        _flashLife = 0;
    }

    #region For the sports

    /// <summary>Adds the sport's dolls and items, in drawing order. Called whenever a new match is shown.</summary>
    protected abstract void Build(CMUBetMatch match);

    /// <summary>
    /// Poses everything for <paramref name="t"/> seconds from kickoff (negative before it, and -1 while the winner
    /// is still secret, when <paramref name="play"/> is null). Called every frame the arena is on screen.
    /// </summary>
    protected abstract void UpdateMatch(CMUBetMatch match, CMUSportsPlay? play, float t);

    /// <summary>The field, under the decals and dolls.</summary>
    protected abstract void DrawField(DrawingHandleScreen handle);

    /// <summary>Over the decals, under the dolls: shadows, chalk, anything the blood should not cover.</summary>
    protected virtual void DrawOverDecals(DrawingHandleScreen handle)
    {
    }

    /// <summary>Over the dolls and particles, under the text: ropes in front, glass, lights.</summary>
    protected virtual void DrawOverlay(DrawingHandleScreen handle)
    {
    }

    /// <summary>
    /// Runs once for every event of the match as its time comes. <paramref name="live"/> is false for events that
    /// happened before the arena was looking (a late join, a tab switched back to): leave their lasting marks, like
    /// decals, but make no sound, particles or shake. Effects calls made while not live are ignored anyway.
    /// </summary>
    protected virtual void OnEvent(CMUBetMatch match, CMUSportsEvent ev, int index, bool live)
    {
    }

    /// <summary>Whether effects play now. False while catching up on events that happened off screen.</summary>
    protected bool Live { get; private set; }

    protected int AddDoll(string kit, float scale)
    {
        // Fit would shrink the doll to fit the box and resize it as it turns; dolls keep their own size
        var view = new SpriteView(Entities)
        {
            Scale = new Vector2(scale, scale),
            SetSize = new Vector2(32 * scale, 32 * scale),
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.None,
        };
        _dolls.Add(new Doll(view, kit));
        AddChild(view);
        return _dolls.Count - 1;
    }

    protected int DollCount => _dolls.Count;

    /// <summary>The doll's view, to tint (<c>Modulate</c>) or scale it.</summary>
    protected SpriteView DollView(int doll)
    {
        return _dolls[doll].View;
    }

    /// <summary>An item sprite, hidden every frame until placed.</summary>
    protected EntityPrototypeView AddItem(EntProtoId prototype, float size, float scale = 1)
    {
        var view = new EntityPrototypeView(prototype, Entities)
        {
            SetSize = new Vector2(size, size),
            Scale = new Vector2(scale, scale),
            Visible = false,
        };
        _items.Add(view);
        AddChild(view);
        return view;
    }

    /// <summary>Centres a control on a spot of the arena, shaken with it.</summary>
    protected void Place(Control control, float x, float y)
    {
        control.Visible = true;
        SetPosition(control, new Vector2(x - control.SetSize.X / 2, y - control.SetSize.Y / 2) + _shakeOffset);
    }

    /// <summary>
    /// Stands a doll on a spot facing a way, turned by <paramref name="rotation"/> degrees anticlockwise on screen.
    /// The turn goes on the doll's own sprite: a view's world rotation would pick another facing for anything past
    /// 45 degrees and turn it back upright, so a boxer laid flat would still stand.
    /// </summary>
    protected void Pose(int doll, float x, float y, Direction direction, float rotation = 0)
    {
        if (doll >= _dolls.Count)
            return;

        var entry = _dolls[doll];
        entry.View.OverrideDirection = direction;
        if ((rotation != 0 || entry.Turned) && entry.Dummy is { } dummy && Entities.TryGetComponent<SpriteComponent>(dummy, out var sprite))
        {
            Entities.System<SpriteSystem>().SetRotation((dummy, sprite), Angle.FromDegrees(rotation));
            entry.Turned = rotation != 0;
        }

        Place(entry.View, x, y);
    }

    protected void Hide(int doll)
    {
        if (doll < _dolls.Count)
            _dolls[doll].View.Visible = false;
    }

    /// <summary>Text for this frame. Big calls are centred on their spot; labels hang from it, or end at it when right aligned.</summary>
    protected void Say(string text, float x, float y, Color colour, bool big = false, bool right = false)
    {
        _texts.Add((text, new Vector2(x, y), colour, big, right));
    }

    /// <summary>Text that floats up and fades by itself, like a hit word.</summary>
    protected void Pop(string text, Vector2 at, Color colour, bool big = false, float life = 0.8f, float rise = 24)
    {
        if (Live)
            _popups.Add(new Popup { Text = text, Position = at, Colour = colour, Big = big, Life = life, Rise = rise });
    }

    /// <summary>A sprite state's first frame, cached. Paths are rooted, like <c>/Textures/Fluids/splatter.rsi</c>.</summary>
    protected Texture Sprite(string rsi, string state)
    {
        var key = $"{rsi}:{state}";
        if (!_textures.TryGetValue(key, out var texture))
        {
            texture = Entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(new ResPath(rsi), state));
            _textures[key] = texture;
        }

        return texture;
    }

    #endregion

    #region Effects

    /// <summary>
    /// Throws particles from a spot. <paramref name="direction"/> and <paramref name="spread"/> are in radians, with
    /// 0 pointing right and positive angles turning down the screen.
    /// </summary>
    protected void Burst(Vector2 at, int count, Color colour, float speed, float life = 0.6f, float size = 2,
        float direction = 0, float spread = MathF.Tau, float gravity = 0, float floor = float.NaN, bool stains = false,
        bool streak = false, float drag = 0)
    {
        if (!Live)
            return;

        for (var i = 0; i < count && _particles.Count < MaxParticles; i++)
        {
            var angle = direction + (FxFloat() - 0.5f) * spread;
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * (0.35f + FxFloat() * 0.65f);
            _particles.Add(new Particle
            {
                Position = at,
                Velocity = velocity,
                Life = life * (0.6f + FxFloat() * 0.6f),
                Size = size * (0.6f + FxFloat() * 0.8f),
                Colour = colour,
                Gravity = gravity,
                Floor = floor,
                Stains = stains,
                Streak = streak,
                Drag = drag,
            });
        }
    }

    /// <summary>
    /// Blood sprays from a wound towards <paramref name="direction"/>, and the drops that reach
    /// <paramref name="floor"/> stain it. <paramref name="amount"/> 1 is a nick, 3 a gusher.
    /// </summary>
    protected void Bleed(Vector2 at, float floor, int amount, float direction = -MathF.PI / 2, float spread = 1.6f)
    {
        Burst(at, 6 + amount * 8, Blood, 70 + amount * 45, 1.4f, 1.6f + amount * 0.5f, direction, spread,
            gravity: 420, floor: floor, stains: true);
        Burst(at, 2 + amount * 2, DarkBlood, 30 + amount * 20, 0.9f, 2.4f + amount * 0.6f, direction, spread * 0.6f,
            gravity: 420, floor: floor, stains: true);
    }

    /// <summary>Stamps a blood decal on the floor. Unlike the other effects it also lands while catching up.</summary>
    protected void Splat(Vector2 at, float size, Color? colour = null)
    {
        if (_decals.Count >= MaxDecals)
            _decals.RemoveAt(0);

        _decals.Add(new Decal(at, size, FxRandom.Next(SplatterStates), colour ?? Blood, FxFloat() * MathF.Tau));
    }

    /// <summary>Shakes the whole arena, strongest at the start and dying out over <paramref name="time"/>.</summary>
    protected void Shake(float strength, float time = 0.35f)
    {
        // a light tap does not cut short a big hit still shaking
        if (!Live || _shakeTime > 0 && strength < _shake)
            return;

        _shake = strength;
        _shakeTime = time;
    }

    /// <summary>Washes the arena with a colour that fades out over <paramref name="time"/>.</summary>
    protected void Flash(Color colour, float time = 0.25f)
    {
        if (!Live)
            return;

        _flash = colour;
        _flashLife = time;
        _flashAge = 0;
    }

    /// <summary>Plays a sound from <c>/Audio/...</c> while live and on screen. Volume is in dB over the game base.</summary>
    protected void Sound(string path, float volume = 0, float pitch = 1)
    {
        if (Live)
            CMUGameAudio.Play(this, path, volume, pitch);
    }

    protected void SoundAny(IReadOnlyList<string> paths, float volume = 0, float spread = 0.08f)
    {
        if (Live)
            CMUGameAudio.PlayAny(this, paths, volume, spread);
    }

    protected void Scream(bool female, float volume = 0)
    {
        if (Live)
            CMUGameAudio.Scream(this, female, volume);
    }

    protected float FxFloat()
    {
        return (float) FxRandom.NextDouble();
    }

    private void UpdateEffects(float delta)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += delta;
            p.Velocity.Y += p.Gravity * delta;
            if (p.Drag > 0)
                p.Velocity *= MathF.Max(0, 1 - p.Drag * delta);

            p.Position += p.Velocity * delta;
            var landed = !float.IsNaN(p.Floor) && p.Position.Y >= p.Floor && p.Velocity.Y > 0;
            if (landed && p.Stains)
            {
                // stamp the drop where it fell; the bigger it was, the wider it spreads
                if (_decals.Count >= MaxDecals)
                    _decals.RemoveAt(0);

                _decals.Add(new Decal(p.Position with { Y = p.Floor }, p.Size * 2.6f, FxRandom.Next(SplatterStates), p.Colour, FxFloat() * MathF.Tau));
            }

            if (landed || p.Age >= p.Life)
            {
                _particles.RemoveAt(i);
                continue;
            }

            _particles[i] = p;
        }

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            _popups[i].Age += delta;
            if (_popups[i].Age >= _popups[i].Life)
                _popups.RemoveAt(i);
        }

        if (_shakeTime > 0)
        {
            _shakeTime -= delta;
            var strength = _shakeTime > 0 ? _shake * MathF.Min(1, _shakeTime * 4) : 0;
            _shakeOffset = new Vector2((FxFloat() - 0.5f) * 2 * strength, (FxFloat() - 0.5f) * 2 * strength);
        }
        else
        {
            _shakeOffset = Vector2.Zero;
        }

        if (_flashLife > 0)
            _flashAge += delta;
    }

    #endregion

    // Dolls live only while the arena is on screen, like the ads.
    protected override void EnteredTree()
    {
        base.EnteredTree();
        SpawnDolls();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ClearDolls(false);
    }

    private void SpawnDolls()
    {
        var doll = Prototypes.Index(HumanoidCharacterProfile.DefaultSpecies).DollPrototype;
        var spawning = Entities.System<StationSpawningSystem>();
        foreach (var entry in _dolls)
        {
            if (entry.Dummy != null)
                continue;

            var dummy = Entities.SpawnEntity(doll, MapCoordinates.Nullspace);
            if (Prototypes.HasIndex<Content.Shared.Roles.StartingGearPrototype>(entry.Kit))
                spawning.EquipStartingGear(dummy, entry.Kit);

            entry.Dummy = dummy;
            entry.Turned = false;
            entry.View.SetEntity(dummy);
        }
    }

    private void ClearDolls(bool forget = true)
    {
        foreach (var entry in _dolls)
        {
            if (entry.Dummy is not { } dummy)
                continue;

            entry.View.SetEntity(null);
            Entities.QueueDeleteEntity(dummy);
            entry.Dummy = null;
        }

        if (forget)
            _dolls.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _texts.Clear();
        if (_match is not { } match)
            return;

        var t = (float) (Timing.CurTime - _start).TotalSeconds;
        var length = CMUSports.Length(match.Sport);
        _header = Loc.GetString($"cmu-sport-{match.Sport}").ToUpperInvariant();
        Clock = _play == null
            ? Loc.GetString("cmu-sport-starts", ("seconds", Math.Max(0, (int) -t)))
            : t >= length ? Loc.GetString("cmu-sport-final") : $"{(int) t / 60}:{(int) t % 60:00}";

        var play = _play;
        var time = play == null ? -1 : Math.Min(t, length);
        if (play != null)
        {
            if (time < _seen)
                ResetEffects();

            for (var i = 0; i < play.Events.Count; i++)
            {
                var ev = play.Events[i];
                if (ev.Time <= _seen || ev.Time > time)
                    continue;

                Live = _caughtUp && time - ev.Time < Fresh;
                OnEvent(match, ev, i, Live);
            }

            _seen = time;
        }

        _caughtUp = true;
        Live = true;
        UpdateEffects(args.DeltaSeconds);
        foreach (var item in _items)
        {
            item.Visible = false;
        }

        UpdateMatch(match, play, time);

        if (play != null && t >= length)
            Say(Loc.GetString("cmu-sport-winner", ("name", match.Name(match.Winner))), ArenaWidth / 2, ArenaHeight - 30, Color.Gold, true);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_match == null)
        {
            Rect(handle, 0, 0, ArenaWidth, ArenaHeight, Color.Black);
        }
        else
        {
            DrawField(handle);
            foreach (var decal in _decals)
            {
                DrawDecal(handle, decal);
            }

            DrawOverDecals(handle);
        }

        // the scoreboard bar stays still while the field shakes
        handle.DrawRect(new UIBox2(0, 0, ArenaWidth * UIScale, Bar * UIScale), Color.FromHex("#0C0F14"));
        handle.DrawRect(new UIBox2(0, (Bar - 2) * UIScale, ArenaWidth * UIScale, Bar * UIScale), Color.FromHex("#7CFF00"));
    }

    private void DrawDecal(DrawingHandleScreen handle, Decal decal)
    {
        var texture = Sprite(Splatter.ToString(), $"splatter-{decal.State}");
        var centre = (decal.Position + _shakeOffset) * UIScale;
        var half = new Vector2(decal.Size, decal.Size) * UIScale / 2;
        var transform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.Multiply(Matrix3x2.CreateRotation(decal.Rotation) * Matrix3x2.CreateTranslation(centre), transform));
        handle.DrawTextureRect(texture, new UIBox2(-half, half), decal.Colour);
        handle.SetTransform(transform);
    }

    #region Drawing helpers

    /// <summary>A filled rectangle in arena units, shaken with the field.</summary>
    protected void Rect(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        handle.DrawRect(Box(x, y, width, height), colour);
    }

    protected void Frame(DrawingHandleScreen handle, float x, float y, float width, float height, Color colour)
    {
        handle.DrawRect(Box(x, y, width, height), colour, false);
    }

    protected UIBox2 Box(float x, float y, float width, float height)
    {
        x += _shakeOffset.X;
        y += _shakeOffset.Y;
        return new UIBox2(x * UIScale, y * UIScale, (x + width) * UIScale, (y + height) * UIScale);
    }

    protected void Circle(DrawingHandleScreen handle, float x, float y, float radius, Color colour, bool filled = true)
    {
        handle.DrawCircle((new Vector2(x, y) + _shakeOffset) * UIScale, radius * UIScale, colour, filled);
    }

    protected void Line(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color colour)
    {
        handle.DrawLine((from + _shakeOffset) * UIScale, (to + _shakeOffset) * UIScale, colour);
    }

    protected void Poly(DrawingHandleScreen handle, Color colour, params Vector2[] points)
    {
        var scaled = points.Select(p => (p + _shakeOffset) * UIScale).ToArray();
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, scaled, colour);
    }

    /// <summary>Draws a texture centred on a spot, at a size in arena units, turned by <paramref name="rotation"/> radians.</summary>
    protected void Texture(DrawingHandleScreen handle, Texture texture, Vector2 at, Vector2 size, float rotation = 0, Color? modulate = null)
    {
        var centre = (at + _shakeOffset) * UIScale;
        var half = size * UIScale / 2;
        if (rotation == 0)
        {
            handle.DrawTextureRect(texture, new UIBox2(centre - half, centre + half), modulate);
            return;
        }

        var transform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.Multiply(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(centre), transform));
        handle.DrawTextureRect(texture, new UIBox2(-half, half), modulate);
        handle.SetTransform(transform);
    }

    /// <summary>The colour a match entrant wears, by slot.</summary>
    protected static Color KitColour(CMUBetMatch match, int slot)
    {
        if (slot < 0 || slot >= match.Entrants.Count)
            return Color.White;

        return CMUSports.Colours[CMUSports.Colour(match.Sport, slot, match.Entrants[slot])] switch
        {
            "Red" => Color.FromHex("#E04040"),
            "Blue" => Color.FromHex("#4080FF"),
            "Green" => Color.FromHex("#40C060"),
            "Yellow" => Color.FromHex("#F0D040"),
            "Orange" => Color.FromHex("#F09030"),
            "Purple" => Color.FromHex("#A060E0"),
            "Brown" => Color.FromHex("#A07040"),
            _ => Color.White,
        };
    }

    /// <summary>The last word of a team's name, for the scoreboard.</summary>
    protected static string Short(string team)
    {
        var words = team.Split(' ');
        return words[^1].ToUpperInvariant();
    }

    #endregion

    /// <summary>Particles and the flash, over the dolls.</summary>
    private sealed class FxLayer(CMUSportsArena arena) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            var scale = UIScale;
            foreach (var p in arena._particles)
            {
                var colour = p.Colour.WithAlpha(p.Colour.A * Math.Clamp(1 - p.Age / p.Life, 0, 1) * 1.4f);
                var at = (p.Position + arena._shakeOffset) * scale;
                if (p.Streak)
                {
                    handle.DrawLine(at, at - p.Velocity * 0.03f * scale, colour);
                    continue;
                }

                var half = p.Size * scale / 2;
                handle.DrawRect(new UIBox2(at.X - half, at.Y - half, at.X + half, at.Y + half), colour);
            }

            arena.DrawOverlay(handle);
            if (arena._flashLife > 0 && arena._flashAge < arena._flashLife)
            {
                var alpha = arena._flash.A * (1 - arena._flashAge / arena._flashLife);
                handle.DrawRect(new UIBox2(0, Bar * scale, ArenaWidth * scale, ArenaHeight * scale), arena._flash.WithAlpha(alpha));
            }
        }
    }

    /// <summary>Text above the dolls: the scoreboard, calls and hit words.</summary>
    private sealed class TextLayer(CMUSportsArena arena) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            var outline = new TextOutline(2, Color.Black);
            var scale = UIScale;
            handle.DrawString(arena.SmallFont, new Vector2(8, 7) * scale, arena._header, scale, Color.FromHex("#7CFF00"));
            DrawBadge(handle, scale);
            var clock = handle.GetDimensions(arena.SmallFont, arena.Clock, scale);
            handle.DrawString(arena.SmallFont, new Vector2(ArenaWidth - 8, 7) * scale - clock with { Y = 0 }, arena.Clock, scale, Color.White);
            foreach (var (text, position, colour, big, right) in arena._texts)
            {
                Text(handle, text, position + arena._shakeOffset, colour, big, right, outline);
            }

            foreach (var popup in arena._popups)
            {
                var progress = popup.Age / popup.Life;
                var at = popup.Position + arena._shakeOffset - new Vector2(0, popup.Rise * progress);
                Text(handle, popup.Text, at, popup.Colour.WithAlpha(Math.Clamp(2 - progress * 2, 0, 1)), popup.Big, false, outline);
            }
        }

        /// <summary>A blinking LIVE light while a match plays, or REPLAY while one is watched again.</summary>
        private void DrawBadge(DrawingHandleScreen handle, float scale)
        {
            if (arena._match is not { } match || arena._play == null)
                return;

            var t = (float) (arena.Timing.CurTime - arena._start).TotalSeconds;
            if (t < 0 || t >= CMUSports.Length(match.Sport))
                return;

            var text = Loc.GetString(arena._replay ? "cmu-game-arena-replay" : "cmu-game-arena-live");
            var header = handle.GetDimensions(arena.SmallFont, arena._header, scale);
            var x = 8 * scale + header.X + 10 * scale;
            var on = arena.Timing.RealTime.TotalSeconds % 1 < 0.6;
            var colour = arena._replay ? Color.FromHex("#F2C230") : Color.FromHex("#FF3030");
            if (on)
                handle.DrawCircle(new Vector2(x + 4 * scale, 13 * scale), 4 * scale, colour);

            handle.DrawString(arena.SmallFont, new Vector2(x + 11 * scale, 7 * scale), text, scale, colour);
        }

        private void Text(DrawingHandleScreen handle, string text, Vector2 position, Color colour, bool big, bool right, TextOutline outline)
        {
            var scale = UIScale;
            var font = big ? arena.BigFont : arena.SmallFont;
            var size = handle.GetDimensions(font, text, scale);
            var at = position * scale;
            if (big)
                at -= size / 2;
            else if (right)
                at.X -= size.X;

            handle.DrawString(font, at, text, scale, colour, outline);
        }
    }
}
