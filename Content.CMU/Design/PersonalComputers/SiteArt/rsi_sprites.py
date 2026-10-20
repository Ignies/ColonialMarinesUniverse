"""Loads frames of the game's real sprites from .rsi folders so the site art can reuse them.

The project rule for the personal computer art: anything that exists in the game as a sprite (pods, eggs,
xenos, marines' gear, Working Joes, items, machines) is shown with that sprite, never painted again by hand.
Generators import this module and paste the frames into their own canvases, scaled with nearest neighbour.

    from rsi_sprites import frame, frames
    pod = frame("_RMC14/Structures/Machines/hypersleep.rsi", "closed")   # PIL RGBA image, 32x32
    big = scaled(pod, 3)                                                    # 96x96, nearest neighbour

Paths are relative to Resources/Textures or Content.CMU/Resources/Textures; the first folder that holds the
RSI wins. Frames are laid out the way RobustToolbox packs them: every direction's frames in order, left to
right and top to bottom across the sheet.
"""

import json
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
TEXTURE_ROOTS = [
    os.path.join(REPO, "Content.CMU", "Resources", "Textures"),
    os.path.join(REPO, "Resources", "Textures"),
]

_cache = {}


def rsi_path(rsi):
    for root in TEXTURE_ROOTS:
        path = os.path.join(root, rsi)
        if os.path.isdir(path):
            return path
    raise FileNotFoundError(rsi)


def meta(rsi):
    with open(os.path.join(rsi_path(rsi), "meta.json"), encoding="utf-8-sig") as f:
        return json.load(f)


def frames(rsi, state, direction=0):
    """Every frame of one direction of a state, as RGBA images of the RSI's size."""
    key = (rsi, state, direction)
    if key in _cache:
        return [f.copy() for f in _cache[key]]
    m = meta(rsi)
    w, h = m["size"]["x"], m["size"]["y"]
    st = next((s for s in m["states"] if s["name"] == state), None)
    if st is None:
        raise KeyError(f"{rsi} has no state {state}")
    delays = st.get("delays")
    dirs = st.get("directions", 1)
    per_dir = [len(d) for d in delays] if delays else [1] * dirs
    sheet = Image.open(os.path.join(rsi_path(rsi), f"{state}.png")).convert("RGBA")
    cols = max(1, sheet.width // w)
    start = sum(per_dir[:direction])
    out = []
    for i in range(start, start + per_dir[direction]):
        x, y = (i % cols) * w, (i // cols) * h
        out.append(sheet.crop((x, y, x + w, y + h)))
    _cache[key] = [f.copy() for f in out]
    return out


def frame(rsi, state, index=0, direction=0):
    fs = frames(rsi, state, direction)
    return fs[index % len(fs)]


def scaled(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST) if k != 1 else img.copy()


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


def tinted(img, colour, amount=1.0):
    """Recolours the sprite's opaque pixels toward colour by amount, keeping its own shading as brightness."""
    c = colour.lstrip("#") if isinstance(colour, str) else None
    rgb = (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16)) if c else colour[:3]
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            lum = (r * 30 + g * 59 + b * 11) / 25500
            tr, tg, tb = (int(v * (0.35 + lum * 0.9)) for v in rgb)
            px[x, y] = (int(r + (min(255, tr) - r) * amount), int(g + (min(255, tg) - g) * amount),
                        int(b + (min(255, tb) - b) * amount), a)
    return out


def silhouette(img, colour, alpha=255):
    c = colour.lstrip("#")
    rgb = (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            if src[x, y][3] > 40:
                dst[x, y] = rgb + (alpha,)
    return out


def darkened(img, k):
    """Multiplies the sprite's colour by k (0..1), keeping alpha: a dim, half seen sprite."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            px[x, y] = (int(r * k), int(g * k), int(b * k), a)
    return out


def outlined(img, colour):
    """Adds a one pixel outline of colour around the sprite's opaque pixels."""
    c = colour.lstrip("#")
    rgb = (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)
    out = Image.new("RGBA", (img.width + 2, img.height + 2), (0, 0, 0, 0))
    src = img.load()
    dst = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if src[x, y][3] > 40:
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    dst[x + 1 + dx, y + 1 + dy] = rgb
    out.alpha_composite(img, (1, 1))
    return out


def compose(layers, direction=0, index=0):
    """Stacks (rsi, state) layers into one image, bottom layer first, the way the game layers a sprite."""
    out = None
    for rsi, state in layers:
        f = frame(rsi, state, index, direction)
        out = f if out is None else Image.alpha_composite(out, f)
    return out


# The Working Joe as the game draws it: the Joe body parts (CMU14/Mobs/WorkingJoe/parts.rsi) in the mob's layer
# order (AU14MobWorkingJoe), its jumpsuit (CMJumpsuitAverageJoe) and black boots (CMBootsBlack), from
# AU14GearWorkingJoeBase.
_JOE = "CMU14/Mobs/WorkingJoe/parts.rsi"
WORKING_JOE = [
    (_JOE, "torso_m"), (_JOE, "head_m"), (_JOE, "r_arm"), (_JOE, "l_arm"), (_JOE, "r_leg"), (_JOE, "l_leg"),
    ("_RMC14/Objects/Clothing/Uniforms/Synthetic/joe.rsi", "equipped-INNERCLOTHING"),
    (_JOE, "l_foot"), (_JOE, "r_foot"), (_JOE, "l_hand"), (_JOE, "r_hand"),
    ("_RMC14/Objects/Clothing/Shoes/Boots/black.rsi", "equipped-FEET"),
]

# Directions in RSI order.
SOUTH, NORTH, EAST, WEST = 0, 1, 2, 3


def working_joe(direction=SOUTH):
    return compose(WORKING_JOE, direction)


def paste(base, img, x, y):
    """Alpha composites img onto the PIL image base at x, y, clipping at the edges."""
    x0, y0 = max(0, -x), max(0, -y)
    x1, y1 = min(img.width, base.width - x), min(img.height, base.height - y)
    if x1 <= x0 or y1 <= y0:
        return
    base.alpha_composite(img.crop((x0, y0, x1, y1)), (x + x0, y + y0))
