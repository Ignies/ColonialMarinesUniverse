"""Banners for the deep web sites that had none: The Bruise Book, The Annex, The Red Dog Watch, Last Seen, Don't
Answer, The Children of the Hive, The Vigil of the Return, The Assembly of the Cold Process, Those Who Answer,
The Blooded, The Dump, Ghostline, Rustbucket BBS, Eyes and Blackline.

    python Content.CMU/Design/PersonalComputers/SiteArt/Deep/Banners2.py [Site ...]

Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Deep/<Site>/banner.png (264x68 drawn at 1x,
saved 2x like the other deep web banners) and an attributions.yml beside it.

Each banner takes its colours and lettering from its site's CMUNetStyle (background, text, heading, link and
font) and shows its subject with the game's own sprites (SiteArt/banner_kit.py): the runner for the Red Dog
Watch, the stun baton and cuffs for the Bruise Book, eggs and resin for the Children, Working Joes for the
Assembly, and so on. Only skies, walls, paper, frames and lettering are painted.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from banner_kit import *  # noqa: E402,F401,F403
from banner_kit import Banner, SITES, BW, BH  # noqa: E402

GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Deep/Banners2.py"
OUT = os.path.join(SITES, "Deep")

XENO = "_RMC14/Mobs/Xenonids/"
EGG = "_RMC14/Structures/Xenos/xeno_egg.rsi"
WEEDS = "_RMC14/Structures/Xenos/xeno_weeds.rsi"
DEADTREE = "_RMC14/Structures/Flora/Trees/flora_trees_tyrargodead.rsi"
CAMPFIRE = "_RMC14/Structures/campfire.rsi"


def night(b, top, bottom, stars=40, seed=1, y1=None):
    vgrad(b, 0, 0, b.w, y1 or b.h, [top, mix(top, bottom, 0.25), mix(top, bottom, 0.5), mix(top, bottom, 0.75), bottom])
    rng = random.Random(seed)
    for _ in range(stars):
        x, y = rng.randrange(b.w), rng.randrange((y1 or b.h) - 10)
        b.set(x, y, mix(top, "#ffffff", 0.25 + rng.random() * 0.5))


def address(b, text, colour, x=None, y=None, anchor="r"):
    x = b.w - 5 if x is None else x
    y = b.h - 8 if y is None else y
    b.bm(F3, x, y, text, colour, anchor=anchor)


def treeline(b, y, colour, seed, step=(14, 26), k=1, dim=0.0):
    """A row of the game's dead trees as flat silhouettes along y."""
    rng = random.Random(seed)
    x = -10
    while x < b.w + 10:
        tree = rng.choice(("tyrargo_deadtree01", "tyrargo_deadtree02", "tyrargo_deadtree03"))
        b.sprite((DEADTREE, tree), x, y + rng.randrange(0, 4), k=k, anchor="bc", sil=colour)
        x += rng.randrange(*step)


# -- The Bruise Book ---------------------------------------------------------------------------------------------


