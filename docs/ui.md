# UI direction

The VX5 has five physical menu buttons. The desktop app follows those labels and keeps a separate **Advanced** editor for the complete property tree. The pedal view remains the home screen. Preset creation and editing live inside the Preset menu.

The current window uses native WPF controls with custom templates, vector shapes, and a dark hardware palette. This keeps the pedal face recognizable and the executable easy to build without external packages.

For a future packaged build, [WPF UI](https://github.com/lepoco/wpfui) is a strong candidate for general navigation, dialogs, and notifications; its [NuGet package](https://www.nuget.org/packages/wpf-ui) advertises .NET Framework 4.6.2 compatibility. [MahApps.Metro](https://github.com/MahApps/MahApps.Metro) also supports .NET Framework 4.6.2 and newer. Either can style standard panels; the pedal face and draggable controls should remain purpose built. Adding a package now would require a package restore build path, so this build keeps the verified dependency free path.
