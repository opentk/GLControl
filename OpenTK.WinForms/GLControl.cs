using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

namespace OpenTK.WinForms
{
    /// <summary>
    /// OpenGL-aware WinForms control.
    /// The WinForms designer will always call the default constructor.
    /// Inherit from this class and call one of its specialized constructors
    /// to enable antialiasing or custom <see cref="GraphicsMode"/>s.
    /// </summary>
    public class GLControl : Control
    {
        #region Private/internal fields

        /// <summary>
        /// The OpenGL configuration of this control.
        /// </summary>
        private GLControlSettings _glControlSettings;

        /// <summary>
        /// The underlying native window.  This will be reparented to be a child of
        /// this control.
        /// </summary>
        private NativeWindow _nativeWindow = null!;

        // Indicates that OnResize was called before OnHandleCreated.
        // To avoid issues with missing OpenGL contexts, we suppress
        // the premature Resize event and raise it as soon as the handle
        // is ready.
        private bool _resizeEventSuppressed;

        #endregion

        #region Public configuration

        /// <summary>
        /// Get or set a value representing the current graphics API.
        /// If you change this, the OpenGL context will be recreated, and any
        /// data previously allocated with it will be lost.
        /// </summary>
        public ContextAPI API
        {
            get => _nativeWindow?.API ?? _glControlSettings.API;
            set
            {
                if (value != API)
                {
                    _glControlSettings.API = value;
                    RecreateControl();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value representing the current graphics API profile.
        /// If you change this, the OpenGL context will be recreated, and any
        /// data previously allocated with it will be lost.
        /// </summary>
        public ContextProfile Profile
        {
            get => _nativeWindow?.Profile ?? _glControlSettings.Profile;
            set
            {
                if (value != Profile)
                {
                    _glControlSettings.Profile = value;
                    RecreateControl();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value representing the current graphics profile flags.
        /// If you change this, the OpenGL context will be recreated, and any
        /// data previously allocated with it will be lost.
        /// </summary>
        public ContextFlags Flags
        {
            get => _nativeWindow?.Flags ?? _glControlSettings.Flags;
            set
            {
                if (value != Flags)
                {
                    _glControlSettings.Flags = value;
                    RecreateControl();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value representing the current version of the graphics API.
        /// If you change this, the OpenGL context will be recreated, and any
        /// data previously allocated with it will be lost.
        /// </summary>
        public Version APIVersion
        {
            get => _nativeWindow?.APIVersion ?? _glControlSettings.APIVersion;
            set
            {
                if (value != APIVersion)
                {
                    _glControlSettings.APIVersion = value;
                    RecreateControl();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not this window is event-driven.
        /// An event-driven window will wait for events before updating/rendering. It is useful for non-game applications,
        /// where the program only needs to do any processing after the user inputs something.
        /// </summary>
        public bool IsEventDriven
        {
            get => _nativeWindow?.IsEventDriven ?? _glControlSettings.IsEventDriven;
            set
            {
                if (value != IsEventDriven)
                {
                    _glControlSettings.IsEventDriven = value;
                    if (IsHandleCreated)
                    {
                        _nativeWindow.IsEventDriven = value;
                    }
                }
            }
        }

        #endregion

        #region Read-only status properties

        /// <summary>
        /// Gets the graphics context associated with the underlying NativeWindow
        /// of this GLControl.
        /// </summary>
        [Browsable(false)]
        public IGLFWGraphicsContext Context
            => _nativeWindow?.Context ?? DummyGLFWGraphicsContext.Instance;

        /// <summary>
        /// Gets a value indicating whether the underlying native window was
        /// successfully created.
        /// </summary>
        [Browsable(false)]
        public bool HasValidContext => _nativeWindow != null;

        /// <summary>
        /// Gets the aspect ratio of this GLControl.
        /// </summary>
        [Description("The aspect ratio of the client area of this GLControl.")]
        public float AspectRatio
            => Width / (float)Height;

        /// <summary>
        /// Gets the underlying NativeWindow that is used inside this control.
        /// You should generally avoid accessing this property and prefer methods
        /// and properties on this control; however, it is available if there is
        /// no other way to perform a task.
        /// </summary>
        [Browsable(false)]
        public NativeWindow NativeWindow => _nativeWindow;

        #endregion

        #region Construction/creation

        /// <summary>
        /// Constructs a new instance with default GLControlSettings.
        /// </summary>
        public GLControl()
            : this(null)
        {
        }

        /// <summary>
        /// Constructs a new instance with the specified GLControlSettings.
        /// </summary>
        /// <param name="glControlSettings">The preferred configuration for the OpenGL
        /// renderer.  If null, 'GLControlSettings.Default' will be used instead.</param>
        public GLControl(GLControlSettings? glControlSettings)
        {
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = false;

            _glControlSettings = glControlSettings != null
                ? glControlSettings.Clone() : new GLControlSettings();
        }

        /// <summary>
        /// Raises the HandleCreated event.
        /// </summary>
        /// <param name="e">The EventArgs of the event that triggered this event.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            CreateNativeWindow(_glControlSettings.ToNativeWindowSettings());

            base.OnHandleCreated(e);

            if (_resizeEventSuppressed)
            {
                OnResize(EventArgs.Empty);
                _resizeEventSuppressed = false;
            }

            if (DesignMode)
            {
                _designTimeTimer = new Timer();
                _designTimeTimer.Tick += OnDesignTimeTimerTick;
                _designTimeTimer.Interval = 100;
                _designTimeTimer.Start();
            }
        }

        /// <summary>
        /// Construct the child NativeWindow that will wrap the underlying GLFW instance.
        /// </summary>
        /// <param name="nativeWindowSettings">The NativeWindowSettings to use for
        /// the new GLFW window.</param>
        private void CreateNativeWindow(NativeWindowSettings nativeWindowSettings)
        {
            if (DesignMode)
                return;

            _nativeWindow = new NativeWindow(nativeWindowSettings);

            NonportableReparent(_nativeWindow);

            // Force the newly child-ified GLFW window to be resized to fit this control.
            ResizeNativeWindow();

            // And now show the child window, since it hasn't been made visible yet.
            _nativeWindow.IsVisible = true;
        }

        /// <summary>
        /// Gets the <c>CreateParams</c> instance for this <c>GLControl</c>.
        /// This is overridden to force correct child behavior.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_VREDRAW = 0x1;
                const int CS_HREDRAW = 0x2;
                const int CS_OWNDC = 0x20;

                CreateParams cp = base.CreateParams;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    cp.ClassStyle |= CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
                }
                return cp;
            }
        }

        /// <summary>
        /// When major OpenGL-configuration properties are changed, this method is
        /// invoked to recreate the underlying NativeWindow accordingly.
        /// </summary>
        private void RecreateControl()
        {
            if (_nativeWindow != null && !DesignMode)
            {
                DestroyNativeWindow();
                CreateNativeWindow(_glControlSettings.ToNativeWindowSettings());
            }
        }

        /// <summary>
        /// Ensure that the required underlying GLFW window has been created.
        /// </summary>
        private void EnsureCreated()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);

            if (!IsHandleCreated)
            {
                CreateControl();
            }

            if (_nativeWindow == null && !DesignMode)
            {
                RecreateHandle();
            }
        }

        /// <summary>
        /// Reparent the given NativeWindow to be a child of this GLControl.  This is a
        /// non-portable operation, as its name implies:  It works wildly differently
        /// between OSes.  The current implementation only supports Microsoft Windows.
        /// </summary>
        /// <param name="nativeWindow">The NativeWindow that must become a child of
        /// this control.</param>
        private unsafe void NonportableReparent(NativeWindow nativeWindow)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr hWnd = GLFWNative.glfwGetWin32Window(nativeWindow.WindowPtr);

                // Reparent the real HWND under this control.
                Win32.SetParent(hWnd, Handle);

                // Change the real HWND's window styles to be "WS_CHILD" (i.e., a child of
                // some container), and turn off *all* the other style bits (most of the rest
                // of them could cause trouble).  In particular, this turns off stuff like
                // WS_BORDER and WS_CAPTION and WS_POPUP and so on, any of which GLFW might
                // have turned on for us.
                IntPtr style = (IntPtr)(long)Win32.WindowStyles.WS_CHILD;
                Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE, style);

                // Change the real HWND's extended window styles to be "WS_EX_NOACTIVATE", and
                // turn off *all* the other extended style bits (most of the rest of them
                // could cause trouble).  We want WS_EX_NOACTIVATE because we don't want
                // Windows mistakenly giving the GLFW window the focus as soon as it's created,
                // regardless of whether it's a hidden window.
                style = (IntPtr)(long)Win32.WindowStylesEx.WS_EX_NOACTIVATE;
                Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_EXSTYLE, style);
            }
            else throw new NotSupportedException("The current operating system is not supported by this control.");
        }

        #endregion

        #region Destruction/cleanup

        /// <summary>
        /// Raises the HandleDestroyed event.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            // Ensure that context is still alive when passing to events
            // => This allows to perform cleanup operations in OnHandleDestroyed handlers
            base.OnHandleDestroyed(e);

            DestroyNativeWindow();

            if (_designTimeTimer != null)
            {
                _designTimeTimer.Dispose();
                _designTimeTimer = null;
            }
        }

        /// <summary>
        /// Destroy the child NativeWindow that wraps the underlying GLFW instance.
        /// </summary>
        private void DestroyNativeWindow()
        {
            if (_nativeWindow != null)
            {
                _nativeWindow.Dispose();
                _nativeWindow = null!;
            }
        }

        #endregion

        #region WinForms event handlers

        /// <summary>
        /// Raises the System.Windows.Forms.Control.Paint event.
        /// </summary>
        /// <param name="e">A System.Windows.Forms.PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureCreated();

            if (DesignMode)
            {
                PaintInDesignMode(e.Graphics);
            }

            base.OnPaint(e);
        }

