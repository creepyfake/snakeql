using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Dock.Model.Mvvm.Controls;
using TextMateSharp.Grammars;

namespace snakeql.ViewModels.Documents;

public class DocumentViewModel : Document
{
    public string Code { get; set; }

    public DocumentViewModel()
    {
        Code = "SELECT 1 FROM DUAL;";
    }

    public void CopyMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Copy.Execute(null, textArea);
    }

    public void CutMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Cut.Execute(null, textArea);
    }

    public void PasteMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Paste.Execute(null, textArea);
    }

    public void SelectAllMouseCommand(TextArea textArea)
    {
        ApplicationCommands.SelectAll.Execute(null, textArea);
    }

    // Undo Status is not given back to disable it's item in ContextFlyout; therefore it's not being used yet.
    public void UndoMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Undo.Execute(null, textArea);
    }
}
