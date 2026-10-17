/*
  Ele's shape: the one drawing every Ele is made from.

  Pure geometry, no DOM, so the same numbers draw the live mascot (elefi-mascot.js, on a
  canvas) and the still pictures (the app icon, the splash, the start-up vector and the brand
  icon), which docs/brand/generate.mjs writes from this file. Change Ele here and run that
  script; never hand-edit the generated pictures.

  A cheerful young elephant in the spirit of Duolingo's Duo: bold round shapes, big bright eyes,
  and the features where an elephant's are. No mouth and no brows. A round head, two big eyes
  close together with large pupils and two sparkles each, soft puffed cheeks, round perky ears
  held out to the sides, a short chubby trunk that curls up at the tip even at rest, and two
  small tusks beside it.

  Feelings show the way Duo's do, in the eyes: lids that smile, slant or droop. The ears and the
  trunk back them up: raised and curled when pleased, curled up a little at rest, drooping when
  cross.

  Units: the head's centre is (0, 0), y grows downward, and the whole drawing fits in
  x -62..62, y -38..48. Paths are SVG path strings, which a canvas (Path2D), an SVG and an
  Android vector all read.
*/

const f = (n) => Math.round(n * 100) / 100;
const lerp = (a, b, t) => a + (b - a) * t;

/** The head: big and round. */
export const HEAD =
  'M 0,-38 C 21,-38 33,-24 33,-6 C 33,9 25,18 13,21 L -13,21 C -25,18 -33,9 -33,-6 C -33,-24 -21,-38 0,-38 Z';

/** A soft light across the top of the head, so it reads as a dome rather than a disc. */
export const FOREHEAD = 'M -18,-27 C -10,-35 10,-35 18,-27 C 10,-30 -10,-30 -18,-27 Z';

/** Where the right ear joins the head; it flaps around this point. */
export const EAR_HINGE = { x: 24, y: -8 };

/** The right ear, round and held out, outside and inside. The left is the same, mirrored. */
export const EAR_OUTER =
  'M 22,-24 C 34,-38 60,-35 61,-12 C 62,6 50,19 36,19 C 30,19 25,15 23,9 Z';
export const EAR_INNER =
  'M 27,-19 C 36,-30 54,-27 55,-12 C 56,1 47,12 37,12 C 32,12 29,9 27,5 Z';

/** The middle of the right ear, used when it folds over an eye. */
export const EAR_MIDDLE = { x: 42, y: -6 };

/** The right tusk: small, beside the trunk, curving down and out. Mirrored for the left. */
export const TUSK = 'M 9,16 C 13,19 15,24 15,30 C 13,28.5 11.5,24.5 8,21 Z';

/** The right eye's white: big, and close to the other one. Mirrored for the left. */
export const EYE = { x: 10.5, y: -7, rx: 8.4, ry: 9.4 };

/** The pupil: large, looking a little up and in, which reads as bright and interested. */
export const PUPIL = { dx: -0.8, dy: 0.6, r: 6 };

/** Two sparkles in each eye, a big one up and out and a small one down and in. */
export const SHINES = [
  { dx: 1.8, dy: -2.4, r: 2.3 },
  { dx: -2.2, dy: 2.6, r: 1.1 },
];

/** A soft puffed cheek under the right eye. Mirrored for the left. */
export const CHEEK = { x: 19, y: 7, rx: 7, ry: 5 };

/**
 * The right upper eyelid, the part of the eye it covers: everything above a line from the inner
 * corner to the outer one. Clip it to the eye's white. Mirror for the left.
 * @param {number} inner How far the lid comes down at the inner corner, in units.
 * @param {number} outer How far it comes down at the outer corner.
 */
export function upperLid(inner, outer) {
  const top = EYE.y - EYE.ry - 2;
  const left = EYE.x - EYE.rx - 1;
  const right = EYE.x + EYE.rx + 1;
  return `M ${f(left)},${f(top)} L ${f(right)},${f(top)} L ${f(right)},${f(EYE.y - EYE.ry + outer)} L ${f(left)},${f(EYE.y - EYE.ry + inner)} Z`;
}

/** The edge of the upper eyelid, drawn as a line so the lid reads as a lid. */
export function upperLidEdge(inner, outer) {
  return `M ${f(EYE.x - EYE.rx - 1)},${f(EYE.y - EYE.ry + inner)} L ${f(EYE.x + EYE.rx + 1)},${f(EYE.y - EYE.ry + outer)}`;
}

