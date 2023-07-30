using Hyper.HUD;
using Hyper.MarchingCubes;
using Hyper.MathUtiils;
using Hyper.Meshes;
using Hyper.UserInput;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Hyper;

internal class Scene : IInputSubscriber
{
    public readonly List<Chunk> Chunks;

    public readonly List<LightSource> LightSources;

    public readonly List<Projectile> Projectiles;

    public readonly Camera Camera;

    public readonly HudManager Hud;

    public const float Scale = 0.03f;

    private readonly Shader _objectShader;

    private readonly Shader _lightSourceShader;

    private readonly ScalarFieldGenerator _scalarFieldGenerator;

    public Scene(float aspectRatio)
    {
        _scalarFieldGenerator = new ScalarFieldGenerator(1);
        ChunkFactory chunkFactory = new ChunkFactory(_scalarFieldGenerator);

        Chunks = GetChunks(chunkFactory);
        LightSources = GetLightSources();
        Projectiles = new List<Projectile>();
        Camera = GetCamera(aspectRatio);
        Hud = new HudManager(aspectRatio);

        _objectShader = GetObjectShader();
        _lightSourceShader = GetLightSourceShader();

        RegisterCallbacks();
    }

    public void Render()
    {
        SetUpObjectShaderParams();

        foreach (var chunk in Chunks)
        {
            chunk.Render(_objectShader, Scale);
        }

        foreach (var projectile in Projectiles)
        {
            projectile.Render(_objectShader, Scale);
        }

        SetUpLightingShaderParams();

        foreach (var light in LightSources)
        {
            light.Render(_lightSourceShader, Scale);
        }

        Hud.Render();
    }

    public void UpdateProjectiles(float time)
    {
        foreach (var projectile in Projectiles)
        {
            projectile.Update(time);
        }

        Projectiles.RemoveAll(x => x.IsDead);
    }

    private void SetUpObjectShaderParams()
    {
        _objectShader.Use();
        _objectShader.SetFloat("curv", Camera.Curve);
        _objectShader.SetFloat("anti", 1.0f);
        _objectShader.SetMatrix4("view", Camera.GetViewMatrix());
        _objectShader.SetMatrix4("projection", Camera.GetProjectionMatrix());
        _objectShader.SetInt("numLights", LightSources.Count);
        _objectShader.SetVector4("viewPos", GeomPorting.EucToCurved(Vector3.UnitY, Camera.Curve));

        for (int i = 0; i < LightSources.Count; i++)
        {
            _objectShader.SetVector3($"lightColor[{i}]", LightSources[i].Color);
            _objectShader.SetVector4($"lightPos[{i}]", GeomPorting.EucToCurved(LightSources[i].Position * Scale, Camera.Curve));
        }
    }

    private void SetUpLightingShaderParams()
    {
        _lightSourceShader.Use();
        _lightSourceShader.SetFloat("curv", Camera.Curve);
        _lightSourceShader.SetFloat("anti", 1.0f);
        _lightSourceShader.SetMatrix4("view", Camera.GetViewMatrix());
        _lightSourceShader.SetMatrix4("projection", Camera.GetProjectionMatrix());
    }

    private static List<Chunk> GetChunks(ChunkFactory generator)
    {
        var chunks = MakeSquare(2, generator);

        return chunks;
    }

    private static List<Chunk> MakeSquare(int chunksPerSide, ChunkFactory generator)
    {
        if (chunksPerSide % 2 != 0)
            throw new ArgumentException("# of chunks/side must be even");

        List<Chunk> chunks = new List<Chunk>();
        for (int x = -chunksPerSide / 2; x < chunksPerSide / 2; x++)
        {
            for (int y = -chunksPerSide / 2; y < chunksPerSide / 2; y++)
            {
                int offset = Chunk.Size - 1;

                chunks.Add(generator.GenerateChunk(new Vector3i(offset * x, 0, offset * y)));
            }
        }

        return chunks;
    }

    private List<LightSource> GetLightSources()
    {
        var lightSources = new List<LightSource> {
            new(CubeMesh.Vertices, new Vector3(10f, 7f + _scalarFieldGenerator.AvgElevation, 10f), new Vector3(1f, 1f, 1f)),
            new(CubeMesh.Vertices, new Vector3(4f, 7f + _scalarFieldGenerator.AvgElevation, 4f), new Vector3(0f, 1f, 0.5f)),
        };

        return lightSources;
    }

    private Camera GetCamera(float aspectRatio)
    {
        var camera = new Camera(aspectRatio, 0.01f, 100f, Scale)
        {
            Position = (5f + _scalarFieldGenerator.AvgElevation) * Vector3.UnitY * Scale
        };

        return camera;
    }

    private static Shader GetObjectShader()
    {
        var shaderParams = new[]
        {
            ("Shaders/lighting_shader.vert", ShaderType.VertexShader),
            ("Shaders/lighting_shader.frag", ShaderType.FragmentShader)
        };

        return new Shader(shaderParams);
    }

    private static Shader GetLightSourceShader()
    {
        var shaderParams = new[]
        {
            ("Shaders/lighting_shader.vert", ShaderType.VertexShader),
            ("Shaders/light_source_shader.frag", ShaderType.FragmentShader)
        };
        return new Shader(shaderParams);
    }

    public void RegisterCallbacks()
    {
        Context context = Context.Instance;

        context.RegisterMouseButtons(new List<MouseButton> { MouseButton.Left, MouseButton.Right, MouseButton.Middle });

        context.RegisterUpdateFrameCallback((e) => UpdateProjectiles((float)e.Time));
        context.RegisterMouseButtonHeldCallback(MouseButton.Left, (e) =>
        {
            var position = Camera.Position * (1f / Scale);

            foreach (var chunk in Chunks)
            {
                if (chunk.Mine(position, (float)e.Time)) return;
            }
        });

        context.RegisterMouseButtonHeldCallback(MouseButton.Right, (e) =>
        {
            var position = Camera.Position * (1f / Scale);

            foreach (var chunk in Chunks)
            {
                if (chunk.Build(position, (float)e.Time)) return;
            }
        });

        context.RegisterMouseButtonDownCallback(MouseButton.Middle, () =>
        {
            var projectile = new Projectile(CubeMesh.Vertices, Camera.Position * (1f / Scale), Camera.Front, 100f, 5f);
            Projectiles.Add(projectile);
        });
    }
}
