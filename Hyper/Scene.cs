using System.Diagnostics;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Hyper.Collisions;
using Hyper.Collisions.Bepu;
using Hyper.GameEntities;
using Hyper.HUD;
using Hyper.MarchingCubes;
using Hyper.MarchingCubes.Voxels;
using Hyper.Meshes;
using Hyper.Shaders;
using Hyper.TypingUtils;
using Hyper.UserInput;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;


namespace Hyper;

internal class Scene : IInputSubscriber
{
    private readonly List<Chunk> _chunks1;

    private readonly List<Chunk> _chunks2;

    private readonly List<LightSource> _lightSources;

    private readonly List<Projectile> _projectiles;

    public Camera Camera { get; set; }

    private readonly CharacterControllers _characterControllers;

    public HudManager Hud { get; private set; }

    private readonly Shader _objectShader;

    private readonly Shader _lightSourceShader;

    private readonly Shader _characterShader;

    private readonly ScalarFieldGenerator _scalarFieldGenerator;

    private readonly int _chunksPerSide = 2;

    private readonly SimulationManager<NarrowPhaseCallbacks, PoseIntegratorCallbacks> _simulationManager;

    private readonly CollidableProperty<SimulationProperties> _properties;

    private readonly SimpleCar _simpleCar;

    private readonly Player _player;

    private readonly List<Humanoid> _bots;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly Vector3i _chunk1Center = new Vector3i(0, 0, 0);
    private readonly Vector3i _chunk2Center = new Vector3i((int)(MathF.PI / WorldProperties.Instance.Scale), 0, 0);

    private int _currentSphere = 0;

