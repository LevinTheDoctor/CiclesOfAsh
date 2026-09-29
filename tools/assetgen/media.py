"""Bitmap-Fonts und Soundeffekte."""
import json
import math
import struct
import wave

from PIL import ImageDraw, ImageFont

from .core import FONT_SOURCES, new_image, rng

CHARSET = "".join(chr(code) for code in range(32, 127)) + "ÄÖÜäöüß–·…›‹"
# Controller-Glyphen (Content/Data/controllers.json). Das PlayStation-Kreuz fehlt in Tiny5
# und würde als leerer Kasten erscheinen – dafür steht dort schlicht ein "X".
CHARSET += "○□△"
# Für weitere Sprachen (Content/Lang): die übrigen westeuropäischen Buchstaben und Satzzeichen.
# Immer HINTEN anhängen – die Position im Raster ist die Glyphennummer. Beide Schriften führen
# diese Zeichen (mit fontTools geprüft), eine neue Sprache braucht also keine neue Schrift.
CHARSET += "ÀÁÂÃÅÆÇÈÉÊËÌÍÎÏÑÒÓÔÕØÙÚÛÝàáâãåæçèéêëìíîïñòóôõøùúûýÿŒœ¡¿«»‘’“”„€—"


def build_font(fonts, name, source_file, size):
    """
    Rendert eine TTF ohne Anti-Aliasing (fontmode '1') in ein Glyphenraster + JSON-Beschreibung.

    Jede Glyphe bekommt eine Zelle, in der sie VOLLSTÄNDIG liegt. Akzente auf Großbuchstaben
    (É, Ê …) ragen über die Oberlänge hinaus, manche Glyphen links über ihren Ursprung – ohne Rand
    malten sie in die Nachbarzelle, und dort stand dann etwa unter jedem "·" ein Strich.
    Der Rand steckt in glyphOffsetX/Y; BitmapFont zieht ihn beim Zeichnen wieder ab, damit die
    Grundlinie bleibt, wo sie war. Zeilenhöhe und Layout ändern sich dadurch nicht.
    """
    font = ImageFont.truetype(str(FONT_SOURCES / source_file), size)
    ascent, descent = font.getmetrics()
    boxes = [font.getbbox(ch) for ch in CHARSET]            # (links, oben, rechts, unten) je Glyphe
    offset_x = max(0, -min(box[0] for box in boxes))        # Überhang nach links
    offset_y = max(0, -min(box[1] for box in boxes))        # Überhang nach oben (Akzente)
    cell_height = max(ascent + descent, max(box[3] for box in boxes)) + offset_y
    advances = [max(1, int(round(font.getlength(ch)))) for ch in CHARSET]
    cell_width = max(max(advances), max(box[2] for box in boxes)) + 2 + offset_x
    columns = 16
    rows = math.ceil(len(CHARSET) / columns)
    atlas = new_image(columns * cell_width, rows * cell_height)
    draw = ImageDraw.Draw(atlas)
    draw.fontmode = "1"
    for index, character in enumerate(CHARSET):
        origin = ((index % columns) * cell_width + offset_x, (index // columns) * cell_height + offset_y)
        draw.text(origin, character, font=font, fill=(255, 255, 255, 255))
    atlas.save(fonts / f"{name}.png")
    descriptor = {"texture": f"Fonts/{name}.png", "cellWidth": cell_width, "cellHeight": cell_height,
                  "lineHeight": ascent + descent + 1, "glyphOffsetX": offset_x, "glyphOffsetY": offset_y,
                  "charset": CHARSET, "advances": advances}
    (fonts / f"{name}.font.json").write_text(json.dumps(descriptor, ensure_ascii=False, indent=2), encoding="utf-8")


SAMPLE_RATE = 22050


def write_wav(audio, name, samples):
    with wave.open(str(audio / f"{name}.wav"), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(SAMPLE_RATE)
        file.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767 * 0.8)) for s in samples))


def tone(start, end, seconds, kind="square", noise=0.0):
    count = int(SAMPLE_RATE * seconds)
    phase = 0.0
    output = []
    for i in range(count):
        t = i / count
        phase += (start + (end - start) * t) / SAMPLE_RATE
        if kind == "square":
            value = 1.0 if (phase % 1.0) < 0.5 else -1.0
        elif kind == "saw":
            value = 2.0 * (phase % 1.0) - 1.0
        else:
            value = math.sin(phase * 2 * math.pi)
        value = value * (1 - noise) + rng.uniform(-1, 1) * noise
        output.append(value * (1 - t) ** 2 * 0.6)
    return output


def generate(fonts, audio):
    build_font(fonts, "body", "Tiny5-Regular.ttf", 8)
    build_font(fonts, "title", "JacquardaBastarda9-Regular.ttf", 18)
    write_wav(audio, "hit", tone(220, 80, 0.10, "square", noise=0.6))
    write_wav(audio, "shoot", tone(900, 300, 0.12, "square"))
    write_wav(audio, "slash", tone(400, 120, 0.12, "saw", noise=0.5))
    write_wav(audio, "pickup", tone(700, 1400, 0.08, "sine"))
    write_wav(audio, "hurt", tone(160, 60, 0.25, "saw", noise=0.3))
    write_wav(audio, "levelup", tone(440, 440, 0.08, "square") + tone(554, 554, 0.08, "square") + tone(660, 880, 0.2, "square"))
    write_wav(audio, "roar", tone(90, 40, 0.8, "saw", noise=0.7))
    write_wav(audio, "unseal", tone(300, 900, 0.4, "sine"))
    write_wav(audio, "lever", tone(180, 120, 0.15, "square", noise=0.4) + tone(90, 90, 0.08, "square"))
    write_wav(audio, "chest", tone(260, 520, 0.18, "saw", noise=0.2) + tone(880, 1320, 0.2, "sine"))
    write_wav(audio, "crumble", tone(120, 50, 0.35, "saw", noise=0.85))
    write_wav(audio, "flap", tone(500, 300, 0.05, "square", noise=0.8))
    write_wav(audio, "gate", tone(70, 50, 0.9, "square", noise=0.5))
    write_wav(audio, "error", tone(200, 140, 0.2, "square") + tone(140, 90, 0.25, "square"))
    write_wav(audio, "splash", tone(600, 200, 0.2, "sine", noise=0.7))
