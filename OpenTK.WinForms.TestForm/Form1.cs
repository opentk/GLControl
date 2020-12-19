using System;
using System.ComponentModel;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace OpenTK.WinForms.TestForm
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
                "This demonstrates a simple use of the new OpenTK 4.x GLControl.",
                "GLControl Test Form",
                MessageBoxButtons.OK);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            glControl.Resize += new EventHandler(glControl_Resize);
            glControl.Paint += new PaintEventHandler(glControl_Paint);

            Text =
                GL.GetString(StringName.Vendor) + " " +
                GL.GetString(StringName.Renderer) + " " +
                GL.GetString(StringName.Version);

            GL.ClearColor(Color4.MidnightBlue);
            GL.Enable(EnableCap.DepthTest);

            _timer = new Timer();
            _timer.Tick += (sender, e) =>
            {
                _angle += 0.5f;
                Render();
            };
            _timer.Interval = 50;
            _timer.Start();

            // Ensure that the viewport and projection matrix are set correctly.
            glControl_Resize(glControl, EventArgs.Empty);
        }

        protected override void OnClosing(CancelEventArgs e)
        {

            base.OnClosing(e);
        }

        void glControl_Resize(object sender, EventArgs e)
        {
            GLControl? c = sender as GLControl;

            if (c.ClientSize.Height == 0)
                c.ClientSize = new System.Drawing.Size(c.ClientSize.Width, 1);

            GL.Viewport(0, 0, c.ClientSize.Width, c.ClientSize.Height);

            float aspect_ratio = Width / (float)Height;
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);
        }

        void glControl_Paint(object sender, PaintEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            Matrix4 lookat = Matrix4.LookAt(0, 5, 5, 0, 0, 0, 0, 1, 0);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookat);

            GL.Rotate(_angle, 0.0f, 1.0f, 0.0f);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            DrawCube();

            glControl.SwapBuffers();
        }

        private void DrawCube()
        {
            GL.Begin(BeginMode.Quads);

            GL.Color4(Color4.Silver);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);

            GL.Color4(Color4.Honeydew);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);

            GL.Color4(Color4.Moccasin);
            GL.Vertex3(-1.0f, -1.0f, -1.0f);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);

            GL.Color4(Color4.IndianRed);
            GL.Vertex3(-1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);

            GL.Color4(Color4.PaleVioletRed);
            GL.Vertex3(-1.0f, 1.0f, -1.0f);
            GL.Vertex3(-1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);

            GL.Color4(Color4.ForestGreen);
            GL.Vertex3(1.0f, -1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, -1.0f);
            GL.Vertex3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(1.0f, -1.0f, 1.0f);

            GL.End();
        }
    }
}
