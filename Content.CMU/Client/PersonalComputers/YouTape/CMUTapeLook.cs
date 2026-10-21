using System.Linq;
using Content.Client.Body;
using Content.Client.Station;
using Content.Shared.Body;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.PersonalComputers;

/// <summary>
/// How the people on a tape look and dress. Each character's look (sex, skin tone, eyes, hair and its colour, and
/// sometimes a beard) is drawn from the tape's seed and their place in the cast, so a tape always shows the same
/// cast. They are dressed the way a player in that job spawns: the job's default role loadout first, then the
/// starting gear the tape names on top (which only fills what the loadout left empty, and the hands).
/// </summary>
public static class CMUTapeLook
{
    public static readonly ProtoId<MarkingPrototype>[] MaleHair =
    {
        "HumanHairCrewcut", "HumanHairBusiness", "HumanHairBusiness2", "HumanHairBuzzcut", "HumanHairBedhead",
        "HumanHairAfro", "HumanHairSidecut", "HumanHairCombover", "HumanHairQuiff", "HumanHairPompadour",
        "HumanHairUndercut", "HumanHairMulder", "HumanHairGelled", "HumanHairSpiky", "HumanHairCornrows",
        "HumanHairLowfade", "HumanHairHighfade", "HumanHairFather", "HumanHairThinning",
    };

    public static readonly ProtoId<MarkingPrototype>[] FemaleHair =
    {
        "HumanHairPonytail", "HumanHairPonytail2", "HumanHairBob", "HumanHairBob2", "HumanHairBobcurl",
        "HumanHairBun", "HumanHairLongstraightponytail", "HumanHairPixie", "HumanHairBraid", "HumanHairPigtails",
        "HumanHairSidetail", "HumanHairHimecut", "HumanHairCurls", "HumanHairUpdo", "HumanHairLargebun",
        "HumanHairEmo", "HumanHairAfro", "HumanHairCornrows",
    };

    public static readonly ProtoId<MarkingPrototype>[] FacialHair =
    {
        "HumanFacialHairFullbeard", "HumanFacialHairMoustache", "HumanFacialHairFiveoclock", "HumanFacialHairVandyke",
        "HumanFacialHairChin", "HumanFacialHairGt", "HumanFacialHairMutton", "HumanFacialHairSmallstache",
        "HumanFacialHairSideburn", "HumanFacialHairCroppedfullbeard", "HumanFacialHair5oclockmoustache",
    };

    /// <summary>HumanoidCharacterAppearance's realistic eye colours.</summary>
    public static readonly Color[] Eyes = { Color.Brown, Color.Gray, Color.Azure, Color.SteelBlue, Color.Black };

    /// <summary>Natural hair colours, each nudged a little so no two heads are quite the same.</summary>
    public static readonly Color[] NaturalHair =
    {
        Color.FromHex("#1A1410"), Color.FromHex("#3B2416"), Color.FromHex("#6B4423"), Color.FromHex("#8D4A2B"),
        Color.FromHex("#B5532A"), Color.FromHex("#D8B66A"), Color.FromHex("#E8DDB5"), Color.FromHex("#9A9A9A"),
        Color.FromHex("#DDDDDD"),
    };

    /// <summary>The odd dyed head.</summary>
    public static readonly Color[] DyedHair =
    {
        Color.FromHex("#C2185B"), Color.FromHex("#1E88E5"), Color.FromHex("#43A047"), Color.FromHex("#8E24AA"),
        Color.FromHex("#E53935"), Color.FromHex("#FF7043"), Color.FromHex("#26C6DA"), Color.FromHex("#F5F5F5"),
    };

    public const double DyedChance = 0.1;
    public const double BeardChance = 0.35;

    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";

    /// <summary>The seed a character's look is drawn from.</summary>
    public static int LookSeed(int tapeSeed, int character)
    {
        return unchecked(tapeSeed * 31 + character + 1);
    }

