shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx;

uniform vec3 grass_color : source_color = vec3(0.2, 0.4, 0.15);
uniform vec3 dirt_color : source_color = vec3(0.3, 0.2, 0.1);
uniform float height_scale = 10.0;
uniform float noise_scale = 0.05;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void vertex() {
	// No displacement for stability
}

void fragment() {
	// Triplanar-like noise for grass/dirt mix
	float h = noise(UV * 200.0);
	float patches = noise(UV * 10.0);
	
	vec3 color = mix(grass_color, dirt_color, patches * 0.4);
	color = mix(color, color * 0.9, h * 0.2); // Add some grain
	
	ALBEDO = color;
	ROUGHNESS = 0.9;
}
