/*
  Ele, drawn on a canvas.

  This module is the ONLY place animation-runtime code lives. C# talks to it through
  IMascotService and never touches IJSRuntime directly, so swapping this for Rive or Lottie
  is one file plus one class (ADR-0011).

  Rewritten after the first version was unreadable on a phone: it drew at CSS pixel size on
  a 3x display, so every edge was blurred, and the palette was so low-contrast that the face
  disappeared into the card behind it. The fixes that mattered:

    - Scale the backing store by devicePixelRatio. A canvas is a bitmap; sizing it in CSS
      pixels on a 3x screen renders a third of the resolution and stretches it.
    - Draw the head opaque and dark enough to separate from the surface behind it.
    - Make the features large. At 88px on screen, an eye smaller than 4px is a smudge.

  Nothing here decides anything. It draws. A renderer that could affect a balance would be
  in the wrong layer.
*/

const instances = new Map();

const MOODS = {
  Idle: { ear: 1.0, brow: 0.0, curl: 0.25, blink: 1.0, bounce: 1.0 },
  Happy: { ear: 1.35, brow: -0.35, curl: 1.0, blink: 1.4, bounce: 2.4 },
  Concerned: { ear: 0.72, brow: 0.55, curl: -0.35, blink: 0.7, bounce: 0.7 },
  Thinking: { ear: 0.9, brow: 0.2, curl: 0.1, blink: 1.6, bounce: 1.1 },
  Sad: { ear: 0.55, brow: 0.75, curl: -0.7, blink: 0.5, bounce: 0.5 },
  // Amounts are hidden: the ears fold forward over the eyes, peekaboo style.
  Hiding: { ear: 1.0, brow: 0.2, curl: 0.35, blink: 1.0, bounce: 0.6, cover: 1 },
};