    /// <summary>A character's profile: sex, skin, eyes, hair and beard drawn from the look seed.</summary>
    public static HumanoidCharacterProfile Profile(int look, IPrototypeManager prototypes)
    {
        var random = new System.Random(look);
        var female = random.Next(2) == 1;
        var tone = random.Next(101);
        var eyes = Eyes[random.Next(Eyes.Length)];
        var styles = female ? FemaleHair : MaleHair;
        var hair = styles[random.Next(styles.Length)];
        Color hairColor;
        if (random.NextDouble() < DyedChance)
        {
            hairColor = DyedHair[random.Next(DyedHair.Length)];
        }
        else
        {
            var natural = NaturalHair[random.Next(NaturalHair.Length)];
            var r = Nudge(natural.R, random);
            var g = Nudge(natural.G, random);
            var b = Nudge(natural.B, random);
            hairColor = new Color(r, g, b);
        }

        ProtoId<MarkingPrototype>? beard = null;
        if (!female && random.NextDouble() < BeardChance)
            beard = FacialHair[random.Next(FacialHair.Length)];

        var sex = female ? Sex.Female : Sex.Male;
        var species = prototypes.Index<SpeciesPrototype>(HumanoidCharacterProfile.DefaultSpecies);
        var skin = prototypes.Index(species.SkinColoration).Strategy.FromUnary(tone);
        var layers = new Dictionary<HumanoidVisualLayers, List<Marking>>
        {
            [HumanoidVisualLayers.Hair] = new() { new Marking(hair, new[] { hairColor }) },
        };
        if (beard != null)
            layers[HumanoidVisualLayers.FacialHair] = new() { new Marking(beard.Value, new[] { hairColor }) };

        var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>
        {
            [Head] = layers,
        };
        var appearance = HumanoidCharacterAppearance.EnsureValid(new HumanoidCharacterAppearance(eyes, skin, markings), species.ID, sex);
        return HumanoidCharacterProfile.DefaultWithSpecies(species.ID, sex)
            .WithGender(female ? Gender.Female : Gender.Male)
            .WithCharacterAppearance(appearance);
    }

    private static float Nudge(float channel, System.Random random)
    {
        return Math.Clamp(channel + (float) (random.NextDouble() * 0.12 - 0.06), 0f, 1f);
    }

    /// <summary>The job whose starting gear this is, the first by id that has a role loadout; null for none.</summary>
    public static JobPrototype? Job(string outfit, IPrototypeManager prototypes)
    {
        JobPrototype? first = null;
        foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>().OrderBy(j => j.ID, StringComparer.Ordinal))
        {
            if (job.StartingGear?.Id != outfit)
                continue;

            if (LoadoutSystem.GetJobLoadoutInfo(job.ID, prototypes).Prototype != null)
                return job;

            first ??= job;
        }

        return first;
    }

    /// <summary>
    /// Gives a doll its look and dresses it: as the lobby preview does, the profile goes onto the body, then the job's
    /// default role loadout, then the outfit. An outfit no job starts with is just the outfit.
    /// </summary>
    public static void Dress(EntityUid doll, string outfit, int look, IEntityManager entities, IPrototypeManager prototypes)
    {
        var profile = Profile(look, prototypes);
        entities.System<VisualBodySystem>().ApplyProfileTo(doll, profile);
        entities.System<HumanoidProfileSystem>().ApplyProfileTo(doll, profile);

        var spawning = entities.System<StationSpawningSystem>();
        if (Job(outfit, prototypes) is { } job
            && LoadoutSystem.GetJobLoadoutInfo(job.ID, prototypes).Prototype is { } role)
        {
            var loadout = new RoleLoadout(role.ID);
            loadout.SetDefault(profile, null, prototypes, force: true);
            spawning.EquipRoleLoadout(doll, loadout, role, applyEffects: false);
        }

        if (prototypes.HasIndex<StartingGearPrototype>(outfit))
            spawning.EquipStartingGear(doll, outfit);
    }
}
