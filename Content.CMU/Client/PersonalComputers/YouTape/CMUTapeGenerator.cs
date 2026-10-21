using System.Linq;
using System.Numerics;
using System.Text;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>Someone or something on screen in a scene: a dressed doll or a prop, placed by fractions of the frame.</summary>
public sealed class CMUTapeActor
{
    /// <summary>Starting gear a doll wears; null for a prop.</summary>
    public string? Outfit;

    public EntProtoId? Prop;

    /// <summary>Where its feet stand, from 0,0 top left to 1,1 bottom right.</summary>
    public Vector2 At;

    public float Scale = 4;

    /// <summary>Bobs while it talks.</summary>
    public bool Talking;

    /// <summary>Who this is in the tape's cast, numbered as the tape casts them; their look is drawn from it.</summary>
    public int Character;

    /// <summary>Seen from the chest up in a facecam, the way a streamer is.</summary>
    public bool Bust;
}

/// <summary>Someone cast in a tape: the starting gear they are dressed for, and their place in the cast.</summary>
public readonly record struct CMUTapeCast(string Outfit, int Character);

/// <summary>A weather symbol on the map, with what it says beside it.</summary>
public readonly record struct CMUTapeWeather(string Icon, string Label, Vector2 At);

/// <summary>One shot of a tape: what is behind, who stands in it, and what is said, line by line.</summary>
public sealed class CMUTapeScene
{
    /// <summary>studio, field, weather, ad, food, bunk, wild or card.</summary>
    public string Kind = "studio";

    public string? Background;
    public float Duration = 8;

    /// <summary>Spoken or captioned, one after another over the scene.</summary>
    public readonly List<string> Lines = new();

    /// <summary>The headline or name strap across the lower third.</summary>
    public string? Strap;

    /// <summary>A news crawl along the bottom.</summary>
    public string? Ticker;

    public readonly List<CMUTapeActor> Actors = new();
    public readonly List<CMUTapeWeather> Weather = new();

    /// <summary>The ad picture, in an ad break.</summary>
    public string? Ad;

    /// <summary>A sportsbook match replayed in its arena, from <see cref="MatchFrom"/> seconds in.</summary>
    public CMUBetMatch? Match;

    public float MatchFrom;

    /// <summary>An arcade game playing itself, for a streamer to talk over.</summary>
    public CMUArcadeGame? Game;
}

/// <summary>A whole tape, built from its seed.</summary>
public sealed class CMUTape
{
    public int Seed;
    public string Genre = "news";
    public string Title = string.Empty;
    public string Channel = string.Empty;
    public int Rewinds;
    public readonly List<CMUTapeScene> Scenes = new();

    public float Length => Scenes.Sum(s => s.Duration);
}

/// <summary>
/// Makes YouTape's random tapes: a genre, then a run of scenes told from the grammar, with ad breaks between the
/// segments. The same seed and grammar always make the same tape.
/// </summary>
public static class CMUTapeGenerator
{
    public static readonly string[] Genres = { "news", "news", "weather", "food", "bunk", "doc", "sports", "sports", "play", "play" };

    private static readonly CMUArcadeGame[] Games = { CMUArcadeGame.HuggerRun, CMUArcadeGame.TrackerReflex };

    private const int MaxDepth = 12;

    /// <param name="results">The round's finished sportsbook matches; sports tapes replay these when there are any.</param>
    /// <param name="genre">Makes a tape of this genre rather than a random one, for links to a particular tape.</param>
    public static CMUTape Generate(int seed, CMUTapeGrammarPrototype grammar, string colony, IReadOnlyList<CMUBetMatch>? results = null,
        string? genre = null)
    {
        // Scrambled first: the runtime's generator starts nearby seeds on nearly the same numbers.
        var random = new System.Random(Scramble(seed));
        var teller = new Teller(grammar, random, colony);
        var tape = new CMUTape { Seed = seed, Genre = Genres[random.Next(Genres.Length)] };
        if (genre != null)
            tape.Genre = genre;

        // Sports and let's plays are about something: picked first, so the title can name it.
        CMUBetMatch? match = null;
        CMUArcadeGame game = default;
        if (tape.Genre == "sports")
        {
            match = results is { Count: > 0 } && teller.Chance(0.6) ? teller.Pick(results) : Synthetic(random, seed);
            teller.Fill["{sport}"] = Loc.GetString($"cmu-sport-{match.Sport}");
            teller.Fill["{match}"] = MatchName(match);
            teller.Fill["{winner}"] = match.Name(match.Winner);
        }
        else if (tape.Genre == "play")
        {
            game = teller.Pick(Games);
            teller.Fill["{game}"] = Loc.GetString($"cmu-arcade-{game}");
        }

        tape.Title = teller.Say($"#title.{tape.Genre}#");
        tape.Channel = teller.Say($"#channel.{tape.Genre}#");
        tape.Rewinds = (int) Math.Pow(10, 1 + random.NextDouble() * 5);

        // Some tapes start on a card, the way a dub off the relay does.
        if (random.NextDouble() < 0.35)
            tape.Scenes.Add(Card(teller, random.NextDouble() < 0.5 ? "card" : "standby", 3));

        switch (tape.Genre)
        {
            case "news":
                News(tape, teller);
                break;
            case "weather":
                Forecast(tape, teller, true);
                break;
            case "sports":
                Sports(tape, teller, match!);
                break;
            case "play":
                Play(tape, teller, game);
                break;
            case "static":
                Static(tape, teller);
                break;
            default:
                Show(tape, teller, tape.Genre);
                break;
        }

        return tape;
    }

