using Hyper.Meshes;
using OpenTK.Mathematics;

namespace Hyper.MarchingCubes;

internal class ChunkFactory
{
    private readonly ScalarFieldGenerator _scalarFieldGenerator;

    public ChunkFactory(ScalarFieldGenerator scalarFieldGenerator)
    {
        _scalarFieldGenerator = scalarFieldGenerator;
    }

    public Chunk GenerateChunk(Vector3i position, Vector3i sphereCenter)
    {
        var scalarField = _scalarFieldGenerator.Generate(Chunk.Size, position);
        var meshGenerator = new MeshGenerator(scalarField);
        //Vertex[] data = meshGenerator.GetMesh();
        Vertex[] data = meshGenerator.GetSphericalMesh(position - new Vector3i(0, (int)_scalarFieldGenerator.AvgElevation, 0), sphereCenter);

        return new Chunk(data, position, scalarField, sphereCenter);
    }
}
