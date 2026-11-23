// Stylized grass: ~520k instanced blades in one draw call, and a Lambert material patched (onBeforeCompile) so
// the grass still gets sun shadows, hemisphere light and fog from three.
import * as THREE from 'three';

export const LAWN = 64;          // metres, square, centred on the origin

/** One draw call of instanced blades. `uniforms` is shared with the rest of the scene so sliders drive it live. */
export function makeGrass(uniforms, count = 520000) {
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
        uniform float uTime, uHeight, uWind;
        varying vec2 vGA; varying float vGH; varying vec3 vGW;`)
      .replace('#include <beginnormal_vertex>', `
        float a = aRnd.x * 6.2831853;
        vec2 ax = vec2(cos(a), sin(a));
        vec2 fd = vec2(-ax.y, ax.x);
        float h0 = uHeight * (0.62 + 0.76 * aRnd.y);
        float H = h0;
        float by = position.y;
        float side = position.x;
        float W0 = (0.05 + 0.03 * aRnd.w) * mix(1.9, 1.0, clamp(H / 0.2, 0.0, 1.0));
        float wd = W0 * pow(max(1.0 - by, 0.0), 0.75);
        float lean = (0.10 + 0.32 * aRnd.z) * H;
        float gust = sin(dot(aPos, vec2(0.31, 0.18)) + uTime * 1.25) * 0.55
                   + sin(dot(aPos, vec2(-0.23, 0.41)) * 1.7 + uTime * 2.3) * 0.3;
        float sway = gust * uWind * 0.22 * H;
        vec3 gPos = vec3(aPos.x, 0.0, aPos.y)
                  + vec3(ax.x, 0.0, ax.y) * side * wd
                  + vec3(0.0, by * H * (1.0 - 0.18 * by * by), 0.0)
                  + by * by * (vec3(fd.x, 0.0, fd.y) * lean + vec3(0.87, 0.0, 0.5) * sway);
        vec3 objectNormal = normalize(vec3(0.0, 1.0, 0.0) + vec3(ax.x, 0.0, ax.y) * side * 0.9 + vec3(fd.x, 0.0, fd.y) * (0.15 + 0.35 * by));
        vGA = vec2(by, aRnd.w);
        vGH = H;
        vGW = gPos;`)
      .replace('#include <begin_vertex>', 'vec3 transformed = gPos;');

    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>
        uniform vec3 uRoot, uMid, uTip, uSunDirW, uSunColor;
        uniform float uGlow, uGradAmt, uGradBias;
        varying vec2 vGA; varying float vGH; varying vec3 vGW;
        float gh(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float gn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
          return mix(mix(gh(i), gh(i + vec2(1, 0)), f.x), mix(gh(i + vec2(0, 1)), gh(i + vec2(1, 1)), f.x), f.y); }`)
      .replace('#include <color_fragment>', `
        float y = clamp(vGA.x, 0.0, 1.0);
        float expo = mix(2.4, 0.5, uGradBias * 0.5 + 0.5);
        float g = pow(y, expo);
        g *= mix(0.72, 1.0, smoothstep(0.03, 0.3, vGH));
        g = mix(0.45, g, uGradAmt);
        vec3 gc = g < 0.5 ? mix(uRoot, uMid, g * 2.0) : mix(uMid, uTip, (g - 0.5) * 2.0);
        float pn = gn(vGW.xz * 0.28) * 0.6 + gn(vGW.xz * 1.1) * 0.4;
        gc *= mix(0.8, 1.2, pn);
        gc = mix(gc, uTip, smoothstep(0.55, 0.95, pn) * 0.2 * y);
        gc *= 0.88 + 0.24 * vGA.y;
        float ao = mix(0.42, 1.0, smoothstep(0.0, 0.55, y));
        gc *= ao;
        diffuseColor.rgb = gc;
        vec3 gV = normalize(cameraPosition - vGW);
        float back = pow(clamp(dot(-gV, uSunDirW), 0.0, 1.0), 3.0);
        vec3 gEmit = gc * uGlow * (0.05 + 0.95 * y * y) + uSunColor * gc * back * y * 0.9;`)
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