    private static void News(CMUTape tape, Teller teller)
    {
        var anchor = teller.Cast("anchor");
        var headline = teller.Say("#news.headline#");
        var ticker = string.Join("   ///   ", Enumerable.Range(0, 4).Select(_ => teller.Say("#news.crawl#")));

        var open = teller.Scene("studio", 9);
        open.Strap = headline;
        open.Ticker = ticker;
        open.Lines.Add(teller.Say("#news.open#"));
        open.Lines.Add(teller.Say("#news.story#"));
        open.Actors.Add(Person(anchor, new Vector2(0.5f, 0.86f)));
        tape.Scenes.Add(open);

        var reporter = teller.Cast("reporter");
        var field = teller.Scene("field", 10);
        field.Strap = teller.Say("#news.reporter#");
        field.Lines.Add(teller.Say("#news.field#"));
        field.Lines.Add(teller.Say("#news.field#"));
        field.Lines.Add(teller.Say("#news.witness#"));
        field.Actors.Add(Person(reporter, new Vector2(0.3f, 0.9f)));
        if (teller.Chance(0.7))
            field.Actors.Add(Person(teller.Cast("guest"), new Vector2(0.62f, 0.9f), false));
        teller.Props(field, "field", 1);
        tape.Scenes.Add(field);

        var back = teller.Scene("studio", 8);
        back.Strap = teller.Say("#news.headline#");
        back.Ticker = ticker;
        back.Lines.Add(teller.Say("#news.followup#"));
        back.Lines.Add(teller.Say("#news.toss#"));
        back.Actors.Add(Person(anchor, new Vector2(0.5f, 0.86f)));
        tape.Scenes.Add(back);

        tape.Scenes.Add(Ad(teller));
        Forecast(tape, teller, false);

        var close = teller.Scene("studio", 7);
        close.Ticker = ticker;
        close.Lines.Add(teller.Say("#news.close#"));
        close.Actors.Add(Person(anchor, new Vector2(0.5f, 0.86f)));
        tape.Scenes.Add(close);
    }

    /// <summary>A weather report: the presenter, the map with its symbols, and on a full show an outlook after an ad.</summary>
    private static void Forecast(CMUTape tape, Teller teller, bool full)
    {
        var presenter = teller.Cast("presenter");
        var map = teller.Scene("weather", 10);
        map.Strap = teller.Say("#weather.strap#");
        map.Lines.Add(teller.Say("#weather.open#"));
        var conditions = teller.Conditions();
        var spots = new[] { new Vector2(0.5f, 0.3f), new Vector2(0.78f, 0.36f), new Vector2(0.56f, 0.62f), new Vector2(0.84f, 0.7f) };
        foreach (var spot in spots)
        {
            var condition = teller.Pick(conditions);
            var temperature = teller.Range(-30, 42);
            var place = teller.Say("#weather.place#");
            map.Weather.Add(new CMUTapeWeather(condition, $"{place} {temperature}", spot));
            if (map.Lines.Count < 3)
                map.Lines.Add(teller.Say($"#weather.{condition}#").Replace("{place}", place).Replace("{temp}", temperature.ToString()));
        }

        map.Actors.Add(Person(presenter, new Vector2(0.18f, 0.9f)));
        tape.Scenes.Add(map);

        if (!full)
            return;

        tape.Scenes.Add(Ad(teller));
        var outlook = teller.Scene("weather", 9);
        outlook.Strap = teller.Say("#weather.outlook#");
        var days = teller.Say("#weather.days#").Split(',');
        for (var i = 0; i < 4 && i < days.Length; i++)
        {
            var condition = teller.Pick(conditions);
            outlook.Weather.Add(new CMUTapeWeather(condition, $"{days[i].Trim()} {teller.Range(-30, 42)}", new Vector2(0.4f + i * 0.16f, 0.45f)));
        }

        outlook.Lines.Add(teller.Say("#weather.close#"));
        outlook.Actors.Add(Person(presenter, new Vector2(0.18f, 0.9f)));
        tape.Scenes.Add(outlook);
        if (teller.Chance(0.5))
            tape.Scenes.Add(Ad(teller));
    }

