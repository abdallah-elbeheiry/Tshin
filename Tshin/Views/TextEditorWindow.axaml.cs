using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Tshin.Behaviors;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;
using Tshin.Utilities;
using Tshin.ViewModels;

namespace Tshin.Views;

public partial class TextEditorWindow : Window
{
    private readonly ObservableCollection<EntityViewModel> _entities;
    private EntityManager? _entityManager;
    private bool _closing;

    public TextEditorWindow(string title, string initialText,
        ObservableCollection<EntityViewModel> entities)
    {
        InitializeComponent();
        Title = title;
        _entities = entities;

        EditorTextBox.Text = initialText;
        EditorTextBox.TextChanged += OnEditorTextChanged;

        // Build completion source from the same entity data so the auto-complete
        // popup works inside the full-screen editor.
        var completionSource = entities.Select(e => new EntityCompletionEntry
        {
            EntityName = e.Name,
            EntityId = e.Id,
            ComponentNames = e.Components.Select(c => c.Name).ToList()
        }).ToList();
        EditorTextBox.SetValue(TextBoxCompletionBehavior.CompletionSourceProperty,
            completionSource);

        UpdatePreview();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_closing) return;
        _closing = true;
        e.Cancel = true;
        Dispatcher.UIThread.Post(() => Close(EditorTextBox.Text));
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var text = EditorTextBox.Text ?? string.Empty;

        // Lazily build EntityManager once; only rebuild if entities change.
        _entityManager ??= BuildEntityManager();

        var resolved = VariableInterpolator.Interpolate(text, _entityManager);
        BbCodeProperties.SetBbCodeText(PreviewTextBlock, resolved);
    }

    private EntityManager BuildEntityManager()
    {
        var em = new EntityManager();
        foreach (var evm in _entities)
        {
            var entity = em.CreateEntity(Guid.Parse(evm.Id));
            entity.Name = evm.Name;
            entity.X = evm.X;
            entity.Y = evm.Y;
            entity.Visible = evm.Visible;

            foreach (var cvm in evm.Components)
            {
                switch (cvm)
                {
                    case NumberComponentViewModel n:
                        em.SetComponent(entity, new NumberComponent
                        {
                            Name = n.Name,
                            Value = n.Value,
                            MinValue = n.MinValue,
                            MaxValue = n.MaxValue,
                            Visible = n.Visible
                        });
                        break;
                    case TextComponentViewModel t:
                        em.SetComponent(entity, new TextComponent
                        {
                            Name = t.Name,
                            Value = t.Value,
                            Visible = t.Visible
                        });
                        break;
                    case ConditionComponentViewModel c:
                        em.SetComponent(entity, new ConditionComponent
                        {
                            Name = c.Name,
                            Value = c.Value,
                            Visible = c.Visible
                        });
                        break;
                }
            }
        }
        return em;
    }

}
