shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 fire_color : source_color = vec4(1.0, 0.5, 0.1, 1.0);
uniform float speed = 2.0;

void fragment() {
	vec2 uv = UV;
	uv.y -= TIME * speed;
	
	// Noise based on UV
	float n = fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);
	
	float mask = (1.0 - UV.y) * 2.0;
	mask *= smoothstep(0.5, 0.0, abs(UV.x - 0.5));
	
	ALBEDO = fire_color.rgb * n * 2.0;
	ALPHA = mask * n;
}
