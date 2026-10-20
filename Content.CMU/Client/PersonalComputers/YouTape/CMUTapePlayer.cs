using System.Numerics;
using Content.Client.Resources;
using Content.Client.Station;
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
/// Plays a YouTape tape on a 16:9 screen: each scene's background, the people in it as dressed dolls and its props as
/// real sprites, then over them the scene's captions typed out line by line, the strap across the lower third, the
/// news crawl, the channel's bug, ad and weather graphics, and the deck's own overlay: PLAY and the counter, tracking
/// noise and the odd roll between scenes.
/// </summary>
public sealed class CMUTapePlayer : LayoutContainer
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _cache = default!;

    public const float ScreenWidth = 480;
    public const float ScreenHeight = 270;

    private readonly Dictionary<string, (EntityUid Dummy, SpriteView View)> _dolls = new();
    private readonly List<(Control View, CMUTapeActor Actor)> _onScreen = new();
    private readonly CMUTapeOverlay _overlay;
    private readonly Random _noise = new();

    private CMUTape? _tape;
    private int _scene = -1;
    private float _time;

    public bool Playing { get; private set; }

    /// <summary>The tape ran out.</summary>
    public event Action? OnEnded;

    public CMUTapePlayer()
    {
        IoCManager.InjectDependencies(this);
        SetSize = new Vector2(ScreenWidth, ScreenHeight);
        MinSize = SetSize;
        RectClipContent = true;
        _overlay = new CMUTapeOverlay(this);
        AddChild(_overlay);
        SetAnchorPreset(_overlay, LayoutPreset.Wide);
    }

    public CMUTape? Tape => _tape;
    public float Time => _time;

    public void Load(CMUTape tape)
    {
        _tape = tape;
        _time = 0;
        _scene = -1;
        Playing = true;
        ShowScene();
    }

    public void TogglePlay()
    {
        if (_tape == null)
            return;

        if (_time >= _tape.Length)
            _time = 0;

        Playing = !Playing;
    }

    /// <summary>Winds the tape to this many seconds in, for previews.</summary>
    public void Seek(float seconds)
    {
        _time = Math.Clamp(seconds, 0, _tape?.Length ?? 0);
        ShowScene();
    }

    public void Rewind()
    {
        _time = 0;
        _scene = -1;
        Playing = false;
        ShowScene();
    }

    /// <summary>The scene playing now and how far into it.</summary>
    public (CMUTapeScene? Scene, float At) Current()
    {
        if (_tape == null)
            return (null, 0);

        var start = 0f;
        foreach (var scene in _tape.Scenes)
        {
            if (_time < start + scene.Duration)
                return (scene, _time - start);

            start += scene.Duration;
        }

        return (null, 0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_tape == null)
            return;

        if (Playing)
        {
            _time += args.DeltaSeconds;
            if (_time >= _tape.Length)
            {
                _time = _tape.Length;
                Playing = false;
                OnEnded?.Invoke();
            }
        }

        ShowScene();
        var (scene, at) = Current();
        foreach (var (view, actor) in _onScreen)
        {
            var size = view.SetSize;
            var bob = actor.Talking && Playing && scene != null ? MathF.Abs(MathF.Sin(at * 9f)) * 2f : 0;
            SetPosition(view, new Vector2(actor.At.X * ScreenWidth - size.X / 2f, actor.At.Y * ScreenHeight - size.Y - bob));
        }
    }

    /// <summary>Puts the current scene's people and props on screen when the scene changes.</summary>
    private void ShowScene()
    {
        var index = -1;
        if (_tape != null)
        {
            var start = 0f;
            for (var i = 0; i < _tape.Scenes.Count; i++)
            {
                if (_time < start + _tape.Scenes[i].Duration)
                {
                    index = i;
                    break;
                }

                start += _tape.Scenes[i].Duration;
            }
        }

        if (index == _scene)
            return;

        _scene = index;
        foreach (var (view, _) in _onScreen)
            view.Orphan();

        _onScreen.Clear();
        if (_tape == null || index < 0)
            return;

        foreach (var actor in _tape.Scenes[index].Actors)
        {
            if (Actor(actor) is not { } view)
                continue;

            AddChild(view);
            _onScreen.Add((view, actor));
        }

        _overlay.SetPositionLast();
    }

    private Control? Actor(CMUTapeActor actor)
    {
        if (actor.Prop is { } prop)
        {
            if (!_prototypes.HasIndex(prop))
                return null;

            return new EntityPrototypeView(prop, _entities)
            {
                SetSize = new Vector2(32, 32) * actor.Scale,
                Scale = new Vector2(actor.Scale, actor.Scale),
            };
        }

        if (string.IsNullOrEmpty(actor.Outfit))
            return null;

        return Doll(actor.Outfit, actor.Scale);
    }

    /// <summary>A doll in the outfit, one per outfit for as long as the player is open.</summary>
    private SpriteView Doll(string outfit, float scale)
    {
        if (_dolls.TryGetValue(outfit, out var existing))
            return existing.View;

        var species = _prototypes.Index(HumanoidCharacterProfile.DefaultSpecies).DollPrototype;
        var dummy = _entities.SpawnEntity(species, MapCoordinates.Nullspace);
        if (_prototypes.HasIndex<Content.Shared.Roles.StartingGearPrototype>(outfit))
            _entities.System<StationSpawningSystem>().EquipStartingGear(dummy, outfit);

        var view = new SpriteView(_entities)
        {
            Scale = new Vector2(scale, scale),
            SetSize = new Vector2(32 * scale, 32 * scale),
            OverrideDirection = Direction.South,
            Stretch = SpriteView.StretchMode.None,
        };
        view.SetEntity(dummy);
        _dolls[outfit] = (dummy, view);
        return view;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        foreach (var (dummy, view) in _dolls.Values)
        {
            view.SetEntity(null);
            _entities.QueueDeleteEntity(dummy);
        }

        _dolls.Clear();
        _onScreen.Clear();
        _scene = -1;
    }

    // Drawing

    public Texture? Picture(string? path)
    {
        if (path == null)
            return null;

        var res = new ResPath(path);
        return _cache.ContentFileExists(res) && _cache.TryGetResource<TextureResource>(res, out var texture) ? texture.Texture : null;
    }

    public Font Font(string path, int size)
    {
        return _cache.GetFont(path, size);
    }

    /// <summary>Behind the actors: the background, an ad's picture and the weather map's symbols.</summary>
    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color.Black);
        var (scene, _) = Current();
        if (scene == null)
            return;

        if (Picture(scene.Background) is { } background)
            handle.DrawTextureRect(background, PixelSizeBox);

        var scale = UIScale;
        if (scene.Ad != null && Picture(scene.Ad) is { } ad)
        {
            var width = Math.Min(PixelWidth * 0.7f, ad.Width * 2 * scale);
            var height = width * ad.Height / ad.Width;
            var box = UIBox2.FromDimensions(new Vector2((PixelWidth - width) / 2f, PixelHeight * 0.18f), new Vector2(width, height));
            handle.DrawTextureRect(ad, box);
        }
    }

    /// <summary>Over the actors: graphics, captions and the deck's overlay.</summary>
    private sealed class CMUTapeOverlay(CMUTapePlayer player) : Control
    {
        private const string Vhs = "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf";
        private const string Sans = "/Fonts/NotoSans/NotoSans-Bold.ttf";
        private const float TypeSpeed = 34;

        protected override void Draw(DrawingHandleScreen handle)
        {
            var scale = UIScale;
            var (scene, at) = player.Current();
            var tape = player.Tape;
            if (tape == null)
            {
                DrawCentre(handle, player.Font(Vhs, 14), Loc.GetString("cmu-tape-no-tape"), PixelSizeBox, Color.White, scale);
                return;
            }

            if (scene == null)
            {
                DrawCentre(handle, player.Font(Vhs, 14), Loc.GetString("cmu-tape-ended"), PixelSizeBox, Color.White, scale);
                DrawDeck(handle, tape, scale);
                return;
            }

            DrawWeather(handle, scene, scale);

            var small = player.Font(Sans, 9);
            var bottom = (float) PixelHeight;
            if (scene.Ticker is { } ticker)
            {
                var band = 14 * scale;
                handle.DrawRect(new UIBox2(0, bottom - band, PixelWidth, bottom), Color.FromHex("#101030").WithAlpha(0.9f));
                var width = CMUComputerShellStyle.Width(small, ticker, scale);
                var x = PixelWidth - (player.Time * 45 * scale) % (PixelWidth + width);
                handle.DrawString(small, new Vector2(x, bottom - band + 1 * scale), ticker, scale, Color.White);
                bottom -= band;
            }

            if (scene.Strap is { } strap)
            {
                var font = player.Font(Sans, 11);
                var height = 18 * scale;
                var top = bottom - 44 * scale;
                var width = Math.Min(PixelWidth - 20 * scale, CMUComputerShellStyle.Width(font, strap, scale) + 20 * scale);
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(10 * scale, top), new Vector2(width, height)), scene.Kind == "ad" ? Color.FromHex("#E8C020") : Color.FromHex("#B01818"));
                handle.DrawString(font, new Vector2(20 * scale, top + 2 * scale), strap, scale, scene.Kind == "ad" ? Color.Black : Color.White);
            }

            DrawCaption(handle, scene, at, bottom, scale);
            if (scene.Kind == "ad")
            {
                var advert = Loc.GetString("cmu-tape-advert");
                var font = player.Font(Vhs, 10);
                var width = CMUComputerShellStyle.Width(font, advert, scale);
                handle.DrawString(font, new Vector2(PixelWidth - width - 14 * scale, 8 * scale), advert, scale, Color.White.WithAlpha(0.8f));
            }
            else if (tape.Channel.Length > 0 && scene.Kind is not ("card" or "standby"))
                handle.DrawString(player.Font(Sans, 9), new Vector2(PixelWidth - CMUComputerShellStyle.Width(player.Font(Sans, 9), tape.Channel, scale) - 10 * scale, 8 * scale), tape.Channel, scale, Color.White.WithAlpha(0.7f));

            DrawDeck(handle, tape, scale);
            DrawNoise(handle, at, scale);
        }

        /// <summary>The line being said now, typed out, in a box over the bottom of the picture.</summary>
        private void DrawCaption(DrawingHandleScreen handle, CMUTapeScene scene, float at, float bottom, float scale)
        {
            if (scene.Lines.Count == 0)
                return;

            var each = scene.Duration / scene.Lines.Count;
            var index = Math.Min(scene.Lines.Count - 1, (int) (at / each));
            var line = scene.Lines[index];
            var shown = line[..Math.Min(line.Length, (int) ((at - index * each) * TypeSpeed))];
            var font = player.Font(Sans, 10);
            var lines = Wrap(font, shown, PixelWidth - 40 * scale, scale);
            var lineHeight = font.GetLineHeight(scale);
            var height = lines.Count * lineHeight + 8 * scale;
            var top = bottom - height - 4 * scale;
            if (scene.Strap != null)
                top = Math.Min(top, bottom - 44 * scale - height - 4 * scale);

            if (lines.Count > 0)
                handle.DrawRect(new UIBox2(12 * scale, top, PixelWidth - 12 * scale, top + height), Color.Black.WithAlpha(0.65f));

            for (var i = 0; i < lines.Count; i++)
                handle.DrawString(font, new Vector2(20 * scale, top + 4 * scale + i * lineHeight), lines[i], scale, Color.FromHex("#FFF6C8"));
        }

        private void DrawWeather(DrawingHandleScreen handle, CMUTapeScene scene, float scale)
        {
            var font = player.Font(Sans, 9);
            foreach (var weather in scene.Weather)
            {
                var centre = new Vector2(weather.At.X * PixelWidth, weather.At.Y * PixelHeight);
                var icon = player.Picture(IconPath(weather.Icon));
                var size = 24 * 2 * scale;
                if (icon != null)
                    handle.DrawTextureRect(icon, UIBox2.FromDimensions(centre - new Vector2(size / 2f, size / 2f), new Vector2(size, size)));

                var width = CMUComputerShellStyle.Width(font, weather.Label, scale);
                var pos = new Vector2(centre.X - width / 2f, centre.Y + size / 2f);
                handle.DrawRect(UIBox2.FromDimensions(pos - new Vector2(3, 1) * scale, new Vector2(width + 6 * scale, font.GetLineHeight(scale) + 2 * scale)), Color.Black.WithAlpha(0.55f));
                handle.DrawString(font, pos, weather.Label, scale, Color.White);
            }
        }

        private string? IconPath(string condition)
        {
            return IoCManager.Resolve<IPrototypeManager>().TryIndex<Content.Shared.CMU14.PersonalComputers.CMUTapeGrammarPrototype>(CMUNetTape.Grammar, out var grammar)
                   && grammar.Weather.TryGetValue(condition, out var path)
                ? path
                : null;
        }

        /// <summary>PLAY or PAUSE and the tape counter, top left, the way a deck shows them.</summary>
        private void DrawDeck(DrawingHandleScreen handle, CMUTape tape, float scale)
        {
            var font = player.Font(Vhs, 11);
            var state = Loc.GetString(player.Playing ? "cmu-tape-play" : "cmu-tape-pause");
            var counter = $"{(int) player.Time / 3600}:{(int) player.Time / 60 % 60:00}:{(int) player.Time % 60:00}";
            handle.DrawString(font, new Vector2(10 * scale, 8 * scale), state, scale, Color.White.WithAlpha(0.85f));
            handle.DrawString(font, new Vector2(10 * scale, 10 * scale + font.GetLineHeight(scale) + 2 * scale), counter, scale, Color.White.WithAlpha(0.85f));
        }

        /// <summary>Tracking lines, and a roll for the first moment of every scene.</summary>
        private void DrawNoise(DrawingHandleScreen handle, float at, float scale)
        {
            var random = player._noise;
            for (var i = 0; i < 3; i++)
            {
                var y = random.NextSingle() * PixelHeight;
                handle.DrawRect(new UIBox2(0, y, PixelWidth, y + 1 * scale), Color.White.WithAlpha(0.06f + random.NextSingle() * 0.08f));
            }

            if (at < 0.25f)
            {
                var band = PixelHeight * (1 - at / 0.25f);
                handle.DrawRect(new UIBox2(0, band, PixelWidth, Math.Min(PixelHeight, band + 18 * scale)), Color.White.WithAlpha(0.25f));
            }
        }

        private static void DrawCentre(DrawingHandleScreen handle, Font font, string text, UIBox2 box, Color colour, float scale)
        {
            var width = CMUComputerShellStyle.Width(font, text, scale);
            handle.DrawString(font, new Vector2(box.Center.X - width / 2f, box.Center.Y - font.GetLineHeight(scale) / 2f), text, scale, colour);
        }

        private static List<string> Wrap(Font font, string text, float width, float scale)
        {
            var lines = new List<string>();
            var line = string.Empty;
            foreach (var word in text.Split(' '))
            {
                var next = line.Length == 0 ? word : $"{line} {word}";
                if (line.Length > 0 && CMUComputerShellStyle.Width(font, next, scale) > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = next;
                }
            }

            if (line.Length > 0)
                lines.Add(line);

            return lines;
        }
    }
}
