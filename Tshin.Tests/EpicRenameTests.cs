using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Tshin.Models;
using Tshin.Services;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Editable epic title: observable summary + editor → sidebar propagation.</summary>
public class EpicRenameTests
{
    [AvaloniaFact]
    public void Project_summary_name_is_observable()
    {
        var summary = new ProjectSummary { Id = "1", Name = "Before" };
        var changed = new List<string?>();
        summary.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        summary.Name = "After";

        Assert.Contains(nameof(ProjectSummary.Name), changed);
        Assert.Equal("After", summary.Name);
    }

    [AvaloniaFact]
    public void Editing_project_name_marks_the_editor_dirty()
    {
        var editor = TestFactory.Editor("Original");
        Assert.False(editor.IsDirty);

        editor.ProjectName = "Renamed";

        Assert.True(editor.IsDirty);
    }

    [AvaloniaFact]
    public async Task Renaming_in_the_editor_updates_the_sidebar_summary()
    {
        var service = new MockProjectService();
        var created = await service.CreateProjectAsync("Untitled Epic");

        var main = new MainWindowViewModel(service);
        main.SelectedProject = main.Projects.Single(p => p.Id == created.Id);
        Assert.NotNull(main.CurrentEditor);

        main.CurrentEditor!.ProjectName = "The Great Saga";

        Assert.Equal("The Great Saga", main.SelectedProject!.Name);
    }
}
