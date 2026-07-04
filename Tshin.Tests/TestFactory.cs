using System;
using System.Collections.ObjectModel;
using Tshin.Models;
using Tshin.Services;
using Tshin.ViewModels;

namespace Tshin.Tests;

/// <summary>Builders that spin up editor/player view models from scratch for tests.</summary>
internal static class TestFactory
{
    public static EditorViewModel Editor(string name = "My Epic")
        => new(new StorySnapshot { ProjectId = "test" }, name, new MockProjectService());

    /// <summary>Adds a choice to a node and returns it.</summary>
    public static ChoiceViewModel AddChoice(EditorViewModel editor, NodeViewModel node)
    {
        editor.AddChoiceCommand.Execute(node);
        return node.Choices[^1];
    }

    /// <summary>Adds a command to a choice and returns it.</summary>
    public static CommandViewModel AddCommand(EditorViewModel editor, ChoiceViewModel choice)
    {
        editor.AddCommandToChoiceCommand.Execute(choice);
        return choice.Commands[^1];
    }

    public static ObservableCollection<EntityViewModel> Entities(EditorViewModel editor) => editor.Entities;
}
