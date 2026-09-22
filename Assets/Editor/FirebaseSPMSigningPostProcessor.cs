#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class FirebaseSPMSigningPostProcessor
{
    // Ensure this runs late enough
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        // Team ID from Apple Developer account
        string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
        if (string.IsNullOrEmpty(teamId))
            teamId = "PSPB7FPY8J";

        // Apply the team ID and Manual signing to all targets, including SPM
        foreach (var targetGuid in project.GetAllTargets())
        {
            project.SetBuildProperty(targetGuid, "DEVELOPMENT_TEAM", teamId);
            
            // Set code sign style to manual to match UCB credentials
            project.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE", "Manual");
        }

        project.WriteToFile(projectPath);
    }
}
#endif
