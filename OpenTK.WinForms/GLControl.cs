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
    /// OpenGL-capable WinForms control that is a specialized wrapper around
    /// OpenTK's NativeWindow.
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
        /// Constructs a new instance with default GLControlSettings.  Various things
        /// that like to use reflection want to have an empty constructor available,
        /// so we offer this constructor rather than just adding `= null` to the
        /// constructor that does the actual construction work.
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
        /// This event handler will be invoked by WinForms when the HWND of this
        /// control itself has been created and assigned in the Handle property.
        /// We capture the event to construct the NativeWindow that will be responsible
        /// for all of the actual OpenGL rendering and native device input.
        /// </summary>
        /// <param name="e">An EventArgs instance (ignored).</param>
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
        /// Gets the CreateParams instance for this GLControl.
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
        /// This is triggered when the underlying Handle/HWND instance is *about to be*
        /// destroyed (this is called *before* the Handle/HWND is destroyed).  We use it
        /// to cleanly destroy the NativeWindow before its parent disappears.
        /// </summary>
        /// <param name="e">An EventArgs instance (ignored).</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
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
        /// This is raised by WinForms to paint this instance.
        /// </summary>
        /// <param name="e">A PaintEventArgs object that describes which areas
        /// of the control need to be painted.</param>
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
        /// This is invoked when the Resize event is triggered, and is used to position
        /// the internal GLFW window accordingly.
        /// 
        /// Note: This method may be called before the OpenGL context is ready or the
        /// NativeWindow even exists, so everything inside it requires safety checks.
        /// </summary>
        /// <param name="e">An EventArgs instance (ignored).</param>
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
        /// This event is raised when this control's parent control is changed,
        /// which may result in this control becoming a different size or shape, so
        /// we capture it to ensure that the underlying GLFW window gets correctly
        /// resized and repositioned as well.
        /// </summary>
        /// <param name="e">An EventArgs instance (ignored).</param>
        protected override void OnParentChanged(EventArgs e)
        {
            ResizeNativeWindow();

            base.OnParentChanged(e);
        }

        #endregion

        #region Public OpenGL-related proxy methods

        /// <summary>
        /// Swaps the front and back buffers, presenting the rendered scene to the user.
        /// </summary>
        public void SwapBuffers()
        {
            if (DesignMode)
                return;

            EnsureCreated();
            _nativeWindow.Context.SwapBuffers();
        }

        /// <summary>
        /// Makes this control's OpenGL context current in the calling thread.
        /// All OpenGL commands issued are hereafter interpreted by this context.
        /// When using multiple GLControls, calling MakeCurrent on one control
        /// will make all other controls non-current in the calling thread.
        /// A GLControl can only be current in one thread at a time.
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
