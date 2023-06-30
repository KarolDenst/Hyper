﻿#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform float curv;
uniform float anti;

out vec3 Normal;
out vec3 FragPos; // in world space coordinates

vec4 port(vec4 ePoint) {
    vec3 p = ePoint.xyz;
    float d = length(p);
    if(d < 0.0001 || curv == 0) return vec4(p, 1);
    if(curv > 0) return vec4(p / d * sin(d), cos(d));
    if(curv < 0) return vec4(p / d * sinh(d), cosh(d));
}

void main(void)
{
    vec4 eucPos = vec4(aPosition, 1);

    gl_Position = anti * port(eucPos * model) * view * projection;
    FragPos = vec3(anti * port(eucPos * model)); // TODO change for non-euc.
    Normal = aNormal;
}
