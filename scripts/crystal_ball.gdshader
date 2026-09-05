shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform vec4 fog_color_1 : source_color = vec4(0.05, 0.92, 0.85, 1.0);
uniform vec4 fog_color_2 : source_color = vec4(0.62, 0.12, 0.95, 1.0);
uniform float fog_speed : hint_range(0.0, 5.0) = 0.55;
uniform float fog_density : hint_range(1.0, 10.0) = 3.2;
uniform float glass_reflection : hint_range(0.0, 1.0) = 0.55;
uniform float opacity : hint_range(0.0, 1.0) = 0.42;
uniform float lift : hint_range(0.0, 1.0) = 0.10;
uniform float session_seed : hint_range(0.0, 99.0) = 1.0;
uniform float spin_sign : hint_range(-1.0, 1.0) = 1.0;
uniform float tumble : hint_range(0.0, 1.0) = 0.35;
uniform float light_drift : hint_range(0.05, 0.8) = 0.32;

// Tap ripples (epkids water baseline) — только внутри сферы.
uniform float ripple_time = 0.0;
uniform vec4 ripple0;
uniform vec4 ripple1;
uniform vec4 ripple2;
uniform vec4 ripple3;
uniform vec4 ripple4;
uniform vec4 ripple5;
uniform vec4 ripple6;
uniform vec4 ripple7;

const float RIPPLE_FREQ = 15.5;
const float RIPPLE_SPEED = 0.24;
const float RIPPLE_AMP = 0.085;
const float RIPPLE_LIFE = 4.8;
const float RIPPLE_ONSET = 0.34;
const float RIPPLE_SPATIAL_DECAY = 1.05;
const float RIPPLE_TIME_DECAY = 0.15;
const vec2 RIPPLE_ASPECT = vec2(1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 5; i++) {
		value += amplitude * noise(p);
		p *= 2.07;
		amplitude *= 0.5;
	}
	return value;
}

vec2 rotate(vec2 p, float a) {
	float c = cos(a);
	float s = sin(a);
	return vec2(c * p.x - s * p.y, s * p.x + c * p.y);
}

vec4 fetch_ripple(int i) {
	if (i == 0) return ripple0;
	if (i == 1) return ripple1;
	if (i == 2) return ripple2;
	if (i == 3) return ripple3;
	if (i == 4) return ripple4;
	if (i == 5) return ripple5;
	if (i == 6) return ripple6;
	return ripple7;
}

float ripple_hash(float n) {
	return fract(sin(n) * 43758.5453123);
}

