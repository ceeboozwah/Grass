// Stylized grass: ~520k instanced blades, a per-texel cut-height map the mower writes into, and a Lambert
// material patched (onBeforeCompile) so the grass still gets sun shadows, hemisphere light and fog from three.
import * as THREE from 'three';

export const LAWN = 64;          // metres, square, centred on the origin
const RES = 1280;                // cut-map texels per side (5 cm)
const MAX_CUT = 0.15;            // the map stores 0..MAX_CUT metres in 0..254; 255 = never cut
const TAU = Math.PI * 2;

/** Point in polygon (pts: [[x, z], ...]). */
export function inPoly(x, z, pts) {
  let c = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const [xi, zi] = pts[i], [xj, zj] = pts[j];
    if ((zi > z) !== (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) c = !c;
  }
  return c;
}

export class Lawn {
  constructor() {
    this.size = LAWN;
    this.res = RES;
    this.data = new Uint8Array(RES * RES * 2);
    this.tex = new THREE.DataTexture(this.data, RES, RES, THREE.RGFormat, THREE.UnsignedByteType);
    this.tex.minFilter = this.tex.magFilter = THREE.NearestFilter;
    this.tex.generateMipmaps = false;
    this.clears = [];            // ground where nothing grows: {t:'c',x,z,r} circles and {t:'p',pts:[[x,z]..]} polygons
    this.reset();
  }

  reset() {
    const d = this.data;
    for (let i = 0; i < d.length; i += 2) { d[i] = 255; d[i + 1] = 0; }
    this.excluded = 0;
    this.mowed = 0;
    for (const c of this.clears) c.t === 'c' ? this.#clear(c.x, c.z, c.r) : this.#clearPoly(c.pts);
    this.tex.needsUpdate = true;
  }

