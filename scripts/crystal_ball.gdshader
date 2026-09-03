shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform vec4 fog_color_1 : source_color = vec4(0.05, 0.92, 0.85, 1.0);
uniform vec4 fog_color_2 : source_color = vec4(0.62, 0.12, 0.95, 1.0);
uniform float fog_speed : hint_range(0.0, 5.0) = 0.55;
uniform float fog_density : hint_range(1.0, 10.0) = 3.2;
uniform float glass_reflection : hint_range(0.0, 1.0) = 0.55;
uniform float opacity : hint_range(0.0, 1.0) = 0.58;
uniform float session_seed : hint_range(0.0, 99.0) = 1.0;
uniform float spin_sign : hint_range(-1.0, 1.0) = 1.0;
uniform float tumble : hint_range(0.0, 1.0) = 0.35;
uniform float light_drift : hint_range(0.05, 0.8) = 0.32;

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

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);
	float sphere = 1.0 - smoothstep(0.97, 1.0, r);
	float z = sqrt(max(0.0, 1.0 - r * r));
	vec3 n = normalize(vec3(uv, max(z, 0.001)));

	float t = TIME * fog_speed * spin_sign;
	float seed = session_seed;
	vec2 spun = rotate(n.xy, t * 0.55 + seed);
	spun = rotate(spun, sin(TIME * 0.08 * fog_speed + seed) * tumble);
	vec2 p = spun * fog_density + n.z * 1.15;
	vec2 warp = vec2(
		fbm(p * 0.55 + vec2(t * 0.12, -t * 0.07) + seed),
		fbm(p * 0.55 + vec2(4.2, 1.7) - t * 0.09 - seed * 0.31)
	);
	float n1 = fbm(p * 0.85 + warp * 1.6);
	float n2 = fbm(rotate(p, 0.9 + seed * 0.2) * 1.35 - warp * 1.1 + vec2(2.1, -1.4));

	vec3 fog = mix(fog_color_1.rgb, fog_color_2.rgb, n1);
	fog += vec3(0.35, 0.55, 1.0) * n2 * 0.35;
	float fog_a = clamp(n1 * 0.72 + n2 * 0.28, 0.0, 1.0) * smoothstep(1.0, 0.18, r);

	vec3 glass = vec3(0.07, 0.05, 0.16);
	glass = mix(glass, fog, fog_a * 0.92);
	glass += mix(fog_color_1.rgb, fog_color_2.rgb, 0.45) * (1.0 - r) * 0.18;

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
	glass += vec3(1.0) * spec * 0.62 + mix(fog_color_1.rgb, vec3(1.0), 0.55) * spec2 + fog_color_2.rgb * bloom;

	float alpha = sphere * (opacity + fog_a * 0.18 + fresnel * 0.22);
	COLOR = vec4(glass, clamp(alpha, 0.0, 0.82));
}
