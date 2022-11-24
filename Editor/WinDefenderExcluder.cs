using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WinDefenderExcluder
{
	[InitializeOnLoad]
	public class WinDefenderExcluder
	{
        static Process _process;
        static readonly string _exclusionFolder;
        static WinDefenderExcluder()
		{
            if (EditorPrefs.GetBool("WinDefenderExcluder.IgnoreDontAskAgain", false) || EditorPrefs.GetBool("WinDefenderExcluder.ExclusionDone", false)) return;

            if(SystemInfo.operatingSystemFamily != OperatingSystemFamily.Windows)
            {
                EditorPrefs.SetBool("WinDefenderExcluder.IgnoreDontAskAgain", true);
                return;
            }

            if (!SessionState.GetBool("WinDefenderExcluder.Initialized", false))
            {
                SessionState.SetBool("WinDefenderExcluder.Initialized", true);
                var _unityProjectFolder = Directory.GetParent(Application.dataPath).FullName;

                // Try to locate the root git repo and assign the exclusion there, otherwise fall back to project dir:
                if (TryFindGitParentDirectory(_unityProjectFolder, out var gitDir))
                {
                    _exclusionFolder = gitDir;
                }
                else
                {
                    _exclusionFolder = _unityProjectFolder;
                }

                RequestToExcludeDirectory(_exclusionFolder);
            }
        }

        static bool TryFindGitParentDirectory(string currentDirectory, out string directory)
        {
            while(Directory.GetDirectoryRoot(currentDirectory) != currentDirectory)
            {
                if(Directory.GetDirectories(currentDirectory, ".git", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    directory = currentDirectory;
                    return true;
                }
                currentDirectory = Directory.GetParent(currentDirectory).FullName;
            }
            directory = "";
            return false;
        }

        static void RequestToExcludeDirectory(string directory)
        {
            var response = EditorUtility.DisplayDialogComplex("Windows Defender Excluder",
                "Windows Defender may be monitoring this project directory, which " +
                "can cause fairly dramatic performance issues around file IO and your editor experience." + Environment.NewLine +
                "If you trust this project it is recommended that you exclude this project from Defender " +
                "scans to improve your editor experience." + Environment.NewLine + Environment.NewLine +
                $"Identified directory: {directory}", "Add an exclusion", "Ignore", "Ignore and don't ask again");
            if(response == 0)
            {
                ExcludeDirectory(_exclusionFolder);
            } else if(response == 1)
            {
                return;
            } else if (response == 2)
            {
                EditorPrefs.SetBool("WinDefenderExcluder.IgnoreDontAskAgain", true);
            }
        }

        static void ExcludeDirectory(string directory)
        {
            _process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + $"powershell -Command Add-MpPreference -ExclusionPath \"{directory}\"; pause",
                    Verb = "runas", // Run as admin
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true, // This must be true in order for UAC to elevate to admin
                }
            };
            _process.Start();
            EditorPrefs.SetBool("WinDefenderExcluder.ExclusionDone", true);
        }
    }
}