  addClear(x, z, r) { this.clears.push({ t: 'c', x, z, r }); this.#clear(x, z, r); this.tex.needsUpdate = true; }
  addClearPoly(pts) { this.clears.push({ t: 'p', pts }); this.#clearPoly(pts); this.tex.needsUpdate = true; }

  #clearPoly(pts) {
    const t = this.res / this.size, h = this.size / 2;
    let x0 = 1e9, x1 = -1e9, z0 = 1e9, z1 = -1e9;
    for (const [x, z] of pts) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); z0 = Math.min(z0, z); z1 = Math.max(z1, z); }
    for (let iz = Math.max(0, Math.floor((z0 + h) * t)); iz <= Math.min(this.res - 1, Math.ceil((z1 + h) * t)); iz++) {
      for (let ix = Math.max(0, Math.floor((x0 + h) * t)); ix <= Math.min(this.res - 1, Math.ceil((x1 + h) * t)); ix++) {
        const i = (iz * this.res + ix) * 2;
        if (this.data[i] !== 0 && inPoly((ix + 0.5) / t - h, (iz + 0.5) / t - h, pts)) { this.data[i] = 0; this.excluded++; }
      }
    }
  }

  #clear(cx, cz, r) {
    const t = this.res / this.size, h = this.size / 2;
    for (let iz = Math.max(0, Math.floor((cz - r + h) * t)); iz <= Math.min(this.res - 1, Math.ceil((cz + r + h) * t)); iz++) {
      for (let ix = Math.max(0, Math.floor((cx - r + h) * t)); ix <= Math.min(this.res - 1, Math.ceil((cx + r + h) * t)); ix++) {
        const dx = (ix + 0.5) / t - h - cx, dz = (iz + 0.5) / t - h - cz;
        const i = (iz * this.res + ix) * 2;
        if (dx * dx + dz * dz <= r * r && this.data[i] !== 0) { this.data[i] = 0; this.excluded++; }
      }
    }
  }

  /** After loading cut data: count what has been mown. */
  recount() { let n = 0; const d = this.data; for (let i = 0; i < d.length; i += 2) if (d[i] !== 255 && d[i] !== 0) n++; this.mowed = n; this.tex.needsUpdate = true; }

  get total() { return this.res * this.res - this.excluded; }
  get percent() { return this.mowed / this.total; }

  /** Cut a round patch (the string trimmer). Returns texels changed. */
  stampDisc(cx, cz, r, height, yaw) {
    const t = this.res / this.size, h = this.size / 2;
    const x0 = Math.max(0, Math.floor((cx - r + h) * t)), x1 = Math.min(this.res - 1, Math.ceil((cx + r + h) * t));
    const z0 = Math.max(0, Math.floor((cz - r + h) * t)), z1 = Math.min(this.res - 1, Math.ceil((cz + r + h) * t));
    const hv = Math.max(0, Math.min(254, Math.round(height / MAX_CUT * 254)));
    const dir = Math.round((((yaw % TAU) + TAU) % TAU) / TAU * 255);
    const d = this.data; let changed = 0;
    for (let iz = z0; iz <= z1; iz++) for (let ix = x0; ix <= x1; ix++) {
      const dx = (ix + 0.5) / t - h - cx, dz = (iz + 0.5) / t - h - cz;
      if (dx * dx + dz * dz > r * r) continue;
      const i = (iz * this.res + ix) * 2;
      if (d[i] > hv) { if (d[i] === 255) this.mowed++; d[i] = hv; d[i + 1] = dir; changed++; }
    }
    if (changed) this.tex.needsUpdate = true;
    return changed;
  }

  /** Cut the oriented rectangle (centre cx,cz; heading yaw; half width hw across, half length hl along). Returns texels changed. */
  stamp(cx, cz, yaw, height, hw, hl) {
    const s = Math.sin(yaw), c = Math.cos(yaw);
    const r = Math.hypot(hw, hl), t = this.res / this.size, h = this.size / 2;
    const x0 = Math.max(0, Math.floor((cx - r + h) * t)), x1 = Math.min(this.res - 1, Math.ceil((cx + r + h) * t));
    const z0 = Math.max(0, Math.floor((cz - r + h) * t)), z1 = Math.min(this.res - 1, Math.ceil((cz + r + h) * t));
    const hv = Math.max(0, Math.min(254, Math.round(height / MAX_CUT * 254)));
    const dir = Math.round((((yaw % TAU) + TAU) % TAU) / TAU * 255);
    const d = this.data;
    let changed = 0;
    for (let iz = z0; iz <= z1; iz++) {
      const wz = (iz + 0.5) / t - h - cz;
      for (let ix = x0; ix <= x1; ix++) {
        const wx = (ix + 0.5) / t - h - cx;
        const f = wx * s + wz * c, q = wx * c - wz * s;
        if (f > hl || f < -hl || q > hw || q < -hw) continue;
        const i = (iz * this.res + ix) * 2;
        if (d[i] > hv) {
          if (d[i] === 255) this.mowed++;
          d[i] = hv; d[i + 1] = dir; changed++;
        }
      }
    }
    if (changed) this.tex.needsUpdate = true;
    return changed;
  }
}

