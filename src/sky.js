// The stylized sky and the sun for a time of day. Fills the shared uniforms the grass and ground read.
import * as THREE from 'three';

const C = (h) => new THREE.Color(h);
const lerpC = (a, b, t) => a.clone().lerp(b, t);

// colour keys: noon -> horizon-low sun
const PAL = {
  zenithNoon: C('#2f86f0'), horizonNoon: C('#bfe6ff'), sunNoon: C('#fff3da'),
  zenithLow: C('#4a58c8'), horizonLow: C('#ffa86b'), sunLow: C('#ffb877'),
};

const SKY_FRAG = `
  uniform vec3 uZenith, uHorizon, uSunCol, uCloud; uniform vec3 uSunDir; uniform float uTime;
  varying vec3 vDir;
  float h(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
  float n(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.-2.*f);
    return mix(mix(h(i),h(i+vec2(1,0)),f.x), mix(h(i+vec2(0,1)),h(i+vec2(1,1)),f.x), f.y); }
  float fbm(vec2 p){ float a=.5, s=0.; for(int i=0;i<5;i++){ s+=a*n(p); p=p*2.03+vec2(17.,9.); a*=.5; } return s; }
  void main(){
    vec3 d = normalize(vDir);
    float up = max(d.y, 0.0);
    vec3 col = mix(uHorizon, uZenith, pow(up, 0.5));
    float s = max(dot(d, uSunDir), 0.0);
    col += uSunCol * (pow(s, 900.0) * 9.0 + pow(s, 24.0) * 0.45 + pow(s, 4.0) * 0.18);
    // soft, flat stylized clouds on a plane
    vec2 p = d.xz / (d.y + 0.28) * 1.3 + vec2(uTime * 0.006, 0.0);
    float c = smoothstep(0.52, 0.82, fbm(p));
    c *= smoothstep(0.0, 0.2, d.y);
    float lit = 0.65 + 0.35 * pow(s, 2.0);
    col = mix(col, uCloud * lit + uSunCol * pow(s, 6.0) * 0.4, c * 0.75);
    if (d.y < 0.0) col = mix(uHorizon, uHorizon * 0.8, clamp(-d.y * 4.0, 0.0, 1.0));
    gl_FragColor = vec4(col, 1.0);
  }`;

export function makeSky(uniforms) {
  const mat = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false,
    uniforms: {
      uZenith: uniforms.uZenith, uHorizon: uniforms.uHorizon, uSunCol: uniforms.uSunColor, uCloud: uniforms.uCloud,
      uSunDir: uniforms.uSunDirW, uTime: uniforms.uTime,
    },
    vertexShader: 'varying vec3 vDir; void main(){ vDir = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: SKY_FRAG,
  });
  const mesh = new THREE.Mesh(new THREE.SphereGeometry(800, 32, 16), mat);
  mesh.renderOrder = -10; mesh.frustumCulled = false;
  const env = new THREE.Mesh(new THREE.SphereGeometry(50, 32, 16), mat);   // same sky, small, for the reflection probe
  return { mesh, env };
}

/** Time of day s in 0..1 (sunrise .. sunset). Fills the shared uniforms and returns the numbers the lights need. */
export function applySun(s, U) {
  const el = Math.sin(Math.PI * s) * 1.15 + 0.07;                  // radians above the horizon
  const az = THREE.MathUtils.lerp(-1.9, 1.5, s);
  const sd = new THREE.Vector3(Math.cos(el) * Math.sin(az), Math.sin(el), Math.cos(el) * Math.cos(az)).normalize();
  const warm = Math.pow(1 - Math.min(1, Math.sin(el) * 1.35), 1.6);
  U.uSunDirW.value.copy(sd);
  U.uZenith.value.copy(lerpC(PAL.zenithNoon, PAL.zenithLow, warm));
  U.uHorizon.value.copy(lerpC(PAL.horizonNoon, PAL.horizonLow, warm));
  U.uSunColor.value.copy(lerpC(PAL.sunNoon, PAL.sunLow, warm));
  U.uCloud.value.copy(lerpC(C('#ffffff'), C('#ffd2b0'), warm));
  const k = 0.35 + 0.65 * Math.sqrt(Math.max(Math.sin(el), 0));
  return { dir: sd, warm, sunIntensity: 2.7 * k, hemiIntensity: 1.0 + 0.6 * k, elevation: el };
}
