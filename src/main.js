// A small playground for the grass: the same blades, cut map, lighting and post as the game, with every adjustable
// control. Drag on the lawn to mow it with a 1.35 m deck; the sliders drive the shader live.
import * as THREE from 'three';
import { Lawn, LAWN, makeGrass, makeGround } from './grass.js';
import { Post } from './post.js';
import { makeSky, applySun } from './sky.js';

const $ = (id) => document.getElementById(id);

// ---------------------------------------------------------------- settings
const DEFAULTS = {
  height: 0.32, glow: 0.8, grad: 0.85, bias: 0, wind: 0.7, deck: 0.05, sun: 0.8, dens: 1,
  root: '#0b8a4d', mid: '#3be06b', tip: '#d6ff4f',
};
const PRESETS = {
  'Neon meadow': ['#0b8a4d', '#3be06b', '#d6ff4f'],
  'Aqua': ['#0a6f7a', '#1fd6a2', '#a8fff0'],
  'Golden hour': ['#1d7a3a', '#8fe03c', '#ffd84a'],
  'Candy': ['#0e9a6a', '#55e08a', '#ffb3e6'],
  'Deep forest': ['#05452e', '#18a05a', '#79f58f'],
  'Ember tips': ['#0f7d45', '#37d46b', '#ff8a4a'],
};
const KEY = 'grass-playground';
const S = { ...DEFAULTS };
try { Object.assign(S, JSON.parse(localStorage.getItem(KEY) || '{}')); } catch (e) { /* private mode */ }
const save = () => { try { localStorage.setItem(KEY, JSON.stringify(S)); } catch (e) { /* ignore */ } };

// ---------------------------------------------------------------- renderer and scene
const canvas = $('view');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
renderer.setSize(window.innerWidth, window.innerHeight, false);
const post = new Post(renderer);

const scene = new THREE.Scene();
scene.fog = new THREE.FogExp2('#bfe6ff', 0.0042);
const camera = new THREE.PerspectiveCamera(62, window.innerWidth / window.innerHeight, 0.08, 2000);

const lawn = new Lawn();
const U = {
  uTime: { value: 0 }, uHeight: { value: S.height }, uWind: { value: S.wind }, uLawn: { value: LAWN },
  uCut: { value: lawn.tex }, uCamFwd: { value: new THREE.Vector2(0, 1) },
  uRoot: { value: new THREE.Color(S.root) }, uMid: { value: new THREE.Color(S.mid) }, uTip: { value: new THREE.Color(S.tip) },
  uSunDirW: { value: new THREE.Vector3(0, 1, 0) }, uSunColor: { value: new THREE.Color() },
  uGlow: { value: S.glow }, uGradAmt: { value: S.grad }, uGradBias: { value: S.bias },
  uZenith: { value: new THREE.Color() }, uHorizon: { value: new THREE.Color() }, uCloud: { value: new THREE.Color() },
};

const sky = makeSky(U);
scene.add(sky.mesh);
const envScene = new THREE.Scene(); envScene.add(sky.env);
const pmrem = new THREE.PMREMGenerator(renderer);

const ground = makeGround(U); scene.add(ground);
const grass = makeGrass(lawn, U); scene.add(grass);

const sun = new THREE.DirectionalLight('#ffffff', 3);
const hemi = new THREE.HemisphereLight('#bfe6ff', '#2a8a4a', 2);
scene.add(sun, sun.target, hemi);

// the deck's footprint, shown under the pointer
const cursor = new THREE.Mesh(
  new THREE.PlaneGeometry(1.35, 0.9),
  new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.16, depthWrite: false, fog: false }));
cursor.rotation.x = -Math.PI / 2; cursor.visible = false; cursor.renderOrder = 5;
scene.add(cursor);