/** One draw call of instanced blades. `uniforms` is shared with the rest of the scene so sliders drive it live. */
export function makeGrass(lawn, uniforms, count = 520000) {
  // blade: 3 rows of two vertices plus a tip. x = side (-.5..+.5), y = 0..1 up the blade.
  const rows = [0, 0.35, 0.7];
  const pos = [];
  for (const y of rows) pos.push(-0.5, y, 0, 0.5, y, 0);
  pos.push(0, 1, 0);
  const index = [0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4, 4, 5, 6];
  const geo = new THREE.InstancedBufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  geo.setIndex(index);

  // stratified jitter, then shuffled so any prefix of the instances is an even scatter (the density slider just draws fewer)
  const n = Math.ceil(Math.sqrt(count)), cell = LAWN / n;
  const total = n * n;
  const order = new Uint32Array(total);
  for (let i = 0; i < total; i++) order[i] = i;
  for (let i = total - 1; i > 0; i--) { const j = (Math.random() * (i + 1)) | 0; const t = order[i]; order[i] = order[j]; order[j] = t; }
  const aPos = new Float32Array(total * 2), aRnd = new Float32Array(total * 4);
  for (let k = 0; k < total; k++) {
    const i = order[k], gx = i % n, gz = (i / n) | 0;
    aPos[k * 2] = (gx + Math.random()) * cell - LAWN / 2;
    aPos[k * 2 + 1] = (gz + Math.random()) * cell - LAWN / 2;
    aRnd[k * 4] = Math.random(); aRnd[k * 4 + 1] = Math.random(); aRnd[k * 4 + 2] = Math.random(); aRnd[k * 4 + 3] = Math.random();
  }
  geo.setAttribute('aPos', new THREE.InstancedBufferAttribute(aPos, 2));
  geo.setAttribute('aRnd', new THREE.InstancedBufferAttribute(aRnd, 4));
  geo.instanceCount = total;

  const mat = new THREE.MeshLambertMaterial({ side: THREE.DoubleSide, color: 0xffffff });
  mat.customProgramCacheKey = () => 'grass-blades';
  mat.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>
        attribute vec2 aPos; attribute vec4 aRnd;
        uniform float uTime, uHeight, uWind, uLawn;
        uniform sampler2D uCut; uniform vec2 uCamFwd;
        varying vec4 vGA; varying vec3 vGB; varying vec3 vGW;`)
      .replace('#include <beginnormal_vertex>', `
        float a = aRnd.x * 6.2831853;
        vec2 ax = vec2(cos(a), sin(a));
        vec2 fd = vec2(-ax.y, ax.x);
        vec2 cs = texture2D(uCut, aPos / uLawn + 0.5).rg;
        float cutF = step(cs.r, 0.998);
        float cutH = mix(100.0, cs.r * 1.003937 * ${MAX_CUT.toFixed(3)}, cutF);
        float h0 = uHeight * (0.62 + 0.76 * aRnd.y);
        float H = min(h0, cutH * (0.85 + 0.3 * aRnd.z));
        float by = position.y;
        float side = position.x;
        float vis = smoothstep(0.002, 0.008, H);
        float W0 = (0.05 + 0.03 * aRnd.w) * mix(1.9, 1.0, clamp(H / 0.2, 0.0, 1.0)) * vis;
        float wd = W0 * pow(max(1.0 - by, 0.0), 0.75);
        float lean = (0.10 + 0.32 * aRnd.z) * H * (1.0 - cutF * 0.5);
        float gust = sin(dot(aPos, vec2(0.31, 0.18)) + uTime * 1.25) * 0.55
                   + sin(dot(aPos, vec2(-0.23, 0.41)) * 1.7 + uTime * 2.3) * 0.3;
        float sway = gust * uWind * 0.22 * H;
        vec3 gPos = vec3(aPos.x, 0.0, aPos.y)
                  + vec3(ax.x, 0.0, ax.y) * side * wd
                  + vec3(0.0, by * H * (1.0 - 0.18 * by * by), 0.0)
                  + by * by * (vec3(fd.x, 0.0, fd.y) * lean + vec3(0.87, 0.0, 0.5) * sway);
        vec3 objectNormal = normalize(vec3(0.0, 1.0, 0.0) + vec3(ax.x, 0.0, ax.y) * side * 0.9 + vec3(fd.x, 0.0, fd.y) * (0.15 + 0.35 * by));
        float ang = cs.g * 6.2831853;
        vGA = vec4(by, aRnd.w, cutF, dot(vec2(sin(ang), cos(ang)), uCamFwd));
        vGB = vec3(H, 0.0, 0.0);
        vGW = gPos;`)
      .replace('#include <begin_vertex>', 'vec3 transformed = gPos;');

    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>
        uniform vec3 uRoot, uMid, uTip, uSunDirW, uSunColor;
        uniform float uGlow, uGradAmt, uGradBias;
        varying vec4 vGA; varying vec3 vGB; varying vec3 vGW;
        float gh(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float gn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
          return mix(mix(gh(i), gh(i + vec2(1, 0)), f.x), mix(gh(i + vec2(0, 1)), gh(i + vec2(1, 1)), f.x), f.y); }`)
      .replace('#include <color_fragment>', `
        float y = clamp(vGA.x, 0.0, 1.0);
        float expo = mix(2.4, 0.5, uGradBias * 0.5 + 0.5);
        float g = pow(y, expo);
        g *= mix(0.72, 1.0, smoothstep(0.03, 0.3, vGB.x));
        g = mix(0.45, g, uGradAmt);
        vec3 gc = g < 0.5 ? mix(uRoot, uMid, g * 2.0) : mix(uMid, uTip, (g - 0.5) * 2.0);
        float pn = gn(vGW.xz * 0.28) * 0.6 + gn(vGW.xz * 1.1) * 0.4;
        gc *= mix(0.8, 1.2, pn);
        gc = mix(gc, uTip, smoothstep(0.55, 0.95, pn) * 0.2 * y);
        gc *= 0.88 + 0.24 * vGA.y;
        gc *= 1.0 + vGA.z * (0.08 + 0.18 * vGA.w);
        float ao = mix(0.42, 1.0, smoothstep(0.0, 0.55, y));
        gc *= ao;
        diffuseColor.rgb = gc;
        vec3 gV = normalize(cameraPosition - vGW);
        float back = pow(clamp(dot(-gV, uSunDirW), 0.0, 1.0), 3.0);
        vec3 gEmit = gc * uGlow * (0.05 + 0.95 * y * y + vGA.z * 0.3) + uSunColor * gc * back * y * 0.9;`)
      .replace('#include <normal_fragment_begin>', `
        vec3 normal = normalize(vNormal);
        vec3 nonPerturbedNormal = normal;`)
      .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
        totalEmissiveRadiance += gEmit;`);
  };

  const mesh = new THREE.Mesh(geo, mat);
  mesh.frustumCulled = false;
  mesh.receiveShadow = true;
  mesh.userData.total = total;
  return mesh;
}

/** The ground under and around the lawn: dark root green inside the border, a bright flowery meadow outside it. */
export function makeGround(uniforms) {
  const mat = new THREE.MeshLambertMaterial({ color: 0xffffff });
  mat.customProgramCacheKey = () => 'grass-ground';
  mat.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vWPG;')
      .replace('#include <begin_vertex>', '#include <begin_vertex>\nvWPG = (modelMatrix * vec4(position, 1.0)).xyz;');
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>
        uniform vec3 uRoot, uMid, uTip; uniform float uLawn;
        varying vec3 vWPG;
        float gh(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float gn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
          return mix(mix(gh(i), gh(i + vec2(1, 0)), f.x), mix(gh(i + vec2(0, 1)), gh(i + vec2(1, 1)), f.x), f.y); }`)
      .replace('#include <color_fragment>', `
        vec2 p = vWPG.xz;
        float n = gn(p * 0.07) * 0.6 + gn(p * 0.31) * 0.3 + gn(p * 1.3) * 0.1;
        vec2 q = abs(p) - uLawn * 0.5;
        float inLawn = step(max(q.x, q.y), 0.0);
        vec3 inside = mix(uRoot, uMid, 0.3) * 0.72 * (0.8 + 0.4 * n);
        vec3 outside = mix(uMid * 0.75, uTip * 0.65, smoothstep(0.35, 0.8, n));
        vec2 cp = p * 2.2; vec2 cf = fract(cp) - 0.5; vec2 ci = floor(cp);
        float fl = smoothstep(0.13, 0.08, length(cf)) * step(0.965, gh(ci));
        vec3 fc = mix(vec3(1.0, 0.95, 0.8), vec3(1.0, 0.55, 0.75), step(0.5, gh(ci + 7.0)));
        outside = mix(outside, fc, fl * (1.0 - inLawn));
        diffuseColor.rgb = mix(outside, inside, inLawn);`);
  };
  const m = new THREE.Mesh(new THREE.PlaneGeometry(1600, 1600), mat);
  m.rotation.x = -Math.PI / 2;
  m.receiveShadow = true;
  return m;
}
