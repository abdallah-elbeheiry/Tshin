using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace Tshin.ViewModels;

/// <summary>
/// Entity/component removal commands (partial of <see cref="EditorViewModel"/>).
/// </summary>
public partial class EditorViewModel
{
    [RelayCommand]
    private void RemoveComponentFromEntity(ComponentViewModel? component)
    {
        if (component is null) return;
        // Find the entity that owns this component
        foreach (var entity in Entities)
        {
            if (entity.Components.Remove(component))
            {
                if (SelectedComponent == component) SelectedComponent = null;
                MarkDirty();
                return;
            }
        }
    }

    [RelayCommand]
    private void RemoveEntity(EntityViewModel? entity)
    {
        entity ??= SelectedEntity;
        if (entity is null) return;
        Entities.Remove(entity);
        if (SelectedEntity == entity) SelectedEntity = null;
        RefreshAvailableEntitiesOnChoices();
        MarkDirty();
    }
}