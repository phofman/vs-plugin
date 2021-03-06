﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using Microsoft.Win32;

namespace BlackBerry.Package.Helpers
{
    /// <summary>
    /// Helper class for accessing Visual C++ project properties.
    /// </summary>
    internal static class ProjectHelper
    {
        /// <summary>
        /// Gets the project out of any selected item inside Solution Explorer of Visual Studio.
        /// </summary>
        public static Project GetProject(IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
                throw new ArgumentNullException("hierarchy");

            object obj;
            Project project = null;
            if (ErrorHandler.Succeeded(hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj)) && obj != null)
            {
                project = obj as Project;
            }
            return project;
        }

        /// <summary>
        /// Gets the list of all projects within the solution.
        /// </summary>
        public static Project[] GetProjects(IVsSolution solution)
        {
            if (solution == null)
                throw new ArgumentNullException("solution");

            var result = new List<Project>();

            var hierarchy = solution as IVsHierarchy;
            if (hierarchy != null)
            {
                // list projects:
                object pVar;
                int hr = hierarchy.GetProperty((uint) VSConstants.VSITEMID.Root, (int) __VSHPROPID.VSHPROPID_FirstChild, out pVar);
                if (hr == VSConstants.S_OK)
                {
                    while (pVar != null && hr == VSConstants.S_OK)
                    {
                        uint itemID = (uint) (int) pVar;
                        if (itemID == VSConstants.VSITEMID_NIL)
                            break;

                        var nestedHierarchy = GetNestedHierarchy(hierarchy, itemID);

                        if (nestedHierarchy != null
                            && nestedHierarchy.GetProperty((uint) VSConstants.VSITEMID.Root, (int) __VSHPROPID.VSHPROPID_ConfigurationProvider, out pVar) == VSConstants.S_OK)
                        {
                            var project = GetProject(nestedHierarchy);
                            if (project != null && !string.IsNullOrEmpty(project.FullName))
                            {
                                result.Add(project);
                            }
                        }

                        // move to next item in hierarchy:
                        hr = hierarchy.GetProperty(itemID, (int)__VSHPROPID.VSHPROPID_NextSibling, out pVar);
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Gets the nested hierarchy for the hierarchy item.
        /// </summary>
        private static IVsHierarchy GetNestedHierarchy(IVsHierarchy hierarchy, uint itemID)
        {
            IntPtr nestedHierarchyObject;
            uint nestedItemId;
            var hierarchyGuid = typeof(IVsHierarchy).GUID;
            int hr = hierarchy.GetNestedHierarchy(itemID, ref hierarchyGuid, out nestedHierarchyObject, out nestedItemId);
            if (hr == VSConstants.S_OK && nestedHierarchyObject != IntPtr.Zero)
            {
                var nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHierarchyObject) as IVsHierarchy;
                Marshal.Release(nestedHierarchyObject);

                return nestedHierarchy;
            }

            return null;
        }

        /// <summary>
        /// Updates specific project settings. It can be used to setup value only for particular platform (or 'null' to overwrite for all of them).
        /// </summary>
        public static void SetValue(VCProject project, string ruleName, string propertyName, string platformName, string configurationName, string value, bool appendValues, char valueSeparator, string inheritDefaults)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            if (string.IsNullOrEmpty(value))
                return;
            if (valueSeparator == '\0' && (appendValues || !string.IsNullOrEmpty(inheritDefaults)))
                throw new ArgumentNullException("valueSeparator");

            foreach (VCConfiguration configuration in (IVCCollection) project.Configurations)
            {
                if (string.IsNullOrEmpty(configurationName) || string.Compare(configuration.ConfigurationName, configurationName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var rulePropertyStore = configuration.Rules.Item(ruleName) as IVCRulePropertyStorage;
                    if (rulePropertyStore != null)
                    {
                        var currentPlatformName = ((VCPlatform) configuration.Platform).Name;
                        if (string.IsNullOrEmpty(platformName) || string.Compare(currentPlatformName, platformName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            var existingPropertyValue = rulePropertyStore.GetUnevaluatedPropertyValue(propertyName);
                            var newPropertyValue = appendValues
                                ? MergePropertyValues(existingPropertyValue, value, valueSeparator, inheritDefaults)
                                : (string.IsNullOrEmpty(inheritDefaults) ? value : MergePropertyValues(null, value, valueSeparator, inheritDefaults));

                            // set new value, if it's really new:
                            if (!string.IsNullOrEmpty(newPropertyValue) && newPropertyValue != existingPropertyValue && newPropertyValue != inheritDefaults)
                            {
                                rulePropertyStore.SetPropertyValue(propertyName, newPropertyValue);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Merges existing and new values set to the project (MSBuild) property.
        /// If 'inheritDefaults' value is specified, it is enforced to be placed at the very end.
        /// </summary>
        public static string MergePropertyValues(string existingValue, string value, char valueSeparator, string inheritDefaults)
        {
            if (valueSeparator == '\0')
                throw new ArgumentNullException("valueSeparator");
            if (string.IsNullOrEmpty(value))
                return existingValue;

            var resultArray = new List<string>();
            bool canAddInheritDefaults = false;

            // first add existing values:
            if (!string.IsNullOrEmpty(existingValue))
            {
                var existingValueArray = existingValue.Split(new[] { valueSeparator }, StringSplitOptions.RemoveEmptyEntries);

                if (string.IsNullOrEmpty(inheritDefaults))
                {
                    resultArray.AddRange(existingValueArray);
                }
                else
                {
                    foreach (var ev in existingValueArray)
                    {
                        if (string.CompareOrdinal(ev, inheritDefaults) != 0)
                        {
                            resultArray.Add(ev);
                        }
                        else
                        {
                            canAddInheritDefaults = true;
                        }
                    }
                }
            }
            else
            {
                canAddInheritDefaults = true;
            }

            // than append new values:
            var valueArray = value.Split(new[] { valueSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var v in valueArray)
            {
                if (!resultArray.Contains(v))
                {
                    resultArray.Add(v);
                }
            }

            // and add inherited marker, if specified:
            if (!string.IsNullOrEmpty(inheritDefaults) && resultArray.Count > 0 && canAddInheritDefaults)
            {
                resultArray.Add(inheritDefaults);
            }

            // serialize final value:
            var result = new StringBuilder();

            for (int i = 0; i < resultArray.Count; i++)
            {
                result.Append(resultArray[i]);

                if (i < resultArray.Count - 1)
                {
                    result.Append(valueSeparator);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Adds additional set of dependencies (libraries) at the end of existing list in project properties.
        /// </summary>
        public static void AddAdditionalDependencies(VCProject project, string platformName, string configurationName, params string[] values)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "Link", "AdditionalDependencies", platformName, configurationName, string.Join(";", values), true, ';', "%(AdditionalDependencies)");
        }

        /// <summary>
        /// Adds additional set of dependency-directories (search directories for libraries) at the end of existing list in project properties.
        /// </summary>
        public static void AddAdditionalDependencyDirectories(VCProject project, string platformName, string configurationName, params string[] values)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "Link", "AdditionalLibraryDirectories", platformName, configurationName, string.Join(";", values), true, ';', "%(AdditionalLibraryDirectories)");
        }

        /// <summary>
        /// Adds additional set of preprocessor definitions at the end of existing list in project properties.
        /// </summary>
        public static void AddPreprocessorDefines(VCProject project, string platformName, string configurationName, params string[] values)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "CL", "PreprocessorDefinitions", platformName, configurationName, string.Join(";", values), true, ';', "%(PreprocessorDefinitions)");
        }

        /// <summary>
        /// Adds additional set of include-directories (search directories for header files) at the end of existing list in project properties.
        /// </summary>
        public static void AddAdditionalIncludeDirectories(VCProject project, string platformName, string configurationName, params string[] values)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "CL", "AdditionalIncludeDirectories", platformName, configurationName, string.Join(";", values), true, ';', "%(AdditionalIncludeDirectories)");
        }

        /// <summary>
        /// Adds additional set of compiler-specific options at the end of existing list in project properties.
        /// </summary>
        public static void AddAdditionalCompilerOptions(VCProject project, string platformName, string configurationName, params string[] values)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "CL", "AdditionalOptions", platformName, configurationName, string.Join(" ", values).Trim(), true, ' ', "%(AdditionalOptions)");
        }

        /// <summary>
        /// Updates build-output directories in project properties.
        /// </summary>
        public static void SetBuildOutputDirectory(VCProject project, string platformName, string configurationName, string value)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            SetValue(project, "ConfigurationGeneral", "OutDir", platformName, configurationName, value, false, '\0', null);
        }

        /// <summary>
        /// Gets the evaluated value of specific Visual C++ project property.
        /// </summary>
        public static string GetValue(Project project, string rule, string propertyName)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            if (project.ConfigurationManager == null || project.ConfigurationManager.ActiveConfiguration == null)
                return null;

            var activeConfiguration = project.ConfigurationManager.ActiveConfiguration;
            var currentConfigurationName = activeConfiguration.ConfigurationName;
            var currentPlatformName = activeConfiguration.PlatformName;
            var cppProject = project.Object as VCProject;

            if (cppProject != null)
            {
                foreach (VCConfiguration configuration in (IVCCollection) cppProject.Configurations)
                {
                    if (string.Compare(configuration.ConfigurationName, currentConfigurationName, StringComparison.InvariantCulture) == 0)
                    {
                        var platformName = ((VCPlatform) configuration.Platform).Name;
                        if (string.Compare(platformName, currentPlatformName, StringComparison.InvariantCulture) == 0)
                        {
                            var rulePropertyStorage = configuration.Rules.Item(rule) as IVCRulePropertyStorage;
                            if (rulePropertyStorage != null)
                            {
                                var value = rulePropertyStorage.GetEvaluatedPropertyValue(propertyName);
                                return value;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the full path to the target outcome of specified Visual C++ project.
        /// </summary>
        public static string GetTargetFullName(Project project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            var targetName = GetValue(project, "ConfigurationGeneral", "TargetName");
            if (string.IsNullOrEmpty(targetName))
                return null;

            targetName += GetValue(project, "ConfigurationGeneral", "TargetExt");

            // The output folder can be anything, let's assume it's any of these patterns:
            // 1) "\\server\folder"
            // 2) "drive:\folder"
            // 3) "..\..\folder"
            // 4) "folder"
            // 5) ""
            var outputPath = GetValue(project, "ConfigurationGeneral", "OutDir");

            if (string.IsNullOrEmpty(outputPath))
            {
                // 5) ""
                var projectFolder = Path.GetDirectoryName(project.FullName);
                if (string.IsNullOrEmpty(projectFolder))
                    return targetName;
                return Path.Combine(projectFolder, targetName);
            }

            if (outputPath.Length >= 2 && outputPath[0] == Path.DirectorySeparatorChar && outputPath[1] == Path.DirectorySeparatorChar)
            {
                // 1) "\\server\folder"
                return Path.Combine(outputPath, targetName);
            }

            if (outputPath.Length >= 3 && outputPath[1] == Path.VolumeSeparatorChar && outputPath[2] == Path.DirectorySeparatorChar)
            {
                // 2) "drive:\folder"
                return Path.Combine(outputPath, targetName);
            }

            if (outputPath.StartsWith("..\\") || outputPath.StartsWith("../"))
            {
                // 3) "..\..\folder"

                var projectFolder = Path.GetDirectoryName(project.FullName);
                while (outputPath.StartsWith("..\\") || outputPath.StartsWith("../"))
                {
                    outputPath = outputPath.Substring(3);
                    if (!string.IsNullOrEmpty(projectFolder))
                    {
                        projectFolder = Path.GetDirectoryName(projectFolder);
                    }
                }

                if (string.IsNullOrEmpty(projectFolder))
                    return Path.Combine(outputPath, targetName);
                return Path.Combine(projectFolder, outputPath, targetName);
            }

            // 4) "folder"
            var folder = Path.GetDirectoryName(project.FullName);
            if (string.IsNullOrEmpty(folder))
                return Path.Combine(outputPath, targetName);
            return Path.Combine(folder, outputPath, targetName);
        }

        /// <summary>
        /// Gets the project's target architecture (x86 for simulator and armle-v7 for device).
        /// </summary>
        public static string GetTargetArchitecture(Project project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            return GetValue(project, "ConfigurationGeneral", "TargetArch");
        }

        /// <summary>
        /// Tries to guess the full path to the target outcome of specified Visual C++ project.
        /// This is a complimentary method to GetTargetFullName(). As the extended BlackBerry projects
        /// are mostly based on makefiles it might be quite hard to say sure (based on Visual Studio settings only),
        /// where the target binary is created. That's why we use some non-common knowledge
        /// about the Cascades make system.
        /// </summary>
        public static string GuessTargetFullName(Project project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            if (project.ConfigurationManager == null || project.ConfigurationManager.ActiveConfiguration == null)
                return null;

            var appType = GetValue(project, "ConfigurationGeneral", "ConfigurationAppType");
            if (string.Compare(appType, "Regular", StringComparison.OrdinalIgnoreCase) == 0
                || string.IsNullOrEmpty(appType))
            {
                return GetTargetFullName(project);
            }

            // get file name and extension:
            var targetName = GetValue(project, "ConfigurationGeneral", "TargetName");
            if (string.IsNullOrEmpty(targetName))
                return null;

            targetName += GetValue(project, "ConfigurationGeneral", "TargetExt");

            // check, whether we compile against device or simulator:
            var targetCpu = GetValue(project, "ConfigurationGeneral", "TargetArch");
            var projectDir = Path.GetDirectoryName(project.FullName);

            if (string.Compare(targetCpu, "armle-v7", StringComparison.OrdinalIgnoreCase) == 0 && !string.IsNullOrEmpty(projectDir))
            {
                var path = Path.Combine(projectDir, "arm", "o.le-v7-g", targetName);
                if (File.Exists(path))
                    return path;
                return Path.Combine(projectDir, "arm", "o.le-v7", targetName + ".so"); // Cascades apps in Device-Release mode are regular shared-object!
            }

            if (string.Compare(targetCpu, "x86", StringComparison.OrdinalIgnoreCase) == 0 && !string.IsNullOrEmpty(projectDir))
            {
                var path = Path.Combine(projectDir, "x86", "o-g", targetName);
                if (File.Exists(path))
                    return path;
                return Path.Combine(projectDir, "x86", "o", targetName);
            }

            // unsupported architecture:
            return null;
        }

        /// <summary>
        /// Gets the name of the file with info about run application on the target for passed deployment in MSBuild.
        /// </summary>
        public static string GetFlagFileNameForRunInfo(Project project)
        {
            return GetFlagFileName(project, "runinfo");
        }

        /// <summary>
        /// Gets the name of the file for debug-native flag passed to MSBuild.
        /// </summary>
        public static string GetFlagFileNameForDebugNative(Project project)
        {
            return GetFlagFileName(project, "debug-native");
        }

        /// <summary>
        /// Gets the name of the file for CSK-password passed to MSBuild.
        /// </summary>
        public static string GetFlagFileNameForCSKPassword(Project project)
        {
            return GetFlagFileName(project, "csk-password");
        }

        private static string GetFlagFileName(Project project, string flagName)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            if (string.IsNullOrEmpty(flagName))
                throw new ArgumentNullException("flagName");

            // is it 'miscellaneous files'?
            if (string.IsNullOrEmpty(project.FullName))
                return null;

            return Path.Combine(Path.GetDirectoryName(project.FullName), string.Concat("vsndk-", flagName, ".flag"));
        }

        /// <summary>
        /// Gets the global default folder, where Visual Studio is supposed to store projects (generally suggesting it at startup of New Project Wizard).
        /// </summary>
        public static string GetDefaultProjectFolder(DTE2 dte)
        {
            if (dte == null)
                throw new ArgumentNullException("dte");

            RegistryKey key = null;
            try
            {
                key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\VisualStudio\" + dte.Version);

                return key != null ? key.GetValue("DefaultNewProjectLocation", null).ToString() : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (key != null)
                {
                    key.Close();
                }
            }
        }
    }
}
