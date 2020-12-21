using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

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

        /// <summary>
        /// This is used to render the control at design-time, since we cannot
        /// use a real GLFW instance in the WinForms Designer.
        /// </summary>
        private GLControlDesignTimeRenderer? _designTimeRenderer;

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

            _designTimeRenderer = DesignMode ? new GLControlDesignTimeRenderer(this) : null;
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
                _designTimeRenderer = new GLControlDesignTimeRenderer(this);
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

            if (_designTimeRenderer != null)
            {
                _designTimeRenderer.Dispose();
                _designTimeRenderer = null;
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
                _designTimeRenderer?.Paint(e.Graphics);
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
        /// Swaps the front and back buffers, presenting the rendered scene to the screen.
        /// This method will have no effect on a single-buffered <c>GraphicsMode</c>.
        /// </summary>
        public void SwapBuffers()
        {
            if (DesignMode)
                return;

            EnsureCreated();
            _nativeWindow.Context.SwapBuffers();
        }

        /// <summary>
        /// <para>
        /// Makes OpenGL context current in the calling thread.
        /// All OpenGL commands issued are hereafter interpreted by this context.
        /// </para>
        /// <para>
        /// When using multiple <c>GLControl</c>s, calling <c>MakeCurrent</c> on
        /// one control will make all other controls non-current in the calling thread.
        /// </para>
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
    }
}
