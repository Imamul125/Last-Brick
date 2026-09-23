#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Adds the Game Center capability, the Push Notifications capability (aps-environment) and the
/// remote-notification background mode so Firebase Cloud Messaging works on iOS.
/// The App ID must have Push Notifications enabled and the provisioning profile
/// uploaded to Unity Cloud must be regenerated after enabling it.
/// </summary>
public class IOSCapabilitiesPostProcessor
{
    [PostProcessBuild(101)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);
        string mainTarget = project.GetUnityMainTargetGuid();

        var capabilities = new ProjectCapabilityManager(projectPath, "Unity-iPhone.entitlements", null, mainTarget);
        capabilities.AddPushNotifications(false); // false = production (TestFlight / App Store)
        capabilities.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
        capabilities.AddGameCenter(); // Game Center leaderboard (GooglePlayManager)
        capabilities.WriteToFile();
    }
}
#endif
