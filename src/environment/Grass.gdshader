shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_schlick_ggx;

uniform vec3 color_top : source_color = vec3(0.2, 0.6, 0.1);
uniform vec3 color_bottom : source_color = vec3(0.1, 0.3, 0.05);
uniform float wind_speed = 1.0;
uniform float wind_strength = 0.2;

void vertex() {
	// Simple wind sway
	float strength = (1.0 - UV.y) * wind_strength; // Only sway the top
	strength *= sin(TIME * wind_speed + VERTEX.x + VERTEX.z);
	VERTEX.x += strength;
	VERTEX.z += strength * 0.5;
}

void fragment() {
	ALBEDO = mix(color_bottom, color_top, 1.0 - UV.y);
	NORMAL = vec3(0, 1, 0); // Flat normal for better lighting
}