    public Scene(float aspectRatio)
    {
        _scalarFieldGenerator = new ScalarFieldGenerator(1);
        ChunkFactory chunkFactory = new ChunkFactory(_scalarFieldGenerator);

        //_chunks1 = GetChunks(chunkFactory, _chunk1Center);
        //_chunks2 = GetChunks(chunkFactory, _chunk2Center);
        (_chunks1, _chunks2) = CreateSpheres(_chunksPerSide, _chunk1Center, _chunk2Center);

        _lightSources = GetLightSources(_chunksPerSide);
        _projectiles = new List<Projectile>();

        Hud = new HudManager(aspectRatio);

        _objectShader = ShaderFactory.CreateObjectShader();
        _objectShader.SetVector3("lowerSphereCenter", new Vector3(_chunk2Center.X, _chunk2Center.Y, _chunk2Center.Z) * WorldProperties.Instance.Scale);
        _lightSourceShader = ShaderFactory.CreateLightSourceShader();
        _characterShader = ShaderFactory.CreateModelShader();

        RegisterCallbacks();

        var bufferPool = new BufferPool();

        _properties = new CollidableProperty<SimulationProperties>();

        _characterControllers = new CharacterControllers(bufferPool);

        _simulationManager = new SimulationManager<NarrowPhaseCallbacks, PoseIntegratorCallbacks>(new NarrowPhaseCallbacks(_characterControllers, _properties),
            new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, -10, 0)),
            new SolveDescription(6, 1), bufferPool);

        var characterInitialPosition = new Vector3(0, _scalarFieldGenerator.AvgElevation + 8, 0);
        _player = new Player(CreatePhysicalHumanoid(characterInitialPosition));

        int botsCount = 1;
        _bots = Enumerable.Range(0, botsCount) // initialize them however you like
            .Select(i => new Vector3(i * 4 - botsCount * 2, _scalarFieldGenerator.AvgElevation + 5, i * 4 - botsCount * 2))
            .Select(pos => new Humanoid(CreatePhysicalHumanoid(pos)))
            .ToList();

        var carInitialPosition = new Vector3(5, _scalarFieldGenerator.AvgElevation + 5, 12);
        _simpleCar = SimpleCar.CreateStandardCar(_simulationManager.Simulation, _simulationManager.BufferPool, _properties, Conversions.ToNumericsVector(carInitialPosition));

        Camera = GetCamera(aspectRatio);

        foreach (var chunk in _chunks1)
        {
            chunk.CreateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
        }
        foreach (var chunk in _chunks2)
        {
            chunk.CreateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
        }
    }

    public void Render()
    {
        ShaderFactory.SetUpObjectShaderParams(_objectShader, Camera, _lightSources, WorldProperties.Instance.Scale, _currentSphere);


        _objectShader.SetInt("sphere", 1);
        foreach (var chunk in _chunks2)
        {
            chunk.Render(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        }

        _objectShader.SetInt("sphere", 0);
        foreach (var chunk in _chunks1)
        {
            chunk.Render(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        }

        foreach (var projectile in _projectiles)
        {
            projectile.Mesh.Render(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        }

        _simpleCar.Mesh.Render(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);

        ShaderFactory.SetUpLightingShaderParams(_lightSourceShader, Camera);

        foreach (var light in _lightSources)
        {
            light.Render(_lightSourceShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        }


        ShaderFactory.SetUpCharacterShaderParams(_characterShader, Camera, _lightSources, WorldProperties.Instance.Scale);

#if BOUNDING_BOXES
        _objectShader.SetInt("sphere", _currentSphere);
        _player.PhysicalCharacter.RenderBoundingBox(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        _objectShader.SetInt("sphere", 0);
#endif
        _player.Render(_characterShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, Camera.FirstPerson, WorldProperties.Instance.Curve);

        foreach (var bot in _bots)
        {
            bot.Render(_characterShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
#if BOUNDING_BOXES
            bot.PhysicalCharacter.RenderBoundingBox(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
#endif
        }

        Hud.Render();
    }

    public void UpdateProjectiles(float dt)
    {
        _projectiles.RemoveAll(x => x.IsDead);
        foreach (var projectile in _projectiles)
        {
            projectile.Update(_simulationManager.Simulation, dt, _simulationManager.BufferPool);
        }
    }

    private List<Chunk> GetChunks(ChunkFactory generator, Vector3i squareCenter)
    {
        return MakeSquare(_chunksPerSide, generator, squareCenter);
    }

    private static List<Chunk> MakeSquare(int chunksPerSide, ChunkFactory generator, Vector3i squareCenter)
    {
        if (chunksPerSide % 2 != 0)
            throw new ArgumentException("# of chunks/side must be even");

        List<Chunk> chunks = new List<Chunk>();
        for (int x = -chunksPerSide / 2; x < chunksPerSide / 2; x++)
        {
            for (int y = -chunksPerSide / 2; y < chunksPerSide / 2; y++)
            {
                int offset = Chunk.Size - 1;

                chunks.Add(generator.GenerateChunk(new Vector3i(offset * x, 0, offset * y) + squareCenter, squareCenter));
            }
        }

        return chunks;
    }

    private (List<Chunk>, List<Chunk>) CreateSpheres(int chunksPerSide, Vector3i sphere1Center, Vector3i sphere2Center)
    {
        if (chunksPerSide % 2 != 0)
            throw new ArgumentException("# of chunks/side must be even");

        List<Chunk> sphere1 = new List<Chunk>();
        List<Chunk> sphere2 = new List<Chunk>();
        for (int x = -chunksPerSide / 2; x < chunksPerSide / 2; x++)
        {
            for (int y = -chunksPerSide / 2; y < chunksPerSide / 2; y++)
            {
                int offset = Chunk.Size - 1;

                var sf1Position = new Vector3i(offset * x, 0, offset * y) + sphere1Center;
                var sf2Position = new Vector3i(offset * x, 0, offset * y) + sphere2Center;

                var scalarField1 = _scalarFieldGenerator.Generate(Chunk.Size, sf1Position);
                var scalarField2 = _scalarFieldGenerator.Generate(Chunk.Size, sf2Position);

                var (averagedSf1, averagedSf2) = AverageScalarFields(scalarField1, sf1Position, sphere1Center, scalarField2, sf2Position, sphere2Center);

                var meshGenerator1 = new MeshGenerator(averagedSf1);
                Vertex[] data1 = meshGenerator1.GetSphericalMesh(sf1Position - new Vector3i(0, (int)_scalarFieldGenerator.AvgElevation, 0), sphere1Center);
                sphere1.Add(new Chunk(data1, sf1Position, averagedSf1, sphere1Center));
                var meshGenerator2 = new MeshGenerator(averagedSf2);
                Vertex[] data2 = meshGenerator2.GetSphericalMesh(sf2Position - new Vector3i(0, (int)_scalarFieldGenerator.AvgElevation, 0), sphere2Center);
                sphere2.Add(new Chunk(data2, sf2Position, averagedSf2, sphere2Center));
            }
        }

        return (sphere1, sphere2);
    }

    private (Voxel[,,], Voxel[,,]) AverageScalarFields(Voxel[,,] sf1, Vector3i sf1Position, Vector3i sphere1Center, Voxel[,,] sf2, Vector3i sf2Position, Vector3i sphere2Center)
    {
        Voxel[,,] averagedSf1 = new Voxel[sf1.GetLength(0), sf2.GetLength(1), sf2.GetLength(2)];
        Voxel[,,] averagedSf2 = new Voxel[sf1.GetLength(0), sf2.GetLength(1), sf2.GetLength(2)];
        float radius = MathF.PI / 2 / WorldProperties.Instance.Scale;
        float beta = .9f;
        float a = .5f;
        float b = beta * radius * a;

        for (int x = 0; x < sf1.GetLength(0); x++)
        {
            for (int y = 0; y < sf1.GetLength(1); y++)
            {
                for (int z = 0; z < sf1.GetLength(2); z++)
                {
                    Vector3i p = new Vector3i(x, y, z) + sf1Position - sphere1Center;
                    if (p.X <= 0 || p.Y <= 0 || p.Z <= 0)
                    {
                        averagedSf1[x, y, z].Value = sf1[x, y, z].Value;
                        averagedSf2[x, y, z].Value = sf2[x, y, z].Value;
                        continue;
                    }
                    averagedSf1[x, y, z].Value = sf1[x, y, z].Value * S2(p.EuclideanLength, a, b) + GetRimValue(p, radius, sf1, sf2) * S1(p.EuclideanLength, a, b);
                    averagedSf2[x, y, z].Value = sf2[x, y, z].Value * S2(p.EuclideanLength, a, b) + GetRimValue(p, radius, sf1, sf2) * S1(p.EuclideanLength, a, b);
                }
            }
        }

        return Smoothen(averagedSf1, averagedSf2, sf1Position, sphere1Center);
    }

    private (Voxel[,,], Voxel[,,]) Smoothen(Voxel[,,] sf1, Voxel[,,] sf2, Vector3i sf1Position, Vector3i sphere1Center)
    {
        Voxel[,,] averagedSf1 = new Voxel[sf1.GetLength(0), sf2.GetLength(1), sf2.GetLength(2)];
        Voxel[,,] averagedSf2 = new Voxel[sf1.GetLength(0), sf2.GetLength(1), sf2.GetLength(2)];
        for (int x = 0; x < sf1.GetLength(0); x++)
        {
            for (int y = 0; y < sf1.GetLength(1); y++)
            {
                for (int z = 0; z < sf1.GetLength(2); z++)
                {
                    Vector3i p = new Vector3i(x, y, z) + sf1Position - sphere1Center;
                    averagedSf1[x, y, z].Value = sf1[x, y, z].Value;
                    averagedSf2[x, y, z].Value = sf2[x, y, z].Value;
                    int includedValues = 1;
                    if (p.X <= 0 || p.Y <= 0 || p.Z <= 0)
                    {
                        continue;
                    }
                    if (x + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x + 1, y, z].Value;
                        averagedSf2[x, y, z].Value += sf1[x + 1, y, z].Value;
                        includedValues++;
                    }
                    if (y + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x, y + 1, z].Value;
                        averagedSf2[x, y, z].Value += sf1[x, y + 1, z].Value;
                        includedValues++;
                    }
                    if (z + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x, y, z + 1].Value;
                        averagedSf2[x, y, z].Value += sf1[x, y, z + 1].Value;
                        includedValues++;
                    }
                    if (x + 1 < Chunk.Size && y + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x + 1, y + 1, z].Value;
                        averagedSf2[x, y, z].Value += sf1[x + 1, y + 1, z].Value;
                        includedValues++;
                    }

                    if (x + 1 < Chunk.Size && z + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x + 1, y, z + 1].Value;
                        averagedSf2[x, y, z].Value += sf1[x + 1, y, z + 1].Value;
                        includedValues++;
                    }
                    if (y + 1 < Chunk.Size && z + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x, y + 1, z + 1].Value;
                        averagedSf2[x, y, z].Value += sf1[x, y + 1, z + 1].Value;
                        includedValues++;
                    }
                    if (x + 1 < Chunk.Size && y + 1 < Chunk.Size && z + 1 < Chunk.Size)
                    {
                        averagedSf1[x, y, z].Value += sf1[x + 1, y + 1, z + 1].Value;
                        averagedSf2[x, y, z].Value += sf1[x + 1, y + 1, z + 1].Value;
                        includedValues++;
                    }
                    averagedSf2[x, y, z].Value /= includedValues;
                    averagedSf1[x, y, z].Value /= includedValues;
                }
            }
        }

        return (averagedSf1, averagedSf2);
    }

    private static float GetRimValue(Vector3i p, float R, Voxel[,,] sf1, Voxel[,,] sf2)
    {
        float r = p.EuclideanLength;
        Vector3 p1 = (R / r) * new Vector3(p);
        int x = (int)(p1.X);
        int y = (int)(p1.Y);
        int z = (int)(p1.Z);
        if (x < 0)
            x += Chunk.Size;
        if (y < 0)
            y += Chunk.Size;
        if (z < 0)
            z += Chunk.Size;
        int includedValues = 1;
        float rimValue = (sf1[x, y, z].Value + sf2[x, y, z].Value) / 2f;

        return rimValue / includedValues;
    }

    private static float S1(float x, float a, float b, float m = 0.5f)
        => m * (MathF.Tanh(a * x - b) + 1);

    private static float S2(float x, float a, float b, float m = 0.5f)
        => m * (-MathF.Tanh(a * x - b) + 1);

    private List<LightSource> GetLightSources(int chunksPerSide)
    {
        if (chunksPerSide % 2 != 0)
            throw new ArgumentException("# of chunks/side must be even");

        List<LightSource> lightSources = new List<LightSource>();
        for (int x = -chunksPerSide / 2; x < chunksPerSide / 2; x++)
        {
            for (int y = -chunksPerSide / 2; y < chunksPerSide / 2; y++)
            {
                if (x != 0 || y != 0)
                    continue;

                int offset = Chunk.Size - 1;

                lightSources.Add(new LightSource(CubeMesh.Vertices, new Vector3(offset * x, _scalarFieldGenerator.AvgElevation + 10f, offset * y), new Vector3(1, 1, 1)));
            }
        }

        return lightSources;
    }

    private Camera GetCamera(float aspectRatio)
    {
        var camera = new Camera(aspectRatio, 0.01f, 100f, WorldProperties.Instance.Scale)
        {
            ReferencePointPosition = (5f + _scalarFieldGenerator.AvgElevation) * Vector3.UnitY
        };

        return camera;
    }

    private Vector3 ToFloatVector(Vector3i v)
        => new(v.X, v.Y, v.Z);

    public void RegisterCallbacks()
    {
        Context context = Context.Instance;

        context.RegisterMouseButtons(new List<MouseButton> { MouseButton.Left, MouseButton.Right });
        context.RegisterKeys(new List<Keys> { Keys.Backspace, Keys.P, Keys.LeftShift, Keys.Space, Keys.W, Keys.S, Keys.A, Keys.D });
        context.RegisterUpdateFrameCallback((e) => UpdateProjectiles((float)e.Time));
        context.RegisterUpdateFrameCallback((e) =>
        {
            // TODO commented out until we have context switching
            /*float steeringSum = 0;
            if (context.HeldKeys[Keys.A]) steeringSum += 1;
            if (context.HeldKeys[Keys.D]) steeringSum -= 1;
            float targetSpeedFraction = context.HeldKeys[Keys.W] ? 1f : context.HeldKeys[Keys.S] ? -1f : 0;*/
            _simpleCar.Update(_simulationManager.Simulation, (float)e.Time, 0/*steeringSum*/, 0f/*targetSpeedFraction*/, false, false /*context.HeldKeys[Keys.Backspace]*/);

            Vector2 movementDirection = default;
            if (context.HeldKeys[Keys.W])
            {
                movementDirection = new Vector2(0, 1);
            }
            if (context.HeldKeys[Keys.S])
            {
                movementDirection += new Vector2(0, -1);
            }
            if (context.HeldKeys[Keys.A])
            {
                movementDirection += new Vector2(-1, 0);
            }
            if (context.HeldKeys[Keys.D])
            {
                movementDirection += new Vector2(1, 0);
            }
            _player.UpdateCharacterGoals(_simulationManager.Simulation, Camera.Front, (float)e.Time,
                tryJump: context.HeldKeys[Keys.Space], sprint: context.HeldKeys[Keys.LeftShift],
                movementDirection);

            if (_currentSphere == 0)
            {
                Vector3 playerPos = Conversions.ToOpenTKVector(_player.PhysicalCharacter.Pose.Position);
                Vector3 normalizedPlayerPos = new Vector3(playerPos.X, 0, playerPos.Z);
                System.Numerics.Quaternion orientation = _player.PhysicalCharacter.Pose.Orientation;
                if (Vector3.Distance(normalizedPlayerPos, _chunk1Center) > ((MathF.PI / 2) / WorldProperties.Instance.Scale))
                {
                    Vector3 normalizedAfterTeleport = ToFloatVector(_chunk2Center) + 0.8f * (normalizedPlayerPos - ToFloatVector(_chunk1Center));
                    Vector3 realAfterTeleport = new Vector3(normalizedAfterTeleport.X, playerPos.Y * 1f, normalizedAfterTeleport.Z); // TODO adjust the height for the target position
                    System.Numerics.Quaternion flip = QuaternionEx.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, MathF.PI);
                    _player.PhysicalCharacter.ForcePoseChange(_simulationManager.Simulation, new RigidPose(Conversions.ToNumericsVector(realAfterTeleport), orientation * flip));
                    _currentSphere = 1;
                    _objectShader.SetInt("characterSphere", 1);
                    Camera.FlipFront();
                    Camera.Sphere = 1;
                    _player.PhysicalCharacter.BoundingBoxMesh.SphereCenter = _chunk2Center;
                }
            }
            else
            {
                Vector3 playerPos = Conversions.ToOpenTKVector(_player.PhysicalCharacter.Pose.Position);
                Vector3 normalizedPlayerPos = new Vector3(playerPos.X, 0, playerPos.Z);
                System.Numerics.Quaternion orientation = _player.PhysicalCharacter.Pose.Orientation;
                if (Vector3.Distance(normalizedPlayerPos, _chunk2Center) > ((MathF.PI / 2) / WorldProperties.Instance.Scale))
                {
                    Vector3 normalizedAfterTeleport = ToFloatVector(_chunk1Center) + 0.8f * (normalizedPlayerPos - ToFloatVector(_chunk2Center));
                    Vector3 realAfterTeleport = new Vector3(normalizedAfterTeleport.X, playerPos.Y * 1f, normalizedAfterTeleport.Z); // TODO adjust the height for the target position
                    System.Numerics.Quaternion flip = QuaternionEx.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, MathF.PI);
                    _player.PhysicalCharacter.ForcePoseChange(_simulationManager.Simulation, new RigidPose(Conversions.ToNumericsVector(realAfterTeleport), orientation * flip));
                    _currentSphere = 0;
                    _objectShader.SetInt("characterSphere", 0);
                    Camera.FlipFront();
                    Camera.Sphere = 0;
                    _player.PhysicalCharacter.BoundingBoxMesh.SphereCenter = _chunk1Center;
                }
            }


            foreach (var bot in _bots)
            {
                float tMs = _stopwatch.ElapsedMilliseconds;
                Vector3 movement = new Vector3(MathF.Sin(tMs / 3000), 0, MathF.Cos(tMs / 3000)); // these poor fellas are cursed with eternal running in circles
                bot.UpdateCharacterGoals(_simulationManager.Simulation, movement, (float)e.Time,
                    tryJump: false, sprint: false, movementDirection: Vector2.UnitY);
            }

            Camera.UpdateWithCharacterSpherical(_player, _currentSphere == 0 ? _chunk1Center : _chunk2Center);

            _simulationManager.Simulation.Timestep((float)e.Time, _simulationManager.ThreadDispatcher);
        });

        context.RegisterMouseButtonHeldCallback(MouseButton.Left, (e) =>
        {
            foreach (var chunk in _chunks1)
            {
                if (chunk.Mine(Conversions.ToOpenTKVector(_player.GetCharacterRay(Camera.Front, 1)), 3, (float)e.Time))
                {
                    chunk.UpdateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
                    return;
                }
            }
            foreach (var chunk in _chunks2)
            {
                if (chunk.Mine(Conversions.ToOpenTKVector(_player.GetCharacterRay(Camera.Front, 1)), 3, (float)e.Time))
                {
                    chunk.UpdateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
                    return;
                }
            }
        });

        context.RegisterMouseButtonHeldCallback(MouseButton.Right, (e) =>
        {
            foreach (var chunk in _chunks1)
            {
                if (chunk.Build(Conversions.ToOpenTKVector(_player.GetCharacterRay(Camera.Front, 3)), 3, (float)e.Time))
                {
                    chunk.UpdateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
                    return;
                }
            }
            foreach (var chunk in _chunks2)
            {
                if (chunk.Build(Conversions.ToOpenTKVector(_player.GetCharacterRay(Camera.Front, 3)), 3, (float)e.Time))
                {
                    chunk.UpdateCollisionSurface(_simulationManager.Simulation, _simulationManager.BufferPool);
                    return;
                }
            }
        });

        context.RegisterKeyDownCallback(Keys.P, () =>
        {
            var q = MathUtils.Helpers.CreateQuaternionFromTwoVectors(System.Numerics.Vector3.UnitX, Conversions.ToNumericsVector(Camera.Front));
            var projectile = Projectile.CreateStandardProjectile(_simulationManager.Simulation,
                _properties,
                new RigidPose(_player.GetCharacterRay(Camera.Front, 2), q),
                Conversions.ToNumericsVector(Camera.Front) * 15,
                new ProjectileMesh(2, 0.5f, 0.5f), lifeTime: 5); // let's throw some refrigerators
            _projectiles.Add(projectile);
        });
    }

    private PhysicalCharacter CreatePhysicalHumanoid(Vector3 initialPosition)
        => new(_characterControllers, _properties, Conversions.ToNumericsVector(initialPosition),
            minimumSpeculativeMargin: 0.1f, mass: 1, maximumHorizontalForce: 20, maximumVerticalGlueForce: 100, jumpVelocity: 6, speed: 4,
            maximumSlope: MathF.PI * 0.4f);
}
