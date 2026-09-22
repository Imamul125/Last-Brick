#if UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Post-process build script that fixes Firebase/Google SPM package signing issues
/// in Unity Cloud Build (Build Automation).
///
/// Problem: Firebase iOS SDK pulled via Swift Package Manager creates Xcode targets
/// (Firebase_FirebaseCoreExtension, Firebase_FirebaseMessaging, GoogleUtilities, etc.)
/// that require a development team for code signing. Unity Cloud Build's fastlane
/// pipeline passes CODE_SIGN_IDENTITY on the xcodebuild command line which applies
/// to ALL targets. SPM targets then try to sign but fail because they have no
/// DEVELOPMENT_TEAM (stripped by UCB's sed step) and no provisioning profile.
///
/// Solution: Set CODE_SIGNING_ALLOWED=NO at the Xcode PROJECT level using PBXProject
/// API. SPM package targets inherit project-level build settings. Then explicitly
/// set CODE_SIGNING_ALLOWED=YES on the main app targets to override the project default.
/// This way SPM targets skip signing entirely while the app targets sign normally.
/// </summary>
public class FirebaseSPMSigningPostProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
        if (string.IsNullOrEmpty(teamId))
            teamId = "PSPB7FPY8J";

        string mainTarget = project.GetUnityMainTargetGuid();
        string frameworkTarget = project.GetUnityFrameworkTargetGuid();
        string projectGuid = project.ProjectGuid();

        // =============================================================
        // STEP 1: Disable signing at the PROJECT level.
        // SPM package targets (Firebase, GoogleUtilities, etc.) inherit
        // project-level build settings. By setting NO here, they won't
        // attempt code signing and won't need a development team.
        // =============================================================
        project.SetBuildProperty(projectGuid, "CODE_SIGNING_ALLOWED", "NO");
        project.SetBuildProperty(projectGuid, "CODE_SIGNING_REQUIRED", "NO");

        // =============================================================
        // STEP 2: Enable signing on the main app targets.
        // These explicit target-level settings override the project-level
        // NO set above. This ensures the app and framework are signed
        // correctly with the provisioning profile and team ID.
        // =============================================================
        
        // Unity-iPhone (main app target)
        project.SetTeamId(mainTarget, teamId);
        project.SetBuildProperty(mainTarget, "DEVELOPMENT_TEAM", teamId);
        project.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(mainTarget, "CODE_SIGNING_ALLOWED", "YES");
        project.SetBuildProperty(mainTarget, "CODE_SIGNING_REQUIRED", "YES");

        // UnityFramework (framework target)
        project.SetTeamId(frameworkTarget, teamId);
        project.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", teamId);
        project.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGNING_ALLOWED", "YES");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGNING_REQUIRED", "YES");

        project.WriteToFile(projectPath);

        Debug.Log($"[FirebaseSPMSigningPostProcessor] Patched Xcode project: " +
                  $"project-level CODE_SIGNING_ALLOWED=NO, " +
                  $"app targets CODE_SIGNING_ALLOWED=YES with team {teamId}");
    }
}
#endif
