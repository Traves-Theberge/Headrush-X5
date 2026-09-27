# Release process

The repository keeps source and documentation in Git. Build outputs and local presets are ignored.

1. Update `VERSION`, `src/App/AssemblyInfo.cs`, and `CHANGELOG.md` together.
2. Run `powershell.exe -ExecutionPolicy Bypass -File .\package.ps1` on Windows.
3. Extract the ZIP into a fresh folder and start `X5Control.exe` there.
4. Verify that the window opens, the five VX5 menus are present, and the VX5 connection state is shown if a VX5 is attached.
5. Compare the ZIP hash with `X5Control-<version>-windows-portable.sha256` before distributing it.

The package contains `X5Control.exe`, `MainWindow.xaml`, `START-HERE.txt`, `README.md`, and `CHANGELOG.md`. `presets.user.json` is deliberately excluded because it contains the user's local preset names and snapshots. The package is portable and unsigned; it is not an installer.

The current project has no GitHub Actions pipeline. A release is built and verified locally before its ZIP and checksum are uploaded to GitHub Releases.
