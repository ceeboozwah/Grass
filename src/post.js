// Frame pipeline: the scene is drawn into a half-float target (linear HDR), a dual-filter bloom is built from it,
// and one last pass adds the bloom, tone maps (ACES), lifts colour a touch and writes sRGB to the screen.
import * as THREE from 'three';

const VERT = 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }';

export class Post {
  constructor(renderer) {
    this.r = renderer;
    this.quad = new THREE.Mesh(new THREE.PlaneGeometry(2, 2));
    this.quad.frustumCulled = false;
    this.scene = new THREE.Scene(); this.scene.add(this.quad);
    this.cam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
    this.levels = 5;
    this.strength = 0.3;
    this.threshold = 0.95;
    this.exposure = 1.0;
    const mk = (frag, uniforms, extra = {}) => new THREE.ShaderMaterial({ vertexShader: VERT, fragmentShader: frag, uniforms, depthTest: false, depthWrite: false, ...extra });
    this.pre = mk(`
      uniform sampler2D tMap; uniform vec2 uTexel; uniform float uThr;
      varying vec2 vUv;
      vec3 s(vec2 o){ vec3 c = texture2D(tMap, vUv + o * uTexel).rgb; c = min(c, vec3(24.0));
        float l = max(c.r, max(c.g, c.b)); return c * smoothstep(uThr * 0.7, uThr * 1.5 + 0.3, l); }
      void main(){ gl_FragColor = vec4((s(vec2(-1,-1)) + s(vec2(1,-1)) + s(vec2(-1,1)) + s(vec2(1,1))) * 0.25, 1.0); }`,
      { tMap: { value: null }, uTexel: { value: new THREE.Vector2() }, uThr: { value: this.threshold } });
    this.down = mk(`
      uniform sampler2D tMap; uniform vec2 uTexel; varying vec2 vUv;
      void main(){ vec3 c = texture2D(tMap, vUv + uTexel * vec2(-1,-1)).rgb + texture2D(tMap, vUv + uTexel * vec2(1,-1)).rgb
                          + texture2D(tMap, vUv + uTexel * vec2(-1,1)).rgb + texture2D(tMap, vUv + uTexel * vec2(1,1)).rgb;
        gl_FragColor = vec4(c * 0.25, 1.0); }`,
      { tMap: { value: null }, uTexel: { value: new THREE.Vector2() } });
    this.up = mk(`
      uniform sampler2D tMap; uniform vec2 uTexel; varying vec2 vUv;
      void main(){
        vec3 c = texture2D(tMap, vUv).rgb * 4.0;
        c += texture2D(tMap, vUv + uTexel * vec2( 1, 0)).rgb * 2.0 + texture2D(tMap, vUv + uTexel * vec2(-1, 0)).rgb * 2.0
           + texture2D(tMap, vUv + uTexel * vec2( 0, 1)).rgb * 2.0 + texture2D(tMap, vUv + uTexel * vec2( 0,-1)).rgb * 2.0;
        c += texture2D(tMap, vUv + uTexel * vec2( 1, 1)).rgb + texture2D(tMap, vUv + uTexel * vec2(-1, 1)).rgb
           + texture2D(tMap, vUv + uTexel * vec2( 1,-1)).rgb + texture2D(tMap, vUv + uTexel * vec2(-1,-1)).rgb;
        gl_FragColor = vec4(c / 16.0 * 0.85, 1.0); }`,
      { tMap: { value: null }, uTexel: { value: new THREE.Vector2() } },
      { blending: THREE.AdditiveBlending, transparent: true });
    this.final = mk(`
      uniform sampler2D tScene, tBloom; uniform float uBloom, uExposure; uniform vec2 uRes; varying vec2 vUv;
      vec3 aces(vec3 x){ return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
      void main(){
        vec3 c = texture2D(tScene, vUv).rgb + texture2D(tBloom, vUv).rgb * uBloom;
        c *= uExposure;
        c = aces(c);
        float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
        c = mix(vec3(l), c, 1.32);                      // vibrance, because ACES pales bright greens
        c = pow(c, vec3(1.0 / 2.2));
        vec2 q = vUv - 0.5; c *= 1.0 - dot(q, q) * 0.5; // soft vignette
        c += (fract(sin(dot(gl_FragCoord.xy, vec2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
        gl_FragColor = vec4(c, 1.0); }`,
      { tScene: { value: null }, tBloom: { value: null }, uBloom: { value: 0.3 }, uExposure: { value: 1 }, uRes: { value: new THREE.Vector2() } });
    this.resize();
  }

  resize() {
    const size = this.r.getDrawingBufferSize(new THREE.Vector2());
    const w = Math.max(2, size.x | 0), h = Math.max(2, size.y | 0);
    this.rt?.dispose(); this.mips?.forEach((m) => m.dispose());
    this.rt = new THREE.WebGLRenderTarget(w, h, { type: THREE.HalfFloatType, samples: 4, depthBuffer: true, colorSpace: THREE.LinearSRGBColorSpace });
    this.mips = [];
    for (let i = 0; i < this.levels; i++) {
      const mw = Math.max(2, w >> (i + 1)), mh = Math.max(2, h >> (i + 1));
      const t = new THREE.WebGLRenderTarget(mw, mh, { type: THREE.HalfFloatType, depthBuffer: false });
      t.texture.minFilter = t.texture.magFilter = THREE.LinearFilter;
      this.mips.push(t);
    }
    this.w = w; this.h = h;
  }

  #pass(mat, target, clear) {
    this.quad.material = mat;
    this.r.setRenderTarget(target);
    if (clear) this.r.clear();
    this.r.render(this.scene, this.cam);
  }

  renderScene(scene, camera) {
    this.r.setRenderTarget(this.rt);
    this.r.clear();
    this.r.render(scene, camera);
  }

  /** Call after renderScene. */
  finish() {
    const r = this.r;
    const prevAuto = r.autoClear; r.autoClear = false;
    // bright pass into mip 0, then down the chain
    this.pre.uniforms.tMap.value = this.rt.texture;
    this.pre.uniforms.uTexel.value.set(1 / this.w, 1 / this.h);
    this.pre.uniforms.uThr.value = this.threshold;
    this.#pass(this.pre, this.mips[0], true);
    for (let i = 1; i < this.levels; i++) {
      const src = this.mips[i - 1];
      this.down.uniforms.tMap.value = src.texture;
      this.down.uniforms.uTexel.value.set(1 / src.width, 1 / src.height);
      this.#pass(this.down, this.mips[i], true);
    }
    // back up, additively
    for (let i = this.levels - 1; i > 0; i--) {
      const src = this.mips[i];
      this.up.uniforms.tMap.value = src.texture;
      this.up.uniforms.uTexel.value.set(1 / src.width, 1 / src.height);
      this.#pass(this.up, this.mips[i - 1], false);
    }
    this.final.uniforms.tScene.value = this.rt.texture;
    this.final.uniforms.tBloom.value = this.mips[0].texture;
    this.final.uniforms.uBloom.value = this.strength;
    this.final.uniforms.uExposure.value = this.exposure;
    this.#pass(this.final, null, true);
    r.autoClear = prevAuto;
  }
}