    /// <summary>A home tape: a host in their own place telling it, with props, and an ad dubbed in halfway.</summary>
    private static void Show(CMUTape tape, Teller teller, string genre)
    {
        var host = teller.Cast("host");
        var place = genre switch
        {
            "food" => "food",
            "bunk" => "bunk",
            _ => "wild",
        };

        var segments = teller.Range(3, 5);
        for (var i = 0; i < segments; i++)
        {
            var key = i == 0 ? "open" : i == segments - 1 ? "close" : "story";
            var scene = teller.Scene(place, teller.Range(7, 10));
            scene.Lines.Add(teller.Say($"#{genre}.{key}#"));
            if (key == "story")
                scene.Lines.Add(teller.Say($"#{genre}.story#"));

            if (i == 0)
                scene.Strap = tape.Channel;

            // The documentary has a narrator, not a host in shot.
            if (genre != "doc")
                scene.Actors.Add(Person(host, new Vector2(teller.Chance(0.5) ? 0.32f : 0.66f, 0.9f)));
            teller.Props(scene, genre, genre == "doc" ? 2 : 1);
            tape.Scenes.Add(scene);

            if (i == segments / 2 && segments > 2)
                tape.Scenes.Add(Ad(teller));
        }
    }

    /// <summary>
    /// The sports desk: the caster sets it up, the match plays in its own arena (the whole sprint, the closing stretch of
    /// anything longer, through to the arena's result banner), then the caster calls it.
    /// </summary>
    private static void Sports(CMUTape tape, Teller teller, CMUBetMatch match)
    {
        var caster = teller.Cast("sportscaster");
        var sport = Loc.GetString($"cmu-sport-{match.Sport}").ToUpperInvariant();
        var open = teller.Scene("sports", 9);
        open.Strap = $"{sport}: {MatchName(match)}";
        open.Lines.Add(teller.Say("#sports.open#"));
        open.Actors.Add(Person(caster, new Vector2(0.5f, 0.9f)));
        tape.Scenes.Add(open);

        var length = CMUSports.Length(match.Sport);
        var shown = match.Sport == CMUSport.Sprint ? length : Math.Min(length, teller.Range(35, 55));
        tape.Scenes.Add(new CMUTapeScene
        {
            Kind = "match",
            Duration = shown + 6,
            Match = match,
            MatchFrom = Math.Max(0, length - shown),
        });

        if (teller.Chance(0.5))
            tape.Scenes.Add(Ad(teller));

        var close = teller.Scene("sports", 8);
        close.Strap = $"{sport}: {match.Name(match.Winner).ToUpperInvariant()}";
        close.Lines.Add(teller.Say("#sports.close#"));
        close.Actors.Add(Person(caster, new Vector2(0.5f, 0.9f)));
        tape.Scenes.Add(close);
    }

    /// <summary>A streamer playing an arcade game: the game plays itself, the streamer talks over it from a facecam.</summary>
    private static void Play(CMUTape tape, Teller teller, CMUArcadeGame game)
    {
        var streamer = teller.Cast("streamer");
        var segments = teller.Range(3, 5);
        for (var i = 0; i < segments; i++)
        {
            var key = i == 0 ? "open" : i == segments - 1 ? "close" : "story";
            var scene = new CMUTapeScene { Kind = "play", Duration = teller.Range(14, 22), Game = game };
            scene.Lines.Add(teller.Say($"#play.{key}#"));
            if (key == "story")
                scene.Lines.Add(teller.Say("#play.story#"));

            if (i == 0)
                scene.Strap = tape.Channel;

            scene.Actors.Add(new CMUTapeActor
            {
                Outfit = streamer.Outfit,
                Character = streamer.Character,
                Bust = true,
                Talking = true,
            });
            tape.Scenes.Add(scene);
        }
    }

