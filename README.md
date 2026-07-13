# Tshin

**An interactive fiction / branching narrative editor.** Tshin is a cross-platform desktop application for creating, editing, and playing branching-story games (choose-your-own-adventure style). It provides a visual node-graph editor where each node is a story beat, and choices lead from one node to another with conditional logic and state mutations.

---

## Project Overview

| Aspect           | Detail                                                |
|------------------|-------------------------------------------------------|
| **Language**     | C#                                                    |
| **Runtime**      | .NET 10.0                                             |
| **UI Framework** | Avalonia UI (cross-platform desktop)                  |
| **Architecture** | MVVM with CommunityToolkit.Mvvm                       |
| **Testing**      | xUnit v3 + Avalonia.Headless                          |
| **File Format**  | `.tshin` (custom text-based script)                   |
| **Platforms**    | Windows x64, Linux x64, macOS (Intel + Apple Silicon) |

---

## Key Features

### Visual Node Graph Editor
- **Infinite canvas** with pan (click-drag background) and zoom (scroll wheel or toolbar buttons).
- **Node cards** representing story beats with editable display text.
- **Entity cards** representing game entities (player, NPCs, items, etc.) with editable components.
- **Drag-to-connect wires**: click an output pin on a choice and drag to an input pin on another node to link story paths.
- **Alternating wire colors** (7 distinct hues) for easy visual distinction of connections.
- **Snap-to-grid** support for precise node/entity placement.
- **Zoom to fit** frames all nodes and entities in the viewport.

### Entity Component System (ECS)
- **Entities** are named containers on the canvas that hold components.
- **Components** are typed data holders:
  - **Number Component**: A numeric value with configurable min/max range.
  - **Text Component**: A string value.
  - **Condition Component**: A boolean value.
- Components can be marked visible/hidden at runtime.
- The player sees visible entity state in a collapsible panel during play.

### Conditional Branching
- **Choices** on each node link to target nodes (or are terminal).
- **Condition trees** gate whether a choice is available:
  - **Atomic conditions**: Compare an entity's component against a value (=, !=, >, >=, <, <= for numbers; =, != for text and booleans).
  - **Logical groups**: AND/OR combinators that nest conditions recursively.
  - **False behavior**: Close (disabled but visible) or Hide (removed entirely).

### Commands / State Mutations
Each choice can carry a list of **commands** that execute when the choice is selected:
- **Set**: Overwrite a component value.
- **Increase / Reduce**: Adjust a numeric component (clamped to min/max).
- Commands can also have their own optional condition trees.

### Play Mode
- Launch a play-through from any node to walk the graph.
- Entities are cloned so play mutations do not affect editor state.
- Visible entities are shown in a collapsible panel with live component values.
- "The End" state when reaching a node with no choices; restart is available.

### Persistence
- **Custom .tshin file format**: a human-readable text-based script format that serializes entities, components, nodes, choices, conditions, and commands.
- **Project index** (index.json) stored in the user's app data directory.
- **Import/Export**: Load .tshin files from disk, export to any location.
- **Auto-save** tracking with an "Edited" indicator in the toolbar.
- **Drag-and-drop** file import onto the main window.

### Undo/Redo
- Full-graph snapshot-based undo/redo.
- Continuous edits (dragging, typing) are coalesced into a single undo step.
- Selection is preserved across undo/redo operations.

### Keyboard Shortcuts
| Shortcut                    | Action               |
|-----------------------------|----------------------|
| Ctrl/Cmd + S                | Save                 |
| Ctrl/Cmd + R                | Run (Play)           |
| Ctrl/Cmd + Z                | Undo                 |
| Ctrl/Cmd + Shift+Z / Ctrl+Y | Redo                 |
| Delete / Backspace          | Delete selected item |

### Theming
- **Always-dark canvas** with a dot-grid background for consistent appearance.
- **System-aware chrome** (sidebar, toolbar, inspector) follows the OS light/dark theme.
- macOS extended client area with custom traffic-light drag strip.
- Acrylic blur effects on supported platforms (graceful fallback).

### Testing
- Comprehensive xUnit test suite with Avalonia headless rendering tests.
- Tests cover: file parsing (round-trips), undo/redo, editor connections, selections, component/command/condition editing, infinite canvas, themes, wire colors, and more.

---

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or later)
- An IDE: JetBrains Rider, Visual Studio 2022+, or VS Code with C# extension.

---

## How to Build and Run

### From the command line:

```bash
# Navigate to the solution directory
cd Tshin

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project Tshin/Tshin.csproj

# Run the tests
dotnet test
```

### From an IDE:

1. Open `Tshin.sln` in your IDE.
2. Set `Tshin` as the startup project.
3. Press F5 (or the equivalent) to build and run.

---

## Publishing for Distribution

The CI workflow (`.github/workflows/release.yml`) publishes self-contained single-file executables for all target platforms:

```bash
# Windows x64
dotnet publish Tshin/Tshin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Linux x64
dotnet publish Tshin/Tshin.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# macOS Intel
dotnet publish Tshin/Tshin.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# macOS Apple Silicon
dotnet publish Tshin/Tshin.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

---

## Dependencies

### Tshin (UI Application)
| Package                              | Version | Purpose                        |
|--------------------------------------|---------|--------------------------------|
| Avalonia                             | 12.0.4  | Cross-platform UI framework    |
| Avalonia.Desktop                     | 12.0.4  | Desktop integration            |
| Avalonia.Themes.Fluent               | 12.0.4  | Fluent design theme            |
| Avalonia.Fonts.Inter                 | 12.0.4  | Inter font                     |
| AvaloniaUI.DiagnosticsSupport        | 2.2.1   | Debug-time diagnostics         |
| CommunityToolkit.Mvvm                | 8.4.1   | MVVM source generators         |

### Tshin.Core (Domain Layer)
- No external dependencies (pure .NET).

### Tshin.Tests (Test Project)
| Package                   | Version | Purpose                               |
|---------------------------|---------|---------------------------------------|
| Microsoft.NET.Test.Sdk    | 17.12.0 | Test runner SDK                       |
| xunit.v3                  | 3.2.2   | Unit testing framework                |
| xunit.runner.visualstudio | 3.1.0   | Visual Studio test adapter            |
| Avalonia.Headless.XUnit   | 12.0.4  | Headless Avalonia rendering for tests |

---

## Architecture Notes

- **MVVM with CommunityToolkit.Mvvm**: ViewModels use [ObservableProperty] and [RelayCommand] source generators for concise, maintainable code.
- **Editor Context pattern**: Child ViewModels (Node, Choice, Command, Condition) receive an IEditorContext reference instead of reaching up to the editor directly, enabling clean separation and safe no-op contexts for play-mode clones.
- **Snapshot-based state management**: The editor operates on a mutable graph of ViewModels. Undo/redo and persistence are handled by capturing/restoring StorySnapshot objects (plain data transfer objects).
- **Infinite canvas technique**: World coordinates are biased by a large constant (CanvasBias = 100,000) so cards always sit inside the Avalonia Canvas bounds and are never culled, regardless of drag direction. The render transform cancels the bias visually.
- **Dot-grid background**: A VisualBrush tiles a single cell containing a dot, with the phase adjusted to follow the pan offset, providing an effectively infinite grid.

---
