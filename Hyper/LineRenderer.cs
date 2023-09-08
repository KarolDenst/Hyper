using System.Runtime.InteropServices;
using Hyper.Meshes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Hyper;
internal class LineRenderer
{

    private int _vaoId;

    private int _vboId;

    private Vertex[] _vertices;

    Shader _shader;

    float _scale;

    public LineRenderer(Shader shader, float scale)
    {
        _vertices = new Vertex[2];
        //_vertices[0] = new Vertex { X = 0, Y = 50, Z = 0 };
        //_vertices[1] = new Vertex { X = 1000, Y = 50, Z = 0 };
        _shader = shader;
        _scale = scale;
        CreateVertexArrayObject();
    }

    public void Render(Vector3 start, Vector3 end, Vector3 color, Vector3 cameraPosition)
    {
        var model = Matrix4.CreateTranslation((-cameraPosition) * _scale);
        var scale = Matrix4.CreateScale(_scale);

        _shader.SetMatrix4("model", scale * model);
        _shader.SetVector3("color", color);

        GL.BindVertexArray(_vaoId);
        _vertices[0] = new Vertex { X = start.X, Y = start.Y, Z = start.Z };
        _vertices[1] = new Vertex { X = end.X, Y = end.Y, Z = end.Z };
        /*_vertices[0] = new Vertex { X = 0, Y = 23, Z = 13 };
        _vertices[1] = new Vertex { X = 0, Y = 23, Z = -7 };*/
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboId);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * Marshal.SizeOf<Vertex>(), _vertices, BufferUsageHint.StaticDraw);

        GL.DrawArrays(PrimitiveType.Lines, 0, 2);
    }

    private void CreateVertexArrayObject()
    {
        int vaoId = GL.GenVertexArray();
        GL.BindVertexArray(vaoId);

        int vboId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * Marshal.SizeOf<Vertex>(), _vertices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<Vertex>(), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<Vertex>(), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<Vertex>(), 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        _vaoId = vaoId;
        _vboId = vboId;

        GL.BindVertexArray(0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
}
