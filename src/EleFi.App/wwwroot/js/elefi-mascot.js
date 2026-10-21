/*
  Ele, drawn on a canvas.

  This module is the ONLY place animation-runtime code lives. C# talks to it through
  IMascotService and never touches IJSRuntime directly, so swapping this for Rive or Lottie
  is one file plus one class (ADR-0011).

  The shape itself is in elefi-ele-shape.js, shared with the app icon, the splash and the
  start-up vector, so every Ele is the same elephant. This file only poses and animates it.

  A cheerful young elephant in the spirit of Duolingo's Duo, so no mouth and no brows: big
  bright eyes whose lids carry the mood (smiling when pleased, slanted when cross, drooping
  when sad), backed by the trunk (raised and curled, curled up at rest, or drooping) and the
  ears (spread or held back).

  What keeps it sharp and readable on a phone:
    - Scale the backing store by devicePixelRatio. A canvas is a bitmap; sizing it in CSS
      pixels on a 3x screen renders a third of the resolution and stretches it.
    - Draw opaque and dark enough to separate from the surface behind it.
    - Keep the features large. At 88px on screen an eye smaller than 4px is a smudge.

  Nothing here decides anything. It draws.
*/

import {
  CHEEK, EAR_COVER, EAR_HINGE, EAR_INNER, EAR_MIDDLE, EAR_OUTER, EYE, FOREHEAD, HEAD, PUPIL, SHINES, TUSK, lowerLid, lowerLidEdge, trunk, upperLid, upperLidEdge,
} from './elefi-ele-shape.js';

const instances = new Map();

// ear: how far the ears spread. curl: the trunk, -1 droop to 1 raised. lidIn and lidOut: how
// far the upper lid comes down at the inner and outer corner (in units; inner low is cross,
// outer low is sad). smile: the lower lid rising into a happy crescent.
const MOODS = {
  Idle: { ear: 1.0, curl: 0, lidIn: 0, lidOut: 0, smile: 0, blink: 1.0, bounce: 1.0 },
  Happy: { ear: 1.35, curl: 1.0, lidIn: 0, lidOut: 0, smile: 1, blink: 1.4, bounce: 2.4 },
  Concerned: { ear: 0.72, curl: -0.35, lidIn: 9.5, lidOut: 2.5, smile: 0, blink: 0.7, bounce: 0.7 },
  Thinking: { ear: 0.9, curl: 0.1, lidIn: 3, lidOut: 3, smile: 0, blink: 1.6, bounce: 1.1 },
  Sad: { ear: 0.55, curl: -0.7, lidIn: 2.5, lidOut: 9.5, smile: 0, blink: 0.5, bounce: 0.5 },
  // Something went wrong, and Ele is sorry but here: soft outer lids, a small kind smile, the
  // trunk a little raised as if to say it is all right (E3, chosen 2026-10-21).
  Comforting: { ear: 0.8, curl: 0.35, lidIn: 1, lidOut: 6, smile: 0.35, blink: 0.8, bounce: 0.6 },
  // Amounts are hidden: the ears fold forward over the eyes, peekaboo style.
  Hiding: { ear: 1.0, curl: 0.2, lidIn: 0, lidOut: 0, smile: 0, blink: 1.0, bounce: 0.6, cover: 1 },
};

const paths = {
  head: new Path2D(HEAD),
  forehead: new Path2D(FOREHEAD),
  earOuter: new Path2D(EAR_OUTER),
  earInner: new Path2D(EAR_INNER),
  tusk: new Path2D(TUSK),
};

const reduceMotion = () =>
  window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function palette(canvas) {
  const s = getComputedStyle(canvas);
  const pick = (name, fallback) => (s.getPropertyValue(name) || '').trim() || fallback;

  return {
    // Deliberately not the page accent. The mascot needs to read against the card it sits
    // on, and the accent is tuned for text contrast rather than for a filled shape.
    hide: pick('--ele-mascot-hide', '#A98BFA'),
    hideDark: pick('--ele-mascot-hide-dark', '#8466EE'),
    ear: pick('--ele-mascot-ear', '#7656E0'),
    ink: pick('--ele-mascot-ink', '#231A3D'),
    cheek: 'rgba(255, 255, 255, 0.2)',
    tusk: '#FFFBF2',
    tuskEdge: '#C4B48C',
    shine: '#FFFFFF',
  };
}

