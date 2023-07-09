using NLog;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;

namespace Hyper
{
    internal class WindowNoCubes : GameWindow
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private CancellationTokenSource _debugCancellationTokenSource = null!;

        private Shader _objectShader = null!;

        private float _scale = 0.01f;

        private Camera _camera = null!;

        private bool _firstMove = true;

        private Vector2 _lastPos;

        private List<float> _vertices = new List<float>();

        private List<int> _indices = new List<int>();

        private int _terrainVao;

        private int _numStrips;

        private int _numVertsPerStrip;

        public WindowNoCubes(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            StartDebugThreadAsync();
        }

        public override void Close()
        {
            StopDebugThread();
            base.Close();
            LogManager.Flush();
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0f, 0f, 0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            var objectShaders = new (string, ShaderType)[]
            {
                ("Shaders/terrain_shader.vert", ShaderType.VertexShader),
                ("Shaders/terrain_shader.frag", ShaderType.FragmentShader)
            };
            _objectShader = new Shader(objectShaders);

            // load height map
            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult image;
            using (FileStream heightMap = File.OpenRead("Resources/iceland_heightmap.png"))
            {
                image = ImageResult.FromStream(heightMap, ColorComponents.RedGreenBlueAlpha);
            }

            byte[] data = image.Data;

            int width = image.Width, height = image.Height, nChannels = (int)image.Comp;
            _numStrips = height - 1;
            _numVertsPerStrip = width * 2;

            _vertices.Capacity = width * height * 3;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    byte y = data[(j + width * i) * nChannels];
                    _vertices.Add(-height / 2f + i);
                    _vertices.Add(y);
                    _vertices.Add(-width / 2f + j);
                }
            }

            _indices.Capacity = _numStrips * _numVertsPerStrip;
            for (int i = 0; i < _numStrips; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        _indices.Add(i * width + j + k * width);
                    }
                }//
            }

            _terrainVao = GL.GenVertexArray();
            GL.BindVertexArray(_terrainVao);

            int terrainVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, terrainVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Count * sizeof(float), _vertices.ToArray(), BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            int terrainEbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, terrainEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Count * sizeof(int), _indices.ToArray(), BufferUsageHint.StaticDraw);

            _camera = new Camera(Size.X / (float)Size.Y, 0.01f, 1000f);

            CursorState = CursorState.Grabbed;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _objectShader.Use();
            _objectShader.SetFloat("curv", _camera.Curve);
            _objectShader.SetFloat("anti", 1.0f);
            _objectShader.SetMatrix4("view", _camera.GetViewMatrix());
            _objectShader.SetMatrix4("projection", _camera.GetProjectionMatrix());
            _objectShader.SetMatrix4("model", Matrix4.CreateScale(_scale));
            _objectShader.SetFloat("yScale", 0.25f);
            _objectShader.SetFloat("yShift", 16f);

            GL.BindVertexArray(_terrainVao);
            for (int strip = 0; strip < _numStrips; strip++)
            {
                GL.DrawElements(BeginMode.TriangleStrip, _numVertsPerStrip, DrawElementsType.UnsignedInt, sizeof(int) * _numVertsPerStrip * strip);
            }

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            if (!IsFocused)
            {
                return;
            }

            var input = KeyboardState;

            if (input.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            if (input.IsKeyDown(Keys.D8))
            {
                _camera.Curve = 0f;
            }

            if (input.IsKeyDown(Keys.D9))
            {
                _camera.Curve = 1f;
            }

            if (input.IsKeyDown(Keys.D0))
            {
                _camera.Curve = -1f;
            }

            if (input.IsKeyDown(Keys.Down))
            {
                _camera.Curve -= 0.0001f;
            }

            if (input.IsKeyDown(Keys.Up))
            {
                _camera.Curve += 0.0001f;
            }

            if (input.IsKeyDown(Keys.Tab))
            {
                Console.WriteLine(_camera.Curve);
            }

            const float sensitivity = 0.2f;

            _camera.Move(input, (float)e.Time);

            var mouse = MouseState;

            if (_firstMove)
            {
                _lastPos = new Vector2(mouse.X, mouse.Y);
                _firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - _lastPos.X;
                var deltaY = mouse.Y - _lastPos.Y;
                _lastPos = new Vector2(mouse.X, mouse.Y);

                _camera.Yaw += deltaX * sensitivity;
                _camera.Pitch -= deltaY * sensitivity; // Reversed since y-coordinates range from bottom to top
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            _camera.Fov -= e.OffsetY;
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, Size.X, Size.Y);
            _camera.AspectRatio = Size.X / (float)Size.Y;
        }

        private async Task StartDebugThreadAsync()
        {
            _debugCancellationTokenSource = new CancellationTokenSource();
            await Task.Run(() => Command(_debugCancellationTokenSource.Token));
        }

        private void StopDebugThread()
        {
            _debugCancellationTokenSource.Cancel();
        }

        private void Command(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? command = Console.ReadLine();
                if (command == null)
                    return;

                _logger.Info($"[Command]{command}");

                try
                {
                    var args = command.Split(' ');
                    var key = args[0];
                    args = args.Skip(1).ToArray();

                    switch (key)
                    {
                        case "camera":
                            _camera.Command(args);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
