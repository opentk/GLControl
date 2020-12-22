using System;
using System.ComponentModel;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace OpenTK.WinForms.MultiControlTest
{
	public partial class Form1 : Form
	{
        private Timer _timer = null!;
        private float _angle = 0.0f;

        public Form1()
		{
			InitializeComponent();
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

            // Update each control if it's resized or needs to be painted.
            glControl1.Resize += (sender, e) => SetupProjection((GLControl)sender);
            glControl1.Paint += (sender, e) => RenderControl1((GLControl)sender);

            glControl2.Resize += (sender, e) => SetupProjection((GLControl)sender);
            glControl2.Paint += (sender, e) => RenderControl2((GLControl)sender);

            glControl3.Resize += (sender, e) => SetupProjection((GLControl)sender);
            glControl3.Paint += (sender, e) => RenderControl3((GLControl)sender);

            glControl4.Resize += (sender, e) => SetupProjection((GLControl)sender);
            glControl4.Paint += (sender, e) => RenderControl4((GLControl)sender);

            // Redraw each control every 1/20 of a second.
            _timer = new Timer();
            _timer.Tick += (sender, e) =>
            {
                _angle += 0.5f;
                RenderControl1(glControl1);
                RenderControl2(glControl2);
                RenderControl3(glControl3);
                RenderControl4(glControl4);
            };
            _timer.Interval = 50;   // 1000 ms per sec / 50 ms per frame = 20 FPS
            _timer.Start();

            // Ensure that the viewport and projection matrix are initially
            // set correctly for each control.
            SetupProjection(glControl1);
            SetupProjection(glControl2);
            SetupProjection(glControl3);
            SetupProjection(glControl4);
        }

        // All four example GLControls in this demo use the same projection,
        // but they don't have to.
        private void SetupProjection(GLControl control)
        {
            control.MakeCurrent();

            if (control.ClientSize.Height == 0)
                control.ClientSize = new System.Drawing.Size(control.ClientSize.Width, 1);

            GL.Viewport(0, 0, control.ClientSize.Width, control.ClientSize.Height);

            float aspect_ratio = Math.Max(control.ClientSize.Width, 1) / (float)Math.Max(control.ClientSize.Height, 1);
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);
        }

        // All four GLControls will render the same cube, but in different colors
        // and at different angles, to show that they are rendering independently.
        private void RenderControl1(GLControl control)
        {
            RenderCube(control, 1.0f, Color4.Black, Color4.Aqua);
        }

        private void RenderControl2(GLControl control)
        {
            RenderCube(control, 1.7f, Color4.MidnightBlue, Color4.Pink);
        }

        private void RenderControl3(GLControl control)
        {
            RenderCube(control, -1.0f, Color4.DarkGray, Color4.Orange);
        }

        private void RenderControl4(GLControl control)
        {
            RenderCube(control, -1.7f, Color4.DarkViolet, Color4.LightGreen);
        }

        private void RenderCube(GLControl control, float extraRotation, Color4 backgroundColor, Color4 cubeColor)
        {
            control.MakeCurrent();

            Matrix4 lookat = Matrix4.LookAt(0, 5, 5, 0, 0, 0, 0, 1, 0);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookat);

            GL.Rotate(_angle * extraRotation, 0.0f, 1.0f, 0.0f);

            GL.ClearColor(backgroundColor);
            GL.Enable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Begin(BeginMode.Quads);

            GL.Color4(cubeColor);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);

            GL.Color4(MixColors(cubeColor, Color4.White, 64));
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);

            GL.Color4(MixColors(cubeColor, Color4.White, 128));
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);

            GL.Color4(MixColors(cubeColor, Color4.White, 192));
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);

            GL.Color4(MixColors(cubeColor, Color4.Black, 64));
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);

            GL.Color4(MixColors(cubeColor, Color4.Black, 128));
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);

            GL.End();

            control.SwapBuffers();
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
    }
}
