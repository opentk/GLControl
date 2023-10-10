using System;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace OpenTK.WinForms.InputTest
{
	public partial class Form1 : Form
	{
        private INativeInput? _nativeInput;

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

            WinFormsInputRadioButton.Checked = true;

            // Make sure that when the GLControl is resized or needs to be painted,
            // we update our projection matrix or re-render its contents, respectively.
            glControl.Resize += glControl_Resize;
            glControl.Paint += glControl_Paint;

            // Ensure that the viewport and projection matrix are set correctly initially.
            glControl_Resize(glControl, EventArgs.Empty);

            // Log any focus changes.
            glControl.GotFocus += (sender, e) =>
                Log("Focus in");
            glControl.LostFocus += (sender, e) =>
                Log("Focus out");

            // Log WinForms keyboard/mouse events.
            glControl.MouseDown += (sender, e) =>
            {
                glControl.Focus();
                Log($"WinForms Mouse down: ({e.X},{e.Y})");
            };
            glControl.MouseUp += (sender, e) =>
                Log($"WinForms Mouse up: ({e.X},{e.Y})");
            glControl.MouseMove += (sender, e) =>
                Log($"WinForms Mouse move: ({e.X},{e.Y})");
            glControl.KeyDown += (sender, e) =>
                Log($"WinForms Key down: {e.KeyCode}");
            glControl.KeyUp += (sender, e) =>
                Log($"WinForms Key up: {e.KeyCode}");
            glControl.KeyPress += (sender, e) =>
                Log($"WinForms Key press: {e.KeyChar}");
        }

        private void glControl_Resize(object? sender, EventArgs e)
        {
            glControl.MakeCurrent();

            if (glControl.ClientSize.Height == 0)
                glControl.ClientSize = new System.Drawing.Size(glControl.ClientSize.Width, 1);

            GL.Viewport(0, 0, glControl.ClientSize.Width, glControl.ClientSize.Height);
        }

        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            glControl.MakeCurrent();

            GL.ClearColor(Color4.MidnightBlue);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            glControl.SwapBuffers();
        }

        private void WinFormsInputRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            glControl.DisableNativeInput();
        }

        private void NativeInputRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            INativeInput nativeInput = glControl.EnableNativeInput();

            if (_nativeInput == null)
            {
                _nativeInput = nativeInput;

                _nativeInput.MouseDown += (e) =>
                {
                    glControl.Focus();
                    Log("Native Mouse down");
                };
                _nativeInput.MouseUp += (e) =>
                    Log("Native Mouse up");
                _nativeInput.MouseMove += (e) =>
                    Log($"Native Mouse move: {e.DeltaX},{e.DeltaY}");
                _nativeInput.KeyDown += (e) =>
                    Log($"Native Key down: {e.Key}");
                _nativeInput.KeyUp += (e) =>
                    Log($"Native Key up: {e.Key}");
                _nativeInput.TextInput += (e) =>
                    Log($"Native Text input: {e.AsString}");
                _nativeInput.JoystickConnected += (e) =>
                    Log($"Native Joystick connected: {e.JoystickId}");
            }
        }

        private void Log(string message)
        {
            LogTextBox.AppendText(message + "\r\n");
        }
    }
}
