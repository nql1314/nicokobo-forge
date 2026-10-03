"""Export the ImageGen workshop card to the native 2x1 inventory sprite.

Only asset sizing and alpha preparation happen here; the artwork is in
src/Nicokobo.Forge/Assets/IconSources/nico_card.png.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src/Nicokobo.Forge/Assets"
SOURCE = ASSETS / "IconSources/nico_card.png"
DESTINATION = ASSETS / "Icons/nico_card.png"


def main():
    source = Image.open(SOURCE).convert("RGBA")
    alpha = source.getchannel("A")
    if alpha.getextrema()[0] != 0:
        raise ValueError("The generated card must have a transparent background")
    bounds = alpha.point(lambda value: 255 if value >= 112 else 0).getbbox()
    if bounds is None:
        raise ValueError("The generated card is empty")
    art = source.crop(bounds)
    scale = min(30 / art.width, 14 / art.height)
    art = art.resize((round(art.width * scale), round(art.height * scale)), Image.Resampling.NEAREST)
    art.putalpha(art.getchannel("A").point(lambda value: 255 if value >= 112 else 0))
    sprite = Image.new("RGBA", (32, 16))
    sprite.alpha_composite(art, ((32 - art.width) // 2, (16 - art.height) // 2))
    left, top, right, bottom = sprite.getbbox()
    if min(left, top, 32 - right, 16 - bottom) < 1:
        raise ValueError("Inventory sprite requires a transparent border")
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    sprite.save(DESTINATION, optimize=True)

    # Design preview of the header coordinates in ForgeWorkshopOverlay.
    # This is a local rendering, not a native game screenshot.
    preview = Image.new("RGB", (1100, 244), (40, 34, 41))
    draw = ImageDraw.Draw(preview)
    header, accent, ink = (60, 48, 59), (196, 110, 71), (230, 219, 204)
    draw.rectangle((20, 20, 1080, 78), fill=(158, 87, 69))
    draw.rectangle((23, 23, 1077, 75), fill=header)
    x, y = 39, 32
    draw.rectangle((x, y, x + 33, y + 33), fill=accent)
    draw.rectangle((x + 2, y + 2, x + 31, y + 31), fill=header)
    draw.rectangle((x + 7, y + 7, x + 11, y + 26), fill=ink)
    draw.rectangle((x + 22, y + 7, x + 26, y + 26), fill=ink)
    for pixel in range(5):
        dx, dy = x + 10 + pixel * 3, y + 7 + pixel * 3
        draw.rectangle((dx, dy, dx + 4, dy + 7), fill=ink)
    font = "C:/Windows/Fonts/msyh.ttc"
    draw.text((85, 49), "Nico工坊", font=ImageFont.truetype(font, 27), fill=accent, anchor="lm")
    draw.text((1007, 49), "nicokobo.com", font=ImageFont.truetype(font, 20), fill=ink, anchor="rm")
    draw.text((1048, 49), "X", font=ImageFont.truetype(font, 24), fill=ink, anchor="mm")
    preview.paste(sprite.resize((256, 128), Image.Resampling.NEAREST), (54, 94),
                  sprite.resize((256, 128), Image.Resampling.NEAREST))
    draw.text((350, 129), "Nico工坊名片", font=ImageFont.truetype(font, 24), fill=ink, anchor="lm")
    draw.text((350, 167), "32 × 16 px / 2 × 1 格 · 左侧为 8 倍预览", font=ImageFont.truetype(font, 16), fill=ink, anchor="lm")
    destination = ROOT / "artifacts/nico-brand-2026-10-03/workshop-brand-preview.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    preview.save(destination)
    print(f"Exported {DESTINATION}: 32x16 RGBA, binary alpha, transparent border")
    print(f"Design preview: {destination}")


if __name__ == "__main__":
    main()
