"""Banners for the retro ExtraNet sites, the colonies' answer to the early web: AtmoCast, ColonyPedia, DropDash,
eBuoy, The Hydrogen Line, Indentured, MyBunk, OreMatch, SpaceGPT, VectorQuest, Yotta! and YouTape.

    python Content.CMU/Design/PersonalComputers/SiteArt/Retro/Banners.py [Site ...]

Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Retro/<Site>/banner.png (264x68 drawn at 1x,
saved 2x) and an attributions.yml beside it.

These sites take their colours from their cmuNetSite style (they carry no CMUNetStyle line): each banner is a
logo bar in that palette, in the brash look of the site it parodies, with the game's own sprites as the goods on
show: weather effects for AtmoCast, the delivery food for DropDash, listings for eBuoy, the AI core for SpaceGPT,
cassettes and the camcorder for YouTape. Only bars, boxes, maps and lettering are painted.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from banner_kit import *  # noqa: E402,F401,F403
from banner_kit import Banner, SITES, BW, BH  # noqa: E402

GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Retro/Banners.py"
OUT = os.path.join(SITES, "Retro")
CASSETTE = "_RMC14/Objects/Devices/cassette_player.rsi"


def bevel_box(b, x0, y0, x1, y1, face, hi, lo):
    b.rect(x0, y0, x1 + 1, y1 + 1, face)
    b.hline(x0, x1, y0, hi)
    b.vline(x0, y0, y1, hi)
    b.hline(x0, x1, y1, lo)
    b.vline(x1, y0, y1, lo)


# -- AtmoCast ------------------------------------------------------------------------------------------------------


def atmocast():
    BG, TEXT, HEAD, LINK = "#0B2A5B", "#E6F0FF", "#FFD24A", "#8FD3FF"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#123a7a", "#0b2a5b", "#071c40"])
    b.rect(0, 0, b.w, 3, HEAD)
    b.text("display_bold_italic", 22, 8, 8, "AtmoCast", HEAD, shadow="#04122a", fit=104)
    b.bm(F3, 10, 30, "LOCAL ON THE 8S", LINK)
    b.bm(F3, 10, 38, "FORECASTS FOR EVERY", TEXT)
    b.bm(F3, 10, 44, "COLONY WITH A SKY", TEXT)
    # A five shift forecast: each tile is a window onto the game's own weather overlay.
    tiles = [("SHIFT 1", "_RMC14/Effects/weather.rsi", "strata_clearsky", "#3a7ad0", "+21"),
             ("SHIFT 2", "Effects/weather.rsi", "rain", "#2a4a7a", "+14"),
             ("SHIFT 3", "Effects/weather.rsi", "storm", "#1a2a4a", "+9"),
             ("SHIFT 4", "_RMC14/Effects/weather.rsi", "bigred_dust", "#8a5a3a", "+30"),
             ("SHIFT 5", "Effects/weather.rsi", "snowfall_med", "#6a8aaa", "-12")]
    for i, (label, rsi, state, sky, temp) in enumerate(tiles):
        x0 = 118 + i * 29
        b.rect(x0, 6, x0 + 27, 62, "#06183a")
        frame(b, "#4a7ac0", None, None, x0, 6, x0 + 27, 62)
        b.bm(F3, x0 + 14, 9, label[-1], HEAD, anchor="c")
        b.rect(x0 + 2, 16, x0 + 26, 44, sky)
        if i == 0:
            b.fill(m_ellipse(x0 + 14, 30, 6, 6), "#ffd24a")
            b.fill(edge(m_ellipse(x0 + 14, 30, 8, 8)), "#ffe9a0")
        b.sprite((rsi, state), x0 + 2, 15, trim=False)
        b.rect(x0 + 1, 45, x0 + 27, 62, "#06183a")
        frame(b, "#4a7ac0", None, None, x0, 6, x0 + 27, 62)
        b.bm(F5, x0 + 14, 50, temp, TEXT, anchor="c")
    b.bm(F3, 10, 58, "ATMOCAST.WX", "#4a7ac0")
    return b


# -- ColonyPedia ---------------------------------------------------------------------------------------------------


def colonypedia():
    BG, TEXT, HEAD, LINK = "#F8F8F4", "#202122", "#000000", "#0645AD"
    b = Banner(BG)
    # The game's books, stacked like a library's logo.
    b.sprite(("Objects/Misc/books.rsi", "book_icon"), 24, 62, k=2, anchor="bc")
    for i, icon in enumerate(("icon_planet", "icon_stars", "icon_atmos", "icon_question")):
        b.sprite(("Objects/Misc/books.rsi", icon), 210 + (i % 2) * 22, 30 + (i // 2) * 22, anchor="cc")
    b.text("display", 26, 50, 9, "ColonyPedia", HEAD, fit=150)
    b.text("display_italic", 11, 52, 36, "The Free Encyclopedia of the frontier", TEXT, fit=150)
    b.hline(50, 200, 50, "#a2a9b1")
    b.bm(F3, 52, 54, "ARTICLE", LINK)
    b.bm(F3, 88, 54, "TALK", LINK)
    b.bm(F3, 112, 54, "81,220 ARTICLES", "#54595d")
    frame(b, "#a2a9b1", None, None)
    return b


# -- DropDash ------------------------------------------------------------------------------------------------------


def dropdash():
    BG, TEXT, HEAD, LINK = "#FFF5E6", "#2A1A0A", "#FF3B00", "#B33000"
    b = Banner(BG)
    b.rect(0, 0, 150, b.h, HEAD)
    for y in range(8, 60, 6):
        b.hline(4, 30 - (y % 12), y, "#ff8a5a")
    b.text("sans_bold", 26, 30, 10, "DropDash", "#ffffff", shadow="#8a1a00", fit=112)
    b.text("sans_bold", 11, 34, 38, "We drop it. You dash.", "#fff0d8", fit=110)
    b.bm(F3, 34, 54, "HOT FOOD TO YOUR PAD", "#ffd8c0")
    b.line(150, 0, 140, b.h, HEAD)
    # The order: the game's pizza box, burger, noodles and a donut.
    b.sprite(("_RMC14/Objects/Storage/pizza_galaxy_box.rsi", "pizzabox1"), 188, 62, k=2, anchor="bc")
    b.sprite(("Objects/Consumable/Food/burger.rsi", "plain"), 236, 40, k=2, anchor="bc")
    b.sprite(("Objects/Consumable/Food/noodles.rsi", "chowmein"), 238, 62, anchor="bc")
    b.sprite(("Objects/Consumable/Food/Baked/donut.rsi", "pink"), 170, 22, anchor="cc")
    b.bm(F3, 258, 4, "UNDER 90 MIN*", LINK, anchor="r")
    return b


# -- eBuoy ---------------------------------------------------------------------------------------------------------


def ebuoy():
    BG, TEXT, HEAD, LINK = "#FFFFFF", "#222222", "#E53238", "#0064D2"
    b = Banner(BG)
    x = 8
    for ch, c in zip("eBuoy", (HEAD, LINK, "#F5AF02", "#86B817", HEAD)):
        x0, x1 = b.text("display_bold", 30, x, 8, ch, c)
        x = x1 + 1
    b.text("display_italic", 11, 10, 40, "Buy it. Sell it. Wait for it.", TEXT, fit=110)
    b.bm(F3, 10, 56, "BIDS TRAVEL BY RELAY BUOY", "#707070")
    # Listings: the game's own goods, each with a price.
    items = [("Objects/Vehicles/atv.rsi", "vehicle", "$900"), ("_RMC14/Objects/Devices/radio.rsi", "walkietalkie", "$12"),
             ("_RMC14/Objects/Medical/health_analyzer.rsi", "health", "$40"), ("_RMC14/Objects/Tools/motion_detector.rsi", "detector", "$75")]
    for i, (rsi, state, price) in enumerate(items):
        x0 = 128 + i * 33
        b.rect(x0, 8, x0 + 30, 60, "#f7f7f7")
        frame(b, "#dddddd", None, None, x0, 8, x0 + 30, 60)
        b.sprite((rsi, state), x0 + 15, 44, anchor="bc")
        b.bm(F3, x0 + 15, 50, price, HEAD if i % 2 == 0 else LINK, anchor="c")
    return b


# -- The Hydrogen Line ---------------------------------------------------------------------------------------------


def hydrogenline():
    BG, TEXT, HEAD, LINK = "#050510", "#CFE8FF", "#00E5FF", "#FF66CC"
    b = Banner(BG)
    # A radio dial: the scale, tick marks and the needle parked on 1420.
    b.rect(8, 44, 172, 62, "#0a0a20")
    frame(b, "#1a3a5a", None, None, 8, 44, 172, 62)
    for i, f in enumerate(range(1400, 1441, 5)):
        x = 14 + i * 19
        b.vline(x, 47, 50 if f % 10 else 52, "#3a6a8a")
        if f % 10 == 0:
            b.bm(F3, x, 54, str(f)[-2:], "#3a6a8a", anchor="c")
    b.vline(14 + 4 * 19, 45, 61, LINK)
    glow(b, 14 + 4 * 19, 53, 6, 8, LINK, 0.4)
    b.text("box", 15, 8, 6, "THE HYDROGEN LINE", HEAD, shadow="#002a3a", fit=164)
    b.bm(F5, 8, 26, "1420", LINK)
    b.bm(F3, 34, 28, "THE FREQUENCY OF EVERYTHING", TEXT)
    b.sprite(("Objects/Fun/Instruments/microphone.rsi", "icon"), 196, 40, k=2, anchor="cc")
    b.sprite(("Objects/Fun/Instruments/h_synthesizer.rsi", "supersynth"), 240, 64, anchor="bc")
    b.sprite(("_RMC14/Objects/Devices/cassette_player.rsi", "playing"), 230, 24, anchor="cc")
    for r in (4, 8, 12):
        for a in range(-50, 51, 10):
            t = math.radians(a)
            b.set(int(196 + 18 + math.cos(t) * r), int(28 + math.sin(t) * r), "#2a6a8a")
    return b


# -- Indentured ----------------------------------------------------------------------------------------------------


def indentured():
    BG, TEXT, HEAD, LINK = "#F2F6FC", "#1B1B1B", "#2557A7", "#164081"
    b = Banner(BG)
    b.text("roboto_bold", 24, 8, 8, "Indentured", HEAD, fit=140)
    b.bm(F3, 10, 30, "FIND WORK ON THE FRONTIER.", TEXT)
    b.bm(F3, 10, 37, "FIND OUT HOW LONG IT LASTS.", "#6a6a6a")
    # The search bar.
    for x0, label in ((10, "WHAT"), (78, "WHERE")):
        b.rect(x0, 46, x0 + 62, 58, "#ffffff")
        frame(b, "#9aaac8", None, None, x0, 46, x0 + 62, 58)
        b.bm(F3, x0 + 3, 50, label, "#9aaac8")
    b.rect(146, 46, 168, 58, HEAD)
    b.bm(F3, 157, 50, "GO", "#ffffff", anchor="c")
    # The game's clipboard, approval stamp, pickaxe and briefcase: the contract and the work.
    b.sprite(("_RMC14/Objects/Misc/clipboard.rsi", "clipboard"), 198, 40, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Misc/paper.rsi", "paper_stamp-approve"), 200, 40, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Weapons/Melee/pickaxe.rsi", "icon"), 240, 24, anchor="cc")
    b.sprite(("_RMC14/Objects/Storage/Briefcases/brown.rsi", "icon"), 242, 54, anchor="cc")
    b.hline(0, b.w - 1, b.h - 2, HEAD)
    return b


# -- MyBunk --------------------------------------------------------------------------------------------------------


def mybunk():
    BG, TEXT, HEAD, LINK = "#E8EEF8", "#111111", "#003399", "#E05A00"
    b = Banner(BG)
    b.rect(0, 0, b.w, 28, HEAD)
    vgrad(b, 0, 0, b.w, 28, ["#1a4ab0", "#003399", "#00267a"])
    b.text("sans_bold", 20, 8, 5, "MyBunk", "#ffffff", fit=90)
    b.text("sans", 10, 98, 11, "a place for friends on the night shift", "#cfe0ff", fit=160)
    # The profile panel and its Top 8.
    b.bm(F3, 8, 33, "COOL NEW PEOPLE", LINK)
    b.bm(F3, 8, 40, "WHO HAS BEEN READING YOUR", TEXT)
    b.bm(F3, 8, 46, "PROFILE AT THREE IN THE MORNING?", TEXT)
    b.bm(F3, 8, 56, "TOP 8", HEAD)
    for i in range(8):
        x0 = 32 + i * 11
        b.rect(x0, 54, x0 + 9, 63, "#c8d4ec")
        frame(b, "#8a9ac8", None, None, x0, 54, x0 + 9, 63)
    b.sprite(("Effects/hearts.rsi", "hearts"), 150, 46, anchor="cc")
    b.sprite(("_RMC14/Objects/Devices/cassette_player.rsi", "walkman"), 178, 50, anchor="cc")
    b.sprite(("Objects/Fun/Instruments/guitar.rsi", "icon"), 212, 48, anchor="cc")
    b.sprite(("Objects/Fun/dice.rsi", "d20_20"), 238, 54, anchor="cc")
    b.sprite(("_RMC14/Objects/Devices/clocks.rsi", "cat_clock_motion"), 254, 46, anchor="cc")
    return b


# -- OreMatch ------------------------------------------------------------------------------------------------------


def orematch():
    BG, TEXT, HEAD, LINK = "#2A0A1A", "#FFE6F0", "#FF6FA8", "#FFD27A"
    b = Banner(BG)
    vgrad(b, 0, 0, b.w, b.h, ["#3a0e26", "#2a0a1a", "#1a0610"])
    # A rock face with a seam of the game's diamond ore, and the game's pickaxe and mug.
    b.fill(m_poly([(150, 68), (168, 30), (200, 18), (240, 22), (264, 10), (264, 68)]), "#1a0e14")
    b.fill(edge(m_poly([(150, 68), (168, 30), (200, 18), (240, 22), (264, 10), (264, 68)])), "#3a2030")
    b.sprite(("Objects/Materials/ore.rsi", "diamond"), 214, 44, k=2, anchor="cc")
    b.sprite(("_RMC14/Objects/Weapons/Melee/pickaxe.rsi", "icon"), 178, 46, k=2, anchor="cc")
    b.sprite(("Objects/Consumable/Drinks/mug_heart.rsi", "icon"), 250, 60, anchor="bc")
    b.sprite(("Effects/hearts.rsi", "hearts"), 236, 20, anchor="cc")
    b.text("display_bold_italic", 28, 8, 8, "OreMatch", HEAD, shadow="#000000", fit=128)
    b.text("display_italic", 12, 10, 37, "Strike it rich in love.", LINK, shadow="#000000", fit=128)
    b.bm(F3, 10, 54, "VIDEO DATING FOR THE MINING FRONTIER", TEXT)
    return b


# -- SpaceGPT ------------------------------------------------------------------------------------------------------


def spacegpt():
    BG, TEXT, HEAD, LINK = "#101418", "#D9E2E8", "#19C37D", "#8AB4F8"
    b = Banner(BG)
    # The machine that answers: the game's AI core, dim behind the prompt.
    b.sprite(("_RMC14/Structures/Machines/AIStuff/aicore.rsi", "hydra"), 214, 66, anchor="bc", dim=0.8)
    b.sprite(("Objects/Fun/pondering_orb.rsi", "icon"), 150, 22, anchor="cc")
    glow(b, 150, 22, 10, 10, LINK, 0.3)
    b.text("roboto_bold", 22, 8, 8, "SpaceGPT", HEAD, fit=110)
    b.bm(F3, 10, 29, "ASK IT ANYTHING. IT WILL", TEXT)
    b.bm(F3, 10, 35, "ANSWER SOMETHING.", TEXT)
    b.rect(8, 46, 160, 60, "#1e2630")
    frame(b, "#3a4a5a", None, None, 8, 46, 160, 60)
    b.bm(F3, 12, 51, "> IS THE PROCESSOR SAFE?", LINK)
    b.rect(110, 50, 113, 55, HEAD)
    b.rect(142, 49, 156, 57, HEAD)
    b.bm(F3, 145, 51, "ASK", "#101418")
    return b


# -- VectorQuest ---------------------------------------------------------------------------------------------------


def vectorquest():
    BG, TEXT, HEAD, LINK = "#0E1A0E", "#D7F5C8", "#9BE15D", "#FFE066"
    b = Banner(BG)
    # Corridors as glowing vector lines, a plotted route and the game's map blips at either end.
    for x in range(140, b.w, 12):
        b.vline(x, 0, b.h, "#14261a")
    for y in range(0, b.h, 12):
        b.hline(140, b.w - 1, y, "#14261a")
    route = [(150, 58), (150, 40), (178, 40), (178, 20), (214, 20), (214, 48), (246, 48), (246, 12)]
    for (x0, y0), (x1, y1) in zip(route, route[1:]):
        b.line(x0, y0, x1, y1, LINK)
        b.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, "#5a4a10")
    b.sprite(("_RMC14/Interface/map_blips.rsi", "supply"), 150, 58, k=2, anchor="cc")
    b.sprite(("_RMC14/Interface/map_blips.rsi", "correspondent"), 246, 12, k=2, anchor="cc")
    b.sprite(("_RMC14/Structures/hybrisa_computer_props.rsi", "mapping_comp"), 112, 64, anchor="bc")
    b.text("osd", 18, 8, 8, "VECTORQUEST", HEAD, shadow="#000000", fit=124)
    b.bm(F3, 10, 28, "MAPS AND DIRECTIONS FOR", TEXT)
    b.bm(F3, 10, 34, "THE FRONTIER", TEXT)
    b.bm(F5, 10, 46, "GET THERE.", LINK)
    b.bm(F3, 10, 56, "EVENTUALLY.", "#7aa070")
    scanlines(b, 0, 0, b.w, b.h, 0.85)
    return b


# -- Yotta! --------------------------------------------------------------------------------------------------------


def yotta():
    BG, TEXT, HEAD, LINK = "#F4F0FF", "#1A1030", "#6A0DAD", "#0033CC"
    b = Banner(BG)
    b.text("display_bold_italic", 32, 8, 6, "Yotta!", HEAD, shadow="#c8b8e8", fit=110)
    b.text("display_italic", 11, 10, 38, "The biggest index in known space", TEXT, fit=130)
    links = ("ARTS", "COLONIES", "SCIENCE", "NEWS", "JOBS")
    x = 10
    for l in links:
        x0, x1 = b.bm(F3, x, 54, l, LINK)
        b.hline(x0, x1 - 1, 60, LINK)
        x = x1 + 6
    b.sprite(("_RMC14/Structures/communications_tower_dish.rsi", "comm_tower"), 214, 70, anchor="bc")
    b.sprite(("Objects/Misc/books.rsi", "icon_planet"), 170, 24, k=2, anchor="cc")
    b.sprite(("Objects/Misc/books.rsi", "icon_stars"), 252, 24, anchor="cc")
    sticker(b, 166, 54, 12, 9, 12, ramp("#8a5a00", "#e0a000", "#ffd040", "#fff09a"), "#3a2400",
            [(F3, "NEW", "#6a0dad", -2)])
    return b


# -- YouTape -------------------------------------------------------------------------------------------------------


def youtape():
    BG, TEXT, HEAD, LINK = "#141414", "#EEEEEE", "#FF2A2A", "#FFB0B0"
    b = Banner(BG)
    x0, x1 = b.text("display_bold", 28, 8, 9, "You", TEXT, fit=60)
    b.rect(x1 + 3, 6, x1 + 82, 34, HEAD)
    b.text("display_bold", 24, x1 + 8, 11, "Tape", "#ffffff", fit=68)
    b.text("display_italic", 11, 10, 42, "Broadcast yourself. Then post it.", TEXT, fit=150)
    b.bm(F3, 10, 56, "BE KIND, REWIND", LINK)
    # The game's camcorder, tape recorder and a fan of cassettes.
    b.sprite(("_RMC14/Objects/Devices/broadcasting_camera.rsi", "icon"), 196, 26, k=2, anchor="cc")
    b.sprite((CASSETTE, "taperecorder_recording"), 248, 30, anchor="cc")
    for i, state in enumerate(("cassette_red_stripe", "cassette_green", "cassette_ocean", "cassette_rainbow")):
        b.sprite((CASSETTE, state), 178 + i * 18, 60, anchor="bc")
    return b


BANNERS = {
    "AtmoCast": ("AtmoCast", atmocast),
    "ColonyPedia": ("ColonyPedia", colonypedia),
    "DropDash": ("DropDash", dropdash),
    "EBuoy": ("eBuoy", ebuoy),
    "HydrogenLine": ("The Hydrogen Line 1420", hydrogenline),
    "Indentured": ("Indentured", indentured),
    "MyBunk": ("MyBunk", mybunk),
    "OreMatch": ("OreMatch", orematch),
    "SpaceGpt": ("SpaceGPT", spacegpt),
    "VectorQuest": ("VectorQuest", vectorquest),
    "Yotta": ("Yotta!", yotta),
    "YouTape": ("YouTape", youtape),
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