const reduceMotion = () =>
  window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function palette(canvas) {
  const s = getComputedStyle(canvas);
  const pick = (name, fallback) => (s.getPropertyValue(name) || '').trim() || fallback;

  return {
    // Deliberately not the page accent. The mascot needs to read against the card it sits
    // on, and the accent is tuned for text contrast rather than for a filled shape.
    hide: pick('--ele-mascot-hide', '#B9A5D8'),
    hideDark: pick('--ele-mascot-hide-dark', '#8E77B4'),
    ear: pick('--ele-mascot-ear', '#7C63A6'),
    ink: pick('--ele-mascot-ink', '#2A2140'),
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

function ease(current, target, k) {
  return current + (target - current) * k;
}

function draw(state, now) {
  resize(state);

  const { ctx } = state;
  const target = MOODS[state.mood] || MOODS.Idle;
  const k = reduceMotion() ? 1 : 0.14;

  state.ear = ease(state.ear, target.ear, k);
  state.brow = ease(state.brow, target.brow, k);
  state.curl = ease(state.curl, target.curl, k);
  state.cover = ease(state.cover, target.cover || 0, k);

  const w = state.cssWidth;
  const h = state.cssHeight;
  const t = reduceMotion() ? 0 : now / 1000;

  ctx.setTransform(state.dpr, 0, 0, state.dpr, 0, 0);
  ctx.clearRect(0, 0, w, h);

  const c = palette(state.canvas);

  // One unit of scale so the drawing is resolution independent.
  const u = Math.min(w, h) / 100;
  const cx = w / 2;

  const breathe = Math.sin(t * 1.7) * 1.1 * target.bounce;
  const excited = state.cheerUntil > now ? Math.sin(t * 14) * 2.4 : 0;
  const cy = h / 2 + breathe + excited - 3 * u;

  const headR = 30 * u;

  // Ears behind the head, so the head's edge cuts them cleanly.
  for (const side of [-1, 1]) {
    const ex = cx + side * headR * 1.02;
    ctx.save();
    ctx.translate(ex, cy - headR * 0.1);
    ctx.rotate(side * 0.22);
    ctx.fillStyle = c.hideDark;
    ctx.beginPath();
    ctx.ellipse(0, 0, headR * 0.66 * state.ear, headR * 0.86, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = c.ear;
    ctx.beginPath();
    ctx.ellipse(side * headR * 0.06, headR * 0.04, headR * 0.4 * state.ear, headR * 0.56, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  // Trunk, drawn before the head so it tucks under the chin.
  ctx.strokeStyle = c.hide;
  ctx.lineWidth = 13 * u;
  ctx.lineCap = 'round';
  ctx.beginPath();
  ctx.moveTo(cx, cy + headR * 0.35);
  ctx.quadraticCurveTo(
    cx + state.curl * 5 * u,
    cy + headR * 1.28,
    cx + state.curl * 15 * u,
    cy + headR * 1.24 - Math.abs(state.curl) * 9 * u);
  ctx.stroke();

  // Head.
  ctx.fillStyle = c.hide;
  ctx.beginPath();
  ctx.ellipse(cx, cy, headR, headR * 0.94, 0, 0, Math.PI * 2);
  ctx.fill();

  // Tusks, short and outboard so they do not merge with the trunk.
  ctx.strokeStyle = c.shine;
  ctx.lineWidth = 4.2 * u;
  for (const side of [-1, 1]) {
    ctx.beginPath();
    ctx.moveTo(cx + side * 13 * u, cy + headR * 0.46);
    ctx.quadraticCurveTo(
      cx + side * 17 * u, cy + headR * 0.78,
      cx + side * 12 * u, cy + headR * 0.92);
    ctx.stroke();
  }

  // Eyes. Large on purpose: at 88px on screen anything under 4px is a smudge.
  const blink = !reduceMotion() && Math.sin(t * target.blink * 0.8) > 0.975;
  const eyeY = cy - headR * 0.16;

  for (const side of [-1, 1]) {
    const ex = cx + side * headR * 0.38;

    ctx.fillStyle = c.ink;
    ctx.beginPath();
    if (blink) {
      ctx.ellipse(ex, eyeY, 5.2 * u, 0.9 * u, 0, 0, Math.PI * 2);
    } else {
      ctx.ellipse(ex, eyeY, 5.2 * u, 6.1 * u, 0, 0, Math.PI * 2);
    }
    ctx.fill();

    if (!blink) {
      ctx.fillStyle = c.shine;
      ctx.beginPath();
      ctx.arc(ex + 1.7 * u, eyeY - 2.1 * u, 1.9 * u, 0, Math.PI * 2);
      ctx.fill();
    }

    // Brows carry the mood more than anything else here.
    ctx.strokeStyle = c.ink;
    ctx.lineWidth = 3 * u;
    ctx.beginPath();
    ctx.moveTo(ex - 6 * u, eyeY - 10 * u + side * state.brow * 2.2 * u);
    ctx.quadraticCurveTo(
      ex, eyeY - 12.5 * u + state.brow * 4.5 * u,
      ex + 6 * u, eyeY - 10 * u - side * state.brow * 2.2 * u);
    ctx.stroke();
  }

  // Hiding: each ear swings in from the side of the head and folds over its eye. Drawn
  // after the eyes so it covers them, and blended by `cover` so it animates both ways.
  if (state.cover > 0.01) {
    const p = state.cover;
    for (const side of [-1, 1]) {
      const fromX = cx + side * headR * 1.02;
      const toX = cx + side * headR * 0.36;
      const x = fromX + (toX - fromX) * p;
      const y = (cy - headR * 0.1) + ((eyeY + 1 * u) - (cy - headR * 0.1)) * p;
      const scale = 1 - 0.32 * p;

      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(side * (0.22 - 0.9 * p));
      ctx.fillStyle = c.hideDark;
      ctx.beginPath();
      ctx.ellipse(0, 0, headR * 0.66 * scale, headR * 0.86 * scale, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = c.ear;
      ctx.beginPath();
      ctx.ellipse(side * headR * 0.05, headR * 0.03, headR * 0.4 * scale, headR * 0.56 * scale, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  // A smile only when there is something to smile about.
  if (state.curl > 0.5) {
    ctx.strokeStyle = c.ink;
    ctx.lineWidth = 2.4 * u;
    ctx.globalAlpha = Math.min(1, (state.curl - 0.5) * 2);
    ctx.beginPath();
    ctx.arc(cx, cy + headR * 0.1, 7 * u, 0.25 * Math.PI, 0.75 * Math.PI);
    ctx.stroke();
    ctx.globalAlpha = 1;
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
    brow: 0,
    curl: 0.25,
    cover: 0,
    cheerUntil: 0,
    frame: 0,
    dpr: 0,
    cssWidth: 0,
    cssHeight: 0,
  };

  instances.set(canvasId, state);
  loop(canvasId);
  return true;
}

export function setMood(canvasId, mood) {
  const state = instances.get(canvasId);
  if (state) {
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
