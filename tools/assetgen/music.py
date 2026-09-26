"""Prozeduraler Soundtrack: loopende Stücke je Ort (CC0).

Bewusst dieselbe Technik wie media.py – reine Standardbibliothek, 16-Bit-Mono-WAV.
Die Stücke sind nahtlos loopbar: Jedes Stück ist ein ganzzahliges Vielfaches eines Taktes,
und Anfang wie Ende liegen in der Stille zwischen zwei Anschlägen.

Musikalische Idee: eine tiefe, langsam schwebende Drone trägt das Stück, darüber eine
Akkordfolge in Moll und eine sehr sparsame Melodie. Je tiefer der Kreis, desto tiefer die
Grundstimmung und desto mehr Rauschen liegt darüber.
"""
import math

from .core import rng
from .media import SAMPLE_RATE, write_wav

# Halbtonabstände zum Kammerton. Zwölfstufig gleichstufig: jeder Halbton ist Faktor 2^(1/12).
A4 = 440.0


def note(semitones_from_a4, octave_shift=0):
    """Frequenz eines Tons, angegeben in Halbtonschritten relativ zu A4."""
    return A4 * (2.0 ** ((semitones_from_a4 + 12 * octave_shift) / 12.0))


# Moll-Dreiklänge als Halbton-Offsets (Grundton, kleine Terz, Quinte) bzw. Dur, wo es heller werden soll.
MINOR = (0, 3, 7)
MAJOR = (0, 4, 7)
SUS = (0, 5, 7)


def _sine(frequency, seconds, amplitude, phase=0.0):
    """Reine Sinusschwingung – das weichste Material, das wir haben."""
    count = int(SAMPLE_RATE * seconds)
    step = 2 * math.pi * frequency / SAMPLE_RATE
    return [amplitude * math.sin(phase + step * i) for i in range(count)]


def _mix(target, samples, offset, gain=1.0):
    """Mischt samples ab offset additiv in target (das Stück wächst also Schicht für Schicht)."""
    for index, value in enumerate(samples):
        position = offset + index
        if 0 <= position < len(target):
            target[position] += value * gain


def _envelope(samples, attack, release):
    """Weiches Ein- und Ausblenden eines einzelnen Tons – ohne das knackt es hörbar."""
    count = len(samples)
    attack_count = max(1, int(count * attack))
    release_count = max(1, int(count * release))
    for index in range(attack_count):
        samples[index] *= index / attack_count
    for index in range(release_count):
        samples[count - 1 - index] *= index / release_count
    return samples


def _pad(root, chord, seconds, amplitude, detune=0.004):
    """Ein Akkord als Flächenklang: jede Stufe doppelt, minimal verstimmt -> lebendige Schwebung."""
    layer = [0.0] * int(SAMPLE_RATE * seconds)
    for step in chord:
        frequency = root * (2.0 ** (step / 12.0))
        _mix(layer, _sine(frequency, seconds, amplitude), 0)
        _mix(layer, _sine(frequency * (1 + detune), seconds, amplitude * 0.7), 0)
    return _envelope(layer, 0.25, 0.35)


def _drone(frequency, seconds, amplitude):
    """Grundton plus Quinte und Oktave, sehr leise – das Fundament unter allem."""
    layer = [0.0] * int(SAMPLE_RATE * seconds)
    _mix(layer, _sine(frequency, seconds, amplitude), 0)
    _mix(layer, _sine(frequency * 1.5, seconds, amplitude * 0.35), 0)
    _mix(layer, _sine(frequency * 2.0, seconds, amplitude * 0.25), 0)
    # Langsames Auf- und Abschwellen, damit die Drone atmet statt zu stehen.
    count = len(layer)
    for index in range(count):
        layer[index] *= 0.75 + 0.25 * math.sin(2 * math.pi * index / count)
    return layer


def _bell(frequency, seconds, amplitude):
    """Glockenartiger Melodieton: Grundton plus leise Oktave, schnell abklingend."""
    count = int(SAMPLE_RATE * seconds)
    layer = [0.0] * count
    _mix(layer, _sine(frequency, seconds, amplitude), 0)
    _mix(layer, _sine(frequency * 2.0, seconds, amplitude * 0.3), 0)
    for index in range(count):
        layer[index] *= (1.0 - index / count) ** 2.2
    return layer


