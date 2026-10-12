/*
  Ele's sounds, synthesised with the Web Audio API.

  No audio files: a short cue is a few oscillators, so there is nothing to ship, nothing to
  download, and nothing that can fail to load. Reached only through ISoundCue, like the mascot
  is through IMascotService, so no component touches JavaScript.

  One sound per face, all from the same small elephant:
  - Pleased (happy face): a short, jolly trumpet. A brassy blare that swoops up through the
    trunk, with the rough flutter that makes it an elephant rather than a horn.
  - Calm (calm face): a soft, low hum, a contented rumble that settles. Nothing to react to.
  - Cross (cross face): a short grumpy snort, two low falling blows. Never a buzzer.

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

// A breath of noise through the trunk, for the snort and the start of the trumpet.
function breath(ctx, out, { start, length, gain = 0.02, trunk = 900 }) {
  const frames = Math.floor(ctx.sampleRate * length);
  const buffer = ctx.createBuffer(1, frames, ctx.sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < frames; i++) {
    data[i] = Math.random() * 2 - 1;
  }

  const source = ctx.createBufferSource();
  const band = ctx.createBiquadFilter();
  const amp = ctx.createGain();

  source.buffer = buffer;
  band.type = 'bandpass';
  band.frequency.value = trunk;
  band.Q.value = 0.9;

  amp.gain.setValueAtTime(0.0001, start);
  amp.gain.exponentialRampToValueAtTime(gain, start + 0.02);
  amp.gain.exponentialRampToValueAtTime(0.0001, start + length);

  source.connect(band);
  band.connect(amp);
  amp.connect(out);
  source.start(start);
  source.stop(start + length + 0.02);
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
      // A short, jolly trumpet: a quick upward swoop that holds, then lifts at the end.
      breath(ctx, out, { start: t, length: 0.12, gain: 0.018, trunk: 1300 });
      voice(ctx, out, {
        start: t + 0.02,
        length: 0.62,
        pitch: [[0, 360], [0.09, 720], [0.38, 760], [0.55, 880]],
        type: 'sawtooth',
        gain: 0.055,
        trunk: 1250,
        q: 1.6,
        flutter: 28,
        flutterDepth: 0.4,
      });
      // A second, slightly sharper reed in the trunk: what makes it brassy.
      voice(ctx, out, {
        start: t + 0.02,
        length: 0.6,
        pitch: [[0, 364], [0.09, 728], [0.38, 768], [0.55, 890]],
        type: 'square',
        gain: 0.018,
        trunk: 1700,
        q: 2,
        flutter: 31,
        flutterDepth: 0.3,
      });
      break;

    case 'Cross':
      // A grumpy snort: two short, low, falling blows.
      breath(ctx, out, { start: t, length: 0.16, gain: 0.03, trunk: 600 });
      voice(ctx, out, {
        start: t + 0.01,
        length: 0.2,
        pitch: [[0, 240], [0.18, 190]],
        type: 'sawtooth',
        gain: 0.045,
        trunk: 650,
        q: 1.4,
        flutter: 22,
        flutterDepth: 0.45,
      });
      breath(ctx, out, { start: t + 0.24, length: 0.18, gain: 0.025, trunk: 520 });
      voice(ctx, out, {
        start: t + 0.25,
        length: 0.3,
        pitch: [[0, 200], [0.26, 150]],
        type: 'sawtooth',
        gain: 0.045,
        trunk: 560,
        q: 1.4,
        flutter: 20,
        flutterDepth: 0.45,
      });
      break;

    default:
      // Calm: a soft, low, contented hum that settles. No rasp, no rise.
      voice(ctx, out, {
        start: t,
        length: 0.9,
        pitch: [[0, 196], [0.5, 185], [0.85, 175]],
        type: 'triangle',
        gain: 0.05,
        trunk: 420,
        q: 0.8,
        attack: 0.12,
      });
      voice(ctx, out, {
        start: t,
        length: 0.8,
        pitch: [[0, 392], [0.7, 350]],
        type: 'sine',
        gain: 0.012,
        trunk: 800,
        q: 0.7,
        attack: 0.12,
      });
      break;
  }
}

export function dispose() {
  if (context) {
    context.close();
    context = null;
  }
}
