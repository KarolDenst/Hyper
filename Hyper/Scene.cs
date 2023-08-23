using System.Diagnostics;
using BepuPhysics;
using BepuUtilities.Memory;
using Hyper.Collisions;
using Hyper.Collisions.Bepu;
using Hyper.GameEntities;
using Hyper.HUD;
using Hyper.MarchingCubes;
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

    public Scene(float aspectRatio)
    {
        _scalarFieldGenerator = new ScalarFieldGenerator(1);
        ChunkFactory chunkFactory = new ChunkFactory(_scalarFieldGenerator);

        _chunks1 = GetChunks(chunkFactory);
        _chunks2 = GetChunks(chunkFactory);

        _lightSources = GetLightSources(_chunksPerSide);
        _projectiles = new List<Projectile>(); ;

        Hud = new HudManager(aspectRatio);

        _objectShader = ShaderFactory.CreateObjectShader();
        //_objectShader.SetVector3("upperSphereCenter", new Vector3(upperSphereCenter.X, upperSphereCenter.Y, upperSphereCenter.Z) * WorldProperties.Instance.Scale);
        // _objectShader.SetVector3("lowerSphereCenter", new Vector3(lowerSphereCenter.X, lowerSphereCenter.Y, lowerSphereCenter.Z) * WorldProperties.Instance.Scale);
        //_objectShader.SetFloat("avgElevation", _scalarFieldGenerator.AvgElevation * WorldProperties.Instance.Scale);
        _lightSourceShader = ShaderFactory.CreateLightSourceShader();
        _characterShader = ShaderFactory.CreateModelShader(); ;

        RegisterCallbacks(); ;

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
        ShaderFactory.SetUpObjectShaderParams(_objectShader, Camera, _lightSources, WorldProperties.Instance.Scale);


        _objectShader.SetInt("sphere", 1);
        foreach (var chunk in _chunks2)
        {
            chunk.Render(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
        }

        _objectShader.SetInt("sphere", 0); ;
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
        _player.PhysicalCharacter.RenderBoundingBox(_objectShader, WorldProperties.Instance.Scale, Camera.ReferencePointPosition, WorldProperties.Instance.Curve);
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

    private List<Chunk> GetChunks(ChunkFactory generator)
    {
        return MakeSquare(_chunksPerSide, generator);
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
            foreach (var bot in _bots)
            {
                float tMs = _stopwatch.ElapsedMilliseconds;
                Vector3 movement = new Vector3(MathF.Sin(tMs / 3000), 0, MathF.Cos(tMs / 3000)); // these poor fellas are cursed with eternal running in circles
                bot.UpdateCharacterGoals(_simulationManager.Simulation, movement, (float)e.Time,
                    tryJump: false, sprint: false, movementDirection: Vector2.UnitY);
            }

            Camera.UpdateWithCharacter(_player);

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