def _kick(seconds=0.16, amplitude=0.9, accent=1.0):
    """Tiefer Stampfer: Frequenz rutscht von 110 auf 35 Hz, Exponent 3 macht den Nachfall kurz."""
    count = int(SAMPLE_RATE * seconds)
    phase = 0.0
    out = []
    for i in range(count):
        t = i / count
        phase += (110 * (1.0 - t) + 35) / SAMPLE_RATE
        out.append(math.sin(phase * 2 * math.pi) * (1.0 - t) ** 3 * amplitude * accent)
    return out


def _thud(seconds=0.10, amplitude=0.6, accent=1.0):
    """Dumpfer Holzklop: Sinus 70 Hz plus Rauschklacks, schnell endend."""
    count = int(SAMPLE_RATE * seconds)
    out = []
    for i in range(count):
        t = i / count
        body = math.sin(2 * math.pi * 70 * i / SAMPLE_RATE) * (1.0 - t) ** 4
        out.append((body + rng.uniform(-1, 1) * (1.0 - t) ** 6 * 0.4) * amplitude * accent)
    return out


def _tick(seconds=0.05, amplitude=0.35, accent=1.0):
    """Metallisches Ticken: kurzer Rauschimpuls mit hartem Ende (Uhren-Anmutung)."""
    count = int(SAMPLE_RATE * seconds)
    return [rng.uniform(-1, 1) * (1.0 - i / count) ** 2 * amplitude * accent for i in range(count)]


def _clang(seconds=0.22, amplitude=0.4, accent=1.0):
    """Schlag auf Metall: zwei verstimmt-reibende Sinusse (217/341 Hz), hart abklingend."""
    count = int(SAMPLE_RATE * seconds)
    out = []
    for i in range(count):
        t = i / count
        value = math.sin(2 * math.pi * 217 * i / SAMPLE_RATE) + math.sin(2 * math.pi * 341 * i / SAMPLE_RATE)
        out.append((value * 0.5 * (1.0 - t) ** 2.5 * amplitude + rng.uniform(-1, 1) * (1.0 - t) ** 5 * 0.15) * accent)
    return out


