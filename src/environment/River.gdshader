shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_schlick_ggx;

uniform vec4 water_color : source_color = vec4(0.0, 0.4, 0.6, 0.7);
uniform vec4 deep_water_color : source_color = vec4(0.0, 0.2, 0.4, 1.0);
uniform float wave_speed = 0.2;
uniform float wave_frequency = 5.0;
uniform float wave_amplitude = 0.05;

void vertex() {
	// Disabled for stability on low-poly mesh
	// float wave = sin(UV.x * wave_frequency + TIME * wave_speed) * wave_amplitude;
	// wave += cos(UV.y * wave_frequency * 0.7 + TIME * wave_speed * 1.2) * wave_amplitude * 0.5;
	// VERTEX.y += wave;
}

void fragment() {
	// Fresnel effect
	float fresnel = pow(1.0 - dot(NORMAL, VIEW), 2.0);
	
	// Scrolling noise for waves
	vec2 uv1 = UV * 10.0 + vec2(TIME * wave_speed * 0.5, TIME * wave_speed * 0.2);
	vec2 uv2 = UV * 15.0 - vec2(TIME * wave_speed * 0.3, TIME * wave_speed * 0.4);
	
	float n1 = fract(sin(dot(uv1, vec2(12.9898, 78.233))) * 43758.5453);
	float n2 = fract(sin(dot(uv2, vec2(12.9898, 78.233))) * 43758.5453);
	float noise = mix(n1, n2, 0.5);
	
	vec3 color = mix(water_color.rgb, deep_water_color.rgb, 0.3);
	if (noise > 0.98) color += vec3(0.3); // Foam
	
	ALBEDO = color;
	ALPHA = water_color.a;
	METALLIC = 0.1;
	ROUGHNESS = 0.1;
	SPECULAR = 0.5;
	
	// Shine
	ALBEDO += fresnel * 0.2;
}
