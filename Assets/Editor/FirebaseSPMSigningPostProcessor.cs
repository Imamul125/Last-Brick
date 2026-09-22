#if UNITY_IOS
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Post-process build script that fixes Firebase/Google SPM package signing issues
/// in Unity Cloud Build (Build Automation).
///
/// Problem: Firebase iOS SDK pulled via Swift Package Manager creates Xcode targets
/// (Firebase_FirebaseCoreExtension, Firebase_FirebaseMessaging, GoogleUtilities, etc.)
/// that require a development team for code signing. These SPM targets are resolved
/// by Xcode AFTER Unity generates the project, so Unity's PBXProject API cannot
/// configure them.
///
/// Solution: We create a custom .xcconfig file and reference it as the base
/// configuration for the Xcode project. This xcconfig sets CODE_SIGNING_ALLOWED=NO
/// by default, which SPM targets will inherit. We then explicitly override it to YES
/// on the main Unity-iPhone and UnityFramework targets.
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

        // --- Configure signing for the main app targets ---
        project.SetTeamId(mainTarget, teamId);
        project.SetTeamId(frameworkTarget, teamId);

        project.SetBuildProperty(mainTarget, "DEVELOPMENT_TEAM", teamId);
        project.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", teamId);

        project.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Manual");

        // Explicitly allow signing on the main app targets — these override
        // the project-level NO that we inject below.
        project.SetBuildProperty(mainTarget, "CODE_SIGNING_ALLOWED", "YES");
        project.SetBuildProperty(mainTarget, "CODE_SIGNING_REQUIRED", "YES");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGNING_ALLOWED", "YES");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGNING_REQUIRED", "YES");

        project.WriteToFile(projectPath);

        // --- Patch the pbxproj to disable signing at the project level ---
        // This ensures SPM package targets (which inherit project-level settings)
        // won't fail with "requires a development team" errors.
        PatchProjectLevelSigning(projectPath);

        Debug.Log("[FirebaseSPMSigningPostProcessor] Successfully patched Xcode project.");
    }

    /// <summary>
    /// Directly patches the .pbxproj file to inject CODE_SIGNING_ALLOWED = NO
    /// into the project-level build settings. 
    /// 
    /// SPM package targets inherit from the project object's build settings when
    /// they don't have their own explicit override. By setting NO at the project
    /// level, SPM targets won't require signing. The Unity-iPhone and
    /// UnityFramework targets already have explicit YES set above, so they
    /// override this project-level default.
    /// </summary>
    private static void PatchProjectLevelSigning(string projectPath)
    {
        string content = File.ReadAllText(projectPath);

        if (content.Contains("/* SPM_SIGNING_FIX */"))
        {
            Debug.Log("[FirebaseSPMSigningPostProcessor] Project already patched, skipping.");
            return;
        }

        // Strategy: Find all occurrences of "ALWAYS_SEARCH_USER_PATHS = NO;" in the 
        // project-level build settings and inject our signing overrides after them.
        // ALWAYS_SEARCH_USER_PATHS is present in every Unity-generated pbxproj
        // in both the Debug and Release build configuration sections.
        string searchStr = "ALWAYS_SEARCH_USER_PATHS = NO;";
        string replaceStr = 
            "ALWAYS_SEARCH_USER_PATHS = NO;\n" +
            "\t\t\t\tCODE_SIGNING_ALLOWED = NO; /* SPM_SIGNING_FIX */\n" +
            "\t\t\t\tCODE_SIGNING_REQUIRED = NO; /* SPM_SIGNING_FIX */";
        
        content = content.Replace(searchStr, replaceStr);
        File.WriteAllText(projectPath, content);
        
        Debug.Log("[FirebaseSPMSigningPostProcessor] Injected project-level CODE_SIGNING_ALLOWED=NO.");
    }
}
#endif