// ---------------------------------------------------------------- sliders
function fill(el) { el.style.setProperty('--p', ((el.value - el.min) / (el.max - el.min) * 100) + '%'); }
function bindSlider(id, get, set, fmt, onChange) {
  const el = $('s-' + id), out = $('o-' + id);
  el.addEventListener('input', () => { set(+el.value); out.textContent = fmt(+el.value); fill(el); save(); onChange?.(); });
  el.value = get(); out.textContent = fmt(+el.value); fill(el);
}
let envTimer = 0;
bindSlider('height', () => Math.round(S.height * 100), (v) => { S.height = v / 100; U.uHeight.value = S.height; }, (v) => v + ' cm');
bindSlider('glow', () => Math.round(S.glow * 100), (v) => { S.glow = v / 100; U.uGlow.value = S.glow; applyBloom(); }, (v) => v + '%');
bindSlider('grad', () => Math.round(S.grad * 100), (v) => { S.grad = v / 100; U.uGradAmt.value = S.grad; }, (v) => v + '%');
bindSlider('bias', () => Math.round(S.bias * 100), (v) => { S.bias = v / 100; U.uGradBias.value = S.bias; }, (v) => (v > 0 ? '+' : '') + v);
bindSlider('wind', () => Math.round(S.wind * 100), (v) => { S.wind = v / 100; U.uWind.value = S.wind; }, (v) => v + '%');
bindSlider('deck', () => Math.round(S.deck * 1000), (v) => { S.deck = v / 1000; }, (v) => (v / 10).toFixed(1) + ' cm');
bindSlider('sun', () => Math.round(S.sun * 100), (v) => { S.sun = v / 100; lighting(); }, (v) => {
  const hr = 6 + (v / 100) * 13; const h = Math.floor(hr), m = Math.floor((hr - h) * 60);
  return ((h + 11) % 12 + 1) + ':' + String(m).padStart(2, '0') + (h >= 12 ? ' pm' : ' am');
}, () => { clearTimeout(envTimer); envTimer = setTimeout(rebuildEnv, 250); });
bindSlider('dens', () => Math.round(S.dens * 100), (v) => { S.dens = v / 100; applyDensity(); }, (v) => v + '%');

function setColors(root, mid, tip) {
  S.root = root; S.mid = mid; S.tip = tip;
  U.uRoot.value.set(root); U.uMid.value.set(mid); U.uTip.value.set(tip);
  $('c-root').value = root; $('c-mid').value = mid; $('c-tip').value = tip;
  lighting(); save();
}
for (const k of ['root', 'mid', 'tip']) {
  $('c-' + k).value = S[k];
  $('c-' + k).addEventListener('input', (e) => { S[k] = e.target.value; U['u' + k[0].toUpperCase() + k.slice(1)].value.set(e.target.value); lighting(); save(); });
}
for (const [name, c] of Object.entries(PRESETS)) {
  const b = document.createElement('button'); b.textContent = name;
  b.style.borderColor = c[2]; b.addEventListener('click', () => setColors(...c)); $('presets').appendChild(b);
}
$('b-regrow').onclick = () => lawn.reset();
window.addEventListener('keydown', (e) => { if (e.code === 'KeyH') document.body.classList.toggle('hide'); });

// ---------------------------------------------------------------- lighting and post (same numbers as the game)
function lighting() {
  const info = applySun(S.sun, U);
  sun.color.copy(U.uSunColor.value);
  sun.intensity = info.sunIntensity;
  sun.position.copy(info.dir).multiplyScalar(80);
  hemi.color.copy(U.uHorizon.value).lerp(U.uZenith.value, 0.55);
  hemi.groundColor.copy(U.uMid.value).multiplyScalar(0.55);
  hemi.intensity = info.hemiIntensity;
  scene.fog.color.copy(U.uHorizon.value);
  applyBloom();
}
function applyBloom() { post.strength = 0.16 + 0.34 * S.glow; post.threshold = 0.95; post.exposure = 0.95; }
function rebuildEnv() {
  const t = pmrem.fromScene(envScene, 0.02, 0.1, 200).texture;
  scene.environment?.dispose?.(); scene.environment = t;
}
function applyDensity() { grass.geometry.instanceCount = Math.floor(grass.userData.total * S.dens); }
lighting(); rebuildEnv(); applyDensity();

