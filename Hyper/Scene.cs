﻿namespace Hyper;

internal class Scene : IInputSubscriber
{
    private readonly List<Chunk> _chunks;

    private readonly List<LightSource> _lightSources;

    public readonly List<Projectile> Projectiles;

    public readonly List<Character> Characters;

    public readonly Player Player;

    public Camera Camera => Player.Camera;

    public HudManager Hud { get; private set; }

    private readonly float _scale = 0.1f;

    private readonly Shader _objectShader;

    private readonly Shader _lightSourceShader;

    private readonly Shader _characterShader;

    private readonly ScalarFieldGenerator _scalarFieldGenerator;

    private readonly int _chunksPerSide = 2;

    private readonly SimulationManager<NarrowPhaseCallbacks, PoseIntegratorCallbacks> _simulationManager;

    private readonly CollidableProperty<SimulationProperties> _properties;

    private readonly SimpleCar _simpleCar;

    private readonly Vector3 _carInitialPosition;

    public Scene(float aspectRatio)
    {
        _scalarFieldGenerator = new ScalarFieldGenerator(1);
        ChunkFactory chunkFactory = new ChunkFactory(_scalarFieldGenerator);

        _chunks = GetChunks(chunkFactory);
        _lightSources = GetLightSources(_chunksPerSide);
        _projectiles = new List<Projectile>();
        _carInitialPosition = new Vector3(-5, _scalarFieldGenerator.AvgElevation + 10, 10);

        Camera = GetCamera(aspectRatio);
        Hud = new HudManager(aspectRatio);

        _objectShader = GetObjectShader();
        _lightSourceShader = GetLightSourceShader();
        _characterShader = GetModelShader();

        RegisterCallbacks();

        _properties = new CollidableProperty<SimulationProperties>();

        _simulationManager = new SimulationManager<NarrowPhaseCallbacks, PoseIntegratorCallbacks>(new NarrowPhaseCallbacks() { Properties = _properties }, new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, -10, 0)), new SolveDescription(6, 1));

        _simpleCar = SimpleCar.CreateStandardCar(_simulationManager.Simulation, _simulationManager.BufferPool, _properties, TypingUtils.ToNumericsVector(_carInitialPosition));

        foreach (var chunk in _chunks)
        {
            var mesh = MeshHelper.CreateMeshFromChunk(chunk, _scale, _simulationManager.BufferPool);
            var position = chunk.Position;
            _simulationManager.Simulation.Statics.Add(new StaticDescription(
                new System.Numerics.Vector3(position.X, position.Y, position.Z),
                QuaternionEx.Identity,
                _simulationManager.Simulation.Shapes.Add(mesh)));
        }
    }

    public void Render()
    {
        SetUpObjectShaderParams();

        foreach (var chunk in _chunks)
        {
            chunk.Render(_objectShader, _scale, Camera.ReferencePointPosition);
        }

        foreach (var projectile in _projectiles)
        {
            projectile.Mesh.Render(_objectShader, _scale, Camera.ReferencePointPosition);
        }

        SetUpLightingShaderParams();

        foreach (var light in _lightSources)
        {
            light.Render(_lightSourceShader, _scale, Camera.ReferencePointPosition);
        }

        SetUpCharacterShaderParams();

        foreach (var character in Characters)
        {
            character.Render(_characterShader, Scale, Camera.ReferencePointPosition);
        }

        Player.Render(_characterShader, Scale, Camera.ReferencePointPosition);


        _simpleCar.Mesh.Render(_objectShader, _scale, Camera.ReferencePointPosition);

        Hud.Render();
    }

    public void UpdateProjectiles(float dt)
    {
        _projectiles.RemoveAll(x => x.IsDead);
        foreach (var projectile in _projectiles)
        {
            projectile.Update(_simulationManager.Simulation, dt);
        }
    }

    private void SetUpObjectShaderParams()
    {
        _objectShader.Use();
        _objectShader.SetFloat("curv", Camera.Curve);
        _objectShader.SetFloat("anti", 1.0f);
        _objectShader.SetMatrix4("view", Camera.GetViewMatrix());
        _objectShader.SetMatrix4("projection", Camera.GetProjectionMatrix());
        _objectShader.SetInt("numLights", _lightSources.Count);
        _objectShader.SetVector4("viewPos", GeomPorting.EucToCurved(Vector3.UnitY, Camera.Curve));

        _objectShader.SetVector3Array("lightColor", LightSources.Select(x => x.Color).ToArray());
        _objectShader.SetVector4Array("lightPos", LightSources.Select(x =>
            GeomPorting.EucToCurved((x.Position - Camera.ReferencePointPosition) * Scale, Camera.Curve)).ToArray());
    }

    private void SetUpLightingShaderParams()
    {
        _lightSourceShader.Use();
        _lightSourceShader.SetFloat("curv", Camera.Curve);
        _lightSourceShader.SetFloat("anti", 1.0f);
        _lightSourceShader.SetMatrix4("view", Camera.GetViewMatrix());
        _lightSourceShader.SetMatrix4("projection", Camera.GetProjectionMatrix());
    }


    private List<Chunk> GetChunks(ChunkFactory generator)
    {
        return MakeSquare(_chunksPerSide, generator);
    }

    private void SetUpCharacterShaderParams()
    {
        _characterShader.Use();
        _characterShader.SetFloat("curv", Camera.Curve);
        _characterShader.SetMatrix4("view", Camera.GetViewMatrix());
        _characterShader.SetMatrix4("projection", Camera.GetProjectionMatrix());

        _characterShader.SetInt("numLights", LightSources.Count);
        _characterShader.SetVector4("viewPos", GeomPorting.EucToCurved(Vector3.UnitY, Camera.Curve));
        _characterShader.SetVector3Array("lightColor", LightSources.Select(x => x.Color).ToArray());
        _characterShader.SetVector4Array("lightPos", LightSources.Select(x =>
            GeomPorting.EucToCurved((x.Position - Camera.ReferencePointPosition) * Scale, Camera.Curve)).ToArray());
    }

    private static List<Chunk> GetChunks(ChunkFactory generator)
    {
        var chunks = new List<Chunk>
        {
            generator.GenerateChunk(new Vector3i(0, 0, 0)),
            generator.GenerateChunk(new Vector3i(Chunk.Size - 1, 0, 0)),
            generator.GenerateChunk(new Vector3i(0, 0, Chunk.Size - 1)),
            generator.GenerateChunk(new Vector3i(Chunk.Size - 1, 0, Chunk.Size - 1))
        };
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
                    if (x % 2 == 0 && y % 2 == 0)
                        continue;

                    int offset = Chunk.Size - 1;

                    lightSources.Add(new LightSource(CubeMesh.Vertices, new Vector3(offset * x, _scalarFieldGenerator.AvgElevation + 10f, offset * y), new Vector3(1, 1, 1)));
                }
            }

            return lightSources;
        }

        private static List<Character> GetCharacters()
        {
            var models = new List<Character>
            {
                new Cowboy(new Vector3(0, 20, 0), 1f)
            };

            return models;
        }

        private Camera GetCamera(float aspectRatio)
        {
            var camera = new Camera(aspectRatio, 0.01f, 100f, _scale)
            {
                ReferencePointPosition = (5f + _scalarFieldGenerator.AvgElevation) * Vector3.UnitY
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

        private static Shader GetModelShader()
        {
            var shader = new[]
            {
            ("Animation/Shaders/model_shader.vert", ShaderType.VertexShader),
            ("Animation/Shaders/model_shader.frag", ShaderType.FragmentShader)
        };
            return new Shader(shader);
        }

        public void RegisterCallbacks()
        {
            Context context = Context.Instance;

            context.RegisterMouseButtons(new List<MouseButton> { MouseButton.Left, MouseButton.Right });
            context.RegisterKeys(new List<Keys> { Keys.Backspace, Keys.P });
            context.RegisterUpdateFrameCallback((e) => UpdateProjectiles((float)e.Time));
            context.RegisterUpdateFrameCallback((e) =>
            {
                float steeringSum = 0;
                if (context.HeldKeys[Keys.A]) steeringSum += 1;
                if (context.HeldKeys[Keys.D]) steeringSum -= 1;
                float targetSpeedFraction = context.HeldKeys[Keys.W] ? 1f : context.HeldKeys[Keys.S] ? -1f : 0;
                _simpleCar.Update(_simulationManager.Simulation, (float)e.Time, steeringSum, targetSpeedFraction, false, context.HeldKeys[Keys.Backspace]);
                _simulationManager.Simulation.Timestep((float)e.Time, _simulationManager.ThreadDispatcher);
            });

            context.RegisterMouseButtonHeldCallback(MouseButton.Left, (e) =>
            {
                var position = Camera.ReferencePointPosition;

                foreach (var chunk in _chunks)
                {
                    if (chunk.Mine(position, (float)e.Time)) return;
                }
            });

            context.RegisterMouseButtonHeldCallback(MouseButton.Right, (e) =>
            {
                var position = Camera.ReferencePointPosition;

                foreach (var chunk in _chunks)
                {
                    if (chunk.Build(position, (float)e.Time)) return;
                }
            });

            context.RegisterKeyDownCallback(Keys.P, () =>
            {
                var projectile = Projectile.CreateStandardProjectile(_simulationManager.Simulation, _properties, TypingUtils.ToNumericsVector(Camera.ReferencePointPosition), TypingUtils.ToNumericsVector(Camera.Front) * 15);
                _projectiles.Add(projectile);
            });
        }
    }
