# Last Brick

Unity project (iOS builds come from the `IOS` branch via Unity Cloud Build).

## iOS / TestFlight troubleshooting

- If a Unity Cloud Build succeeds and the log shows "Successfully uploaded the new binary to App Store Connect",
  but the build never appears in TestFlight, the first thing to check is the app version:
  the version (`bundleVersion` in `ProjectSettings/ProjectSettings.asset`) is probably already live on the App Store.
  Apple silently rejects the build during processing (the reason only arrives by email).
  Fix: bump `bundleVersion` (e.g. 2.1.0 -> 2.1.1) and rebuild.
- The build number does not need manual changes: `Assets/Editor/CloudBuildNumber.cs` sets it to the Cloud Build number.
- `bundleVersion` is shared with Android, so bumping it also changes the Android version name.
