"""
ClipLite event sounds, synthesised from scratch — filtered noise and glides, no samples.

    python tools/synth_sounds.py assets/sounds

Writes four 16-bit PCM mono 44.1 kHz files, all levelled to the same loudness (-22 dBFS RMS):

  Capture  short percussive knock with its body around 600-800 Hz
  Erase    noisy glide falling from 2.9 kHz down to 110 Hz
  Ignore   steady 826 Hz buzz, then a rising sweep that fades, then a short fall
  Append   bright tick, a pause, then a softer blip falling 1100 -> 830 Hz

Rebuild the executable afterwards: the WAVs are embedded as resources.
"""
import struct, array, math, random, sys, os

RATE = 44100
random.seed(20260916)

def buf(sec):
    return [0.0] * int(sec * RATE)

def svf_noise(out, start, dur, f_from, f_to, q, amp, env, seed_noise=None):
    """White noise through a state-variable band-pass whose centre glides f_from -> f_to."""
    n = int(dur * RATE); s0 = int(start * RATE)
    low = band = 0.0
    for i in range(n):
        if s0 + i >= len(out): break
        p = i / max(n - 1, 1)
        fc = f_from * (f_to / f_from) ** p              # exponential glide sounds natural
        f = 2 * math.sin(math.pi * min(fc, RATE * 0.24) / RATE)
        x = random.uniform(-1, 1)
        high = x - low - q * band
        band += f * high
        low += f * band
        out[s0 + i] += band * amp * env(p)

def tone(out, start, dur, f_from, f_to, amp, env, wave='sine'):
    n = int(dur * RATE); s0 = int(start * RATE)
    phase = 0.0
    for i in range(n):
        if s0 + i >= len(out): break
        p = i / max(n - 1, 1)
        f = f_from * (f_to / f_from) ** p
        phase += 2 * math.pi * f / RATE
        v = math.sin(phase)
        if wave == 'soft':
            v = math.tanh(2.2 * v) / math.tanh(2.2)
        out[s0 + i] += v * amp * env(p)

def modes(out, start, freqs, amps, decays, dur):
    """Inharmonic partials — what gives a knock its body."""
    n = int(dur * RATE); s0 = int(start * RATE)
    for f, a, d in zip(freqs, amps, decays):
        ph = random.uniform(0, 2 * math.pi)
        for i in range(n):
            if s0 + i >= len(out): break
            t = i / RATE
            out[s0 + i] += math.sin(2 * math.pi * f * t + ph) * a * math.exp(-d * t)

# envelope helpers -------------------------------------------------------------
def perc(attack=0.02, curve=4.0):
    """Fast attack, exponential decay."""
    return lambda p: (p / attack if p < attack else math.exp(-curve * (p - attack)))

def plateau(rise=0.22, fall_to=0.62):
    """Rises, holds, then eases down — the shape measured on Erase."""
    def f(p):
        if p < rise: return (p / rise) ** 0.7
        return 1.0 - (1.0 - fall_to) * ((p - rise) / (1 - rise)) ** 1.4
    return f

def flat_then_fade(hold=0.5):
    return lambda p: 1.0 if p < hold else max(0.0, 1.0 - (p - hold) / (1 - hold)) ** 0.8

# the four sounds --------------------------------------------------------------
def capture():
    b = buf(0.22)
    svf_noise(b, 0.008, 0.20, 620, 480, 0.13, 1.0, perc(0.03, 9))
    svf_noise(b, 0.008, 0.02, 1500, 1000, 0.5, 0.06, perc(0.10, 14))   # the initial tick
    modes(b, 0.010,
          [549, 654, 735, 779, 826, 875],
          [0.09, 0.12, 0.13, 0.11, 0.08, 0.03],
          [26, 24, 20, 17, 15, 22], 0.20)
    return b