    /// <summary>Nothing but cards and static, with something said in it now and then that should not be.</summary>
    private static void Static(CMUTape tape, Teller teller)
    {
        tape.Scenes.Clear();
        var cards = teller.Range(4, 6);
        for (var i = 0; i < cards; i++)
        {
            var scene = teller.Scene(i % 2 == 0 ? "standby" : "card", teller.Range(6, 9));
            scene.Lines.Add(teller.Say(i == cards - 1 ? "#static.last#" : "#static.line#"));
            tape.Scenes.Add(scene);
        }
    }

    /// <summary>The kinds of reel: short, tall, one shot, a hook across the top.</summary>
    public static readonly string[] ReelKinds = { "food", "bunk", "doc", "weather", "play", "play" };

    /// <summary>
    /// A reel: one short, tall shot of a food test, a bunk moment, a creature, a forecast or a game clip with its
    /// streamer, its hook as the strap and a line or two over it. Like-counted rather than rewound.
    /// </summary>
    public static CMUTape Reel(int seed, CMUTapeGrammarPrototype grammar, string colony)
    {
        var random = new System.Random(Scramble(seed));
        var teller = new Teller(grammar, random, colony);
        var kind = ReelKinds[random.Next(ReelKinds.Length)];
        var tape = new CMUTape { Seed = seed, Genre = "reel" };
        CMUArcadeGame game = default;
        if (kind == "play")
        {
            game = teller.Pick(Games);
            teller.Fill["{game}"] = Loc.GetString($"cmu-arcade-{game}");
        }

        tape.Title = teller.Say($"#reel.{kind}.title#");
        tape.Channel = teller.Say($"#channel.{kind}#");
        tape.Rewinds = (int) Math.Pow(10, 2 + random.NextDouble() * 4);

        var place = kind switch
        {
            "doc" => "wild",
            _ => kind,
        };
        var scene = kind == "play"
            ? new CMUTapeScene { Kind = "play", Duration = teller.Range(12, 16), Game = game }
            : teller.Scene(place, teller.Range(10, 14));
        scene.Strap = tape.Title;
        scene.Lines.Add(teller.Say($"#reel.{kind}.line#"));
        scene.Lines.Add(teller.Say($"#reel.{kind}.line#"));
        switch (kind)
        {
            case "play":
                var streamer = teller.Cast("streamer");
                scene.Actors.Add(new CMUTapeActor { Outfit = streamer.Outfit, Character = streamer.Character, Bust = true, Talking = true });
                break;
            case "weather":
                var condition = teller.Pick(teller.Conditions());
                var where = teller.Say("#weather.place#");
                var temperature = teller.Range(-30, 42);
                scene.Weather.Add(new CMUTapeWeather(condition, $"{where} {temperature}", new Vector2(0.5f, 0.32f)));
                scene.Lines[1] = teller.Say($"#weather.{condition}#").Replace("{place}", where).Replace("{temp}", temperature.ToString());
                scene.Actors.Add(Person(teller.Cast("presenter"), new Vector2(0.5f, 0.92f)));
                break;
            case "doc":
                teller.Props(scene, "doc", 2);
                break;
            default:
                scene.Actors.Add(Person(teller.Cast("host"), new Vector2(0.5f, 0.92f)));
                teller.Props(scene, kind, 1);
                break;
        }

        tape.Scenes.Add(scene);
        return tape;
    }

    /// <summary>A match nobody bet on: made up from the tape's own seed, played out the same way as a real one.</summary>
    private static CMUBetMatch Synthetic(System.Random random, int seed)
    {
        var sport = (CMUSport) random.Next(4);
        var count = CMUSports.Entrants(sport);
        var pool = CMUSports.UsesTeams(sport) ? CMUSports.Teams.Length : CMUSports.Athletes.Length;
        var entrants = Enumerable.Range(0, pool).OrderBy(_ => random.Next()).Take(count).ToList();
        var odds = Enumerable.Repeat(2f, count).ToList();
        var matchSeed = random.Next();
        var winner = random.Next(count);
        var result = CMUSports.Play(sport, matchSeed, count, winner).Result;
        // Crowds take their looks from the id, so every made up match has its own.
        return new CMUBetMatch(1_000_000 + (seed & 0xFFFFF), sport, entrants, odds, TimeSpan.Zero, matchSeed, winner, result);
    }