/**
 * The right lower eyelid rising in a curve: a smiling eye, the way Duo smiles. Clip it to the
 * eye's white.
 * @param {number} amount 0 none, 1 a full happy crescent.
 */
export function lowerLid(amount) {
  const rise = EYE.ry * 1.25 * amount;
  const bottom = EYE.y + EYE.ry + 2;
  return `M ${f(EYE.x - EYE.rx - 1)},${f(bottom)} L ${f(EYE.x - EYE.rx - 1)},${f(EYE.y + EYE.ry - rise * 0.35)} Q ${f(EYE.x)},${f(EYE.y + EYE.ry - rise * 1.6)} ${f(EYE.x + EYE.rx + 1)},${f(EYE.y + EYE.ry - rise * 0.35)} L ${f(EYE.x + EYE.rx + 1)},${f(bottom)} Z`;
}

/** The edge of the rising lower lid. */
export function lowerLidEdge(amount) {
  const rise = EYE.ry * 1.25 * amount;
  return `M ${f(EYE.x - EYE.rx - 1)},${f(EYE.y + EYE.ry - rise * 0.35)} Q ${f(EYE.x)},${f(EYE.y + EYE.ry - rise * 1.6)} ${f(EYE.x + EYE.rx + 1)},${f(EYE.y + EYE.ry - rise * 0.35)}`;
}

const POSES = {
  // At rest: short, with the tip curled up and out. Cheerful even when nothing is happening.
  hang: [[0, 10], [0, 28], [2, 40], [13, 35]],
  // Pleased: raised up along the right side of the face to eye level, its lighter underside
  // showing along the inner edge and its end turned to face the front.
  raise: [[0, 10], [4, 38], [36, 32], [33, -4]],
  // Drooping straight down, tip tucked in: cross or sad.
  droop: [[0, 10], [0, 28], [0, 40], [-3, 46]],
};

function controlPoints(curl, sway) {
  const c = Math.max(-1, Math.min(1, curl));
  const to = c >= 0 ? POSES.raise : POSES.droop;
  const amount = Math.abs(c);
  const points = POSES.hang.map((p, i) => [lerp(p[0], to[i][0], amount), lerp(p[1], to[i][1], amount)]);

  // A gentle swing, strongest at the tip.
  points[2][0] += sway * 0.6;
  points[3][0] += sway;
  return points;
}

function bezier(p, t) {
  const m = 1 - t;
  const a = m * m * m;
  const b = 3 * m * m * t;
  const c = 3 * m * t * t;
  const d = t * t * t;
  return [
    a * p[0][0] + b * p[1][0] + c * p[2][0] + d * p[3][0],
    a * p[0][1] + b * p[1][1] + c * p[2][1] + d * p[3][1],
  ];
}

function tangent(p, t) {
  const m = 1 - t;
  const x = 3 * m * m * (p[1][0] - p[0][0]) + 6 * m * t * (p[2][0] - p[1][0]) + 3 * t * t * (p[3][0] - p[2][0]);
  const y = 3 * m * m * (p[1][1] - p[0][1]) + 6 * m * t * (p[2][1] - p[1][1]) + 3 * t * t * (p[3][1] - p[2][1]);
  const length = Math.hypot(x, y) || 1;
  return [x / length, y / length];
}

const BASE_WIDTH = 22;
const TIP_WIDTH = 11;
const width = (t) => lerp(BASE_WIDTH, TIP_WIDTH, Math.pow(t, 0.85));

/**
 * The trunk for a pose.
 * @param {number} curl -1 drooping, 0 at rest (tip curled up), 1 raised and curled.
 * @param {number} sway A small sideways swing of the tip, in units.
 * @returns {{ outline: string, edge: string, wrinkles: string[], underside: string, ridges: string[], face: object, tip: { x: number, y: number } }}
 */
export function trunk(curl = 0, sway = 0) {
  return trunkAlong(controlPoints(curl, sway));
}

/**
 * A trunk along any four control points, base first.
 * @param {number[][]} p The cubic curve's points, [[x, y], ...], from the base to the tip.
 */
