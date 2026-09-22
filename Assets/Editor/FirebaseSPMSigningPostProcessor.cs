#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

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

        // Apply team ID using Unity`s native method
        project.SetTeamId(mainTarget, teamId);
        project.SetTeamId(frameworkTarget, teamId);
        
        // Also forcefully set the properties on both targets and the project-level configuration
        // so that dynamically added SPM packages inherit the manual team profile
        project.SetBuildProperty(mainTarget, "DEVELOPMENT_TEAM", teamId);
        project.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", teamId);
        project.SetBuildProperty(projectGuid, "DEVELOPMENT_TEAM", teamId);
        
        project.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Manual");
        project.SetBuildProperty(projectGuid, "CODE_SIGN_STYLE", "Manual");

        project.WriteToFile(projectPath);
    }
}
#endif
