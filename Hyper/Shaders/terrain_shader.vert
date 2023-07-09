#version 330 core

layout(location = 0) in vec3 aPosition;

out float height;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform float curv;
uniform float anti;

uniform float yScale;
uniform float yShift;

vec4 getEucPos(vec3 p) {
    float y = p.y * yScale - yShift;
    return vec4(p.x, y, p.z, 1);
}

vec4 port(vec4 ePoint) {
    vec3 p = ePoint.xyz;
    float d = length(p);
    if(d < 0.0001 || curv == 0) return vec4(p, 1);
    if(curv > 0) return vec4(p / d * sin(d), cos(d));
    if(curv < 0) return vec4(p / d * sinh(d), cosh(d));
}

void main() {
    vec4 eucPos = getEucPos(aPosition);

    gl_Position = anti * port(eucPos * model) * view * projection;
    height = aPosition.y / 256;
}