def erase():
    b = buf(0.275)
    svf_noise(b, 0.000, 0.150, 2900, 900, 0.13, 0.70, plateau(0.16, 0.85))
    tone(b, 0.000, 0.150, 2850, 900, 0.62, plateau(0.16, 0.85))
    svf_noise(b, 0.150, 0.112, 900, 105, 0.13, 0.48, lambda p: 1.0 - 0.45 * p)
    tone(b, 0.150, 0.112, 900, 110, 0.42, lambda p: 1.0 - 0.45 * p)
    return b

def ignore():
    b = buf(0.73)
    svf_noise(b, 0.000, 0.012, 2600, 2000, 0.4, 0.30, perc(0.08, 12))  # opening click
    # steady buzz
    svf_noise(b, 0.020, 0.270, 830, 830, 0.08, 0.45, lambda p: 1.0 if p < 0.85 else 1.0 - (p - 0.85) / 0.15 * 0.25)
    tone(b, 0.020, 0.270, 826, 826, 0.42, lambda p: 1.0 if p < 0.85 else 1.0 - (p - 0.85) / 0.15 * 0.25, 'soft')
    tone(b, 0.020, 0.270, 735, 735, 0.10, lambda p: 1.0, 'sine')
    # rising sweep that fades out
    svf_noise(b, 0.280, 0.270, 870, 2850, 0.11, 0.45, lambda p: (1 - p) ** 1.5)
    tone(b, 0.280, 0.270, 875, 2900, 0.46, lambda p: (1 - p) ** 1.8)
    # short fall at the tail
    svf_noise(b, 0.560, 0.150, 2300, 1150, 0.13, 0.22, lambda p: math.sin(math.pi * p) ** 0.8)
    tone(b, 0.560, 0.150, 2250, 1160, 0.15, lambda p: math.sin(math.pi * p) ** 0.8)
    return b

def append():
    b = buf(0.44)
    # bright tick
    svf_noise(b, 0.000, 0.055, 1700, 1100, 0.22, 0.30, perc(0.08, 11))
    modes(b, 0.000, [1172, 1479, 1979], [0.07, 0.05, 0.03], [45, 55, 60], 0.05)
    # softer blip after the pause
    svf_noise(b, 0.218, 0.165, 1120, 820, 0.12, 0.85, perc(0.18, 7))
    tone(b, 0.218, 0.165, 1105, 830, 0.52, perc(0.18, 6), 'soft')
    tone(b, 0.218, 0.165, 1043, 928, 0.08, perc(0.14, 8))
    return b

# output -----------------------------------------------------------------------
def finish(b, target_rms=0.079, peak_limit=0.92, soften=0.0):
    if soften:
        # the reference set is dense and low-crest; gentle saturation takes the spikes off
        peak = max(abs(v) for v in b) or 1e-9
        b = [math.tanh(soften * v / peak) / math.tanh(soften) * peak for v in b]
    fade = int(0.006 * RATE)
    for i in range(min(fade, len(b))):
        b[i] *= i / fade
        b[len(b) - 1 - i] *= i / fade
    rms = math.sqrt(sum(v * v for v in b) / len(b)) or 1e-9
    peak = max(abs(v) for v in b) or 1e-9
    gain = min(target_rms / rms, peak_limit / peak)
    return [v * gain for v in b]

def write(path, b):
    pcm = array.array('h', [max(-32767, min(32767, int(round(v * 32767)))) for v in b])
    data = pcm.tobytes()
    with open(path, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(data)) + b'WAVEfmt ')
        f.write(struct.pack('<IHHIIHH', 16, 1, 1, RATE, RATE * 2, 2, 16))
        f.write(b'data' + struct.pack('<I', len(data)) + data)

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
for name, fn, soften in (('Capture', capture, 0.0), ('Erase', erase, 2.6), ('Ignore', ignore, 1.8), ('Append', append, 1.6)):
    samples = finish(fn(), soften=soften)
    p = os.path.join(out, name + '.wav')
    write(p, samples)
    print('%-8s %.3fs  %d bytes' % (name, len(samples) / RATE, os.path.getsize(p)))