export function trunkAlong(p) {
  const steps = 40;
  const left = [];
  const right = [];

  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const [x, y] = bezier(p, t);
    const [tx, ty] = tangent(p, t);
    const half = width(t) / 2;
    left.push([x - ty * half, y + tx * half]);
    right.push([x + ty * half, y - tx * half]);
  }

  // Down one side, round the tip, back up the other.
  const tipRadius = TIP_WIDTH / 2;
  const end = right[steps];
  let d = `M ${f(left[0][0])},${f(left[0][1])}`;
  for (let i = 1; i <= steps; i++) {
    d += ` L ${f(left[i][0])},${f(left[i][1])}`;
  }

  d += ` A ${f(tipRadius)},${f(tipRadius)} 0 0 0 ${f(end[0])},${f(end[1])}`;
  for (let i = steps - 1; i >= 0; i--) {
    d += ` L ${f(right[i][0])},${f(right[i][1])}`;
  }

  d += ' Z';

  // The trunk's contour, for where it passes in front of the face: both sides and the tip, but
  // not the first stretch where it grows out of the head, so it starts without a seam.
  const from = 8;
  let edge = `M ${f(left[from][0])},${f(left[from][1])}`;
  for (let i = from + 1; i <= steps; i++) {
    edge += ` L ${f(left[i][0])},${f(left[i][1])}`;
  }

  edge += ` A ${f(tipRadius)},${f(tipRadius)} 0 0 0 ${f(end[0])},${f(end[1])}`;
  for (let i = steps - 1; i >= from; i--) {
    edge += ` L ${f(right[i][0])},${f(right[i][1])}`;
  }

  // A few soft folds across the trunk, bowed toward the tip.
  const wrinkles = [0.42, 0.58, 0.74].map((t) => {
    const [x, y] = bezier(p, t);
    const [tx, ty] = tangent(p, t);
    const half = width(t) * 0.32;
    const bow = 1.3;
    return `M ${f(x - ty * half)},${f(y + tx * half)} Q ${f(x + tx * bow)},${f(y + ty * bow)} ${f(x + ty * half)},${f(y - tx * half)}`;
  });

  // The underside: a strip along the inner edge of the curve (the side toward the face), where a
  // raised trunk shows its lighter, ridged underside. Only the raised part, from the bend up.
  const probe = Math.round(steps * 0.7);
  const inner = Math.hypot(left[probe][0], left[probe][1]) < Math.hypot(right[probe][0], right[probe][1]) ? left : right;
  const outer = inner === left ? right : left;
  const rise = Math.round(steps * 0.34);
  const top = Math.round(steps * 0.96);
  const depth = 0.42;
  const mid = (i) => [lerp(inner[i][0], outer[i][0], depth), lerp(inner[i][1], outer[i][1], depth)];
  let underside = `M ${f(inner[rise][0])},${f(inner[rise][1])}`;
  for (let i = rise + 1; i <= top; i++) {
    underside += ` L ${f(inner[i][0])},${f(inner[i][1])}`;
  }

  for (let i = top; i >= rise; i--) {
    const [mx, my] = mid(i);
    underside += ` L ${f(mx)},${f(my)}`;
  }

  underside += ' Z';
  const ridges = [];
  for (let i = rise + 2; i < top; i += 2) {
    const [mx, my] = mid(i);
    ridges.push(`M ${f(inner[i][0])},${f(inner[i][1])} L ${f(mx)},${f(my)}`);
  }

  // The end of the trunk, turned to face the front, with its two nostrils: an oval across the
  // tip, its long side square to the trunk.
  const [tipX, tipY] = bezier(p, 1);
  const [etx, ety] = tangent(p, 1);
  const faceRx = TIP_WIDTH / 2 + 0.7;
  const face = {
    x: tipX + etx * 0.8,
    y: tipY + ety * 0.8,
    rx: faceRx,
    ry: faceRx * 0.78,
    angle: Math.atan2(ety, etx) + Math.PI / 2,
    nostrils: [-1, 1].map((s) => ({
      x: tipX + etx * 0.8 - ety * 2.3 * s,
      y: tipY + ety * 0.8 + etx * 2.3 * s,
      rx: 1.35,
      ry: 1.75,
    })),
  };

  return { outline: d, edge, wrinkles, underside, ridges, face, tip: { x: tipX, y: tipY } };
}

/**
 * Where the right ear goes when Ele hides her eyes: flopped down over the top of the face from
 * its edge, at full size. The left is mirrored.
 */
export const EAR_COVER = { x: 14.5, y: -10, rotate: 70, scale: 1 };