        /// <summary>
        /// Raises the Resize event.
        /// Note: this method may be called before the OpenGL context is ready.
        /// Check that IsHandleCreated is true before using any OpenGL methods.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            // Do not raise OnResize event before the handle and context are created.
            if (!IsHandleCreated)
            {
                _resizeEventSuppressed = true;
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                BeginInvoke(new Action(ResizeNativeWindow)); // Need the native window to resize first otherwise our control will be in the wrong place.
            }
            else
            {
                ResizeNativeWindow();
            }

            base.OnResize(e);
        }

        /// <summary>
        /// Resize the native window to fit this control.
        /// </summary>
        private void ResizeNativeWindow()
        {
            if (DesignMode)
                return;

            if (_nativeWindow != null)
            {
                _nativeWindow.ClientRectangle = new Box2i(0, 0, Width, Height);
            }
        }

        /// <summary>
        /// Raises the ParentChanged event.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnParentChanged(EventArgs e)
        {
            ResizeNativeWindow();

            base.OnParentChanged(e);
        }

        #endregion

        #region Public OpenGL-related proxy methods

        /// <summary>
        /// Gets the native <see cref="Window"/> pointer for use with <see cref="GLFW"/> API.
        /// </summary>
        [Browsable(false)]
        public unsafe Window* WindowPtr
        {
            get
            {
                if (DesignMode)
                    return null;

                EnsureCreated();
                return _nativeWindow != null ? _nativeWindow.WindowPtr : null;
            }
        }

