"""Banners for the colony boards that had none: the Colony Classifieds and the Colony Bounty Board.

    python Content.CMU/Design/PersonalComputers/SiteArt/Boards/Banners.py [Site ...]

Writes Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Boards/<Site>/banner.png (264x68 drawn at 1x,
saved 2x) and an attributions.yml beside it.

The classifieds are a page of newsprint small ads, each one showing the thing for sale as the game's own sprite;
the bounty board is a wall of pinned job notices with the game's cash and tools as the reward and the work. Only
paper, boards, pins and lettering are painted.
"""

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from banner_kit import *  # noqa: E402,F401,F403
from banner_kit import Banner, SITES, BW, BH  # noqa: E402

GENERATOR = "Content.CMU/Design/PersonalComputers/SiteArt/Boards/Banners.py"
OUT = os.path.join(SITES, "Boards")


def pin(b, x, y, c):
    b.set(x, y, c)
    b.set(x + 1, y, mix(c, "#000000", 0.4))
    b.set(x, y + 1, mix(c, "#000000", 0.5))


# -- Colony Classifieds --------------------------------------------------------------------------------------------


def classifieds():
    BG, TEXT, HEAD, LINK = "#F4EFE2", "#2A2418", "#8A2A12", "#1A4E8A"
    b = Banner(BG)
    noise(b, 0, 0, b.w, b.h, "#e8e0cc", 0.2, 3)
    # Masthead.
    b.hline(4, b.w - 5, 3, TEXT)
    b.hline(4, b.w - 5, 5, TEXT)
    b.text("display_bold", 20, 6, 9, "COLONY CLASSIFIEDS", HEAD, fit=150)
    b.bm(F3, 8, 27, "BUY IT - SELL IT - SWAP IT - FIND IT", TEXT)
    b.bm(F3, 8, 34, "FREE FOR EVERY COLONIST WITH AN ID", LINK)
    b.hline(4, 156, 41, TEXT)
    # Two short ads under the masthead, in newsprint type.
    b.bm(F3, 8, 45, "FOR SALE: GUITAR", "#5a5040")
    b.bm(F3, 8, 51, "ONE STRING SHORT", "#5a5040")
    b.bm(F3, 8, 57, "ASK AT HYDRO", "#5a5040")
    b.vline(80, 44, 62, "#a89a80")
    b.bm(F3, 86, 45, "WANTED: ANY", "#5a5040")
    b.bm(F3, 86, 51, "WALKMAN THAT", "#5a5040")
    b.bm(F3, 86, 57, "WORKS. SWAPS OK", "#5a5040")
    # Boxed display ads on the right, each with the game's sprite of what is on offer.
    ads = [("_RMC14/Objects/Fun/playing_cards.rsi", "hearts_ace", "$2", "CARDS"),
           ("_RMC14/Objects/Tools/Toolboxes/toolbox_red.rsi", "icon", "$15", "TOOLS"),
           ("_RMC14/Objects/Devices/cassette_player.rsi", "walkman", "SWAP", "WALKMAN"),
           ("Objects/Vehicles/atv.rsi", "vehicle", "$900", "ATV")]
    for i, (rsi, state, price, label) in enumerate(ads):
        x0, y0 = 162 + (i % 2) * 50, 8 + (i // 2) * 30
        b.rect(x0, y0, x0 + 46, y0 + 27, "#fbf8f0")
        frame(b, TEXT, None, None, x0, y0, x0 + 46, y0 + 27)
        b.bm(F3, x0 + 23, y0 + 3, label, TEXT, anchor="c")
        b.hline(x0 + 2, x0 + 44, y0 + 9, "#c8bca0")
        b.sprite((rsi, state), x0 + 11, y0 + 26, anchor="bc")
        b.bm(F3 if len(price) > 3 else F5, x0 + 44, y0 + 16, price, HEAD, anchor="r")
    b.hline(4, b.w - 5, b.h - 3, TEXT)
    return b


# -- Colony Bounty Board -------------------------------------------------------------------------------------------


def bounties():
    BG, TEXT, HEAD, LINK = "#16120A", "#E8DCC0", "#F2C230", "#E8A040"
    b = Banner(BG)
    # A plank wall.
    for y in range(b.h):
        for x in range(b.w):
            b.set(x, y, "#221a0e" if (x // 22) % 2 else "#1c160c")
        if y % 17 == 0:
            b.hline(0, b.w - 1, y, "#0e0a04")
    # Pinned notices.
    notices = [(146, 6, 46, 30, "WANTED", "FIND MY", "DOG", "$50"),
               (198, 6, 58, 30, "JOB", "FIX AIR", "SCRUBBER", "$120"),
               (152, 41, 42, 22, "HAUL", "CRATES", "", "$30")]
    for x0, y0, w, h, head, l1, l2, reward in notices:
        b.rect(x0 + 1, y0 + 1, x0 + w + 1, y0 + h + 1, "#0a0602")
        b.rect(x0, y0, x0 + w, y0 + h, "#e8dcc0")
        b.bm(F3, x0 + w // 2, y0 + 3, head, "#8a2a12", anchor="c")
        b.bm(F3, x0 + 3, y0 + 10, l1, "#2a2418")
        if l2:
            b.bm(F3, x0 + 3, y0 + 16, l2, "#2a2418")
        b.bm(F3, x0 + w - 3, y0 + h - 7, reward, "#1a6a1a", anchor="r")
        pin(b, x0 + w // 2, y0 + 1, "#e02a2a")
    # The reward: the game's cash; the work: the game's wrench and crowbar.
    b.sprite(("_RMC14/Objects/Misc/spacecash.rsi", "spacecash_1000"), 246, 64, anchor="bc")
    b.sprite(("_RMC14/Objects/Misc/spacecash.rsi", "spacecash_1000"), 226, 65, anchor="bc")
    b.sprite(("_RMC14/Objects/Tools/wrench.rsi", "icon"), 222, 46, anchor="cc")
    b.sprite(("_RMC14/Objects/Tools/crowbar.rsi", "red-icon"), 208, 54, anchor="cc")
    b.text("display_bold", 13, 8, 8, "JOBS WITH CASH ON THE TABLE", LINK, shadow="#000000", fit=130)
    b.text("display_bold", 24, 8, 23, "BOUNTY BOARD", HEAD, shadow="#000000", fit=130)
    b.bm(F3, 8, 47, "FOUND - FIXED - FETCHED", TEXT, shadow="#000000")
    b.bm(F3, 8, 55, "MONEY HELD UNTIL THE JOB IS DONE", "#a8987a", shadow="#000000")
    frame(b, "#000000", "#3a2a14", None)
    return b


BANNERS = {
    "Classifieds": ("Colony Classifieds", classifieds),
    "Bounties": ("Colony Bounty Board", bounties),
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
