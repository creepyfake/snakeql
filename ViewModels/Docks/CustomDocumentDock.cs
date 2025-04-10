using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using snakeql.ViewModels.Documents;

namespace snakeql.ViewModels.Docks;

public class CustomDocumentDock : DocumentDock
{
    public CustomDocumentDock()
    {
        CreateDocument = new RelayCommand(CreateNewDocument);
    }

    private void CreateNewDocument()
    {
        if (!CanCreateDocument)
        {
            return;
        }

        var index = VisibleDockables?.Count + 1;
        var document = new DocumentViewModel
        {
            Id = $"Document{index}",
            Title = $"Document{index}",
        };

        Factory?.AddDockable(this, document);
        Factory?.SetActiveDockable(document);
        Factory?.SetFocusedDockable(this, document);
    }
}