// ---------------------------------------------------------------- camera: orbit around the middle of the lawn
const cam = { yaw: 0, pitch: 0.44, dist: 6.5, tx: 0, tz: 0 };
function placeCamera() {
  const t = new THREE.Vector3(cam.tx, 0.7, cam.tz);
  const dir = new THREE.Vector3(Math.sin(cam.yaw) * Math.cos(cam.pitch), -Math.sin(cam.pitch), Math.cos(cam.yaw) * Math.cos(cam.pitch));
  camera.position.copy(t).addScaledVector(dir, -cam.dist); camera.position.y = Math.max(camera.position.y, 0.35);
  camera.lookAt(t);
  const f = camera.getWorldDirection(new THREE.Vector3()), l = Math.hypot(f.x, f.z) || 1;
  U.uCamFwd.value.set(f.x / l, f.z / l);
  sky.mesh.position.copy(camera.position);
}

// ---------------------------------------------------------------- pointer: drag to mow, right-drag or shift-drag to orbit
const ray = new THREE.Raycaster(), plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0), hit = new THREE.Vector3(), ndc = new THREE.Vector2();
function groundAt(e) {
  const r = canvas.getBoundingClientRect();
  ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
  ray.setFromCamera(ndc, camera);
  return ray.ray.intersectPlane(plane, hit) ? hit : null;
}
let mode = null, last = null, lastYaw = 0, sx = 0, sy = 0;
function mowTo(p) {
  const half = LAWN / 2;
  const x = THREE.MathUtils.clamp(p.x, -half, half), z = THREE.MathUtils.clamp(p.z, -half, half);
  cursor.visible = true; cursor.position.set(x, 0.02, z);
  if (last) {
    const dx = x - last.x, dz = z - last.z, d = Math.hypot(dx, dz);
    if (d > 0.01) lastYaw = Math.atan2(dx, dz);
    cursor.rotation.z = -lastYaw;
    if (S.deck < S.height * 0.9) for (let t = 0; t <= d; t += 0.2) lawn.stamp(last.x + dx * (t / (d || 1)), last.z + dz * (t / (d || 1)), lastYaw, S.deck, 0.675, 0.45);
  }
  last = { x, z };
}
canvas.addEventListener('contextmenu', (e) => e.preventDefault());
canvas.addEventListener('pointerdown', (e) => {
  canvas.setPointerCapture(e.pointerId); sx = e.clientX; sy = e.clientY;
  if (e.button === 2 || e.shiftKey) { mode = 'orbit'; return; }
  mode = 'mow'; last = null; const p = groundAt(e); if (p) mowTo(p);
});
canvas.addEventListener('pointermove', (e) => {
  if (mode === 'orbit') {
    cam.yaw -= (e.clientX - sx) * 0.005; cam.pitch = THREE.MathUtils.clamp(cam.pitch + (e.clientY - sy) * 0.004, 0.06, 1.35); sx = e.clientX; sy = e.clientY;
  } else if (mode === 'mow') { const p = groundAt(e); if (p) mowTo(p); }
  else if (e.pointerType === 'mouse') { const p = groundAt(e); if (p) { cursor.visible = true; cursor.position.set(p.x, 0.02, p.z); } }
});
canvas.addEventListener('pointerup', () => { mode = null; last = null; });
canvas.addEventListener('pointerleave', () => { if (!mode) cursor.visible = false; });
canvas.addEventListener('wheel', (e) => { cam.dist = THREE.MathUtils.clamp(cam.dist * Math.exp(e.deltaY * 0.001), 1.5, 40); e.preventDefault(); }, { passive: false });

// ---------------------------------------------------------------- loop
function resize() {
  renderer.setSize(window.innerWidth, window.innerHeight, false);
  camera.aspect = window.innerWidth / window.innerHeight; camera.updateProjectionMatrix();
  post.resize();
}
window.addEventListener('resize', resize);
let t0 = performance.now();
function frame(now) {
  U.uTime.value = (now - t0) / 1000;
  placeCamera();
  post.renderScene(scene, camera);
  post.finish();
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
window.__grass = { S, U, lawn, camera, cam, post, scene };   // handy for poking around in the console