        /// <summary>
        /// Swaps the front and back buffers, presenting the rendered scene to the screen.
        /// This method will have no effect on a single-buffered <c>GraphicsMode</c>.
        /// </summary>
        public void SwapBuffers()
        {
            if (DesignMode)
                return;

            EnsureCreated();
            Context.SwapBuffers();
        }

        /// <summary>
        /// <para>
        /// Makes <see cref="GLControl.Context"/> current in the calling thread.
        /// All OpenGL commands issued are hereafter interpreted by this context.
        /// </para>
        /// <para>
        /// When using multiple <c>GLControl</c>s, calling <c>MakeCurrent</c> on
        /// one control will make all other controls non-current in the calling thread.
        /// </para>
        /// <seealso cref="Context"/>
        /// <para>
        /// A <c>GLControl</c> can only be current in one thread at a time.
        /// </para>
        /// </summary>
        public void MakeCurrent()
        {
            if (DesignMode)
                return;

            EnsureCreated();
            _nativeWindow.MakeCurrent();
        }

        #endregion

        #region Design-time rendering support

        //---------------------------------------------------------------------------
        // At design-time, we really can't load OpenGL and GLFW and render with it
        // for real; the WinForms designer is too limited to support such advanced
        // things without exploding.  So instead, we simply use GDI+ to draw a cube
        // at design-time.  It's not very impressive for OpenGL, but a spinning cube
        // is *really* unusual to see in the WinForms designer, so it will hint to
        // the user that yes, this is a 3D control and you can do 3D things inside
        // it; and it helps to show that it's not simply a black rectangle either,
        // which might suggest to the user that the control is broken.  It's *just*
        // enough, without being too much.
        //---------------------------------------------------------------------------

