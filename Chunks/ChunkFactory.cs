﻿using Chunks.MarchingCubes;
using Common.Meshes;
using OpenTK.Mathematics;

namespace Chunks;

public class ChunkFactory
{
    private readonly ScalarFieldGenerator _scalarFieldGenerator;

    public ChunkFactory(ScalarFieldGenerator scalarFieldGenerator)
    {
        _scalarFieldGenerator = scalarFieldGenerator;
    }

    public Chunk GenerateChunk(Vector3i position)
    {
        var scalarField = _scalarFieldGenerator.Generate(Chunk.Size, position);
        var meshGenerator = new MeshGenerator(scalarField);
        Vertex[] data = meshGenerator.GetMesh();

        return new Chunk(data, position, scalarField);
    }

    public Chunk GenerateChunkWithoutVao(Vector3i position)
    {
        var scalarField = _scalarFieldGenerator.Generate(Chunk.Size, position);
        var meshGenerator = new MeshGenerator(scalarField);
        Vertex[] data = meshGenerator.GetMesh();

        return new Chunk(data, position, scalarField, false);
    }
}
