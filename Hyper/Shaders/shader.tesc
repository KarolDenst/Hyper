#version 460 core

layout (vertices = 3) out;

in vec2 uvs[];
out vec2 uvsCoord[];

void main()
{
    gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
    uvsCoord[gl_InvocationID] = uvs[gl_InvocationID];

    gl_TessLevelOuter[0] = 2; // left for triangles
    gl_TessLevelOuter[1] = 2; // bot for triangles
    gl_TessLevelOuter[2] = 2; // right for triangles
        
    gl_TessLevelInner[0] = 4; // all inner sides for triangles
}