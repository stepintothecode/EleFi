/*
  Ele's sounds, synthesised with the Web Audio API.

  No audio files: a short cue is a few oscillators, so there is nothing to ship, nothing to
  download, and nothing that can fail to load. Reached only through ISoundCue, like the mascot
  is through IMascotService, so no component touches JavaScript.

  One sound per face, all from the same small elephant, chosen by ear from a board of options:
  - Pleased (happy face): a short trumpet that swoops up through the trunk, with the rough
    flutter that makes it an elephant rather than a horn, then three bright twinkles.
  - Calm (calm face, and each step of Ele's guide between the first and last): one warm bell
    with a long, quiet tail. Nothing to react to.
  - Cross (cross face): two short low trunk blasts, the second lower. Never a buzzer.

  Quiet on purpose. A habit app's chime is a nudge, not an alarm: every cue stays well under a
  tenth of full volume.
*/

let context = null;

function audio() {
  if (!context) {
    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) {
      return null;
    }

    context = new Context();
  }

  // The WebView starts audio suspended until the page has been touched; every cue follows a
  // tap, so resuming here is allowed.
  if (context.state === 'suspended') {
    context.resume();
  }

  return context;
}

/*
  One voice. The pitch can follow a path of [seconds after start, Hz] points, the tone is shaped
  by a band-pass "trunk" (the nasal, honking part of a trumpet), and flutter is a fast wobble in
  loudness that gives the rasp.
*/
function voice(ctx, out, {
  start,
  length,
  pitch,
  type = 'sawtooth',
  gain = 0.05,
  trunk = 1100,
  q = 1.2,
  flutter = 0,
  flutterDepth = 0.35,
  attack = 0.03,
}) {
  const osc = ctx.createOscillator();
  const band = ctx.createBiquadFilter();
  const soften = ctx.createBiquadFilter();
  const amp = ctx.createGain();

  osc.type = type;
  osc.frequency.setValueAtTime(pitch[0][1], start);
  for (const [at, hz] of pitch.slice(1)) {
    osc.frequency.exponentialRampToValueAtTime(hz, start + at);
  }

  band.type = 'bandpass';
  band.frequency.value = trunk;
  band.Q.value = q;

  // Takes the fizz off the top, so even the trumpet is friendly rather than shrill.
  soften.type = 'lowpass';
  soften.frequency.value = trunk * 2.6;

  // A soft attack and a natural fall, so nothing clicks.
  amp.gain.setValueAtTime(0.0001, start);
  amp.gain.exponentialRampToValueAtTime(gain, start + attack);
  amp.gain.setValueAtTime(gain, start + length * 0.7);
  amp.gain.exponentialRampToValueAtTime(0.0001, start + length);

  osc.connect(band);
  band.connect(soften);
  soften.connect(amp);

  if (flutter) {
    const lfo = ctx.createOscillator();
    const depth = ctx.createGain();
    const rasp = ctx.createGain();
    lfo.frequency.value = flutter;
    depth.gain.value = flutterDepth;
    rasp.gain.value = 1 - flutterDepth;
    lfo.connect(depth);
    depth.connect(rasp.gain);
    amp.connect(rasp);
    rasp.connect(out);
    lfo.start(start);
    lfo.stop(start + length + 0.05);
  } else {
    amp.connect(out);
  }

  osc.start(start);
  osc.stop(start + length + 0.05);
}

// A bell: a pure tone and two quieter, inharmonic overtones, struck and left to ring.
function bell(ctx, out, { start, freq, length = 0.6, gain = 0.035 }) {
  for (const [mult, g] of [[1, gain], [2.76, gain * 0.25], [5.4, gain * 0.08]]) {
    const osc = ctx.createOscillator();
    const amp = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.value = freq * mult;
    amp.gain.setValueAtTime(0.0001, start);
    amp.gain.exponentialRampToValueAtTime(g, start + 0.006);
    amp.gain.exponentialRampToValueAtTime(0.0001, start + length / mult ** 0.3);
    osc.connect(amp);
    amp.connect(out);
    osc.start(start);
    osc.stop(start + length + 0.05);
  }
}

export function play(cue) {
  const ctx = audio();
  if (!ctx) {
    return;
  }

  const out = ctx.createGain();
  out.gain.value = 1;
  out.connect(ctx.destination);

  const t = ctx.currentTime + 0.01;

  switch (cue) {
    case 'Pleased':
      // A short trumpet, then three bright twinkles.
      voice(ctx, out, {
        start: t,
        length: 0.36,
        pitch: [[0, 380], [0.08, 740], [0.3, 800]],
        type: 'sawtooth',
        gain: 0.05,
        trunk: 1300,
        q: 1.6,
        flutter: 28,
        flutterDepth: 0.35,
      });
      [1318, 1568, 2093].forEach((freq, i) => bell(ctx, out, { start: t + 0.3 + i * 0.07, freq, length: 0.5, gain: 0.022 }));
      break;

    case 'Cross':
      // Two short low trunk blasts, the second lower.
      voice(ctx, out, {
        start: t,
        length: 0.16,
        pitch: [[0, 220], [0.12, 200]],
        type: 'sawtooth',
        gain: 0.05,
        trunk: 600,
        q: 1.4,
        flutter: 18,
        flutterDepth: 0.3,
      });
      voice(ctx, out, {
        start: t + 0.2,
        length: 0.28,
        pitch: [[0, 180], [0.22, 150]],
        type: 'sawtooth',
        gain: 0.05,
        trunk: 520,
        q: 1.4,
        flutter: 18,
        flutterDepth: 0.3,
      });
      break;

    default:
      // Calm: one warm bell with a long, quiet tail, and a fainter one a sixth above.
      bell(ctx, out, { start: t, freq: 880, length: 1.1, gain: 0.03 });
      bell(ctx, out, { start: t + 0.05, freq: 1319, length: 0.8, gain: 0.012 });
      break;
  }
}
export function dispose() {
  if (context) {
    context.close();
    context = null;
  }
}
