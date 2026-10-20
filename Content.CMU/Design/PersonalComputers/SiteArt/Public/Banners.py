"""Banners for the public ExtraNet sites: Chroma Sleep, Celeste Varga-Ishikawa, Spin Okafor, Vex Starr, Rosie Ketch,
Rex Maddox, The Colony Tattler, Prosperity Today, Veil Truth, Casa Albarran, Frontier Leaf, The House of Balaji,
Lasalle Bionational Pharmacy, the Colony Clinic Dispensary, The Galley Binder, the Break Room Arcade and the
Zero-G Ball Sportsbook.

    python Content.CMU/Design/PersonalComputers/SiteArt/Public/Banners.py [Site ...]

Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Public/<Site>/banner.png (264x68 drawn at 1x,
saved 2x) and an attributions.yml beside it.

Each banner follows its site's CMUNetStyle colours and font, and its subject is shown with the game's own sprites
(SiteArt/banner_kit.py): the synthesizer and cassettes for Chroma Sleep, the cigars and humidor for Albarran,
the pill bottles for the pharmacy, the arcade cabinet for the arcade, the ball for the sportsbook. Only skies,
walls, labels, frames and lettering are painted.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from banner_kit import *  # noqa: E402,F401,F403
from banner_kit import Banner, SITES, BW, BH  # noqa: E402

GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Public/Banners.py"
OUT = os.path.join(SITES, "Public")


def sparkle(b, x, y, c, arm=2):
    b.set(x, y, "#ffffff")
    for k in range(1, arm + 1):
        for dx, dy in ((k, 0), (-k, 0), (0, k), (0, -k)):
            b.set(x + dx, y + dy, c)


def wy_mark(b, x, y, c, shadow=None):
    wy_logo(b, x, y, c, small=False, shadow=shadow)


# -- Chroma Sleep --------------------------------------------------------------------------------------------------


def chromasleep():
    BG, TEXT, HEAD, LINK = "#120A24", "#E6DDFF", "#FF4FD8", "#00F0FF"
    b = Banner(BG)
    hy = 46
    vgrad(b, 0, 0, b.w, hy, ["#120a24", "#2a0e4a", "#5a1468", "#a02a78", "#e05a7a"])
    # A striped synth sun going down behind the floor line.
    sun = m_ellipse(206, hy, 24, 24)
    for x, y in sun:
        if y < hy and not (y > hy - 14 and (hy - y) % 4 < 1 + (hy - y < 7)):
            b.set(x, y, pick(ramp("#ff4fd8", "#ff8a7a", "#ffd27a"), (hy - y) / 24 * 2, x, y, 4))
    # Perspective grid floor.
    b.rect(0, hy, b.w, b.h, "#0a0418")
    for k in range(1, 8):
        y = hy + int(k * k * 0.42) + 1
        if y < b.h:
            b.hline(0, b.w - 1, y, "#00a0c0")
    for k in range(-14, 15):
        b.line(132 + k * 6, hy, 132 + k * 30, b.h + 4, "#6a1a8a")
    b.hline(0, b.w - 1, hy, "#00f0ff")
    # The band's kit: the game's synthesizer and a scatter of cassettes.
    b.sprite(("Objects/Fun/Instruments/h_synthesizer.rsi", "supersynth"), 34, 66, k=2, anchor="bc")
    for x, y, state in ((238, 64, "cassette_purple"), (252, 58, "cassette_aesthetic"), (224, 60, "cassette_ice")):
        b.sprite(("_RMC14/Objects/Devices/cassette_player.rsi", state), x, y, anchor="bc")
    chrome = bands((4, "#ffffff"), (3, "#bff8ff"), (3, "#00f0ff"), (2, "#ff9ae8"), (4, HEAD), (10, "#a0208a"))
    b.text("display_bold_italic", 26, 78, 6, "CHROMA SLEEP", chrome, outline="#1a0630", fit=150)
    b.text("display_bold", 11, 80, 33, "NEW ALBUM", LINK, shadow="#1a0630")
    b.text("display_italic", 11, 80, 45, "Afterglow Protocol", TEXT, shadow="#1a0630", fit=110)
    for x, y in ((150, 5), (256, 30), (70, 12)):
        sparkle(b, x, y, LINK)
    return b


# -- Celeste Varga-Ishikawa ----------------------------------------------------------------------------------------


def ishikawa():
    BG, TEXT, HEAD, LINK = "#F3F1EC", "#1E1E1E", "#8A6A1C", "#1F4E79"
    b = Banner(BG)
    noise(b, 0, 0, b.w, b.h, "#ebe8e0", 0.15, 7)
    # Letterhead rules and the Company mark.
    b.hline(6, b.w - 7, 4, HEAD)
    b.hline(6, b.w - 7, 63, HEAD)
    b.hline(6, b.w - 7, 61, "#d8ceb4")
    wy_mark(b, 10, 10, HEAD, shadow="#d8ceb4")
    b.bm(F3, 26, 12, "WEYLAND-YUTANI", HEAD)
    b.bm(F3, 26, 18, "CORPORATION", "#9a8a6a")
    b.vline(84, 9, 22, "#d8ceb4")
    b.text("display_bold", 20, 10, 29, "Celeste Varga-Ishikawa", TEXT, fit=178)
    b.text("display", 11, 10, 46, "Executive Vice President, Colonial Development", LINK, fit=178)
    b.text("display_italic", 10, 92, 12, "Building Better Worlds.", HEAD)
    # The desk: the game's potted plant, briefcase, Company pen and a good watch.
    b.rect(192, 54, b.w - 7, 57, "#5a4430")
    b.sprite(("_RMC14/Structures/Furniture/potted_plants.rsi", "pottedplant_3"), 208, 54, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Storage/Briefcases/brown.rsi", "icon"), 240, 54, k=1, anchor="bc")
    b.sprite(("_RMC14/Objects/Misc/paper.rsi", "weya_pen"), 252, 30, anchor="cc")
    b.sprite(("_RMC14/Objects/Clothing/Accessory/Watches/fancy.rsi", "icon"), 238, 24, anchor="cc")
    return b


# -- Spin Okafor ---------------------------------------------------------------------------------------------------


def spinokafor():
    BG, TEXT, HEAD, LINK = "#0A1630", "#DCE6FF", "#FFB000", "#FF7A45"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#0a1630", "#0e1e44", "#0a1630"])
    starfield(b, random.Random(3), 0, 0, b.w, b.h, 60, ("#1a2a5a", "#3a4a8a", "#8a9ad0", "#ffffff"))
    # The arena ring of a zero-g court, seen edge on.
    for x, y in edge(m_ellipse(200, 36, 60, 22)):
        b.set(x, y, "#2a4a9a")
    # The ball in flight with motion arcs, the game's ball.
    for k in range(3):
        b.line(150 - k * 10, 46 + k * 4, 176 - k * 6, 38 + k * 2, mix(LINK, BG, 0.3 + k * 0.2))
    b.sprite(("Objects/Fun/Balls/football.rsi", "icon"), 192, 34, k=2, anchor="cc")
    # The captain's shirt and his medals.
    b.sprite(("_RMC14/Objects/Clothing/Uniforms/T-shirt/red.rsi", "icon"), 240, 46, k=2, anchor="cc")
    b.bm(BOLD, 236, 40, "7", "#ffffff")
    for i, state in enumerate(("gold.rsi", "gold.rsi", "gold.rsi")):
        b.sprite(("_RMC14/Objects/Medals/" + state, "icon"), 150 + i * 12, 64, anchor="bc")
    b.text("sans_bold", 24, 8, 8, "SPIN OKAFOR", HEAD, shadow="#000000", fit=134)
    b.bm(F5, 8, 32, "CAPTAIN, GATEWAY COMETS", TEXT)
    b.bm(F3, 8, 44, "THREE TIME ORBITAL LEAGUE MVP", LINK)
    b.bm(F3, 8, 52, "THE FASTEST HANDS IN ZERO GRAVITY", "#8a9ad0")
    return b


# -- Vex Starr -----------------------------------------------------------------------------------------------------


def vexstarr():
    BG, TEXT, HEAD, LINK = "#1E0F1E", "#F5E6F0", "#FF5FD2", "#FFD27A"
    b = Banner(BG)
    # A spotlight on a velvet curtain.
    for x in range(b.w):
        for y in range(b.h):
            fold = 0.5 + 0.5 * math.sin(x * 0.35)
            b.set(x, y, mix("#1e0f1e", "#3a1430", fold * 0.6))
    glow(b, 210, 34, 70, 40, "#ffd27a", 0.25)
    b.sprite(("_RMC14/Objects/Devices/broadcasting_camera.rsi", "icon"), 168, 60, k=2, anchor="bc")
    b.sprite(("Objects/Consumable/Drinks/champagnebottle.rsi", "icon"), 248, 64, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Clothing/Eyes/Glasses/bigsunglasses.rsi", "icon"), 212, 24, k=2, anchor="cc")
    gold = bands((4, "#fff6d0"), (5, LINK), (6, "#e8a03a"), (8, "#a06020"))
    b.text("display_bold_italic", 30, 8, 6, "VEX STARR", gold, outline="#3a0a2a", fit=140)
    b.text("display_bold", 11, 10, 36, "STAR OF GATEWAY GENERAL", HEAD, shadow="#000000", fit=134)
    b.text("display_italic", 11, 10, 49, "The Long Sleep, coming soon", TEXT, shadow="#000000", fit=134)
    for x, y in ((146, 8), (192, 52), (258, 10), (230, 44), (120, 30)):
        sparkle(b, x, y, HEAD)
    return b


# -- Rosie Ketch ---------------------------------------------------------------------------------------------------


def rosieketch():
    BG, TEXT, HEAD, LINK = "#2A1A0E", "#F2E3C6", "#E8A33C", "#9FD0E0"
    b = Banner(BG)
    # Wooden boards of a cargo bay stage.
    for y in range(b.h):
        for x in range(b.w):
            plank = (y // 9) % 2
            grain = (x * 3 + y * 7 + (y // 9) * 37) % 23 == 0
            b.set(x, y, "#3a2414" if plank else "#32200f")
            if grain:
                b.set(x, y, "#24160a")
        if y % 9 == 0:
            b.hline(0, b.w - 1, y, "#1a0f06")
    glow(b, 210, 30, 60, 34, "#e8a33c", 0.2)
    b.sprite(("Objects/Fun/Instruments/guitar.rsi", "icon"), 214, 64, k=2, anchor="bc")
    b.sprite(("Clothing/Head/Hats/cowboyhatbrown.rsi", "icon"), 166, 22, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Consumable/Drinks/whiskey.rsi", "icon"), 252, 64, anchor="bc")
    b.sprite(("Objects/Fun/Instruments/harmonica.rsi", "icon"), 180, 44, anchor="cc")
    b.text("display_bold", 30, 8, 6, "ROSIE KETCH", HEAD, shadow="#120a04", outline="#120a04", fit=138)
    b.text("display_italic", 12, 10, 36, "The Queen of the Outer Veil", TEXT, shadow="#120a04", fit=134)
    b.bm(F3, 10, 52, "SIX RECORDS - FOUR GUITARS - ONE VOICE", LINK, shadow="#120a04")
    return b


# -- Rex Maddox ----------------------------------------------------------------------------------------------------


def rexmaddox():
    BG, TEXT, HEAD, LINK = "#161B0C", "#E5E8C8", "#FF7A1A", "#FFD24A"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#0c1006", "#161b0c", "#1e2610"])
    # Jungle fronds hanging into frame.
    rng = random.Random(17)
    for _ in range(26):
        x0, y0 = rng.randrange(0, b.w), rng.randrange(-6, 10)
        ln = rng.randrange(10, 26)
        for t in range(ln):
            x = x0 + int(math.sin(t * 0.3) * 3)
            y = y0 + t
            b.set(x, y, "#24300f")
            if t % 3 == 0:
                b.hline(x - 3, x - 1, y + 1, "#1e280c")
                b.hline(x + 1, x + 3, y + 1, "#2a3812")
    b.sprite(("_RMC14/Objects/Weapons/Melee/machete.rsi", "icon"), 172, 34, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/flares.rsi", "flare"), 222, 58, k=2, anchor="cc")
    glow(b, 236, 58, 14, 8, "#ff4a1a", 0.35)
    b.sprite(("CMU14/Clothing/JungleHats/booniehat.rsi", "icon"), 234, 20, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Devices/binoculars.rsi", "binoculars_civ"), 210, 36, anchor="cc")
    b.text("slab", 26, 8, 6, "REX MADDOX", HEAD, shadow="#000000", outline="#3a1404", fit=146)
    b.text("display_bold", 13, 10, 34, "HOSTILE WORLDS", LINK, shadow="#000000")
    b.bm(F3, 10, 50, "SEASON SEVEN: THE DEEP JUNGLE", TEXT, shadow="#000000")
    b.bm(F3, 10, 57, "A KNIFE, A FLARE AND A CAMERA CREW", "#a8ac88", shadow="#000000")
    return b


# -- The Colony Tattler --------------------------------------------------------------------------------------------


def colonytattler():
    BG, TEXT, HEAD, LINK = "#FFF1F6", "#1A1A1A", "#D0105A", "#1440A0"
    b = Banner(BG)
    b.rect(0, 0, b.w, 36, HEAD)
    halftone(b, 0, 0, b.w, 36, rgba("#e02a70"), lambda x, y: 0.4 + 0.4 * (x / b.w))
    b.text("display_bold_italic", 26, 8, 6, "THE COLONY TATTLER", "#ffffff", shadow="#5a0420", fit=196)
    b.hline(0, b.w - 1, 36, "#5a0420")
    b.text("sans_bold", 13, 8, 41, "VEX AND SPIN SHARE A BOWL", TEXT, fit=172)
    b.text("sans", 11, 8, 55, "The biggest names, the smallest details.", LINK, fit=172)
    b.sprite(("Objects/Devices/travel_camera.rsi", "icon"), 232, 54, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Clothing/Eyes/Glasses/bigsunglasses.rsi", "icon"), 196, 48, anchor="cc")
    b.sprite(("_RMC14/Objects/Fun/playing_cards.rsi", "hearts_ace"), 194, 60, anchor="cc")
    # Price flash.
    sticker(b, 236, 18, 14, 10, 12, ramp("#a08000", "#e0c000", "#fff040", "#ffff9a"), "#3a2a00",
            [(F3, "ONLY", "#5a0420", -5), (F3, "$1", "#5a0420", 1)])
    return b


# -- Prosperity Today ----------------------------------------------------------------------------------------------


def prosperitytoday():
    BG, TEXT, HEAD, LINK = "#EEF3F8", "#10243A", "#0B4F8A", "#1C6B3A"
    b = Banner(BG)
    # A sunrise over a colony horizon, behind the masthead.
    for y in range(b.h):
        for x in range(150, b.w):
            d = math.hypot(x - 222, (y - 60) * 1.4)
            b.set(x, y, pick(ramp("#ffe9a8", "#fff3cc", "#f6f2e4", "#eef3f8"), min(3, d / 22), x, y, 4))
    for k in range(9):
        a = math.pi + k * math.pi / 8
        b.line(222, 60, 222 + math.cos(a) * 80, 60 + math.sin(a) * 80, "#ffe9a8")
    b.rect(150, 58, b.w, b.h, "#c8d8e8")
    b.sprite(("_RMC14/Objects/Kitchen/Plants/sunflower.rsi", "produce"), 206, 58, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Medals/gold.rsi", "icon"), 244, 40, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/spacecash.rsi", "spacecash_1000"), 172, 58, anchor="bc")
    b.hline(6, 146, 5, HEAD)
    b.hline(6, 146, 7, HEAD)
    b.text("sans_bold", 22, 6, 12, "PROSPERITY", HEAD, fit=140)
    b.text("sans_bold", 22, 6, 32, "TODAY", LINK, fit=140)
    b.bm(F3, 84, 34, "GOOD NEWS FROM", TEXT)
    b.bm(F3, 84, 40, "THE COLONIES,", TEXT)
    b.bm(F3, 84, 46, "EVERY DAY", TEXT)
    b.hline(6, 146, 56, HEAD)
    b.bm(F3, 6, 59, "READ IN EVERY W-Y SETTLEMENT", "#5a6a7a")
    return b


# -- Veil Truth ----------------------------------------------------------------------------------------------------


def veiltruth():
    BG, TEXT, HEAD, LINK = "#0B0B05", "#E8E8C8", "#FFE000", "#FF5050"
    b = Banner(BG)
    noise(b, 0, 0, b.w, b.h, "#1a1a0e", 0.15, 9)
    # A pyramid with an eye, chalked up on the dark.
    for k in range(26):
        b.set(212 - k, 10 + k, "#5a5a2a")
        b.set(212 + k, 10 + k, "#5a5a2a")
    b.hline(186, 238, 36, "#5a5a2a")
    for x, y in edge(m_ellipse(212, 26, 7, 4)):
        b.set(x, y, HEAD)
    b.fill(m_ellipse(212, 26, 2, 2), LINK)
    # The kit: the game's paper hat, walkie-talkie and a synthetic's token.
    b.sprite(("Clothing/Head/Hats/paper.rsi", "icon"), 170, 54, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Devices/radio.rsi", "walkietalkie"), 244, 62, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Misc/synth_token.rsi", "coin_synth"), 212, 54, anchor="cc")
    b.text("osd", 22, 8, 6, "VEIL TRUTH", HEAD, shadow="#3a3000", fit=140)
    b.bm(F5, 8, 30, "THEY DO NOT WANT YOU", LINK)
    b.bm(F5, 8, 39, "TO READ THIS PAGE", LINK)
    b.bm(F3, 8, 52, "INDEPENDENT. UNFUNDED. UNAFRAID.", TEXT)
    b.bm(F3, 8, 59, "VEILTRUTH.NEWS", "#7a7a5a")
    scan(b, 0.82)
    return b


# -- Casa Albarran -------------------------------------------------------------------------------------------------


def albarran():
    BG, TEXT, HEAD, LINK = "#2B1A10", "#EADBC4", "#D9A441", "#E8C77A"
    b = Banner(BG)
    # A cigar box label: gold rules, corner flourishes and a cedar panel for the cigars.
    frame(b, "#140a04", HEAD, HEAD)
    frame(b, "#140a04", None, None, 4, 4, b.w - 5, b.h - 5)
    for cx, cy in ((7, 7), (b.w - 8, 7), (7, b.h - 8), (b.w - 8, b.h - 8)):
        b.fill(m_burst(cx, cy, 3, 1.2, 4), LINK)
    b.rect(154, 8, b.w - 9, b.h - 9, "#5a3418")
    for y in range(8, b.h - 9, 3):
        b.hline(154, b.w - 10, y, "#4a2a12")
    b.sprite(("Objects/Consumable/Smokeables/Cigars/case.rsi", "open"), 230, 58, anchor="bc")
    for i, (rsi, x, y) in enumerate((("cigar.rsi", 172, 24), ("cigar-gold.rsi", 192, 34), ("cigar.rsi", 176, 46))):
        b.sprite(("Objects/Consumable/Smokeables/Cigars/" + rsi, "unlit-icon"), x, y, k=2, anchor="cc")
    b.sprite(("Objects/Specific/Hydroponics/tobacco.rsi", "dried"), 252, 18, anchor="cc")
    b.sprite(("_RMC14/Objects/Tools/Lighters/zippo.rsi", "open"), 208, 18, anchor="cc")
    gold = bands((5, "#fff0c0"), (6, LINK), (6, HEAD), (8, "#8a5a1a"))
    b.text("display_bold", 25, 12, 9, "CASA ALBARRAN", gold, shadow="#140a04", fit=136)
    b.hline(12, 148, 30, HEAD)
    b.text("display_italic", 12, 12, 35, "Hand rolled in Havana since 2061", TEXT, fit=136)
    b.bm(F3, 12, 53, "EARTH LEAF - EARTH SOIL - EARTH SUN", LINK)
    return b


# -- Frontier Leaf -------------------------------------------------------------------------------------------------


def frontierleaf():
    BG, TEXT, HEAD, LINK = "#EFE6D2", "#2B2416", "#4E6E1A", "#8A3A12"
    b = Banner(BG)
    noise(b, 0, 0, b.w, b.h, "#e4d8bc", 0.25, 13)
    # A hydroponics tray of the game's tobacco plants, from seedling to harvest.
    b.rect(140, 54, b.w - 6, 60, "#6a6a72")
    b.hline(140, b.w - 7, 54, "#9a9aa2")
    for i, state in enumerate(("stage-1", "stage-2", "stage-3", "harvest", "harvest")):
        b.sprite(("Objects/Specific/Hydroponics/tobacco.rsi", state), 152 + i * 22, 56, k=2 if i >= 2 else 1, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/Hydroponics/spade.rsi", "icon"), 146, 42, anchor="cc")
    b.sprite(("CMU14/Items/Smokes/koorlander.rsi", "closed"), 142, 16, anchor="cc")
    b.text("mono_bold", 26, 8, 6, "FRONTIER LEAF", HEAD, fit=124)
    b.hline(8, 118, 25, LINK)
    b.bm(F5, 8, 29, "GROWERS' CO-OP", TEXT)
    b.bm(F3, 8, 41, "COLONY GROWN, FOR COLONY PEOPLE", LINK)
    b.bm(F3, 8, 49, "OWNED BY THE PEOPLE WHO GROW IT", LINK)
    b.bm(F3, 8, 59, "HOPE'S RETREAT - EST. 2176", "#7a6a4a")
    frame(b, "#7a6a4a", None, None)
    return b


# -- The House of Balaji -------------------------------------------------------------------------------------------


def balaji():
    BG, TEXT, HEAD, LINK = "#0B2018", "#EDE6D0", "#D4AF37", "#9ED9C0"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#0e2a20", "#0b2018", "#071610"])
    frame(b, HEAD, "#6a5a1a", None)
    frame(b, "#4a3a0a", None, None, 3, 3, b.w - 4, b.h - 4)
    # A damask of small gold lozenges behind the goods.
    for y in range(8, b.h - 8, 8):
        for x in range(150 + (y // 8 % 2) * 4, b.w - 8, 8):
            b.set(x, y, "#1e3a2a")
    b.sprite(("CMU14/Items/Smokes/balaji_imperials.rsi", "closed"), 178, 60, k=2, anchor="bc")
    b.sprite(("CMU14/Items/Smokes/balaji_imperials.rsi", "open"), 210, 60, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/Lighters/goldzippo.rsi", "closed"), 240, 58, k=2, anchor="bc")
    b.sprite(("CMU14/Objects/Consumable/MRE/balaji_imperial_matchbook.rsi", "mpacket"), 158, 58, anchor="bc")
    gold = bands((5, "#fff4c0"), (6, "#ecc860"), (6, HEAD), (8, "#8a6a1a"))
    b.text("display", 11, 10, 8, "THE HOUSE OF", LINK)
    b.text("display_bold", 27, 10, 21, "BALAJI", gold, shadow="#000000", fit=120)
    b.bm(F3, 10, 46, "MAKERS OF BALAJI IMPERIALS", TEXT)
    b.bm(F3, 10, 55, "MUMBAI - LONDON - KYOTO - GATEWAY", HEAD)
    return b


# -- Lasalle Bionational Pharmacy ----------------------------------------------------------------------------------


def lasallepharmacy():
    BG, TEXT, HEAD, LINK = "#F6FAFA", "#13282E", "#0E7C7B", "#1B5FA8"
    b = Banner(BG)
    # Glass shelves stocked with the game's own pill bottles, packets and autoinjectors.
    b.rect(140, 0, b.w, b.h, "#e2eeee")
    for y in (30, 60):
        b.rect(140, y, b.w, y + 2, "#b8d0d0")
        b.hline(140, b.w - 1, y, "#ffffff")
    for i, n in enumerate((1, 2, 3, 6, 9, 11, 12)):
        b.sprite(("_RMC14/Objects/Chemistry/pill_canister.rsi", f"pill_canister{n}"), 150 + i * 16, 30, k=2 if i % 3 == 0 else 1, anchor="bc")
    for i, state in enumerate(("bicaridine_packet", "kelotane_packet", "tramadol_packet")):
        b.sprite(("_RMC14/Objects/Medical/pill_packets.rsi", state), 152 + i * 16, 60, anchor="bc")
    b.sprite(("_RMC14/Objects/Medical/emergency_auto_injector.rsi", "autoinjector"), 222, 58, anchor="bc")
    b.sprite(("_RMC14/Objects/Medical/first_aid_kits.rsi", "firstaid"), 246, 60, anchor="bc")
    # Green cross and the name.
    cross = m_rect(12, 8, 18, 26) | m_rect(6, 14, 24, 20)
    paint(b, cross, ramp("#0a5a24", "#128a3a", "#18a048", "#5ad080"), f_lin(6, 8, 24, 26, 3, 1), "#064a1c")
    b.text("sans_bold", 20, 30, 8, "LASALLE", HEAD, fit=104)
    b.bm(F3, 31, 26, "BIONATIONAL", "#6a8a8a", gap=2)
    b.text("sans_bold", 15, 8, 34, "PHARMACY", LINK, fit=128)
    b.bm(F3, 8, 52, "TRUSTED MEDICINE FOR THE FRONTIER", TEXT)
    b.bm(F3, 8, 59, "PHARMACY.LASALLE.BIO", "#7a9a9a")
    frame(b, "#0a3a34", None, None)
    return b


# -- Colony Clinic Dispensary --------------------------------------------------------------------------------------


def clinicdispensary():
    BG, TEXT, HEAD, LINK = "#E9F0E9", "#1C2A1C", "#B02020", "#1E5AA0"
    b = Banner(BG)
    # Wall tiles above a counter.
    for y in range(0, 48):
        for x in range(0, b.w):
            if x % 10 == 0 or y % 10 == 0:
                b.set(x, y, "#d6e2d6")
    b.rect(0, 48, b.w, b.h, "#9aa89a")
    b.hline(0, b.w - 1, 48, "#c8d4c8")
    b.hline(0, b.w - 1, 49, "#6a7a6a")
    # A red cross sign.
    b.rect(10, 8, 40, 38, "#ffffff")
    frame(b, "#8a1010", None, None, 10, 8, 40, 38)
    b.fill(m_rect(21, 12, 30, 35) | m_rect(14, 19, 37, 28), HEAD)
    # The counter stock, the game's own kits and tools.
    stock = [("_RMC14/Objects/Medical/first_aid_kits.rsi", "firstaid", 174), ("_RMC14/Objects/Medical/first_aid_kits.rsi", "o2", 198),
             ("_RMC14/Objects/Medical/first_aid_kits.rsi", "ointment", 222), ("_RMC14/Objects/Medical/medical.rsi", "gauze", 243),
             ("_RMC14/Objects/Medical/health_analyzer.rsi", "health", 196)]
    for rsi, state, x in stock:
        b.sprite((rsi, state), x, 30 if state == "health" else 50, anchor="bc")
    b.sprite(("_RMC14/Objects/Medical/stethoscope.rsi", "icon"), 242, 20, anchor="cc")
    b.text("roboto_bold", 15, 48, 9, "CLINIC DISPENSARY", HEAD, fit=128)
    b.bm(F3, 48, 27, "MEDICINES - FIRST AID - ADVICE", TEXT)
    b.bm(F3, 48, 35, "EVERY COLONIST WITH AN ID", LINK)
    b.bm(F3, 8, 54, "BADLY HURT? DO NOT READ THIS PAGE. GET TO THE CLINIC.", "#ffffff")
    return b


# -- The Galley Binder ---------------------------------------------------------------------------------------------


def galley():
    BG, TEXT, HEAD, LINK = "#1E1A14", "#EDE3CF", "#F2A33A", "#8CCB6A"
    b = Banner(BG)
    # A steel counter and a recipe binder's ring holes down the left.
    vgrad(b, 0, 0, b.w, 46, ["#1e1a14", "#2a241a"])
    b.rect(0, 46, b.w, b.h, "#5a5e62")
    b.hline(0, b.w - 1, 46, "#9aa0a6")
    for x in range(0, b.w, 16):
        b.vline(x, 47, b.h, "#4a4e52")
    for y in (12, 34):
        b.fill(m_ellipse(6, y, 2.5, 2.5), "#000000")
        b.fill(edge(m_ellipse(6, y, 3.5, 3.5)), "#8a8a8a")
    b.sprite(("_RMC14/Structures/Machines/microwave.rsi", "mw"), 232, 48, k=2, anchor="bc")
    b.sprite(("_RMC14/Objects/Consumable/Food/Pizza/margherita.rsi", "pizza"), 168, 52, anchor="bc")
    b.sprite(("CMU14/HunterShip/obj/items/food/soups_salads.rsi", "tomatosoup"), 194, 62, anchor="bc")
    b.sprite(("CMU14/HunterShip/obj/items/food/soups_salads.rsi", "herbsalad"), 150, 64, anchor="bc")
    b.sprite(("Objects/Consumable/Food/burger.rsi", "plain"), 188, 44, anchor="bc")
    b.text("sans_bold", 22, 14, 8, "THE GALLEY BINDER", HEAD, shadow="#000000", fit=140)
    b.text("sans", 11, 14, 30, "Every dish the colony galleys know", TEXT, fit=140)
    b.bm(F3, 14, 53, "SEARCH BY DISH OR BY WHAT IS LEFT", "#ffffff")
    b.bm(F3, 14, 60, "IN THE COLD STORE", LINK)
    return b


# -- Break Room Arcade ---------------------------------------------------------------------------------------------


def arcade():
    BG, TEXT, HEAD, LINK = "#05050F", "#E0E0FF", "#FF2FD0", "#3FF2FF"
    b = Banner(BG)
    # Neon floor grid.
    for k in range(1, 7):
        y = 46 + k * k // 2 + k
        if y < b.h:
            b.hline(0, b.w - 1, y, "#2a0a4a")
    for k in range(-12, 13):
        b.line(132 + k * 6, 46, 132 + k * 26, b.h + 4, "#1a0a3a")
    b.hline(0, b.w - 1, 46, LINK)
    # Two of the game's arcade cabinets, glowing.
    for x in (196, 236):
        glow(b, x, 40, 18, 26, "#ff2fd0", 0.22)
        b.sprite(("Structures/Machines/arcade.rsi", "arcade"), x, 62, k=2, anchor="bc")
    b.text("box", 18, 8, 7, "BREAK ROOM ARCADE", HEAD, shadow="#3a0a3a", fit=160)
    b.bm(F5, 8, 28, "INSERT COIN", LINK, shadow="#002a3a")
    b.bm(F3, 8, 40, "TWO CABINETS - ONE HIGH SCORE PER SHIFT", TEXT)
    b.bm(F3, 8, 50, "HI-SCORE", HEAD)
    b.bm(F3, 44, 50, "0041950", "#ffffff")
    for x, y in ((150, 8), (176, 20), (110, 4)):
        b.set(x, y, LINK)
    return b


# -- Zero-G Ball Sportsbook ----------------------------------------------------------------------------------------


def sportsbook():
    BG, TEXT, HEAD, LINK = "#0A1A0A", "#D8F0D0", "#7CFF00", "#F2C230"
    b = Banner(BG)
    # An LED odds board on the right.
    b.rect(150, 4, b.w - 5, 44, "#041004")
    frame(b, "#2a4a2a", None, None, 150, 4, b.w - 5, 44)
    rows = (("COMETS", "4/5"), ("TITAN", "6/4"), ("LUNA", "11/2"))
    for i, (team, odds) in enumerate(rows):
        b.bm(F3, 154, 8 + i * 11, team, HEAD)
        b.bm(F3, b.w - 9, 8 + i * 11, odds, LINK, anchor="r")
        dashes(b, 154, b.w - 9, 14 + i * 11, "#1a3a1a")
    scanlines(b, 150, 4, b.w - 5, 44, 0.7)
    b.sprite(("Objects/Fun/Balls/football.rsi", "icon"), 166, 58, k=2, anchor="cc")
    b.sprite(("Objects/Fun/dice.rsi", "d20_20"), 204, 58, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/spacecash.rsi", "spacecash_1000"), 236, 66, anchor="bc")
    b.text("display_bold_italic", 22, 8, 7, "ZERO-G BALL", HEAD, shadow="#000000", fit=136)
    b.text("display_bold", 15, 8, 29, "SPORTSBOOK", LINK, shadow="#000000", fit=136)
    b.bm(F3, 8, 47, "LIVE FROM THE OUTER RIM LEAGUES", TEXT)
    b.bm(F3, 8, 55, "THE HOUSE ALWAYS WINS EVENTUALLY", "#6a8a6a")
    frame(b, "#000000", "#1a3a1a", None)
    return b


BANNERS = {
    "ChromaSleep": ("Chroma Sleep", chromasleep),
    "Ishikawa": ("Celeste Varga-Ishikawa", ishikawa),
    "SpinOkafor": ("Spin Okafor", spinokafor),
    "VexStarr": ("Vex Starr", vexstarr),
    "RosieKetch": ("Rosie Ketch", rosieketch),
    "RexMaddox": ("Rex Maddox: Hostile Worlds", rexmaddox),
    "ColonyTattler": ("The Colony Tattler", colonytattler),
    "ProsperityToday": ("Prosperity Today", prosperitytoday),
    "VeilTruth": ("Veil Truth", veiltruth),
    "Albarran": ("Casa Albarran", albarran),
    "FrontierLeaf": ("Frontier Leaf Co-op", frontierleaf),
    "Balaji": ("The House of Balaji", balaji),
    "LasallePharmacy": ("Lasalle Bionational Pharmacy", lasallepharmacy),
    "ClinicDispensary": ("Colony Clinic Dispensary", clinicdispensary),
    "Galley": ("The Galley Binder", galley),
    "Arcade": ("Break Room Arcade", arcade),
    "Sportsbook": ("Zero-G Ball Sportsbook", sportsbook),
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