    private static int Scramble(int seed)
    {
        var x = (uint) seed;
        x ^= x >> 16;
        x *= 0x7FEB352D;
        x ^= x >> 15;
        x *= 0x846CA68B;
        x ^= x >> 16;
        return (int) (x & 0x7FFFFFFF);
    }

    private static string MatchName(CMUBetMatch match)
    {
        return match.Entrants.Count == 2
            ? $"{match.Name(0)} vs {match.Name(1)}"
            : $"{match.Name(0)}, {match.Name(1)} and {match.Entrants.Count - 2} more";
    }

    private static CMUTapeActor Person(CMUTapeCast cast, Vector2 at, bool talking = true)
    {
        return new CMUTapeActor { Outfit = cast.Outfit, Character = cast.Character, At = at, Talking = talking };
    }

    private static CMUTapeScene Ad(Teller teller)
    {
        var ad = teller.Scene("ad", 6);
        ad.Ad = teller.Ad();
        ad.Strap = teller.Say("#ad.product#");
        ad.Lines.Add(teller.Say("#ad.slogan#"));
        teller.Props(ad, "ad", 1);
        return ad;
    }

    private static CMUTapeScene Card(Teller teller, string kind, float duration)
    {
        var card = teller.Scene(kind, duration);
        card.Lines.Add(teller.Say($"#{kind}.line#"));
        return card;
    }

    /// <summary>Tells the grammar with the tape's own random numbers.</summary>
    private sealed class Teller(CMUTapeGrammarPrototype grammar, System.Random random, string colony)
    {
        public bool Chance(double p)
        {
            return random.NextDouble() < p;
        }

        public int Range(int min, int max)
        {
            return random.Next(min, max + 1);
        }

        public T Pick<T>(IReadOnlyList<T> list)
        {
            return list[random.Next(list.Count)];
        }

        private int _cast;

        /// <summary>Words filled in after the grammar has spoken, such as the match or game a tape is about.</summary>
        public readonly Dictionary<string, string> Fill = new();

        public string Outfit(string role)
        {
            return grammar.Outfits.TryGetValue(role, out var outfits) && outfits.Count > 0 ? Pick(outfits) : string.Empty;
        }

        /// <summary>Casts someone for a role: an outfit, and the next place in the cast.</summary>
        public CMUTapeCast Cast(string role)
        {
            return new CMUTapeCast(Outfit(role), _cast++);
        }

        public string? Ad()
        {
            return grammar.Ads.Count > 0 ? Pick(grammar.Ads) : null;
        }

        public List<string> Conditions()
        {
            var conditions = grammar.Weather.Keys.ToList();
            conditions.Sort(string.CompareOrdinal);
            return conditions.Count > 0 ? conditions : new List<string> { "sun" };
        }

        public CMUTapeScene Scene(string kind, float duration)
        {
            var scene = new CMUTapeScene { Kind = kind, Duration = duration };
            if (grammar.Backgrounds.TryGetValue(kind, out var backgrounds) && backgrounds.Count > 0)
                scene.Background = Pick(backgrounds);

            return scene;
        }

        public void Props(CMUTapeScene scene, string kind, int count)
        {
            if (!grammar.Props.TryGetValue(kind, out var props) || props.Count == 0)
                return;

            for (var i = 0; i < count; i++)
            {
                var x = 0.45f + (float) random.NextDouble() * 0.45f;
                scene.Actors.Add(new CMUTapeActor { Prop = Pick(props), At = new Vector2(x, 0.88f), Scale = 3 });
            }
        }

        /// <summary>Expands every <c>#rule#</c> in the text, rules within rules, then puts in the colony.</summary>
        public string Say(string text)
        {
            var said = Expand(text, 0).Replace("{colony}", colony);
            foreach (var (key, value) in Fill)
                said = said.Replace(key, value);

            return said;
        }

        private string Expand(string text, int depth)
        {
            if (depth > MaxDepth || !text.Contains('#'))
                return text;

            var output = new StringBuilder();
            var at = 0;
            while (at < text.Length)
            {
                var start = text.IndexOf('#', at);
                var end = start < 0 ? -1 : text.IndexOf('#', start + 1);
                if (start < 0 || end < 0)
                {
                    output.Append(text, at, text.Length - at);
                    break;
                }

                output.Append(text, at, start - at);
                var rule = text[(start + 1)..end];
                output.Append(grammar.Rules.TryGetValue(rule, out var lines) && lines.Count > 0
                    ? Expand(Pick(lines), depth + 1)
                    : rule);
                at = end + 1;
            }

            return output.ToString();
        }
    }
}
