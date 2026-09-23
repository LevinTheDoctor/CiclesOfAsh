"""Prozeduraler Soundtrack: loopende Stücke je Ort (CC0).

Bewusst dieselbe Technik wie media.py – reine Standardbibliothek, 16-Bit-Mono-WAV.
Die Stücke sind nahtlos loopbar: Jedes Stück ist ein ganzzahliges Vielfaches eines Taktes,
und Anfang wie Ende liegen in der Stille zwischen zwei Anschlägen.

Musikalische Idee: eine tiefe, langsam schwebende Drone trägt das Stück, darüber eine
Akkordfolge in Moll und eine sehr sparsame Melodie. Je tiefer der Kreis, desto tiefer die
Grundstimmung und desto mehr Rauschen liegt darüber.
"""
import math

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


def _compose(chords, root, bars, bar_seconds, melody, melody_gain, drone_gain, brightness):
    """
    Baut ein ganzes Stück.

    chords       Liste von (Halbton-Offset zum Grundton, Akkordtyp) – eine Stufe pro Takt
    root         Grundfrequenz des Stücks
    bars         Anzahl Takte (Stücklänge = bars * bar_seconds)
    melody       Halbton-Offsets der Melodietöne; None an einer Stelle = Pause
    brightness   0 = dumpf und tief, 1 = hell; mischt die Melodie eine Oktave höher
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