        /// <summary>
        /// This timer is used to keep the design-time cube rotating so
        /// that it's obvious that you're working with a "3D" control.  It
        /// fires once every 1/10 of a second, which is abysmally slow for
        /// real animation, but which is just fine for design-time rendering.
        /// </summary>
        private Timer? _designTimeTimer;

        /// <summary>
        /// The angle (yaw) of the design-time cube.
        /// </summary>
        private float _designTimeCubeYaw;

        /// <summary>
        /// The angle (pitch) of the design-time cube.
        /// </summary>
        private float _designTimeCubeRoll;

        /// <summary>
        /// Endpoints that can make a cube.  We only use this in design mode.
        /// </summary>
        private static (Vector3, Vector3)[] CubeLines { get; } = new (Vector3, Vector3)[]
        {
            (new Vector3(-1, -1, -1), new Vector3(+1, -1, -1)),
            (new Vector3(-1, -1, -1), new Vector3(-1, +1, -1)),
            (new Vector3(-1, -1, -1), new Vector3(-1, -1, +1)),

            (new Vector3(+1, -1, -1), new Vector3(+1, +1, -1)),
            (new Vector3(+1, -1, -1), new Vector3(+1, -1, +1)),

            (new Vector3(-1, +1, -1), new Vector3(+1, +1, -1)),
            (new Vector3(-1, +1, -1), new Vector3(-1, +1, +1)),

            (new Vector3(-1, -1, +1), new Vector3(+1, -1, +1)),
            (new Vector3(-1, -1, +1), new Vector3(-1, +1, +1)),

            (new Vector3(+1, +1, +1), new Vector3(+1, +1, -1)),
            (new Vector3(+1, +1, +1), new Vector3(-1, +1, +1)),
            (new Vector3(+1, +1, +1), new Vector3(+1, -1, +1)),
        };

        /// <summary>
        /// Draw a simple cube, in an ortho projection, using GDI+.
        /// </summary>
        /// <param name="graphics">The GDI+ Graphics object to draw on.</param>
        /// <param name="color">The color for the cube.</param>
        /// <param name="cx">The X coordinate of the center point of the cube,
        /// in Graphics coordinates.</param>
        /// <param name="cy">The Y coordinate of the center point of the cube,
        /// in Graphics coordinates.</param>
        /// <param name="radius">The radius to the cube's corners from the center point.</param>
        /// <param name="yaw">The yaw (rotation around the Y axis) of the cube.</param>
        /// <param name="pitch">The pitch (rotation around the X axis) of the cube.</param>
        /// <param name="roll">The roll (rotation around the Z axis) of the cube.</param>
        private void DrawCube(System.Drawing.Graphics graphics,
            System.Drawing.Color color,
            float cx, float cy, float radius,
            float yaw = 0, float pitch = 0, float roll = 0)
        {
            // We use matrices to rotate and scale the cube, but we just use a simple
            // center offset to position it.  That saves a lot of extra multiplies all
            // over this code, since we can use Matrix3 and Vector3 instead of Matrix4
            // and Vector4.  And no, quaternions aren't worth the effort here either.
            Matrix3 matrix = Matrix3.CreateRotationZ(roll)
                * Matrix3.CreateRotationY(yaw)
                * Matrix3.CreateRotationX(pitch)
                * Matrix3.CreateScale(radius);

            // Draw the edges of the cube in the given color.  Since it's just a single-
            // color wireframe, the order of the edges doesn't matter at all.
            using System.Drawing.Brush brush = new System.Drawing.SolidBrush(color);
            using System.Drawing.Pen pen = new System.Drawing.Pen(brush);

            foreach ((Vector3 start, Vector3 end) in CubeLines)
            {
                Vector3 projStart = start * matrix;
                Vector3 projEnd = end * matrix;

                graphics.DrawLine(pen, cx + projStart.X, cy - projStart.Y, cx + projEnd.X, cy - projEnd.Y);
            }
        }

