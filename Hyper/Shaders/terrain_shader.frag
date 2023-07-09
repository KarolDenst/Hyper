#version 330

out vec4 outputColor;

in float height;

void main() {
	outputColor = vec4(height, height, height, 1);
}