# OpenTK 4.x WinForms GLControl

This repo contains a WinForms control designed to wrap the OpenTK 4.x APIs
into something WinForms can easily use.  It is designed and built for
.NET Core 3.1+.

## Building it

- Clone the repo.
- Build the solution (or at least the `OpenTK.WinForms.csproj`).
- The `OpenTK.WinForms` project exposes the new `GLControl`.  Add this project
   (or its compiled DLL) as a dependency of your own projects.  This directly
   depends on the WinForms and OpenTK Nuget packages but has no other dependencies.
- `OpenTK.WinForms.TestForm` contains a test program that demonstrates that the
   new `GLControl` works.  Try it first and make sure it works for you.  You should
   see a spinning cube under a standard menubar.

## Usage

The new `GLControl` is reasonably-well documented in its source code.  It has a
similar (but not identical) API to the 3.x `GLControl`.  In general, you do this
to use it:

1. Add the package to your project so you can use it.
2. Use the WinForms Designer to add a new `GLControl` to your form.
3. Configure the `GLControl` in the Designer, if necessary, to use the correct
    OpenGL configuration for your use case:  Set API, APIVersion, Flags, and Profile
    to whatever flavor of OpenGL you need.

   - Note: The WinForms Designer is buggy when handling Version objects: Make sure to
      type a full four-digit version number:  For OpenGL 3.3, for example, you need
      to type `3.3.0.0`; for OpenGL 4.0, you need to type `4.0.0.0`.

4. Bind events on the control that correspond to what you want to do.
   - In general, you probably want to bind `Paint` and `Resize`.
   - See below for a simple example.

5. If necessary, configure the control for `EnableNativeInput()`.  By default, all
    input is done via WinForms using standard WinForms event handlers.  If you need
    direct keyboard/mouse/joystick access, you can use `EnableNativeInput()` to
    get an `INativeInput` interface that will let you bind those events directly.
    Note that the control can be in _either_ native-input mode _or_ in WinForms
    input mode, not both.

## Example Resize/Paint handlers

A complete example can be found in [OpenTK.WinForms.TestForm](OpenTK.WinForms.TestForm/Form.cs),
but the basics of implementing Resize and Paint handlers look like this:

```c#
public partial class MyForm : Form
{
    public MyForm()
    {
        InitializeComponent();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // You can bind the events here or in the Designer.
        MyGLControl.Resize += MyGLControl_Resize;
        MyGLControl.Paint += MyGLControl_Paint;
    }

    private void MyGLControl_Resize(object? sender, EventArgs e)
    {
        MyGLControl.MakeCurrent();    // Tell OpenGL to use MyGLControl.

        // Update OpenGL on the new size of the control.
        GL.Viewport(0, 0, MyGLControl.ClientSize.Width, MyGLControl.ClientSize.Height);

        /*
            Usually you compute projection matrices here too, like this:

            float aspect_ratio = MyGLControl.ClientSize.Width / (float)MyGLControl.ClientSize.Height;
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);

            And then you load that into OpenGL with a call like GL.LoadMatrix() or GL.Uniform().
        */
    }

    private void MyGLControl_Paint(object? sender, PaintEventArgs e)
    {
        MyGLControl.MakeCurrent();    // Tell OpenGL to draw on MyGLControl.
        GL.Clear(...);                // Clear any prior drawing.

        /*
        ... use various other GL.*() calls here to draw stuff ...
        */

        MyGLControl.SwapBuffers();    // Display the result.
    }
}
```

## A note on the Fixed-Function Pipeline (FFP)

The FFP is the "old" way of doing OpenGL that uses calls like `GL.Begin()` and `GL.End()`.
Modern OpenGL uses shaders instead.  Most of the examples you'll find online that use
the previous versions of the `GLControl` use the FFP; but the FFP is *not* enabled by
default in OpenTK 4.

So if you need the FFP because you're running older code or using examples found online,
be sure to set

```c#
Profile = Profile.Compatability;
```

either in the Designer or in your form's constructor, or the FFP functions will *not* work
as expected.

## License

This is released under the terms of the [MIT Open-Source License](LICENSE.md), just like
OpenTK itself.