        /// <summary>
        /// This is invoked every 1/10 of a second when rendering in
        /// design-time mode, just so that we can keep the fake cube spinning.
        /// </summary>
        /// <param name="sender">The object that sent this event.</param>
        /// <param name="e">The event args (which aren't meaningful).</param>
        private void OnDesignTimeTimerTick(object? sender, EventArgs e)
        {
            // The prime numbers here ensure we don't repeat angles for a long time :)
            _designTimeCubeYaw += (float)(Math.PI / 97);
            _designTimeCubeRoll += (float)(Math.PI / 127);

            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(Handle);

            PaintInDesignMode(graphics);
        }

        /// <summary>
        /// In design mode, we have nothing to show, so we paint the
        /// background black and put a spinning cube on it so that it's
        /// obvious that it's a 3D renderer.
        /// </summary>
        private void PaintInDesignMode(System.Drawing.Graphics graphics)
        {
            // Since we're always DoubleBuffered = false, we have to do
            // simple double-buffering ourselves, using a bitmap.
            using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(Width, Height, graphics);
            using System.Drawing.Graphics bitmapGraphics = System.Drawing.Graphics.FromImage(bitmap);

            // Other resources we'll need.
            using System.Drawing.Font bigFont = new System.Drawing.Font("Arial", 12);
            using System.Drawing.Font smallFont = new System.Drawing.Font("Arial", 9);
            using System.Drawing.Brush titleBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using System.Drawing.Brush subtitleBrush = new System.Drawing.SolidBrush(System.Drawing.Color.PaleGoldenrod);

            // Configuration.
            const float cubeRadius = 16;
            const string title = "GLControl";
            int cx = Width / 2, cy = Height / 2;
            string subtitle = $"( {Name} )";

            // These sizes will hold font metrics.
            System.Drawing.SizeF titleSize;
            System.Drawing.SizeF subtitleSize;
            System.Drawing.SizeF totalTextSize;

            // Start with a black background.
            bitmapGraphics.Clear(System.Drawing.Color.Black);
            bitmapGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Measure and the title (fixed) and the subtitle (the control's Name).
            titleSize = bitmapGraphics.MeasureString(title, bigFont);
            subtitleSize = bitmapGraphics.MeasureString(subtitle, smallFont);
            totalTextSize = new System.Drawing.SizeF(
                Math.Max(titleSize.Width, subtitleSize.Width),
                titleSize.Height + subtitleSize.Height
            );

            // Draw both of the title and subtitle centered, now that we know how big they are.
            bitmapGraphics.DrawString(title, bigFont, titleBrush,
                new System.Drawing.PointF(cx - totalTextSize.Width / 2 + cubeRadius + 2, cy - totalTextSize.Height / 2));
            bitmapGraphics.DrawString(subtitle, smallFont, subtitleBrush,
                new System.Drawing.PointF(cx - totalTextSize.Width / 2 + cubeRadius + 2, cy - totalTextSize.Height / 2 + titleSize.Height));

            // Draw a cube beside the title and subtitle.
            DrawCube(bitmapGraphics, System.Drawing.Color.Red,
                cx - totalTextSize.Width / 2 - cubeRadius - 2, cy, cubeRadius,
                _designTimeCubeYaw, (float)(Math.PI / 8), _designTimeCubeRoll);

            // Draw the resulting bitmap on the real window canvas.
            graphics.DrawImage(bitmap, 0, 0);
        }

        #endregion
    }
}