// η = sin(k(r−vt))·e^(−αr)·e^(−βt) / √(r+r₀) — как epkids water.
vec3 tap_ripple_surface(vec2 uv, float time) {
	float h = 0.0;
	vec2 grad = vec2(0.0);

	for (int i = 0; i < 8; i++) {
		vec4 r = fetch_ripple(i);
		if (r.w < 0.01) {
			continue;
		}
		float age = time - r.z;
		if (age < 0.0 || age > RIPPLE_LIFE) {
			continue;
		}
		vec2 d = (uv - r.xy) * RIPPLE_ASPECT;
		float dist = length(d);
		if (dist > 0.95) {
			continue;
		}
		vec2 radial = d / (dist + 0.0004);
		vec2 to_uv = vec2(radial.x / RIPPLE_ASPECT.x, radial.y / RIPPLE_ASPECT.y);

		float seed = r.w;
		int ring_count = 3 + int(floor(ripple_hash(seed * 17.31) * 3.0));
		float fade_out = 1.0 - smoothstep(RIPPLE_LIFE * 0.55, RIPPLE_LIFE, age);
		float tap_onset = 1.0 - exp(-age * age / (RIPPLE_ONSET * RIPPLE_ONSET));

		for (int j = 0; j < 5; j++) {
			if (j >= ring_count) {
				break;
			}
			float fj = float(j);
			float hj = ripple_hash(seed + fj * 1.618);
			float hk = ripple_hash(seed + fj * 2.718);
			float hm = ripple_hash(seed + fj * 3.141);

			float sub_speed = RIPPLE_SPEED * (0.80 + hj * 0.34);
			float sub_freq = RIPPLE_FREQ * (0.86 + hk * 0.28);
			float sub_delay = fj * (0.08 + hm * 0.07);
			float sub_gain = (0.72 + hm * 0.38) / sqrt(float(ring_count));

			float age_sub = age - sub_delay;
			if (age_sub <= 0.0) {
				continue;
			}

			float onset = tap_onset * (1.0 - exp(-age_sub * age_sub / (RIPPLE_ONSET * RIPPLE_ONSET * 0.85)));
			float front = age_sub * sub_speed;
			float wave_loc = dist - front;
			float sigma = 0.042 + hk * 0.030;
			float sigma2 = sigma * sigma;
			float front_soft = 1.0 - smoothstep(front - 0.012, front + 0.11, dist);

			float phase = wave_loc * sub_freq;
			float wave_core = sin(phase);
			float wave_env = exp(-0.5 * wave_loc * wave_loc / sigma2);
			float radial_decay = exp(-dist * RIPPLE_SPATIAL_DECAY) / sqrt(dist * 2.6 + 0.62);
			float time_decay = exp(-age_sub * RIPPLE_TIME_DECAY);

			float weight = wave_env * radial_decay * time_decay * onset * front_soft * fade_out * sub_gain;
			h += wave_core * weight;

			float d_wave = (cos(phase) * sub_freq * wave_env
					+ wave_core * (-wave_loc / sigma2) * wave_env) * weight;
			d_wave += wave_core * wave_env * front_soft * onset * fade_out * sub_gain
					* (-RIPPLE_SPATIAL_DECAY * radial_decay / sqrt(dist * 2.6 + 0.62)
					- radial_decay * 1.3 / pow(dist * 2.6 + 0.62, 1.5));
			grad += to_uv * d_wave;
		}

		float splash_onset = 1.0 - exp(-age * age * 12.0);
		float splash = exp(-dist * dist * 42.0) * exp(-age * 2.1) * splash_onset;
		h += splash * 0.95;
		grad += to_uv * splash * 0.95 * (-2.0 * 42.0 * dist);
	}

	return vec3(grad.x * RIPPLE_AMP, h * RIPPLE_AMP, grad.y * RIPPLE_AMP);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float sphere = 1.0 - smoothstep(0.97, 1.0, r);
	float inside = smoothstep(1.02, 0.88, r);

	vec3 tap = tap_ripple_surface(UV, ripple_time) * inside;
	float tap_h = tap.y;
	vec2 tap_grad = tap.xz;

	// Глубина: лёгкий сдвиг «тумана» и нормали только внутри диаметра.
	uv += tap_grad * 1.65;
	r = length(uv);
	float z = sqrt(max(0.0, 1.0 - min(r * r, 1.0)));
	vec3 n = normalize(vec3(uv, max(z, 0.001)) + vec3(tap_grad.x, tap_grad.y, tap_h * 2.4) * 0.85);

	float t = TIME * fog_speed * spin_sign;
	float seed = session_seed;
	vec2 spun = rotate(n.xy, t * 0.55 + seed);
	spun = rotate(spun, sin(TIME * 0.08 * fog_speed + seed) * tumble);
	spun += tap_grad * 2.8;
	vec2 p = spun * fog_density + n.z * 1.15 + tap_h * 3.2;
	vec2 warp = vec2(
		fbm(p * 0.55 + vec2(t * 0.12, -t * 0.07) + seed),
		fbm(p * 0.55 + vec2(4.2, 1.7) - t * 0.09 - seed * 0.31)
	);
	float n1 = fbm(p * 0.85 + warp * 1.6);
	float n2 = fbm(rotate(p, 0.9 + seed * 0.2) * 1.35 - warp * 1.1 + vec2(2.1, -1.4));

	vec3 fog = mix(fog_color_1.rgb, fog_color_2.rgb, n1);
	fog += vec3(0.35, 0.55, 1.0) * n2 * 0.35;
	float fog_a = clamp(n1 * 0.72 + n2 * 0.28, 0.0, 1.0) * smoothstep(1.0, 0.18, r);

	vec3 glass = vec3(0.16, 0.14, 0.26);
	glass = mix(glass, fog, fog_a * 0.82);
	glass += mix(fog_color_1.rgb, fog_color_2.rgb, 0.45) * (1.0 - r) * 0.22;

	float fresnel = pow(1.0 - z, 2.4) * glass_reflection;
	glass += fog_color_1.rgb * fresnel;
	glass += fog_color_2.rgb * fresnel * 0.45;

	float wander = TIME * (0.16 + light_drift * 0.22) + seed * 1.7;
	vec3 light1 = normalize(vec3(
		-0.22 + sin(wander) * 0.42,
		-0.50 + cos(wander * 0.71 + seed) * 0.30,
		0.70 + sin(wander * 0.37) * 0.08
	));
	vec3 light2 = normalize(vec3(
		0.38 + cos(wander * 0.55 + 1.3) * 0.40,
		-0.10 + sin(wander * 0.83 + seed * 0.5) * 0.32,
		0.58
	));
	float spec = pow(max(dot(n, light1), 0.0), 40.0);
	float spec2 = pow(max(dot(n, light2), 0.0), 14.0) * 0.28;
	float bloom = pow(max(dot(n, light1), 0.0), 8.0) * 0.12;
	glass += vec3(1.0) * spec * 0.68 + mix(fog_color_1.rgb, vec3(1.0), 0.55) * spec2 + fog_color_2.rgb * bloom;

	// Гребень кольца — мягкий блик с затуханием.
	float crest = smoothstep(0.012, 0.055, abs(tap_h)) * (1.0 - smoothstep(0.055, 0.12, abs(tap_h)));
	crest *= inside;
	glass += mix(fog_color_1.rgb, vec3(1.0), 0.65) * crest * 0.55;
	glass += fog_color_2.rgb * abs(tap_h) * 2.8 * inside;

	glass = mix(glass, glass * 1.22 + vec3(0.20, 0.22, 0.28), lift);

	float alpha = sphere * (opacity + fog_a * 0.12 + fresnel * 0.16 + crest * 0.08);
	float a_max = mix(0.68, 0.50, lift);
	COLOR = vec4(glass, clamp(alpha, 0.0, a_max));
}