// A canvas is a bitmap. Without this it renders at a third of the resolution on a 3x
// display and every curve looks smeared.
function resize(state) {
  const dpr = Math.min(window.devicePixelRatio || 1, 3);
  const rect = state.canvas.getBoundingClientRect();
  const w = Math.max(1, Math.round(rect.width || 88));
  const h = Math.max(1, Math.round(rect.height || 88));

  if (state.cssWidth === w && state.cssHeight === h && state.dpr === dpr) {
    return;
  }

  state.cssWidth = w;
  state.cssHeight = h;
  state.dpr = dpr;
  state.canvas.width = Math.round(w * dpr);
  state.canvas.height = Math.round(h * dpr);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

// Slow in, slow out: what makes a move feel smooth rather than snapped.
function easeInOut(t) {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

function smoothstep(a, b, t) {
  const x = Math.max(0, Math.min(1, (t - a) / (b - a)));
  return x * x * (3 - 2 * x);
}

// How long the ears take to fold over the eyes, or back. Timed, so it is the same on every phone
// whatever its frame rate.
const COVER_MS = 620;

// Confetti for a pleased Ele: a short burst around the head that rises, tumbles and falls.
const CONFETTI_COLOURS = ['#FFD166', '#06D6A0', '#EF476F', '#4CC9F0', '#F8961E', '#FFFFFF', '#C77DFF'];
const CONFETTI_MS = 1700;

function burst(now) {
  const pieces = [];
  for (let i = 0; i < 30; i++) {
    const angle = -Math.PI / 2 + (Math.random() - 0.5) * 2.2;
    const speed = 70 + Math.random() * 70;
    pieces.push({
      x: (Math.random() - 0.5) * 50,
      y: -22 + Math.random() * 14,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      spin: (Math.random() - 0.5) * 14,
      turn: Math.random() * Math.PI,
      w: 2.6 + Math.random() * 1.8,
      h: 4 + Math.random() * 2.5,
      colour: CONFETTI_COLOURS[i % CONFETTI_COLOURS.length],
      delay: Math.random() * 140,
    });
  }

  return { start: now, pieces };
}

function drawConfetti(ctx, confetti, now) {
  const age = now - confetti.start;
  for (const piece of confetti.pieces) {
    const s = Math.max(0, age - piece.delay) / 1000;
    if (s <= 0) {
      continue;
    }

    // Rises, slows, and falls back under gravity, with a little air drag on the sideways drift.
    const x = piece.x + piece.vx * s * 0.9;
    const y = piece.y + piece.vy * s + 0.5 * 190 * s * s;
    const life = (age - piece.delay) / CONFETTI_MS;
    ctx.save();
    ctx.globalAlpha = Math.max(0, 1 - smoothstep(0.6, 1, life));
    ctx.translate(x, y);
    ctx.rotate(piece.turn + piece.spin * s);
    ctx.scale(1, Math.cos(piece.spin * s * 1.7));
    ctx.fillStyle = piece.colour;
    ctx.fillRect(-piece.w / 2, -piece.h / 2, piece.w, piece.h);
    ctx.restore();
  }
}

function ease(current, target, k) {
  return current + (target - current) * k;
}

// One ear, posed: `side` 1 for the right, -1 for the left (mirrored), spread by `spread`.
function ear(ctx, c, side, spread) {
  ctx.save();
  ctx.translate(side * EAR_HINGE.x, EAR_HINGE.y);
  ctx.scale(side * spread, 1);
  ctx.translate(-EAR_HINGE.x, -EAR_HINGE.y);
  ctx.fillStyle = c.hide;
  ctx.fill(paths.earOuter);
  ctx.fillStyle = c.hideDark;
  ctx.fill(paths.earInner);
  ctx.restore();
}

function draw(state, now) {
  resize(state);

  const { ctx } = state;
  const target = MOODS[state.mood] || MOODS.Idle;
  const still = reduceMotion();

  // The mood a viewer was opened in is where Ele already is, not something to animate into:
  // with amounts hidden, opening Home shows Ele already hiding rather than covering up again.
  const settle = still || state.snap;
  const k = settle ? 1 : 0.14;

  state.ear = ease(state.ear, target.ear, k);
  state.curl = ease(state.curl, target.curl, k);
  // The ears fold over the eyes on a timed, eased curve, starting from wherever they are, so
  // a quick double tap reverses smoothly mid-way.
  const coverTo = target.cover || 0;
  if (coverTo !== state.coverTo) {
    state.coverFrom = state.cover;
    state.coverTo = coverTo;
    state.coverStart = now;
  }

  if (state.snap) {
    state.coverFrom = coverTo;
    state.coverStart = now - COVER_MS;
    state.snap = false;
  }

  state.cover = settle
    ? coverTo
    : lerp(state.coverFrom, state.coverTo, easeInOut(Math.min(1, (now - state.coverStart) / COVER_MS)));

  // Turning pleased throws a little confetti.
  if (state.mood === 'Happy' && state.lastMood !== 'Happy' && !still) {
    state.confetti = burst(now);
  }

  state.lastMood = state.mood;
  state.lidIn = ease(state.lidIn, target.lidIn, k);
  state.lidOut = ease(state.lidOut, target.lidOut, k);
  state.smile = ease(state.smile, target.smile, k);

  const w = state.cssWidth;
  const h = state.cssHeight;
  const t = still ? 0 : now / 1000;

  ctx.setTransform(state.dpr, 0, 0, state.dpr, 0, 0);
  ctx.clearRect(0, 0, w, h);

  const c = palette(state.canvas);

  // The drawing spans about x -64..64 and y -38..48 in shape units; this fits it, centred.
  const u = Math.min(w, h) / 128;
  const breathe = Math.sin(t * 1.7) * 1.1 * target.bounce;
  const excited = state.cheerUntil > now ? Math.sin(t * 14) * 2.4 : 0;

  ctx.translate(w / 2, h / 2 + breathe + excited - 5 * u);
  ctx.scale(u, u);

  // Ears behind the head, flapping a little. While hiding, these same ears swing round to the
  // front, so they fade here as the front ones arrive: an elephant has two ears, not four.
  // As they come forward the same ears are drawn in front instead (see the end), so these hand
  // over to them early in the move rather than shrinking away.
  const stay = 1 - smoothstep(0.04, 0.32, state.cover);
  const flap = (0.8 + 0.2 * state.ear) + (still ? 0 : Math.sin(t * 2.3) * 0.025 * target.bounce);
  if (stay > 0.01) {
    ctx.globalAlpha = stay;
    for (const side of [-1, 1]) {
      ear(ctx, c, side, flap);
    }

    ctx.globalAlpha = 1;
  }

  // Tusks, then the head and trunk over their roots.
  ctx.fillStyle = c.tusk;
  ctx.strokeStyle = c.tuskEdge;
  ctx.lineWidth = 0.9;
  for (const side of [-1, 1]) {
    ctx.save();
    ctx.scale(side, 1);
    ctx.fill(paths.tusk);
    ctx.stroke(paths.tusk);
    ctx.restore();
  }

  ctx.fillStyle = c.hide;
  ctx.fill(paths.head);
  ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
  ctx.fill(paths.forehead);

  // Puffed cheeks, a little lighter than the hide, under the eyes.
  ctx.fillStyle = c.cheek;
  for (const side of [-1, 1]) {
    ctx.beginPath();
    ctx.ellipse(side * CHEEK.x, CHEEK.y, CHEEK.rx, CHEEK.ry, 0, 0, Math.PI * 2);
    ctx.fill();
  }

  // The trunk carries the mood, and sways a little as it hangs.
  const sway = still ? 0 : Math.sin(t * 1.3) * 1.6 * (1 - Math.max(0, state.curl) * 0.6);
  const shape = trunk(state.curl, sway);
  ctx.fillStyle = c.hide;
  ctx.fill(new Path2D(shape.outline));
  ctx.strokeStyle = c.hideDark;
  ctx.lineWidth = 1.2;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.globalAlpha = 0.7;
  ctx.stroke(new Path2D(shape.edge));
  ctx.globalAlpha = 1;
  ctx.lineWidth = 1.1;
  for (const fold of shape.wrinkles) {
    ctx.stroke(new Path2D(fold));
  }

  // Raised, it shows its lighter, ridged underside along the inner edge.
  const lift = Math.max(0, Math.min(1, (state.curl - 0.3) / 0.6));
  if (lift > 0.01) {
    const underside = new Path2D(shape.underside);
    ctx.globalAlpha = lift;
    ctx.fillStyle = 'rgba(255, 255, 255, 0.34)';
    ctx.fill(underside);
    ctx.globalAlpha = lift * 0.45;
    ctx.lineWidth = 0.9;
    for (const ridge of shape.ridges) {
      ctx.stroke(new Path2D(ridge));
    }

    // The end of the trunk faces the front, with its two holes.
    const face = shape.face;
    ctx.globalAlpha = lift;
    ctx.beginPath();
    ctx.ellipse(face.x, face.y, face.rx, face.ry, face.angle, 0, Math.PI * 2);
    ctx.fillStyle = c.hide;
    ctx.fill();
    ctx.fillStyle = 'rgba(255, 255, 255, 0.28)';
    ctx.fill();
    ctx.lineWidth = 1.1;
    ctx.globalAlpha = lift * 0.8;
    ctx.stroke();
    ctx.fillStyle = c.ink;
    for (const n of face.nostrils) {
      ctx.beginPath();
      ctx.ellipse(n.x, n.y, n.rx, n.ry, face.angle, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.globalAlpha = 1;
  }

  // Big round eyes, Duo style. The lids carry the mood: a lower lid rising into a smile, or an
  // upper lid slanting in (cross) or out (sad). A blink brings the upper lid all the way down.
  const blink = !still && Math.sin(t * target.blink * 0.8) > 0.975;
  const white = new Path2D();
  white.ellipse(EYE.x, EYE.y, EYE.rx, EYE.ry, 0, 0, Math.PI * 2);
  for (const side of [-1, 1]) {
    ctx.save();
    ctx.scale(side, 1);

    ctx.fillStyle = c.shine;
    ctx.fill(white);

    ctx.save();
    ctx.clip(white);
    ctx.fillStyle = c.ink;
    ctx.beginPath();
    ctx.arc(EYE.x + PUPIL.dx, EYE.y + PUPIL.dy, PUPIL.r, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = c.shine;
    for (const s of SHINES) {
      ctx.beginPath();
      ctx.arc(EYE.x + PUPIL.dx + s.dx, EYE.y + PUPIL.dy + s.dy, s.r, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.fillStyle = c.hide;
    const closed = EYE.ry * 2 + 2;
    const [lidIn, lidOut] = blink ? [closed, closed] : [state.lidIn, state.lidOut];
    ctx.fill(new Path2D(upperLid(lidIn, lidOut)));

    // The lid's edge, so a slant reads as a feeling and not as a smudge.
    ctx.strokeStyle = c.ink;
    ctx.lineCap = 'round';
    ctx.lineWidth = 1.4;
    ctx.globalAlpha = Math.min(0.7, 0.18 + Math.max(lidIn, lidOut) * 0.08);
    ctx.stroke(new Path2D(upperLidEdge(lidIn, lidOut)));
    ctx.globalAlpha = 1;

    if (state.smile > 0.02 && !blink) {
      ctx.fill(new Path2D(lowerLid(state.smile)));
      ctx.globalAlpha = Math.min(0.7, state.smile * 0.7);
      ctx.stroke(new Path2D(lowerLidEdge(state.smile)));
      ctx.globalAlpha = 1;
    }

    ctx.restore();

    // A thin line round the eye so the white stands off the hide.
    ctx.strokeStyle = c.ink;
    ctx.globalAlpha = 0.28;
    ctx.lineWidth = 0.9;
    ctx.stroke(white);
    ctx.globalAlpha = 1;
    ctx.restore();
  }
  // Hiding: the big ears flop down over the top of the face from its edges, full size, and
  // cover both eyes. Drawn after the eyes so they cover them, and blended by `cover` so it
  // animates both ways.
  if (state.cover > 0.01) {
    const p = state.cover;
    ctx.globalAlpha = smoothstep(0.02, 0.24, p);
    for (const side of [-1, 1]) {
      const x = lerp(EAR_MIDDLE.x, EAR_COVER.x, p);
      const y = lerp(EAR_MIDDLE.y, EAR_COVER.y, p);
      const scale = lerp(1, EAR_COVER.scale, p);

      ctx.save();
      ctx.scale(side, 1);
      ctx.translate(x, y);
      ctx.rotate((EAR_COVER.rotate * Math.PI / 180) * p);
      ctx.scale(scale, scale);
      ctx.translate(-EAR_MIDDLE.x, -EAR_MIDDLE.y);
      ctx.fillStyle = c.hide;
      ctx.fill(paths.earOuter);
      ctx.strokeStyle = c.hideDark;
      ctx.lineWidth = 1.2 / scale;
      ctx.stroke(paths.earOuter);
      ctx.fillStyle = c.hideDark;
      ctx.fill(paths.earInner);
      ctx.restore();
    }

    ctx.globalAlpha = 1;
  }

  if (state.confetti) {
    if (now - state.confetti.start > CONFETTI_MS + 200) {
      state.confetti = null;
    } else {
      drawConfetti(ctx, state.confetti, now);
    }
  }
}

function loop(id) {
  const state = instances.get(id);
  if (!state) {
    return;
  }

  draw(state, performance.now());
  state.frame = requestAnimationFrame(() => loop(id));
}

export function initialise(canvasId) {
  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    return false;
  }

  dispose(canvasId);

  const state = {
    canvas,
    ctx: canvas.getContext('2d'),
    mood: 'Idle',
    ear: 1.0,
    curl: 0.25,
    cover: 0,
    coverFrom: 0,
    coverTo: 0,
    coverStart: 0,
    lastMood: 'Idle',
    confetti: null,
    fresh: true,
    snap: false,
    lidIn: 0,
    lidOut: 0,
    smile: 0,
    cheerUntil: 0,
    frame: 0,
    dpr: 0,
    cssWidth: 0,
    cssHeight: 0,
  };

  instances.set(canvasId, state);

  // A mood set straight after opening is the pose to start in. After a moment, any change is
  // the user's doing and animates.
  setTimeout(() => {
    state.fresh = false;
  }, 400);

  loop(canvasId);
  return true;
}

export function setMood(canvasId, mood) {
  const state = instances.get(canvasId);
  if (state) {
    // The first mood, given as the viewer opens, is a starting pose; later ones animate.
    state.snap = state.fresh;

    // Confetti is for Ele becoming pleased, or for a pleased message or guide step arriving,
    // never for a picture that simply opens pleased, like the one on About.
    if (state.fresh && !state.canvas.closest('.ele-pop, .guide-ele')) {
      state.lastMood = mood;
    }

    state.fresh = false;
    state.mood = mood;
  }
}

export function pulse(canvasId, mood, milliseconds) {
  const state = instances.get(canvasId);
  if (!state) {
    return;
  }

  const previous = state.mood;
  state.mood = mood;
  state.cheerUntil = performance.now() + (milliseconds || 900);

  setTimeout(() => {
    const current = instances.get(canvasId);
    if (current) {
      current.mood = previous;
    }
  }, milliseconds || 900);
}

export function dispose(canvasId) {
  const state = instances.get(canvasId);
  if (state) {
    cancelAnimationFrame(state.frame);
    instances.delete(canvasId);
  }
}
