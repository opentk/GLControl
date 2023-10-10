using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace OpenTK.WinForms.MultiControlTest
{
    public partial class Form1 : Form
    {
        public class GLControlContext
        {
            public GLControl GLControl;
            public int VAO;
            public int CubeShader;

            public Matrix4 projection;
            public Matrix4 view;
            public Matrix4 model;

            public GLControlContext(GLControl control)
            {
                GLControl = control;
            }
        }

        public Dictionary<GLControl, GLControlContext> Contexts = new Dictionary<GLControl, GLControlContext>();

        public int EBO;
        public int PositionBuffer;
        public int ColorBuffer;

        private Timer _timer = null!;
        private float _angle = 0.0f;

        public Form1()
        {
            InitializeComponent();

            // Setup the shared contexts. Unfortunately we can't do that directly from the winforms editor...
            glControl2.SharedContext = glControl1;
            glControl3.SharedContext = glControl1;
            glControl4.SharedContext = glControl1;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "This demonstrates how to use multiple instances of the new OpenTK 4.x GLControl.",
                "Multi GLControl Test",
                MessageBoxButtons.OK);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Contexts.Add(glControl1, new GLControlContext(glControl1));
            Contexts.Add(glControl2, new GLControlContext(glControl2));
            Contexts.Add(glControl3, new GLControlContext(glControl3));
            Contexts.Add(glControl4, new GLControlContext(glControl4));

            // Update each control if it's resized or needs to be painted.
            glControl1.Resize += (sender, e) => SetupProjection(Contexts[(GLControl)sender]);
            glControl1.Paint += (sender, e) => RenderControl1(Contexts[(GLControl)sender]);

            glControl2.Resize += (sender, e) => SetupProjection(Contexts[(GLControl)sender]);
            glControl2.Paint += (sender, e) => RenderControl2(Contexts[(GLControl)sender]);

            glControl3.Resize += (sender, e) => SetupProjection(Contexts[(GLControl)sender]);
            glControl3.Paint += (sender, e) => RenderControl3(Contexts[(GLControl)sender]);

            glControl4.Resize += (sender, e) => SetupProjection(Contexts[(GLControl)sender]);
            glControl4.Paint += (sender, e) => RenderControl4(Contexts[(GLControl)sender]);

            // Redraw each control every 1/20 of a second.
            _timer = new Timer();
            _timer.Tick += (sender, e) =>
            {
                const float DELTA_TIME = 1 / 50f;
                _angle += 180f * DELTA_TIME;
                RenderControl1(Contexts[glControl1]);
                RenderControl2(Contexts[glControl2]);
                RenderControl3(Contexts[glControl3]);
                RenderControl4(Contexts[glControl4]);
            };
            _timer.Interval = 50;   // 1000 ms per sec / 50 ms per frame = 20 FPS
            _timer.Start();

            // Load the shared data as well as the context specific data.
            LoadShared();
            LoadContextSpecific(Contexts[glControl1]);
            LoadContextSpecific(Contexts[glControl2]);
            LoadContextSpecific(Contexts[glControl3]);
            LoadContextSpecific(Contexts[glControl4]);

            // Ensure that the viewport and projection matrix are initially
            // set correctly for each control.
            SetupProjection(Contexts[glControl1]);
            SetupProjection(Contexts[glControl2]);
            SetupProjection(Contexts[glControl3]);
            SetupProjection(Contexts[glControl4]);
        }

        public void LoadShared()
        {
            glControl1.MakeCurrent();
            PositionBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, PositionBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, VertexData.Length * sizeof(float) * 3, VertexData, BufferUsageHint.StaticDraw);

            ColorBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ColorBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, ColorData.Length * sizeof(float) * 4, ColorData, BufferUsageHint.StaticDraw);

            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ArrayBuffer, IndexData.Length * sizeof(int), IndexData, BufferUsageHint.StaticDraw);
        }

        public void LoadContextSpecific(GLControlContext context)
        {
            context.GLControl.MakeCurrent();

            context.CubeShader = CompileProgram(VertexShaderSource, FragmentShaderSource);

            context.VAO = GL.GenVertexArray();
            GL.BindVertexArray(context.VAO);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);

            GL.BindBuffer(BufferTarget.ArrayBuffer, PositionBuffer);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(float) * 3, 0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, ColorBuffer);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 4, 0);
        }

        // All four example GLControls in this demo use the same projection,
        // but they don't have to.
        private void SetupProjection(GLControlContext context)
        {
            context.GLControl.MakeCurrent();

            if (context.GLControl.ClientSize.Height == 0)
                context.GLControl.ClientSize = new System.Drawing.Size(context.GLControl.ClientSize.Width, 1);

            GL.Viewport(0, 0, context.GLControl.ClientSize.Width, context.GLControl.ClientSize.Height);

            float aspect_ratio = Math.Max(context.GLControl.ClientSize.Width, 1) / (float)Math.Max(context.GLControl.ClientSize.Height, 1);
            context.projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
        }

        // All four GLControls will render the same cube, but in different colors
        // and at different angles, to show that they are rendering independently.
        private void RenderControl1(GLControlContext context)
        {
            RenderCube(context, 1.0f, Color4.Black, Color4.Aqua);
        }

        private void RenderControl2(GLControlContext context)
        {
            RenderCube(context, 1.7f, Color4.MidnightBlue, Color4.Pink);
        }

        private void RenderControl3(GLControlContext context)
        {
            RenderCube(context, -1.0f, Color4.DarkGray, Color4.Orange);
        }

        private void RenderControl4(GLControlContext context)
        {
            RenderCube(context, -1.7f, Color4.DarkViolet, Color4.LightGreen);
        }

        private void RenderCube(GLControlContext context, float extraRotation, Color4 backgroundColor, Color4 cubeColor)
        {
            context.GLControl.MakeCurrent();

            GL.ClearColor(backgroundColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.DepthTest);

            context.view = Matrix4.LookAt(0, 5, 5, 0, 0, 0, 0, 1, 0);
            context.model = Matrix4.CreateFromAxisAngle((0.0f, 1.0f, 0.0f), MathHelper.DegreesToRadians(_angle * extraRotation));

            Matrix4 mvp = context.model * context.view * context.projection;

            GL.UseProgram(context.CubeShader);
            GL.UniformMatrix4(GL.GetUniformLocation(context.CubeShader, "MVP"), true, ref mvp);

            GL.DrawElements(BeginMode.Triangles, IndexData.Length, DrawElementsType.UnsignedInt, 0);

            context.GLControl.SwapBuffers();
        }

        // Mix a given color A with another color B, in a range of 0 (100% A, 0% B)
        // to 255 (0% A, 100% B).
        private static Color4 MixColors(Color4 a, Color4 b, int amount)
        {
            int ia = 255 - amount;
            return new Color4(
                (a.R * ia + b.R * amount) / 255,
                (a.G * ia + b.G * amount) / 255,
                (a.B * ia + b.B * amount) / 255,
                255
            );
        }

        private static readonly Vector3[] VertexData = new Vector3[]
        {
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),

            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),

            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),

            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
        };

        private static readonly Color4[] ColorData = new Color4[]
        {
            Color4.Silver, Color4.Silver, Color4.Silver, Color4.Silver,
            Color4.Honeydew, Color4.Honeydew, Color4.Honeydew, Color4.Honeydew,
            Color4.Moccasin, Color4.Moccasin, Color4.Moccasin, Color4.Moccasin,
            Color4.IndianRed, Color4.IndianRed, Color4.IndianRed, Color4.IndianRed,
            Color4.PaleVioletRed, Color4.PaleVioletRed, Color4.PaleVioletRed, Color4.PaleVioletRed,
            Color4.ForestGreen, Color4.ForestGreen, Color4.ForestGreen, Color4.ForestGreen,
        };

        private static readonly int[] IndexData = new int[]
        {
             0,  1,  2,  2,  3,  0,
             4,  5,  6,  6,  7,  4,
             8,  9, 10, 10, 11,  8,
            12, 13, 14, 14, 15, 12,
            16, 17, 18, 18, 19, 16,
            20, 21, 22, 22, 23, 20,
        };

        private const string VertexShaderSource = @"#version 330 core

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec4 aColor;

out vec4 fColor;

uniform mat4 MVP;

void main()
{
    gl_Position = vec4(aPos, 1) * MVP;
    fColor = aColor;
}
";

        private const string FragmentShaderSource = @"#version 330 core

in vec4 fColor;

out vec4 oColor;

void main()
{
    oColor = fColor;
}
";

        private int CompileProgram(string vertexShader, string fragmentShader)
        {
            int program = GL.CreateProgram();

            int vert = CompileShader(ShaderType.VertexShader, vertexShader);
            int frag = CompileShader(ShaderType.FragmentShader, fragmentShader);

            GL.AttachShader(program, vert);
            GL.AttachShader(program, frag);

            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception($"Could not link program: {log}");
            }

            GL.DetachShader(program, vert);
            GL.DetachShader(program, frag);

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);

            return program;

            static int CompileShader(ShaderType type, string source)
            {
                int shader = GL.CreateShader(type);

                GL.ShaderSource(shader, source);
                GL.CompileShader(shader);

                GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
                if (status == 0)
                {
                    string log = GL.GetShaderInfoLog(shader);
                    throw new Exception($"Failed to compile {type}: {log}");
                }

                return shader;
            }
        }
    }
}
