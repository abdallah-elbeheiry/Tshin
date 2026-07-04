using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tshin.Models;
using Tshin.Services;

namespace Tshin.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly List<ProjectSummary> _allProjects = new();

    /// <summary>Projects shown in the sidebar (after applying the search filter).</summary>
    public ObservableCollection<ProjectSummary> Projects { get; } = new();

    [ObservableProperty]
    private ProjectSummary? _selectedProject;

    [ObservableProperty]
    private EditorViewModel? _currentEditor;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>True while no project is open — drives the right-pane empty state.</summary>
    public bool HasEditor => CurrentEditor is not null;

    /// <summary>Set by the view to let the VM ask for a file to import.</summary>
    public Func<Task<string?>>? ImportFileRequest { get; set; }

    public MainWindowViewModel() : this(new MockProjectService()) { }

    public MainWindowViewModel(IProjectService projectService)
    {
        _projectService = projectService;
        _ = LoadProjectsAsync();
    }

    private async Task LoadProjectsAsync(string? selectId = null)
    {
        var projects = await _projectService.GetProjectsAsync();
        _allProjects.Clear();
        _allProjects.AddRange(projects);
        ApplyFilter();

        if (selectId is not null)
            SelectedProject = Projects.FirstOrDefault(p => p.Id == selectId);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = SearchText?.Trim();
        IEnumerable<ProjectSummary> matches = _allProjects;
        if (!string.IsNullOrEmpty(filter))
            matches = matches.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        Projects.Clear();
        foreach (var p in matches)
            Projects.Add(p);
    }

    async partial void OnSelectedProjectChanged(ProjectSummary? value)
    {
        if (value is null)
        {
            CurrentEditor = null;
            return;
        }

        var snapshot = await _projectService.OpenProjectAsync(value.Id);
        CurrentEditor = new EditorViewModel(snapshot, value.Name, _projectService);
    }

    partial void OnCurrentEditorChanged(EditorViewModel? oldValue, EditorViewModel? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnEditorPropertyChanged;
        if (newValue is not null) newValue.PropertyChanged += OnEditorPropertyChanged;
        OnPropertyChanged(nameof(HasEditor));
    }

    /// <summary>Propagate an epic rename from the editor toolbar back to the sidebar summary.</summary>
    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorViewModel.ProjectName)) return;
        if (CurrentEditor is null || SelectedProject is null) return;
        SelectedProject.Name = CurrentEditor.ProjectName;
    }

    [RelayCommand]
    private async Task NewProject()
    {
        var summary = await _projectService.CreateProjectAsync("Untitled Epic");
        await LoadProjectsAsync(summary.Id);
    }

    [RelayCommand]
    private async Task Import()
    {
        if (ImportFileRequest is null) return;
        var path = await ImportFileRequest();
        if (!string.IsNullOrEmpty(path))
            await ImportFromPathAsync(path);
    }

    /// <summary>Imports a dropped/downloaded file and selects the new project.</summary>
    public async Task ImportFromPathAsync(string filePath)
    {
        var summary = await _projectService.ImportProjectAsync(filePath);
        await LoadProjectsAsync(summary.Id);
    }
}
