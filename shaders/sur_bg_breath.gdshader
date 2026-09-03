shader_type canvas_item;
// Sur backdrop: local wandering warps. Most of the photo stays intact;
// only a few soft patches swell, settle, then appear elsewhere.

uniform vec2 blob_a = vec2(0.28, 0.36);
uniform vec2 blob_b = vec2(0.72, 0.58);
uniform vec2 blob_c = vec2(0.48, 0.22);
uniform vec2 blob_d = vec2(0.55, 0.78);
uniform vec4 blob_amp = vec4(0.0);
uniform vec4 blob_radius = vec4(0.14, 0.16, 0.13, 0.15);
uniform vec4 blob_phase = vec4(0.0);
uniform float warp_strength : hint_range(0.0, 0.08) = 0.016;
uniform float swirl_strength : hint_range(0.0, 2.0) = 0.5;
uniform vec4 glow_color : source_color = vec4(1.0, 0.42, 0.12, 1.0);
uniform float highlight_scale : hint_range(0.0, 1.0) = 0.32;
uniform float aspect : hint_range(0.2, 3.0) = 0.45;

float blob_weight(vec2 uv, vec2 center, float amp, float radius, float asp) {
	if (amp < 0.001 || radius < 0.001) {
		return 0.0;
	}
	vec2 d = uv - center;
	d.x *= asp;
	float q = length(d) / max(radius, 0.001);
	return amp * exp(-q * q * 2.35);
}

vec2 blob_disp(vec2 uv, vec2 center, float amp, float radius, float phase, float asp) {
	float w = blob_weight(uv, center, amp, radius, asp);
	if (w < 0.002) {
		return vec2(0.0);
	}
	vec2 d = uv - center;
	d.x *= asp;
	float dist = length(d);
	vec2 n = dist > 0.00015 ? d / dist : vec2(1.0, 0.0);
	vec2 tangent = vec2(-n.y, n.x);
	float pulse = sin(phase + dist * 17.0);
	float swirl = cos(phase * 0.82 + dist * 13.0);
	vec2 disp = n * pulse + tangent * swirl * swirl_strength;
	disp.x /= max(asp, 0.001);
	return disp * (warp_strength * w);
}

void fragment() {
	vec2 uv0 = UV;
	float wa = blob_weight(uv0, blob_a, blob_amp.x, blob_radius.x, aspect);
	float wb = blob_weight(uv0, blob_b, blob_amp.y, blob_radius.y, aspect);
	float wc = blob_weight(uv0, blob_c, blob_amp.z, blob_radius.z, aspect);
	float wd = blob_weight(uv0, blob_d, blob_amp.w, blob_radius.w, aspect);
	vec2 uv = uv0
		+ blob_disp(uv0, blob_a, blob_amp.x, blob_radius.x, blob_phase.x, aspect)
		+ blob_disp(uv0, blob_b, blob_amp.y, blob_radius.y, blob_phase.y, aspect)
		+ blob_disp(uv0, blob_c, blob_amp.z, blob_radius.z, blob_phase.z, aspect)
		+ blob_disp(uv0, blob_d, blob_amp.w, blob_radius.w, blob_phase.w, aspect);

	vec4 tex = texture(TEXTURE, uv);
	vec3 rgb = tex.rgb;
	float cover = clamp(wa + wb + wc + wd, 0.0, 1.0);
	if (cover > 0.01 && highlight_scale > 0.001) {
		float luma = max(rgb.r, max(rgb.g, rgb.b));
		float hot = smoothstep(0.30, 0.70, luma);
		rgb = mix(rgb, rgb + glow_color.rgb * hot * highlight_scale * 0.22, cover * 0.4);
	}
	COLOR = vec4(rgb, tex.a);
}
