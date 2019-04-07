﻿// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.Bot.Builder.Dialogs.Declarative.Resources
{
    /// <summary>
    /// Class which gives standard access to file based resources
    /// </summary>
    public class ResourceExplorer : IResourceExplorer
    {
        private List<FolderResource> folderResources = new List<FolderResource>();

        public ResourceExplorer()
        {
        }


        public IEnumerable<DirectoryInfo> Folders
        {
            get
            {
                foreach (var folderResource in folderResources)
                {
                    yield return folderResource.Directory;
                }
            }
        }

        IEnumerable<DirectoryInfo> IResourceExplorer.Folders { get => folderResources.Select(s => s.Directory); set => throw new NotImplementedException(); }

        /// <summary>
        /// Occurs when a file or directory in the specified System.IO.FileSystemWatcher.Path is changed.
        /// </summary>
        public event FileSystemEventHandler Changed;

        public void AddFolder(string folder, bool monitorFiles = true)
        {
            var folderResource = new FolderResource(folder, monitorFiles);

            folderResource.Watcher.Changed += (sender, e) =>
            {
                if (this.Changed != null)
                {
                    this.Changed(sender, e);
                }
            };

            this.folderResources.Add(folderResource);
        }

        /// <summary>
        /// Add a .csproj as resource (adding the project, referenced projects and referenced packages)
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="projectFile"></param>
        /// <returns></returns>
        public static ResourceExplorer LoadProject(string projectFile)
        {
            var explorer = new ResourceExplorer();
            if (!File.Exists(projectFile))
            {
                projectFile = Directory.EnumerateFiles(projectFile, "*.*proj").FirstOrDefault();
                if (projectFile == null)
                {
                    throw new ArgumentNullException(nameof(projectFile));
                }
            }
            string projectFolder = Path.GetDirectoryName(projectFile);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(projectFile);

            // add folder for the project
            explorer.AddFolder(projectFolder);

            // add project references
            foreach (XmlNode node in xmlDoc.SelectNodes("//ProjectReference"))
            {
                var path = Path.Combine(projectFolder, node.Attributes["Include"].Value);
                path = Path.GetFullPath(path);
                path = Path.GetDirectoryName(path);
                explorer.AddFolder(path);
            }

            var packages = Path.GetFullPath("packages");
            var relativePackagePath = Path.Combine(@"..", "packages");
            while (!Directory.Exists(packages) && Path.GetDirectoryName(packages) != Path.GetPathRoot(packages))
            {
                packages = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packages), relativePackagePath));
                if (packages == null)
                {
                    throw new ArgumentNullException("Can't find packages folder");
                }
            }
            var pathResolver = new PackagePathResolver(packages);

            // add nuget package references
            foreach (XmlNode node in xmlDoc.SelectNodes("//PackageReference"))
            {
                string packageName = node.Attributes["Include"]?.Value;
                string version = node.Attributes["Version"]?.Value;
                if (!String.IsNullOrEmpty(packageName) && !String.IsNullOrEmpty(version))
                {
                    var package = new PackageIdentity(packageName, new NuGetVersion(version));
                    var folder = Path.Combine(packages, pathResolver.GetPackageDirectoryName(package));
                    if (Directory.Exists(folder))
                    {
                        explorer.AddFolder(folder, monitorFiles: false);
                    }
                }
            }

            return explorer;
        }

        /// <summary>
        /// get resources of a given type
        /// </summary>
        /// <param name="fileExtension"></param>
        /// <returns></returns>
        public IEnumerable<FileInfo> GetResources(string fileExtension)
        {
            foreach (var folder in this.folderResources)
            {
                foreach (var fileInfo in folder.GetResources(fileExtension))
                {
                    yield return fileInfo;
                }
            }
        }

        /// <summary>
        /// Get resource by filename
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public FileInfo GetResource(string filename)
        {
            return GetResources(Path.GetExtension(filename)).Where(fi => fi.Name == filename).SingleOrDefault();
        }

        /// <summary>
        /// Folder/FileResources
        /// </summary>
        internal class FolderResource
        {
            internal FolderResource(string folder, bool monitorChanges = true)
            {
                this.Directory = new DirectoryInfo(folder);
                this.Watcher = new FileSystemWatcher(folder);
                if (monitorChanges)
                {
                    this.Watcher.IncludeSubdirectories = true;
                    this.Watcher.EnableRaisingEvents = true;
                }
            }

            /// <summary>
            /// folder to enumerate
            /// </summary>
            public DirectoryInfo Directory { get; set; }

            public FileSystemWatcher Watcher { get; private set; }

            /// <summary>
            /// id -> Resource object)
            /// </summary>
            public IEnumerable<FileInfo> GetResources(string extension)
            {
                foreach (var fileInfo in this.Directory.EnumerateFiles($"*.{extension.TrimStart('.')}", SearchOption.AllDirectories))
                {
                    yield return fileInfo;
                }
                yield break;
            }
        }


    }
}
