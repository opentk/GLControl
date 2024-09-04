# Release notes

## [4.0.1]
- Renamed the `OpenTK.WinForms` namespace to `OpenTK.GLControl`. (@NogginBops)
- Fixed all remaining renaming issues. (@NogginBops)

## [4.0.0]
- This is the first stable release of GLControl for OpenTK 4.x.
- This version is idential to OpenTK.WinForms 4.0.0-pre.8.

## [4.0.0-pre.8]
- This will be the last release under the name OpenTK.WinForms, the next release is going to be 4.0 under the name OpenTK.GLControl.
- Disabled design mode animation as it was causing flickering when a dropdown menu was supposed to draw on top of GLControl. (@NogginBops)
- Removed the ability to change OpenGL context settings at runtime, attempting this will result in a runtime exception. (@NogginBops)
- The design time properties of the control have been cleaned up and marked with appropriate attributes. (@NogginBops)
- Updated to OpenTK 4.8.2. (@NogginBops)
- Updated to NUKE 8.0.0. (@NogginBops)

## [4.0.0-pre.7]
- Added properties to `GLControlSettings` to control backbuffer bits.
- Added `GLControlSettings.SrgbCapable` to set backbuffer sRGB capabilities.
- Fix issue where OpenTK 4.7.2+ would throw an exception at startup.

## [4.0.0-pre.6]
- _August 27, 2021_
- Update dependencies to OpenTK 4.6.4 packages.

## [4.0.0-pre.5]
- _March 4, 2021_
- Add `Context` property for better backward compatibility with GLControl 3.x.

## [4.0.0-pre.4]
- _February 18, 2021_
- Fix `Control.Site` null-reference bug.
- Add `Load` event for better backward compatibility with GLControl 3.x.
- Update simple test to demonstrate `Load` event.

## [4.0.0-pre.3]
- _December 28, 2020_
- Fix design-mode bugs.

## [4.0.0-pre.2]
- _December 22, 2020_
- Add more example projects to show usage: Simple example, multi-control example, and raw-input example.
- Fix more bugs.

## [4.0.0-pre.1]
- _December 21, 2020_
- All-new WebForms.GLControl, rewritten from the ground up for OpenTK 4.x and .NET Core 3.x+.
- Support both WinForms input events and "native device" input events.
- API is mostly backward compatible with the old GLControl.
- Full support for the new WinForms Designer (VS2019).
- All methods and properties fully XML-documented.
- Example project to show its usage.
- Readme that includes detailed usage and documentation.

