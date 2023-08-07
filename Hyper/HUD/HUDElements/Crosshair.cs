﻿using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Hyper.HUD.HUDElements;

internal class Crosshair : IHudElement
{
    public bool Visible { get; set; } = true;
    
    private static Vector2 Size = new (0.02f);
    
    private readonly int _vao;

    public Crosshair()
    {
        _vao = GetVao();
    }

    public void Render(Shader shader)
    {
        var model = Matrix4.CreateTranslation(0, 0, 0.0f);
        model *= Matrix4.CreateScale(Size.X, Size.Y, 1.0f);

        shader.SetMatrix4("model", model);
        shader.SetBool("useTexture", false);
        shader.SetVector4("color", new Vector4(1, 0, 0, 1));
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, 4);
    }

    private static int GetVao()
    {
        var vertices = GetVertices();
        
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * Marshal.SizeOf<HUDVertex>(), vertices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<HUDVertex>(), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<HUDVertex>(), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);

        return vao;
    }

    private static HUDVertex[] GetVertices()
    {
        HUDVertexBuilder builder = new();
        Vector3 color = new Vector3(1, 0, 0);
        return new[]
        {
            builder.SetPosition(-1, 0).Build(),
            builder.SetPosition(1, 0).Build(),
            builder.SetPosition(0, 1).Build(),
            builder.SetPosition(0, -1).Build()
        };
    }
}
