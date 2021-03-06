﻿namespace tomenglertde.Wax.Model.Mapping
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;

    using JetBrains.Annotations;

    using PropertyChanged;

    using tomenglertde.Wax.Model.VisualStudio;
    using tomenglertde.Wax.Model.Wix;

    using TomsToolbox.ObservableCollections;
    using TomsToolbox.Wpf;

    [AddINotifyPropertyChangedInterface]
    public class FileMapping
    {
        [NotNull]
        private readonly ProjectOutputGroup _projectOutputGroup;
        [NotNull, ItemNotNull]
        private readonly ObservableCollection<ProjectOutputGroup> _allUnmappedProjectOutputs;
        [NotNull, ItemNotNull]
        private readonly ObservableFilteredCollection<ProjectOutputGroup> _unmappedProjectOutputs;
        [NotNull]
        private readonly WixProject _wixProject;
        [NotNull, ItemNotNull]
        private readonly IList<UnmappedFile> _allUnmappedFiles;
        [NotNull, ItemNotNull]
        private readonly ObservableFilteredCollection<UnmappedFile> _unmappedFiles;

        public FileMapping([NotNull] ProjectOutputGroup projectOutputGroup, [NotNull] ObservableCollection<ProjectOutputGroup> allUnmappedProjectOutputs, [NotNull] WixProject wixProject, [NotNull] IList<UnmappedFile> allUnmappedFiles)
        {
            _projectOutputGroup = projectOutputGroup;
            _allUnmappedProjectOutputs = allUnmappedProjectOutputs;
            _wixProject = wixProject;
            _allUnmappedFiles = allUnmappedFiles;

            Id = wixProject.GetFileId(TargetName);

            MappedNode = wixProject.FileNodes.FirstOrDefault(node => node.Id == Id);

            _unmappedProjectOutputs = new ObservableFilteredCollection<ProjectOutputGroup>(_allUnmappedProjectOutputs, item => string.Equals(item?.FileName, DisplayName, StringComparison.OrdinalIgnoreCase));
            _unmappedProjectOutputs.CollectionChanged += UnmappedProjectOutputs_CollectionChanged;

            _unmappedFiles = new ObservableFilteredCollection<UnmappedFile>(allUnmappedFiles, item => string.Equals(item?.Node.Name, DisplayName, StringComparison.OrdinalIgnoreCase));
            _unmappedFiles.CollectionChanged += UnmappedNodes_CollectionChanged;

            UpdateMappingState();
        }

        [NotNull]
        public string DisplayName => _projectOutputGroup.FileName;

        [NotNull]
        public string Id { get; }

        [NotNull]
        public string UniqueName => _projectOutputGroup.TargetName;

        [NotNull]
        public string Extension => Path.GetExtension(_projectOutputGroup.TargetName);

        [NotNull]
        public string TargetName => _projectOutputGroup.TargetName;

        [NotNull]
        public string SourceName => _projectOutputGroup.SourceName;

        [NotNull]
        public IList<UnmappedFile> UnmappedNodes => _unmappedFiles;

        [NotNull, DoNotNotify]
        public ICommand AddFileCommand => new DelegateCommand<IEnumerable>(_ => CanAddFile(), AddFile);

        [NotNull, DoNotNotify]
        public ICommand ClearMappingCommand => new DelegateCommand<IEnumerable>(_ => CanClearMapping(), ClearMapping);

        [NotNull, DoNotNotify]
        public ICommand ResolveFileCommand => new DelegateCommand<IEnumerable>(_ => CanResolveFile(), ResolveFile);

        [NotNull]
        public Project Project => _projectOutputGroup.ProjectOutputs.First().Project;

        [CanBeNull]
        public WixFileNode MappedNodeSetter
        {
            get => null;
            set
            {
                if (value != null)
                {
                    MappedNode = value;
                }
            }
        }

        [CanBeNull]
        [OnChangedMethod(nameof(OnMappedNodeChanged))]
        public WixFileNode MappedNode { get; set; }

        private void OnMappedNodeChanged([CanBeNull] object oldValue, [CanBeNull] object newValue)
        {
            OnMappedNodeChanged(oldValue as WixFileNode, newValue as WixFileNode);
        }

        [SuppressPropertyChangedWarnings]
        private void OnMappedNodeChanged([CanBeNull] WixFileNode oldValue, [CanBeNull] WixFileNode newValue)
        {
            if (oldValue != null)
            {
                _allUnmappedFiles.Add(new UnmappedFile(oldValue, _allUnmappedFiles));
                _wixProject.UnmapFile(TargetName);
                _allUnmappedProjectOutputs.Add(_projectOutputGroup);
            }

            if (newValue != null)
            {
                var unmappedFile = _allUnmappedFiles.FirstOrDefault(file => Equals(file.Node, newValue));
                _allUnmappedFiles.Remove(unmappedFile);
                _wixProject.MapFile(TargetName, newValue);
                _allUnmappedProjectOutputs.Remove(_projectOutputGroup);
            }

            UpdateMappingState();
        }

        public MappingState MappingState { get; set; }

        private void UnmappedNodes_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            UpdateMappingState();
        }

        private void UnmappedProjectOutputs_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            UpdateMappingState();
        }

        private bool CanAddFile()
        {
            return (MappedNode == null) && !_unmappedFiles.Any();
        }

        private static void AddFile(IEnumerable selectedItems)
        {
            selectedItems.Cast<FileMapping>().ToList().ForEach(fileMapping => fileMapping.AddFile());
        }

        private void AddFile()
        {
            if (CanAddFile())
            {
                MappedNode = _wixProject.AddFileNode(this);
            }
        }

        private bool CanClearMapping()
        {
            return (MappedNode != null) && (!_wixProject.HasDefaultFileId(this));
        }

        private static void ClearMapping(IEnumerable selectedItems)
        {
            selectedItems.Cast<FileMapping>().ToList().ForEach(fileMapping => fileMapping.ClearMapping());
        }

        private void ClearMapping()
        {
            if (CanClearMapping())
            {
                MappedNode = null;
            }
        }

        private bool CanResolveFile()
        {
            return (MappedNode == null) && (_unmappedFiles.Count == 1);
        }

        private static void ResolveFile(IEnumerable selectedItems)
        {
            selectedItems.Cast<FileMapping>().ToList().ForEach(fileMapping => fileMapping.ResolveFile());
        }

        private void ResolveFile()
        {
            if (CanResolveFile())
            {
                MappedNode = _unmappedFiles[0];
            }
        }

        private void UpdateMappingState()
        {
            if (MappedNode != null)
            {
                MappingState = MappingState.Resolved;
                return;
            }

            switch (_unmappedFiles.Count)
            {
                case 0:
                    MappingState = MappingState.Unmapped;
                    return;

                case 1:
                    if (_unmappedProjectOutputs.Count == 1)
                    {
                        MappingState = MappingState.Unique;
                        return;
                    }
                    break;
            }

            MappingState = MappingState.Ambiguous;
        }
    }
}