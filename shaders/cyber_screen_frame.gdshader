shader_type canvas_item;
render_mode unshaded, blend_add;

// Neon cyber frame around a rounded rect.
// Ported from MagicCrystalClash (CyberFrameBorder / CyberScreenFrame).

uniform float border_width_px : hint_range(1.0, 12.0) = 3.5;
uniform float glow_width_px : hint_range(0.0, 24.0) = 7.0;
uniform float corner_radius_px : hint_range(0.0, 64.0) = 30.0;
uniform float hue_speed : hint_range(0.02, 0.6) = 0.14;
uniform float saturation : hint_range(0.5, 1.0) = 0.92;
uniform float glow_strength : hint_range(0.0, 2.0) = 0.85;
uniform float motion_mul : hint_range(0.0, 1.0) = 1.0;
uniform float hue_offset : hint_range(0.0, 1.0) = 0.0;
uniform vec2 control_pixel_size = vec2(720.0, 1600.0);

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

// Signed distance to rounded rect (centered). Negative = inside fill.
float rounded_rect_sdf(vec2 p_center, vec2 half_size, float radius) {
	vec2 q = abs(p_center) - half_size + radius;
	return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

void fragment() {
	vec2 px_size = max(control_pixel_size, vec2(1.0));
	vec2 p = UV * px_size;
	vec2 center = px_size * 0.5;
	float r = clamp(corner_radius_px, 0.0, min(px_size.x, px_size.y) * 0.5 - 1.0);
	vec2 half_extents = px_size * 0.5;

	float sdf = rounded_rect_sdf(p - center, half_extents, r);
	if (sdf > 0.5) {
		discard;
	}

	float dist = -sdf;
	float frame_total = border_width_px + glow_width_px;
	if (dist > frame_total) {
		discard;
	}

	float angle = atan(p.y - center.y, p.x - center.x);
	float hue = fract(angle / TAU + 0.5 + hue_offset + TIME * hue_speed * motion_mul);
	vec3 core = hsv2rgb(vec3(hue, saturation, 1.05));
	vec3 hue2 = hsv2rgb(vec3(fract(hue + 0.17), saturation * 0.9, 1.0));

	float in_border = 1.0 - smoothstep(border_width_px - 0.75, border_width_px + 0.25, dist);
	float in_glow = 1.0 - smoothstep(border_width_px, frame_total, dist);
	float pulse = 0.88 + 0.12 * sin(TIME * 1.6 * motion_mul + angle * 2.0 + hue_offset * TAU);

	vec3 col = mix(hue2, core, in_border);
	col *= pulse;
	float alpha = max(in_border, in_glow * glow_strength * 0.55);
	float glow_alpha = in_glow * (1.0 - in_border) * glow_strength;
	alpha = clamp(max(alpha, glow_alpha), 0.0, 1.0);

	COLOR = vec4(col, alpha);
}