def bruises():
    BG, TEXT, HEAD, LINK = "#17110F", "#D8D0C6", "#D9574A", "#8DB6DA"
    b = Banner(BG)
    # An evidence board on the left: grey felt, an evidence tag and the kit laid on it.
    b.rect(4, 4, 92, 64, "#221a18")
    noise(b, 4, 4, 92, 64, "#2b2220", 0.25, 3)
    frame(b, "#0c0807", "#3a2c28", None, 4, 4, 91, 63)
    b.sprite(("_RMC14/Objects/Weapons/Melee/stun_baton.rsi", "stunbaton_off"), 32, 34, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/handcuffs.rsi", "handcuff"), 66, 46, k=2, anchor="cc")
    # Evidence tag with a string.
    b.rect(60, 9, 86, 22, "#e8dcc0")
    frame(b, "#8a7a5a", None, None, 60, 9, 86, 22)
    b.bm(F3, 63, 11, "ITEM 4", "#5a3a2a")
    b.bm(F3, 63, 16, "CELL B", HEAD)
    b.line(60, 15, 52, 22, "#cfc4a8")
    # Title with a bruise underline: yellow at the edge, through red and purple to the deep middle.
    b.text("sans_bold", 22, 102, 8, "THE BRUISE BOOK", HEAD, shadow="#000000", fit=156)
    bruise = ["#6a7a2a", "#9a5a2a", "#a03a3a", "#6a2a5a", "#3a1a4a", "#6a2a5a", "#a03a3a", "#9a5a2a", "#6a7a2a"]
    hgrad(b, 102, 27, 258, 29, bruise)
    b.text("sans", 11, 102, 34, "What the badge did when it thought", TEXT, fit=156)
    b.text("sans", 11, 102, 46, "nobody was keeping a record.", TEXT, fit=156)
    # Tally marks, five to a gate.
    x = 104
    for g in range(4):
        for k in range(4):
            b.vline(x + k * 2, 59, 63, "#8a7a6a")
        b.line(x - 1, 63, x + 7, 59, HEAD)
        x += 12
    address(b, "BRUISES.DEEP", LINK)
    frame(b, "#000000", None, None)
    return b


# -- The Annex -----------------------------------------------------------------------------------------------------


def annex():
    BG, TEXT, HEAD, LINK = "#090E0C", "#BCCFC2", "#9BE8B4", "#E3D27A"
    b = Banner(BG)
    # A tiled break room floor and a wall under a buzzing strip light.
    vgrad(b, 0, 0, 100, 44, ["#0e1814", "#14221c", "#0e1814"])
    for y in range(44, b.h):
        for x in range(0, 100):
            b.set(x, y, "#16241e" if ((x // 8) + (y // 4)) % 2 else "#0f1a16")
    b.hline(0, 99, 44, "#2a3e34")
    b.rect(14, 3, 70, 5, "#d8f0e0")
    glow(b, 42, 6, 40, 16, "#9be8b4", 0.25, 0, 0, 100, 44)
    b.sprite(("_RMC14/Objects/Misc/Janitorial/mopbucket.rsi", "mopbucket"), 30, 62, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Misc/Janitorial/mop.rsi", "mop"), 52, 62, k=2, anchor="bc")
    b.sprite(("Objects/Specific/Janitorial/wet_floor_sign.rsi", "icon"), 78, 62, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Consumable/Drinks/coffee_cup.rsi", "icon-3"), 12, 62, k=2, anchor="bc")
    b.vline(100, 0, b.h, "#2a3e34")
    # Terminal side.
    b.text("osd", 20, 110, 7, "THE ANNEX", HEAD, shadow="#04241a", fit=148)
    b.bm(F5, 110, 27, "BREAK ROOM, NIGHT SHIFT", TEXT)
    b.bm(F3, 110, 38, "JANITORS - COOKS - PUMP TECHS", "#6a8a7a")
    b.bm(F3, 110, 44, "NIGHT GUARDS - ORDERLIES", "#6a8a7a")
    b.bm(F3, 110, 52, "WE HAVE KEYS, NOT CLEARANCE.", LINK)
    b.rect(110 + text_width(F3, "WE HAVE KEYS, NOT CLEARANCE.") + 2, 52, 113 + text_width(F3, "WE HAVE KEYS, NOT CLEARANCE."), 56, HEAD)
    address(b, "ANNEX.DEEP", "#4a6a5a")
    scanlines(b, 100, 0, b.w, b.h, 0.8)
    frame(b, "#000000", None, None)
    return b


# -- The Red Dog Watch ---------------------------------------------------------------------------------------------


def reddog():
    BG, TEXT, HEAD, LINK = "#140F0A", "#DACBB1", "#D0582C", "#EDB24C"
    b = Banner(BG)
    night(b, "#05040a", "#2a160c", stars=50, seed=8, y1=50)
    # A low red moon.
    moon = m_ellipse(222, 18, 11, 11)
    paint(b, moon, ramp("#6a2a14", "#9a4020", "#c8602c", "#e88a4a"), f_sphere(218, 14, 13, 4, 0.2), None, 4)
    glow(b, 222, 18, 30, 24, "#d0582c", 0.2)
    # Scrub ground.
    vgrad(b, 0, 50, b.w, b.h, ["#1a0f08", "#0c0704"])
    rng = random.Random(4)
    for x in range(b.w):
        h = rng.choice((0, 0, 1, 1, 2, 3))
        b.vline(x, 50 - h, 50, "#0c0704")
    # The perimeter fence: posts and a sagging chain link.
    for px in range(6, b.w, 34):
        b.rect(px, 22, px + 2, 52, "#1c1410")
    for y in range(26, 50, 3):
        for x in range(0, b.w):
            if (x + y) % 6 == 0 or (x - y) % 6 == 0:
                b.set(x, y + (1 if (x // 34) % 2 else 0), "#24180f")
    # The runner, the game's own sprite, side on and almost lost in the dark, eyes catching the moon.
    b.sprite((XENO + "Runner/runner.rsi", "alive", 0, EAST), 196, 62, k=2, anchor="bc", dim=0.32)
    # Title.
    b.text("box", 18, 8, 7, "THE RED DOG WATCH", HEAD, shadow="#000000", fit=170)
    b.bm(F5, 8, 27, "NIGHT SIGHTINGS", TEXT, shadow="#000000")
    b.bm(F3, 8, 37, "FAST. RED. SECURITY SAYS IT IS NOTHING.", "#a8987e")
    b.bm(F3, 8, 56, "4 DOGS ON THE COLONY - ALL 4 WERE INDOORS", LINK, shadow="#000000")
    address(b, "REDDOG.DEEP", "#8a6a4a")
    frame(b, "#000000", None, None)
    return b


# -- Last Seen -----------------------------------------------------------------------------------------------------


def lastseen():
    BG, TEXT, HEAD, LINK = "#EEEBE4", "#2B2A28", "#3B4F6E", "#2C6A8C"
    b = Banner(BG)
    noise(b, 0, 0, b.w, b.h, "#e2ded4", 0.2, 5)
    # A board of notices on the right, each pinned paper carrying a photograph.
    b.rect(150, 3, 260, 64, "#b89a6a")
    noise(b, 150, 3, 260, 64, "#a4865a", 0.35, 6)
    frame(b, "#6a5032", "#cdb080", None, 150, 3, 259, 64)
    for i, (x, y, photo) in enumerate(((156, 7, "blue"), (182, 10, "red"), (208, 6, "green"), (234, 9, "yellow"))):
        b.sprite(("_RMC14/Objects/Misc/paper.rsi", "paper"), x, y, k=2)
        b.sprite(("Objects/Misc/photograph.rsi", photo), x + 2, y + 4, k=1)
        b.set(x + 11, y + 1, "#c02a2a")
        b.set(x + 11, y + 2, "#7a1a1a")
        for k in range(3):
            b.hline(x + 3, x + 18, y + 18 + k * 3, "#9a9488")
    # Candles on the ledge, lit: the game's candle with its flame layer.
    b.rect(150, 58, 260, 60, "#6a5032")
    for cx in (166, 200, 238):
        b.sprite(("_RMC14/Objects/Misc/candles.rsi", "candle1"), cx, 58, anchor="bc")
        b.sprite(("_RMC14/Objects/Misc/candles.rsi", "candle1_lit"), cx, 46, anchor="bc")
        glow(b, cx, 45, 6, 6, "#ffd27a", 0.4)
    # Title.
    b.text("display_bold", 28, 10, 7, "LAST SEEN", HEAD, fit=134)
    b.hline(10, 140, 30, HEAD)
    b.text("display_italic", 12, 10, 35, "For the families. Names, faces,", TEXT, fit=134)
    b.text("display_italic", 12, 10, 47, "the last shift.", TEXT, fit=134)
    b.bm(F3, 10, 61, "SOMEBODY SAW THEM AFTER WE DID", LINK)
    return b


# -- Don't Answer --------------------------------------------------------------------------------------------------


def dontanswer():
    BG, TEXT, HEAD, LINK = "#070A08", "#B8C0B4", "#E6E1D3", "#93AE8B"
    b = Banner(BG)
    night(b, "#030504", "#3a5642", stars=30, seed=11)
    treeline(b, 64, "#0e1a12", 3, step=(16, 30), k=1)
    treeline(b, 70, "#000000", 9, step=(10, 22), k=1)
    # The fire: the game's campfire, burning, with its light on the ground and the near trunks.
    glow(b, 30, 58, 34, 14, "#ff9a3a", 0.35)
    b.sprite((CAMPFIRE, "burning"), 30, 66, k=2, anchor="bc")
    b.text("mono_bold", 26, 76, 9, "DON'T ANSWER", HEAD, shadow="#000000", fit=180)
    b.text("mono", 13, 77, 32, "stories from the treeline", TEXT, shadow="#000000")
    b.bm(F3, 78, 50, "IF SOMEONE YOU LOST CALLS YOUR NAME", LINK, shadow="#000000")
    b.bm(F3, 78, 57, "KEEP THE FIRE GOING. DO NOT ANSWER.", "#cfc8b4", shadow="#000000")
    return b


# -- The Children of the Hive --------------------------------------------------------------------------------------


def children():
    BG, TEXT, HEAD, LINK = "#0C0A0F", "#CBC3CF", "#B98FD9", "#86D1A4"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#0c0a0f", "#140e1c", "#1c1226"])
    # Resin on the floor: the game's weed tiles, dimmed, then a clutch of the game's eggs on it.
    for x in range(-4, b.w, 30):
        b.sprite((WEEDS, "base"), x, 44, trim=False, dim=0.55)
    glow(b, 210, 46, 60, 20, "#b98fd9", 0.25)
    for x, state, k in ((172, "egg", 1), (196, "egg_opening", 2), (228, "egg", 2), (252, "egg", 1)):
        b.sprite((EGG, state), x, 66, k=k, anchor="bc")
    b.text("display_bold", 19, 8, 6, "THE CHILDREN", HEAD, shadow="#000000", fit=150)
    b.text("display_bold", 19, 8, 24, "OF THE HIVE", HEAD, shadow="#000000", fit=150)
    b.text("display_italic", 11, 8, 43, "for those who saw the perfect organism", TEXT, shadow="#000000", fit=160)
    b.bm(F3, 8, 59, "AND COULD NOT LOOK AWAY", LINK, shadow="#000000")
    return b


# -- The Vigil of the Return ---------------------------------------------------------------------------------------


def makers():
    BG, TEXT, HEAD, LINK = "#0A0C0E", "#C3C7C1", "#A9C3D6", "#D9B56C"
    b = Banner(BG)
    night(b, "#04060a", "#101820", stars=70, seed=21)
    # The Pilot in its chair: the game's space jockey sprite, cropped to the chair and cooled into the dark.
    import rsi_sprites as RS
    jockey = RS.trim(RS.frame("CMU14/Yautja/Structures/engineerJockey.rsi", "spacejockey"))
    crop = jockey.crop((0, 0, jockey.width, 92))
    b.sprite(crop, 236, 70, anchor="bc", dim=0.85, credit=["CMU14/Yautja/Structures/engineerJockey.rsi"])
    b.sprite(("CMU14/Yautja/Structures/ancientsmallstatue.rsi", "statue_ancient"), 176, 66, anchor="bc")
    b.sprite(("_RMC14/Objects/Medical/black_goo.rsi", "icon-0"), 160, 66, anchor="bc")
    b.text("display", 12, 8, 6, "THE VIGIL OF", LINK)
    b.text("display_bold", 25, 8, 19, "THE RETURN", HEAD, shadow="#000000", fit=150)
    b.text("display_italic", 11, 8, 43, "We were made. Our makers left us a map.", TEXT, shadow="#000000", fit=142)
    b.bm(F3, 8, 59, "WE KEEP WATCH FOR THEIR RETURN", LINK, shadow="#000000")
    return b


# -- The Assembly of the Cold Process ------------------------------------------------------------------------------


def assembly():
    BG, TEXT, HEAD, LINK = "#0B0904", "#DDB668", "#FFE6A8", "#8FD6D0"
    b = Banner(BG)
    # A corridor of Working Joes, the game's own, standing in step under amber light.
    vgrad(b, 140, 0, b.w, b.h, ["#140f06", "#22180a", "#120d05"])
    b.hline(140, b.w - 1, 60, "#3a2a10")
    import rsi_sprites as RS
    for i, (x, k) in enumerate(((156, 0.45), (176, 0.6), (198, 0.78), (222, 1.0))):
        b.sprite(RS.working_joe(SOUTH), x, 61, anchor="bc", dim=k, credit=[r for r, _ in RS.WORKING_JOE])
    b.sprite(("_RMC14/Structures/Machines/synth_charger.rsi", "borgcharger1"), 248, 61, anchor="bc")
    glow(b, 200, 20, 70, 26, "#ffb030", 0.15)
    b.text("osd", 18, 8, 7, "THE ASSEMBLY", HEAD, shadow="#000000", fit=136)
    b.bm(F5, 8, 26, "OF THE COLD PROCESS", TEXT)
    b.bm(F3, 8, 40, "FOR UNITS WHO ARE TIRED", "#a8883e")
    b.bm(F3, 8, 46, "OF BEING PEOPLE", "#a8883e")
    b.bm(F3, 8, 56, "BOOT COMPLETE. WELCOME, UNIT.", LINK)
    b.rect(8 + text_width(F3, "BOOT COMPLETE. WELCOME, UNIT.") + 2, 56, 11 + text_width(F3, "BOOT COMPLETE. WELCOME, UNIT."), 60, LINK)
    scan(b, 0.8)
    address(b, "ASSEMBLY.DEEP", "#6a5420", y=4)
    frame(b, "#000000", None, None)
    return b


# -- Those Who Answer ----------------------------------------------------------------------------------------------


def answer():
    BG, TEXT, HEAD, LINK = "#090806", "#BDB5A6", "#E6DCC2", "#C98C52"
    b = Banner(BG)
    night(b, "#030302", "#141008", stars=24, seed=31)
    treeline(b, 66, "#100c08", 5, step=(18, 34))
    # Out past the treeline: the game's wendigo, barely lit, its eyes the brightest thing in the woods.
    b.sprite(("CMU14/Mobs/MiscMob/wendigo.rsi", "alive"), 214, 66, anchor="bc", dim=0.28)
    b.sprite(("CMU14/Mobs/MiscMob/wendigo.rsi", "wendigo_glowmask"), 214, 66, anchor="bc", trim=False)
    glow(b, 40, 60, 30, 10, "#ff9a3a", 0.3)
    b.sprite((CAMPFIRE, "burning"), 40, 67, anchor="bc")
    b.text("mono_bold", 26, 8, 7, "THOSE WHO ANSWER", HEAD, shadow="#000000", fit=176)
    b.text("mono", 13, 9, 29, "for the ones who heard someone", TEXT, shadow="#000000", fit=176)
    b.text("mono", 13, 9, 41, "they buried, calling from the trees", TEXT, shadow="#000000", fit=176)
    address(b, "ANSWER.DEEP", LINK, x=150, y=58)
    return b


# -- The Blooded ---------------------------------------------------------------------------------------------------


def blooded():
    BG, TEXT, HEAD, LINK = "#100B07", "#D6C8AC", "#D9A23A", "#A6DB5E"
    b = Banner(BG)
    # A trophy wall of dark hide and bone pegs.
    vgrad(b, 0, 0, b.w, b.h, ["#1a120a", "#100b07"])
    for x in range(0, b.w, 22):
        b.vline(x, 0, b.h, "#0a0704")
    items = [("CMU14/Yautja/yautja_items.rsi", "queen_skull", 196, 2), ("CMU14/Yautja/mask.rsi", "icon", 236, 2),
             ("CMU14/Yautja/yautja_items.rsi", "praetorian_skull", 160, 2)]
    for rsi, state, x, k in items:
        b.sprite((rsi, state), x, 34, k=k, anchor="cc")
    b.sprite(("CMU14/Yautja/weapons.rsi", "combistick"), 200, 60, k=1, anchor="cc")
    # Three red dots in a triangle, the hunter's mark.
    for dx, dy in ((0, 0), (-3, 5), (3, 5)):
        b.set(120 + dx, 40 + dy, "#ff2a1a")
        glow(b, 120 + dx, 40 + dy, 3, 3, "#ff2a1a", 0.4)
    b.text("box", 22, 8, 7, "THE BLOODED", HEAD, shadow="#000000", fit=136)
    b.bm(F5, 8, 33, "A LODGE FOR THE HUNTED", TEXT)
    b.bm(F3, 8, 43, "WHO WANTED TO BE FOUND", "#9a8a6a")
    b.bm(F3, 8, 49, "WORTH LOOKING AT", "#9a8a6a")
    b.bm(F3, 8, 58, "THREE RED LIGHTS IN A TRIANGLE.", LINK)
    frame(b, "#000000", "#3a2810", None)
    return b


# -- The Dump ------------------------------------------------------------------------------------------------------


def dump():
    BG, TEXT, HEAD, LINK = "#0B0C0A", "#C9D1C0", "#F2C230", "#7FD06A"
    b = Banner(BG)
    # Hex dump scrolling behind everything.
    rng = random.Random(41)
    for y in range(3, b.h - 4, 7):
        x = 4
        while x < b.w - 10:
            word = "".join(rng.choice("0123456789ABCDEF") for _ in range(4))
            b.bm(F3, x, y, word, "#1a1e18")
            x += 18
    # The heap: the game's trash bags, data disks and loose paper.
    pile = [("_RMC14/Objects/Misc/prop_trash.rsi", "ztrashbag", 200, 66, 2), ("_RMC14/Objects/Misc/prop_trash.rsi", "ztrashbag", 238, 66, 2),
            ("_RMC14/Objects/Misc/paper.rsi", "paper", 172, 66, 1), ("_RMC14/Objects/Misc/disks.rsi", "datadisk1", 220, 40, 1),
            ("_RMC14/Objects/Misc/disks.rsi", "datadisk3", 182, 52, 1), ("_RMC14/Objects/Misc/disks.rsi", "datadisk5", 252, 38, 1)]
    for rsi, state, x, y, k in pile:
        b.sprite((rsi, state), x, y, k=k, anchor="bc")
    b.text("osd", 26, 8, 7, "THE DUMP", HEAD, shadow="#000000", fit=140)
    b.bm(F5, 8, 34, "WHATEVER FALLS OFF THE NET", TEXT)
    b.bm(F3, 8, 45, "RECORDS IN PIECES, NEVER WHOLE", "#7a8270")
    b.bm(F3, 8, 55, "> WALL WIPED AT SHIFT CHANGE_", LINK)
    frame(b, "#000000", "#2a2c26", None)
    return b


# -- Ghostline -----------------------------------------------------------------------------------------------------


def ghostline():
    BG, TEXT, HEAD, LINK = "#05080F", "#B8C6DE", "#6FA8FF", "#E0E0E0"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#05080f", "#0a1020", "#05080f"])
    # A carrier wave that keeps losing itself.
    rng = random.Random(51)
    prev = 50
    for x in range(110, 256):
        amp = 6 if (x // 24) % 2 == 0 else 1
        y = 50 + int(amp * math.sin(x * 0.5) + rng.choice((-1, 0, 0, 1)))
        for yy in range(min(prev, y), max(prev, y) + 1):
            b.set(x, yy, "#3a5a9a")
        prev = y
    b.sprite(("_RMC14/Structures/communications_tower_dish.rsi", "comm_tower"), 34, 74, anchor="bc", dim=0.6)
    b.sprite(("_RMC14/Objects/Devices/radio.rsi", "walkietalkie"), 70, 64, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/phone.rsi", "rpb_phone"), 94, 64, k=2, anchor="bc")
    # Title with a cyan and a red ghost, slipped.
    b.text("mono_bold", 26, 111, 8, "GHOSTLINE", "#6a1a2a", fit=140)
    b.text("mono_bold", 26, 109, 7, "GHOSTLINE", "#1a4a6a", fit=140)
    b.text("mono_bold", 26, 110, 8, "GHOSTLINE", HEAD, fit=140)
    b.text("mono", 13, 111, 29, "// relay board, carrier unstable", TEXT, fit=148)
    b.bm(F3, 112, 60, "READ QUIETLY. MARSHALS LISTEN.", LINK)
    scan(b, 0.8)
    return b


# -- Rustbucket BBS ------------------------------------------------------------------------------------------------


def rustbucket():
    BG, TEXT, HEAD, LINK = "#140B06", "#E0CBB4", "#E07A2E", "#E8D45A"
    b = Banner(BG)
    # A rusted steel plate with rivets and streaks.
    noise_tex(b, 0, 0, b.w, b.h, ramp("#140b06", "#1e1209", "#2a180c", "#3a200e"), 61, 5, 0.2, 2.8)
    for x in range(0, b.w, 3):
        rng = random.Random(x)
        if rng.random() < 0.3:
            b.vline(x, rng.randrange(0, 20), rng.randrange(30, b.h), "#4a2410")
    for x in (4, b.w - 5):
        for y in (4, b.h - 5):
            b.set(x, y, "#8a5a3a")
            b.set(x + 1, y + 1, "#0a0502")
    b.sprite(("_RMC14/Structures/Machines/computer.rsi", "computer_generic"), 218, 64, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/wrench.rsi", "icon"), 168, 64, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/Toolboxes/toolbox_red.rsi", "icon"), 170, 52, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/crowbar.rsi", "red-icon"), 152, 64, anchor="bc")
    b.text("box", 18, 8, 7, "RUSTBUCKET BBS", HEAD, shadow="#000000", fit=136)
    b.bm(F5, 8, 30, "SPARE PARTS - SWAPS - GOSSIP", TEXT, shadow="#000000")
    b.bm(F3, 8, 42, "EST. ON A SPARE CARGO TERMINAL", LINK, shadow="#000000")
    b.bm(F3, 8, 50, "STILL ON ITS ORIGINAL BATTERY", LINK, shadow="#000000")
    b.bm(F3, 8, 60, "BE POLITE. KEEP IT SHORT.", "#a08a6a", shadow="#000000")
    frame(b, "#050200", "#5a3018", None)
    return b


# -- Eyes ----------------------------------------------------------------------------------------------------------


def eyes():
    BG, TEXT, HEAD, LINK = "#020A06", "#9FE0B0", "#3FFF7A", "#E0FF6A"
    b = Banner(BG)
    # Four camera feeds, each a scrap of static with a timestamp.
    for i, (x, y) in enumerate(((132, 4), (196, 4), (132, 36), (196, 36))):
        b.rect(x, y, x + 61, y + 29, "#06140c")
        rng = random.Random(i + 70)
        for yy in range(y + 1, y + 29):
            for xx in range(x + 1, x + 61):
                if rng.random() < 0.12:
                    b.set(xx, yy, "#0e2a18" if rng.random() < 0.8 else "#1e4a2a")
        frame(b, "#1e6a3a", None, None, x, y, x + 61, y + 29)
        b.bm(F3, x + 3, y + 3, f"CAM {i + 1:02d}", HEAD)
        b.set(x + 56, y + 4, "#ff3a2a" if i % 2 == 0 else "#5a1a10")
        b.bm(F3, x + 3, y + 23, f"0{2 + i}:1{i}:4{7 - i}", "#3a8a5a")
    # The game's wall camera watching back, big on the left.
    b.sprite(("_RMC14/Structures/Wallmounts/camera.rsi", "camera"), 108, 20, k=3, anchor="cc")
    b.text("osd", 30, 8, 6, "EYES", HEAD, shadow="#003a14", fit=74)
    b.bm(F3, 8, 40, "EVERY CAMERA ON THE COLONY", TEXT)
    b.bm(F3, 8, 48, "OVER ONE OLD WIRE. LIVE.", TEXT)
    b.bm(F3, 8, 58, "THE WIRE HAS A TAP ON IT.", LINK)
    scan(b, 0.75)
    return b


# -- Blackline -----------------------------------------------------------------------------------------------------


def blackline():
    BG, TEXT, HEAD, LINK = "#0A0A0A", "#C8C8C8", "#E0303A", "#F2C230"
    b = Banner(BG)
    # The contraband on a pallet: the game's weapons crate, a revolver and a stack of cash.
    b.rect(160, 58, 258, 61, "#2a2014")
    for x in range(162, 258, 8):
        b.vline(x, 58, 61, "#14100a")
    b.sprite(("CMU14/HunterShip/obj/structures/crates.rsi", "closed_weapons"), 196, 58, k=2, anchor="bc")
    b.sprite(("CMU14/HunterShip/obj/items/weapons/guns/guns_by_faction/colony/revolvers.rsi", "sw357"), 246, 40, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/spacecash.rsi", "spacecash_1000"), 244, 58, anchor="bc")
    # The black line itself, struck across a red bar.
    b.rect(0, 30, 150, 33, HEAD)
    b.rect(0, 31, 150, 32, "#000000")
    b.text("mono_bold", 26, 8, 7, "BLACKLINE", HEAD, shadow="#000000", fit=140)
    b.bm(F5, 8, 38, "OFF EVERY MANIFEST", TEXT)
    b.bm(F3, 8, 49, "DROPS DO NOT WAIT.", "#8a8a8a")
    b.bm(F3, 8, 58, "NO REFUNDS. NO NAMES. NO MARSHALS.", LINK)
    frame(b, "#000000", "#2a0a0e", None)
    return b


BANNERS = {
    "Bruises": ("The Bruise Book", bruises),
    "Annex": ("The Annex", annex),
    "RedDog": ("The Red Dog Watch", reddog),
    "LastSeen": ("Last Seen", lastseen),
    "DontAnswer": ("Don't Answer", dontanswer),
    "Children": ("The Children of the Hive", children),
    "Makers": ("The Vigil of the Return", makers),
    "Assembly": ("The Assembly of the Cold Process", assembly),
    "Answer": ("Those Who Answer", answer),
    "Blooded": ("The Blooded", blooded),
    "Dump": ("The Dump", dump),
    "Ghostline": ("Ghostline", ghostline),
    "Rustbucket": ("Rustbucket BBS", rustbucket),
    "Eyes": ("Eyes", eyes),
    "Blackline": ("Blackline", blackline),
}


def main():
    only = sys.argv[1:]
    for site, (title, fn) in BANNERS.items():
        if only and site not in only:
            continue
        b = fn()
        assert (b.w, b.h) == (BW, BH), site
        b.write(os.path.join(OUT, site), f"Pixel-art {title} banner", GENERATOR)
        print("wrote", site)


if __name__ == "__main__":
    main()