def _compose(chords, root, bars, bar_seconds, melody, melody_gain, drone_gain, brightness,
             percussion=None, hits=None):
    """
    Baut ein ganzes Stück.

    chords       Liste von (Halbton-Offset zum Grundton, Akkordtyp) – eine Stufe pro Takt
    root         Grundfrequenz des Stücks
    bars         Anzahl Takte (Stücklänge = bars * bar_seconds)
    melody       Halbton-Offsets der Melodietöne; None an einer Stelle = Pause
    brightness   0 = dumpf und tief, 1 = hell; mischt die Melodie eine Oktave höher
    percussion   None oder Klangfabrik (seconds, amplitude) -> samples; gespielt auf jede Position in hits
    hits         None oder Liste von Schlagpositionen im Takt (0..1); None am Takanfang = Akzent
    """
    total = int(SAMPLE_RATE * bar_seconds * bars)
    track = [0.0] * total

    # 1) Drone über die volle Länge
    _mix(track, _drone(root / 2.0, bar_seconds * bars, 0.16 * drone_gain), 0)

    # 2) Akkordfläche: ein Akkord pro Takt
    for bar in range(bars):
        offset_semitones, chord = chords[bar % len(chords)]
        chord_root = root * (2.0 ** (offset_semitones / 12.0))
        _mix(track, _pad(chord_root, chord, bar_seconds * 0.95, 0.075), int(bar * bar_seconds * SAMPLE_RATE))

    # 3) Melodie: ein Ton pro halbem Takt, Pausen bleiben leer
    if melody:
        step_seconds = bar_seconds / 2.0
        steps = bars * 2
        for step in range(steps):
            semitone = melody[step % len(melody)]
            if semitone is None:
                continue
            frequency = root * (2.0 ** (semitone / 12.0)) * (2.0 if brightness > 0.5 else 1.0)
            _mix(track, _bell(frequency, step_seconds * 0.9, 0.12 * melody_gain), int(step * step_seconds * SAMPLE_RATE))

    # 3b) Percussion: Klang auf jede Position in hits, einmal pro Takt; None in hits = Takanfang-Akzent
    if percussion and hits:
        for bar in range(bars):
            for position in hits:
                accent = 1.0 if position is None else 0.55
                offset = int((bar + (position if position is not None else 0.0)) * bar_seconds * SAMPLE_RATE)
                _mix(track, percussion(accent=accent), offset)

    # 4) Sanfte Begrenzung statt hartem Clipping – tanh drückt Spitzen weich zusammen.
    track = [math.tanh(value * 1.4) * 0.55 for value in track]

    # 5) Nahtstelle entschärfen: Die Drone endet nicht zwangsläufig im Nulldurchgang, ein harter
    #    Sprung beim Loop wäre als Knacken hörbar. 15 ms Ein-/Ausblende sind unhörbar kurz.
    seam = int(SAMPLE_RATE * 0.015)
    for index in range(min(seam, len(track) // 2)):
        factor = index / seam
        track[index] *= factor
        track[len(track) - 1 - index] *= factor
    return track


def generate(audio):
    """Schreibt alle Musikstücke nach Content/Audio. IDs siehe Content/manifest.json."""
    # ---------------------------------------------------------------- Titelbild
    # Feierlich, getragen: i - VI - III - VII in a-Moll.
    write_wav(audio, "music_title", _compose(
        chords=[(0, MINOR), (-4, MAJOR), (3, MAJOR), (-2, MAJOR)],
        root=note(0, -1), bars=8, bar_seconds=3.2,
        melody=[0, None, 7, None, 3, None, 5, None],
        melody_gain=1.0, drone_gain=1.0, brightness=1.0))

    # ---------------------------------------------------------------- Tempel (Hub)
    # Warm und ruhig, viel Raum zwischen den Tönen – hier soll man verweilen wollen.
    write_wav(audio, "music_hub", _compose(
        chords=[(0, MAJOR), (5, MAJOR), (-3, MINOR), (-5, SUS)],
        root=note(-5, -1), bars=8, bar_seconds=3.6,
        melody=[0, None, None, 4, None, 7, None, None],
        melody_gain=0.8, drone_gain=0.8, brightness=1.0))

    # ---------------------------------------------------------------- Limbo (erster Kreis)
    # Schwebend und unentschieden: Quartvorhalte, die sich nie ganz auflösen.
    write_wav(audio, "music_limbo", _compose(
        chords=[(0, SUS), (0, MINOR), (-2, SUS), (-4, MAJOR)],
        root=note(-3, -1), bars=8, bar_seconds=4.0,
        melody=[0, None, None, None, 5, None, None, None],
        melody_gain=0.6, drone_gain=1.0, brightness=0.6))

    # ---------------------------------------------------------------- Habgier
    # Unruhiger, engere Schritte – das Gold flimmert.
    write_wav(audio, "music_greed", _compose(
        chords=[(0, MINOR), (2, MINOR), (-1, MAJOR), (0, MINOR)],
        root=note(-7, -1), bars=8, bar_seconds=3.0,
        melody=[0, 3, None, 2, None, 7, 5, None],
        melody_gain=0.7, drone_gain=1.0, brightness=0.6))

    # ---------------------------------------------------------------- Zorn
    # Tief, dicht, drängend: Halbtonreibung im Bass.
    write_wav(audio, "music_wrath", _compose(
        chords=[(0, MINOR), (1, MAJOR), (0, MINOR), (-2, MINOR)],
        root=note(-10, -1), bars=8, bar_seconds=2.6,
        melody=[0, None, 1, None, 0, None, -2, None],
        melody_gain=0.65, drone_gain=1.2, brightness=0.0))

    # ---------------------------------------------------------------- Boss
    # Kürzeste Takte, kein Ausruhen: derselbe Akkord kehrt immer wieder zurück.
    write_wav(audio, "music_boss", _compose(
        chords=[(0, MINOR), (-1, MAJOR), (0, MINOR), (3, MINOR)],
        root=note(-12, -1), bars=8, bar_seconds=2.2,
        melody=[0, 0, 3, 0, 5, 3, 2, 0],
        melody_gain=0.9, drone_gain=1.3, brightness=0.0))

    # ---------------------------------------------------------------- Boss-Stücke (G17)
    # Ein eigenes Stück je Arena. Alle wie music_boss lang (8 Takte × 2,2 s = 17,6 s) und laut
    # (gleiche _compose-Normierung), damit das Ueberblenden nicht springt.

    # Hain des Hirten — getragen, chorartig, trauernd: der Hirte ist kein Boesewicht.
    # Suzuquart-Kirche (SUS als Vorhalt), Melodie erst hoch (Chor) und dann fallend, sehr langsam.
    # Etwas lauter als die Stimmung naeligt (drone_gain), sonst faellt das Ueberblenden hoorbar ab.
    write_wav(audio, "music_boss_shepherd", _compose(
        chords=[(0, SUS), (-5, MINOR), (-3, MAJOR), (-2, SUS)],
        root=note(-5, -1), bars=8, bar_seconds=2.2,
        melody=[7, None, 5, 4, None, 3, 0, None],
        melody_gain=1.05, drone_gain=1.05, brightness=1.0))

    # Mammons Hort — gierig, hektisch, klimpernd: keine Pause in der Melodie,
    # _thud auf jede Viertel wie unruhige Finger auf Goldmuenzen.
    write_wav(audio, "music_boss_mammon", _compose(
        chords=[(0, MINOR), (2, MINOR), (-1, MAJOR), (0, MINOR)],
        root=note(-3, -1), bars=8, bar_seconds=2.2,
        melody=[0, 3, 2, 3, 5, 3, 7, 3],
        melody_gain=0.8, drone_gain=1.0, brightness=1.0,
        percussion=lambda accent=0.55: _thud(amplitude=0.5, accent=accent),
        hits=[0.0, 0.25, 0.5, 0.75]))

    # Mauern von Dis — schwer, stampfend, tief, wenig Melodie:
    # _kick auf jede halbe Taktlaenge, Melodie nur zwei Toene, brightness 0.
    write_wav(audio, "music_boss_titan", _compose(
        chords=[(0, MINOR), (0, MINOR), (-2, MINOR), (-2, MINOR)],
        root=note(-15, -1), bars=8, bar_seconds=2.2,
        melody=[0, None, None, None, 0, None, -2, None],
        melody_gain=0.55, drone_gain=1.5, brightness=0.0,
        percussion=lambda accent=0.55: _kick(amplitude=0.8, accent=accent),
        hits=[0.0, 0.5]))

    # Kerkerhof — dumpf, klopfend, bedrueckend eng:
    # _thud auf 0 und 0.5 (wie Schritte auf Stein), Akkorde eng, Melodie fast nicht da.
    write_wav(audio, "music_warden_limbo", _compose(
        chords=[(0, MINOR), (1, MINOR), (0, MINOR), (-2, MINOR)],
        root=note(-10, -1), bars=8, bar_seconds=2.2,
        melody=[0, None, None, 1, None, None, 0, None],
        melody_gain=0.5, drone_gain=1.2, brightness=0.0,
        percussion=lambda accent=0.55: _thud(amplitude=0.6, accent=accent),
        hits=[0.0, 0.5]))

    # Schuldturm — tickend wie eine Uhr, draengend:
    # _tick auf jede Viertel (Uhren-Metrum), Melodie in Achteln vorwaerts getrieben.
    write_wav(audio, "music_warden_greed", _compose(
        chords=[(0, MINOR), (3, MINOR), (-2, MINOR), (1, MINOR)],
        root=note(-7, -1), bars=8, bar_seconds=2.2,
        melody=[0, 2, 3, 2, 0, 2, 1, 2],
        melody_gain=0.7, drone_gain=1.1, brightness=0.5,
        percussion=lambda accent=0.55: _tick(amplitude=0.4, accent=accent),
        hits=[0.0, 0.25, 0.5, 0.75]))

    # Folterkammer — schrill, metallisch, haemmernd:
    # _clang auf 0/0.5 (Schlag auf Eisen) und _tick dazwischen, Melodie in Reibung.
    write_wav(audio, "music_warden_wrath", _compose(
        chords=[(0, MINOR), (1, MAJOR), (0, MINOR), (6, MINOR)],
        root=note(-12, -1), bars=8, bar_seconds=2.2,
        melody=[0, 1, 0, 1, 3, 1, 0, -2],
        melody_gain=0.75, drone_gain=1.4, brightness=0.0,
        percussion=lambda accent=0.55: _clang(amplitude=0.45, accent=accent),
        hits=[0.0, 0.5]))

    # ---------------------------------------------------------------- Die sechs neuen Kreise
    # Gleiches Format wie limbo/greed/wrath (8 Takte, dieselbe Normierung), damit das Überblenden
    # zwischen den Ebenen nicht in der Lautstärke springt. Die Taktlänge wird nach unten kürzer:
    # Limbus 4,0 s, Verrat 2,4 s — der Abstieg wird hörbar unruhiger.
    #
    # Bewusst GANZ AM ENDE: Die Percussion-Fabriken zeichnen aus dem gemeinsamen Zufallsstrom.
    # Weiter oben eingefügt hätten diese sechs Stücke alle danach erzeugten Stücke verschoben —
    # music_boss_mammon und die drei Wärter-Stücke klangen prompt anders.

    # Wollust — der Sturm, der nie nachlässt: kreisende Melodie ohne Grundton-Ruhe,
    # SUS-Vorhalte, die sich nicht auflösen. Zwei Stimmen, die einander nie erreichen.
    write_wav(audio, "music_lust", _compose(
        chords=[(0, MINOR), (-2, SUS), (3, MINOR), (-4, SUS)],
        root=note(-2, -1), bars=8, bar_seconds=3.6,
        melody=[7, 5, None, 3, 5, None, 7, None],
        melody_gain=0.7, drone_gain=0.9, brightness=0.8))

    # Völlerei — schwer und satt, schleppend: tiefe Akkorde, dumpfe Tropfen auf jede Halbe.
    write_wav(audio, "music_gluttony", _compose(
        chords=[(0, MINOR), (0, MINOR), (-3, MAJOR), (-1, MINOR)],
        root=note(-8, -1), bars=8, bar_seconds=3.4,
        melody=[0, None, None, -2, None, None, 3, None],
        melody_gain=0.55, drone_gain=1.15, brightness=0.35,
        percussion=lambda accent=0.55: _thud(amplitude=0.45, accent=accent),
        hits=[0.0, 0.5]))

    # Ketzerei — offene Gräber unter Feuer: Dur über Moll gesetzt, das klingt falsch und
    # soll es auch. Glut knistert als _tick auf den Achteln dazwischen.
    write_wav(audio, "music_heresy", _compose(
        chords=[(0, MINOR), (4, MAJOR), (0, MINOR), (1, MAJOR)],
        root=note(-9, -1), bars=8, bar_seconds=3.0,
        melody=[0, None, 4, None, 1, None, 0, None],
        melody_gain=0.68, drone_gain=1.2, brightness=0.3,
        percussion=lambda accent=0.55: _tick(amplitude=0.3, accent=accent),
        hits=[0.25, 0.75]))

    # Gewalt — stampfend, ohne Umweg: nur zwei Akkorde, _kick auf jede Viertel.
    write_wav(audio, "music_violence", _compose(
        chords=[(0, MINOR), (0, MINOR), (-1, MINOR), (-1, MINOR)],
        root=note(-13, -1), bars=8, bar_seconds=2.8,
        melody=[0, None, 0, None, -1, None, 0, None],
        melody_gain=0.6, drone_gain=1.35, brightness=0.0,
        percussion=lambda accent=0.55: _kick(amplitude=0.7, accent=accent),
        hits=[0.0, 0.25, 0.5, 0.75]))

    # Betrug — freundlich anhebend, dann kippend: beginnt in Dur und rutscht jeden Takt
    # einen Halbton tiefer weg. Genau das ist die Lüge.
    write_wav(audio, "music_fraud", _compose(
        chords=[(0, MAJOR), (-1, MAJOR), (-2, MINOR), (-3, MINOR)],
        root=note(-6, -1), bars=8, bar_seconds=2.6,
        melody=[0, 4, 3, None, -1, 2, 1, None],
        melody_gain=0.72, drone_gain=1.1, brightness=0.55))

    # Verrat — Eis: fast nur Bordun, ein einzelner Ton alle zwei Takte, metallisch kalt.
    write_wav(audio, "music_treachery", _compose(
        chords=[(0, MINOR), (0, MINOR), (0, MINOR), (-2, MINOR)],
        root=note(-16, -1), bars=8, bar_seconds=2.4,
        melody=[0, None, None, None, None, None, -2, None],
        melody_gain=0.5, drone_gain=1.5, brightness=0.0,
        percussion=lambda accent=0.55: _clang(amplitude=0.3, accent=accent),
        hits=[0.0]))

    # ---------------------------------------------------------------- Boss-Stücke der sechs neuen Kreise
    # Die Bosse tempest/cerberus/heresiarch/minotaur/geryon/lucifer liegen bisher auf
    # music.boss. Hier je ein eigenes Stück, Format wie music_boss (8 Takte × 2,2 s,
    # dieselbe _compose-Normierung), damit das Überblenden nicht in der Lautstärke springt.
    #
    # Bewusst GANZ AM ENDE, hinter den sechs Kreis-Stücken: Die Percussion-Fabriken
    # (_thud, _tick, _kick, _clang) zeichnen aus dem gemeinsamen Zufallsstrom. Alles, was
    # weiter oben eingefügt wird, verschiebt jede danach erzeugte Percussion.

    # Der Sturm der Wollust — wirbelnd, greift um sich, ruht nie:
    # kreisende Melodie (aufwärts gerückt bei jedem dritten Schritt), SUS-Vorhalte, die
    # sich nie auflösen. _tick auf die Achtel zwischen den Vierteln wie peitschende Böen.
    write_wav(audio, "music_boss_tempest", _compose(
        chords=[(0, MINOR), (2, SUS), (-2, MINOR), (3, SUS)],
        root=note(-2, -1), bars=8, bar_seconds=2.2,
        melody=[7, 5, 3, 5, 8, 5, 3, 5],
        melody_gain=0.85, drone_gain=1.1, brightness=1.0,
        percussion=lambda accent=0.55: _tick(amplitude=0.35, accent=accent),
        hits=[0.25, 0.5, 0.75]))

    # Der Schlammpfuhl der Völlerei — drei Köpfe, keiner hört auf die anderen:
    # Melodie in zwei kurzen, gegeneinander verschobenen Gruppen (0/2/4 gegen 3/5/7),
    # dazwischen schmatzt _thud auf jede Viertel. Langsam, schwer, satt.
    write_wav(audio, "music_boss_cerberus", _compose(
        chords=[(0, MINOR), (0, MINOR), (-3, MAJOR), (-1, MINOR)],
        root=note(-8, -1), bars=8, bar_seconds=2.2,
        melody=[0, None, 3, None, -2, None, 3, 5],
        melody_gain=0.7, drone_gain=1.25, brightness=0.3,
        percussion=lambda accent=0.55: _thud(amplitude=0.55, accent=accent),
        hits=[0.0, 0.25, 0.5, 0.75]))

    # Das Feld der offenen Gräber — Dur über Moll, das klingt falsch und soll es:
    # die Harmonie behauptet Frieden, die tiefere Schicht widerspricht. _tick auf die
    # Achtel wie knisternde Glut aus den aufgebrochenen Särgen.
    write_wav(audio, "music_boss_heresiarch", _compose(
        chords=[(0, MINOR), (4, MAJOR), (0, MINOR), (1, MAJOR)],
        root=note(-9, -1), bars=8, bar_seconds=2.2,
        melody=[0, 4, None, 1, 0, 4, None, 1],
        melody_gain=0.72, drone_gain=1.2, brightness=0.4,
        percussion=lambda accent=0.55: _tick(amplitude=0.32, accent=accent),
        hits=[0.25, 0.75]))

    # Die Blutfurt der Gewalt — der Ansturm: zwei Akkorde, _kick auf jede Viertel,
    # die Melodie stammelt nur den Grundton, brightness 0, kein Ausruhen.
    write_wav(audio, "music_boss_minotaur", _compose(
        chords=[(0, MINOR), (0, MINOR), (-1, MINOR), (-1, MINOR)],
        root=note(-13, -1), bars=8, bar_seconds=2.2,
        melody=[0, None, 0, -1, 0, None, 0, None],
        melody_gain=0.6, drone_gain=1.4, brightness=0.0,
        percussion=lambda accent=0.55: _kick(amplitude=0.75, accent=accent),
        hits=[0.0, 0.25, 0.5, 0.75]))

    # Der Abgrund der Malebolge — Betrug: freundlich in Dur einsetzend, dann rutscht die
    # Harmonie Takt für Takt einen Halbton tiefer weg, bis nichts vom Anfang bleibt.
    # Die Melodie tut weiter so, als wäre nichts geschehen. _thud markiert die Stufen.
    write_wav(audio, "music_boss_geryon", _compose(
        chords=[(0, MAJOR), (-1, MAJOR), (-2, MINOR), (-3, MINOR)],
        root=note(-6, -1), bars=8, bar_seconds=2.2,
        melody=[4, 7, 4, None, 2, 5, 2, None],
        melody_gain=0.75, drone_gain=1.15, brightness=0.6,
        percussion=lambda accent=0.55: _thud(amplitude=0.4, accent=accent),
        hits=[0.0]))

    # Der gefrorene Kokytos — Verrat: fast nur Bordun, ein einzelner Ton pro Takt,
    # metallisch kalt (_clang auf die Halbe), brightness 0, nichts bewegt sich weiter.
    write_wav(audio, "music_boss_lucifer", _compose(
        chords=[(0, MINOR), (0, MINOR), (0, MINOR), (-2, MINOR)],
        root=note(-16, -1), bars=8, bar_seconds=2.2,
        melody=[0, None, None, None, -2, None, None, None],
        melody_gain=0.55, drone_gain=1.5, brightness=0.0,
        percussion=lambda accent=0.55: _clang(amplitude=0.35, accent=accent),
        hits=[0.0, 0.5]))
