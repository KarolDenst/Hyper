﻿using Hyper.MarchingCubes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyper.Meshes
{
    internal class Chunk : Mesh
    {
        public const int Size = 16;
        public new Vector3i Position;
        private float[,,] _voxels;

        public Chunk(float[] vertices, Vector3i position, float[,,] voxels) : base(vertices, position)
        {
            _voxels = voxels;
            Position = position;
        }

        // This method returns flase if it did not mine anything. True if it did.
        public bool Mine(Vector3 location, float val)
        {
            var x = (int)location.X - Position.X;
            var y = (int)location.Y - Position.Y;
            var z = (int)location.Z - Position.Z;

            if (x < 0 || y < 0 || z < 0 || x > Size - 1 || y > Size - 1 || z > Size - 1 || _voxels[x, y, z] == 1f) return false;

            _voxels[x, y, z] += val;
            if (_voxels[x, y, z] > 1) _voxels[x, y, z] = 1;

            UpdateMesh();

            var error = GL.GetError();
            if (error != ErrorCode.NoError) Console.WriteLine(error);
            return true;
        }

        // Right now this method recreates the whole VAO. This is slow but easier to implement. Will need to be changed to just updating VBO.
        private void UpdateMesh()
        {
            var renderer = new Renderer(_voxels);
            Triangle[] triangles = renderer.GetMesh();
            float[] vertices = Generator.GetTriangleAndNormalData(triangles);

            GL.BindVertexArray(VaoId);
            GL.DeleteBuffer(VboId);
            VboId = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, VboId);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }
    }
}
