﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AppCenterEditor
{
    public class AppCenterEditorSDKTools : Editor
    {
        private enum SDKState
        {
            SDKNotInstalled, 
            SDKNotInstalledAndInstalling,
            SDKNotFull,
            SDKNotFullAndInstalling,
            SDKIsFull
        }
        public static bool IsInstalled { get { return AreSomePackagesInstalled(); } }
        public static bool IsFullSDK { get { return CheckIfAllPackagesInstalled(); } }
        public static bool IsInstalling { get; set; }
        public static bool IsUpgrading { get; set; }
        public static string LatestSdkVersion { get; private set; }
        public static UnityEngine.Object SdkFolder { get; private set; }
        public static string InstalledSdkVersion { get; private set; }
        public static GUIStyle TitleStyle { get { return new GUIStyle(AppCenterEditorHelper.uiStyle.GetStyle("titleLabel")); } }

        private static Type appCenterSettingsType = null;
        private static bool isInitialized; // used to check once, gets reset after each compile
        private static UnityEngine.Object _previousSdkFolderPath;
        private static bool sdkFolderNotFound;
        public static bool isSdkSupported = true;
        private static int angle = 0;

        private static SDKState GetSDKState()
        {
            if (!IsInstalled)
            {
                if (IsInstalling)
                {
                    return SDKState.SDKNotInstalledAndInstalling;
                }
                else
                {
                    return SDKState.SDKNotInstalled;
                }
            }

            //SDK installed.
            if (IsFullSDK)
            {
                return SDKState.SDKIsFull;
            }

            //SDK is not full.
            if (IsInstalling)
            {
                return SDKState.SDKNotFullAndInstalling;
            }
            else
            {
                return SDKState.SDKNotFull;
            }
        }

        public static void DrawSdkPanel()
        {
            if (!isInitialized)
            {
                //SDK is installed.
                CheckSdkVersion();
                isInitialized = true;
                GetLatestSdkVersion();
                SdkFolder = FindSdkAsset();

                if (SdkFolder != null)
                {
                    AppCenterEditorPrefsSO.Instance.SdkPath = AssetDatabase.GetAssetPath(SdkFolder);
                    // AppCenterEditorDataService.SaveEnvDetails();
                }
            }
            ShowSdkInstallationPanel();
        }

        public static void DisplayPackagePanel(AppCenterSDKPackage sdkPackage)
        {
            using (new AppCenterGuiFieldHelper.UnityVertical(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleGray1")))
            {
                using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                {
                    GUILayout.FlexibleSpace();
                    if (sdkPackage.IsInstalled)
                    {
                        sdkPackage.ShowPackageInstalledMenu();
                    }
                    else
                    {
                        sdkPackage.ShowPackageNotInstalledMenu();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void ShowSdkInstallationPanel()
        {
            sdkFolderNotFound = SdkFolder == null;

            if (_previousSdkFolderPath != SdkFolder)
            {
                // something changed, better save the result.
                _previousSdkFolderPath = SdkFolder;

                AppCenterEditorPrefsSO.Instance.SdkPath = (AssetDatabase.GetAssetPath(SdkFolder));
                //TODO: check if we need this?
                // AppCenterEditorDataService.SaveEnvDetails();

                sdkFolderNotFound = false;
            }
            SDKState SDKstate = GetSDKState();
            using (new AppCenterGuiFieldHelper.UnityVertical(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleGray1")))
            {
                switch (SDKstate)
                {
                    case SDKState.SDKNotInstalled:
                        {
                            ShowNOSDKLabel();
                            ShowInstallButton();
                            break;
                        }
                    case SDKState.SDKNotInstalledAndInstalling:
                        {
                            ShowNOSDKLabel();
                            ShowInstallingButton();
                            break;
                        }
                    case SDKState.SDKNotFull:
                        {
                            ShowSdkInstalledLabel();
                            ShowFolderObject();
                            ShowInstallButton();
                            ShowRemoveButton();
                            break;
                        }
                    case SDKState.SDKNotFullAndInstalling:
                        {
                            ShowSdkInstalledLabel();
                            ShowFolderObject();
                            ShowInstallingButton();
                            ShowRemoveButton();
                            break;
                        }
                    case SDKState.SDKIsFull:
                        {
                            ShowSdkInstalledLabel();
                            ShowFolderObject();
                            ShowRemoveButton();
                            break;
                        }
                }
            }
            if (SDKstate == SDKState.SDKIsFull || SDKstate == SDKState.SDKNotFull)
            {
                ShowUpgradePanel();
            }
        }

        private static void ShowUpgradePanel()
        {
            if (!sdkFolderNotFound)
            {
                using (new AppCenterGuiFieldHelper.UnityVertical(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleGray1")))
                {
                    isSdkSupported = false;
                    string[] versionNumber = !string.IsNullOrEmpty(InstalledSdkVersion) ? InstalledSdkVersion.Split('.') : new string[0];

                    var numerical = 0;
                    bool isEmptyVersion = string.IsNullOrEmpty(InstalledSdkVersion) || versionNumber == null || versionNumber.Length == 0;
                    if (isEmptyVersion || (versionNumber.Length > 0 && int.TryParse(versionNumber[0], out numerical) && numerical < 0))
                    {
                        //older version of the SDK
                        using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField("SDK is outdated. Consider upgrading to the get most features.", AppCenterEditorHelper.uiStyle.GetStyle("orTxt"));
                            GUILayout.FlexibleSpace();
                        }
                    }
                    else if (numerical >= 0)
                    {
                        isSdkSupported = true;
                    }

                    var buttonWidth = 200;
                    if (ShowSDKUpgrade() && isSdkSupported)
                    {
                        if (IsUpgrading)
                        {
                            GUILayout.Space(10);
                            using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                            {
                                GUILayout.FlexibleSpace();
                                GUI.enabled = false;
                                var image = DrawUtils.RotateImage(AssetDatabase.LoadAssetAtPath("Assets/AppCenterEditorExtensions/Editor/UI/Images/wheel.png", typeof(Texture2D)) as Texture2D, angle++);
                                GUILayout.Button(new GUIContent("  Upgrading to " + LatestSdkVersion, image), AppCenterEditorHelper.uiStyle.GetStyle("Button"), GUILayout.MaxWidth(buttonWidth), GUILayout.MinHeight(32));
                                GUILayout.FlexibleSpace();
                            }
                        }
                        else
                        {
                            GUILayout.Space(10);
                            using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                            {
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Upgrade to " + LatestSdkVersion, AppCenterEditorHelper.uiStyle.GetStyle("Button"), GUILayout.MinHeight(32)))
                                {
                                    IsUpgrading = true;
                                    UpgradeSdk();
                                }
                                GUILayout.FlexibleSpace();
                            }
                        }
                    }
                    else if (isSdkSupported)
                    {
                        using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField("You have the latest SDK!", TitleStyle, GUILayout.MinHeight(32));
                            GUILayout.FlexibleSpace();
                        }
                    }

                    using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("VIEW RELEASE NOTES", AppCenterEditorHelper.uiStyle.GetStyle("textButton"), GUILayout.MinHeight(32), GUILayout.MinWidth(200)))
                        {
                            Application.OpenURL("https://github.com/Microsoft/AppCenter-SDK-Unity/releases");
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private static void ShowRemoveButton()
        {
            if (isSdkSupported && !sdkFolderNotFound)
            {
                using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClear")))
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("REMOVE SDK", AppCenterEditorHelper.uiStyle.GetStyle("textButton"), GUILayout.MinHeight(32), GUILayout.MinWidth(200)))
                    {
                        if (IsUpgrading)
                        {
                            GUI.enabled = false;
                        }
                        RemoveSdk();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void ShowFolderObject()
        {
            if (!sdkFolderNotFound)
            {
                GUI.enabled = false;
            }
            else
            {
                EditorGUILayout.LabelField(
                    "An SDK was detected, but we were unable to find the directory. Drag-and-drop the top-level App Center SDK folder below.",
                    AppCenterEditorHelper.uiStyle.GetStyle("orTxt"));
            }

            GUILayout.Space(10);
            using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleClearWithleftPad")))
            {
                GUILayout.FlexibleSpace();
                SdkFolder = EditorGUILayout.ObjectField(SdkFolder, typeof(UnityEngine.Object), false, GUILayout.MaxWidth(200));
                GUILayout.FlexibleSpace();
            }

            if (!sdkFolderNotFound)
            {
                // this is a hack to prevent our "block while loading technique" from breaking up at this point.
                GUI.enabled = !EditorApplication.isCompiling && AppCenterEditor.blockingRequests.Count == 0;
            }
        }

        private static void ShowSdkInstalledLabel()
        {
            EditorGUILayout.LabelField(string.Format("SDK {0} is installed", string.IsNullOrEmpty(InstalledSdkVersion) ? Constants.UnknownVersion : InstalledSdkVersion),
                       TitleStyle, GUILayout.MinWidth(EditorGUIUtility.currentViewWidth));
        }

        private static void ShowInstallingButton()
        {
            var buttonWidth = 250;
            using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleGray1")))
            {
                GUILayout.FlexibleSpace();
                GUI.enabled = false;
                var image = DrawUtils.RotateImage(AssetDatabase.LoadAssetAtPath("Assets/AppCenterEditorExtensions/Editor/UI/Images/wheel.png", typeof(Texture2D)) as Texture2D, angle++);
                GUILayout.Button(new GUIContent("  SDK is installing", image), AppCenterEditorHelper.uiStyle.GetStyle("Button"), GUILayout.MaxWidth(buttonWidth), GUILayout.MinHeight(32));
                GUILayout.FlexibleSpace();
            }
        }

        private static void ShowInstallButton()
        {
            var buttonWidth = 250;
            using (new AppCenterGuiFieldHelper.UnityHorizontal(AppCenterEditorHelper.uiStyle.GetStyle("gpStyleGray1")))
            {
                GUILayout.FlexibleSpace();
                if (IsUpgrading)
                {
                    GUI.enabled = false;
                }
                if (GUILayout.Button("Install all App Center SDK packages", AppCenterEditorHelper.uiStyle.GetStyle("Button"), GUILayout.MaxWidth(buttonWidth), GUILayout.MinHeight(32)))
                {
                    IsInstalling = true;
                    ImportLatestSDK();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static void ShowNOSDKLabel()
        {
            EditorGUILayout.LabelField("No SDK is installed.", TitleStyle, GUILayout.MinWidth(EditorGUIUtility.currentViewWidth));
            GUILayout.Space(10);
        }

        public static void ImportLatestSDK(string existingSdkPath = null)
        {
            PackagesInstaller.ImportLatestSDK(GetNotInstalledPackages(), LatestSdkVersion, existingSdkPath);
        }

        public static bool AreSomePackagesInstalled()
        {
            return GetAppCenterSettings() != null;
        }

        public static List<AppCenterSDKPackage> GetNotInstalledPackages()
        {
            List<AppCenterSDKPackage> notInstalledPackages = new List<AppCenterSDKPackage>();
            if (!IsInstalled)
            {
                notInstalledPackages.AddRange(AppCenterSDKPackage.SupportedPackages);
                return notInstalledPackages;
            }
            foreach (var package in AppCenterSDKPackage.SupportedPackages)
            {
                if (!package.IsInstalled)
                {
                    notInstalledPackages.Add(package);
                }
            }
            return notInstalledPackages;
        }

        public static bool CheckIfAllPackagesInstalled()
        {
            foreach (var package in AppCenterSDKPackage.SupportedPackages)
            {
                if (!package.IsInstalled)
                {
                    return false;
                }
            }
            return GetAppCenterSettings() != null;
        }

        public static Type GetAppCenterSettings()
        {
            if (appCenterSettingsType == typeof(object))
                return null; // Sentinel value to indicate that AppCenterSettings doesn't exist
            if (appCenterSettingsType != null)
                return appCenterSettingsType;

            appCenterSettingsType = typeof(object); // Sentinel value to indicate that AppCenterSettings doesn't exist
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in allAssemblies)
                foreach (var eachType in assembly.GetTypes())
                    if (eachType.Name == AppCenterEditorHelper.APPCENTER_SETTINGS_TYPENAME)
                        appCenterSettingsType = eachType;
            //if (appCenterSettingsType == typeof(object))
            //    Debug.LogWarning("Should not have gotten here: "  + allAssemblies.Length);
            //else
            //    Debug.Log("Found Settings: " + allAssemblies.Length + ", " + appCenterSettingsType.Assembly.FullName);
            return appCenterSettingsType == typeof(object) ? null : appCenterSettingsType;
        }

        private static bool ShowSDKUpgrade()
        {
            if (string.IsNullOrEmpty(LatestSdkVersion) || LatestSdkVersion == Constants.UnknownVersion)
            {
                return false;
            }

            if (string.IsNullOrEmpty(InstalledSdkVersion) || InstalledSdkVersion == Constants.UnknownVersion)
            {
                return true;
            }

            string[] currrent = InstalledSdkVersion.Split('.');
            string[] latest = LatestSdkVersion.Split('.');

            return int.Parse(latest[0]) > int.Parse(currrent[0])
                || int.Parse(latest[1]) > int.Parse(currrent[1])
                || int.Parse(latest[2]) > int.Parse(currrent[2]);
        }

        private static void UpgradeSdk()
        {
            if (EditorUtility.DisplayDialog("Confirm SDK Upgrade", "This action will remove the current App Center SDK and install the lastet version.", "Confirm", "Cancel"))
            {
                IEnumerable<AppCenterSDKPackage> installedPackages = GetInstalledPackages();
                RemoveSdkBeforeUpdate();
                PackagesInstaller.ImportLatestSDK(installedPackages, LatestSdkVersion);
               // ImportLatestSDK(AppCenterEditorPrefsSO.Instance.SdkPath);
            }
        }

        private static IEnumerable<AppCenterSDKPackage> GetInstalledPackages()
        {
            var installedPackages = new List<AppCenterSDKPackage>();
            foreach (var package in AppCenterSDKPackage.SupportedPackages)
            {
                if (package.IsInstalled)
                {
                    installedPackages.Add(package);
                }
            }
            return installedPackages;
        }

        private static void RemoveSdkBeforeUpdate()
        {
            var skippedFiles = new[]
            {
                "AppCenterSettings.asset",
                "AppCenterSettings.asset.meta"
            };

            RemoveAndroidSettings();

            var toDelete = new List<string>();
            toDelete.AddRange(Directory.GetFiles(AppCenterEditorPrefsSO.Instance.SdkPath));
            toDelete.AddRange(Directory.GetDirectories(AppCenterEditorPrefsSO.Instance.SdkPath));

            foreach (var path in toDelete)
            {
                if (!skippedFiles.Contains(Path.GetFileName(path)))
                {
                    FileUtil.DeleteFileOrDirectory(path);
                }
            }
        }

        private static void RemoveSdk(bool prompt = true)
        {
            if (prompt && !EditorUtility.DisplayDialog("Confirm SDK Removal", "This action will remove the current App Center SDK.", "Confirm", "Cancel"))
            {
                return;
            }

            RemoveAndroidSettings();

            if (FileUtil.DeleteFileOrDirectory(AppCenterEditorPrefsSO.Instance.SdkPath))
            {
                FileUtil.DeleteFileOrDirectory(AppCenterEditorPrefsSO.Instance.SdkPath + ".meta");
                AppCenterEditor.RaiseStateUpdate(AppCenterEditor.EdExStates.OnSuccess, "App Center SDK removed.");

                // HACK for 5.4, AssetDatabase.Refresh(); seems to cause the install to fail.
                if (prompt)
                {
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                AppCenterEditor.RaiseStateUpdate(AppCenterEditor.EdExStates.OnError, "An unknown error occured and the App Center SDK could not be removed.");
            }
        }

        private static void RemoveAndroidSettings()
        {
            if (Directory.Exists(Application.dataPath + "/Plugins/Android/res/values"))
            {
                var files = Directory.GetFiles(Application.dataPath + "/Plugins/Android/res/values", "appcenter-settings.xml*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    FileUtil.DeleteFileOrDirectory(file);
                }
            }
        }

        private static void CheckSdkVersion()
        {
            if (!string.IsNullOrEmpty(InstalledSdkVersion))
                return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var package in AppCenterSDKPackage.SupportedPackages)
                        {
                            package.CheckIfInstalled(type);
                        }
                        if (type.FullName == "Microsoft.AppCenter.Unity.WrapperSdk")
                        {
                            foreach (var field in type.GetFields())
                            {
                                if (field.Name == "WrapperSdkVersion")
                                {
                                    InstalledSdkVersion = field.GetValue(field).ToString();
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // For this failure, silently skip this assembly unless we have some expectation that it contains App Center
                    if (assembly.FullName.StartsWith("Assembly-CSharp")) // The standard "source-code in unity proj" assembly name
                    {
                        Debug.LogWarning("App Center Editor Extension error, failed to access the main CSharp assembly that probably contains App Center SDK");
                    }
                    continue;
                }
            }
        }

        private static void GetLatestSdkVersion()
        {
            var threshold = AppCenterEditorPrefsSO.Instance.EdSet_lastSdkVersionCheck != DateTime.MinValue ? AppCenterEditorPrefsSO.Instance.EdSet_lastSdkVersionCheck.AddHours(1) : DateTime.MinValue;

            if (DateTime.Today > threshold)
            {
                AppCenterEditorHttp.MakeGitHubApiCall("https://api.github.com/repos/Microsoft/AppCenter-SDK-Unity/git/refs/tags", (version) =>
                {
                    LatestSdkVersion = version ?? Constants.UnknownVersion;
                    AppCenterEditorPrefsSO.Instance.EdSet_latestSdkVersion = LatestSdkVersion;
                });
            }
            else
            {
                LatestSdkVersion = AppCenterEditorPrefsSO.Instance.EdSet_latestSdkVersion;
            }
        }

        private static UnityEngine.Object FindSdkAsset()
        {
            UnityEngine.Object sdkAsset = null;

            // look in editor prefs
            if (AppCenterEditorPrefsSO.Instance.SdkPath != null)
            {
                sdkAsset = AssetDatabase.LoadAssetAtPath(AppCenterEditorPrefsSO.Instance.SdkPath, typeof(UnityEngine.Object));
            }
            if (sdkAsset != null)
                return sdkAsset;

            sdkAsset = AssetDatabase.LoadAssetAtPath(AppCenterEditorHelper.DEFAULT_SDK_LOCATION, typeof(UnityEngine.Object));
            if (sdkAsset != null)
                return sdkAsset;

            var fileList = Directory.GetDirectories(Application.dataPath, "*AppCenter", SearchOption.AllDirectories);
            if (fileList.Length == 0)
                return null;

            var relPath = fileList[0].Substring(fileList[0].LastIndexOf("Assets" + Path.DirectorySeparatorChar));
            return AssetDatabase.LoadAssetAtPath(relPath, typeof(UnityEngine.Object));
        }
    }
}
