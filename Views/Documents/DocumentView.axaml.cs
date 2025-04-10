using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using snakeql.ViewModels.Documents;
using TextMateSharp.Grammars;
using Pair = System.Collections.Generic.KeyValuePair<int, Avalonia.Controls.Control>;

namespace snakeql.Views.Documents
{
    public partial class DocumentView : UserControl
    {
        private TextEditor _textEditor;
        private ElementGenerator _generator = new ElementGenerator();
        private RegistryOptions _registryOptions;
        private int _currentTheme = (int)ThemeName.DarkPlus;
        private TextMate.Installation _textMateInstallation;
        private TextBlock _statusTextBlock;

        private CustomMargin _customMargin;

        public DocumentView()
        {
            Console.WriteLine("DocumentView ctr");

            InitializeComponent();

            initAvalonEdit();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void initAvalonEdit()
        {
            _textEditor = this.FindControl<TextEditor>("Editor");
            _textEditor.HorizontalScrollBarVisibility = Avalonia
                .Controls
                .Primitives
                .ScrollBarVisibility
                .Visible;
            _textEditor.Background = Brushes.Transparent;
            _textEditor.ShowLineNumbers = true;
            _textEditor.TextArea.Background = this.Background;
            /* _textEditor.TextArea.TextEntered += textEditor_TextArea_TextEntered;
            _textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering; */
            _textEditor.Options.AllowToggleOverstrikeMode = true;
            _textEditor.Options.EnableTextDragDrop = true;
            _textEditor.Options.ShowBoxForControlCharacters = true;
            _textEditor.Options.ColumnRulerPositions = new List<int>() { 80, 100 };
            //_textEditor.TextArea.IndentationStrategy = new Indentation.CSharp.CSharpIndentationStrategy(_textEditor.Options);
            //_textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            _textEditor.TextArea.RightClickMovesCaret = true;
            _textEditor.Options.HighlightCurrentLine = true;

            _textEditor.TextArea.TextView.ElementGenerators.Add(_generator);

            _registryOptions = new RegistryOptions((ThemeName)_currentTheme);

            _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);

            //_textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme;

            Language csharpLanguage = _registryOptions.GetLanguageByExtension(".cs");

            string scopeName = _registryOptions.GetScopeByLanguageId(csharpLanguage.Id);

            _textEditor.Document = new TextDocument(
                "// AvaloniaEdit supports displaying control chars: \a or \b or \v"
                    + Environment.NewLine
                    + "// AvaloniaEdit supports displaying underline and strikethrough"
                    + Environment.NewLine
                    + @"
using System.IO;
using System.Reflection;

namespace AvaloniaEdit.Demo.Resources{

    public class ResourceLoader{}
}"
            );
            _textMateInstallation.SetGrammar(
                _registryOptions.GetScopeByLanguageId(csharpLanguage.Id)
            );
            //        _textEditor.TextArea.TextView.LineTransformers.Add(new UnderlineAndStrikeThroughTransformer());

            

            _statusTextBlock = this.Find<TextBlock>("StatusText");

            //CTRL + mouse wheel  =>  font size
            this.AddHandler(
                PointerWheelChangedEvent,
                (o, i) =>
                {
                    if (i.KeyModifiers != KeyModifiers.Control)
                        return;
                    if (i.Delta.Y > 0)
                        _textEditor.FontSize++;
                    else
                        _textEditor.FontSize =
                            _textEditor.FontSize > 1 ? _textEditor.FontSize - 1 : 1;
                },
                RoutingStrategies.Bubble,
                true
            );

            // Add a custom margin at the left of the text area, which can be clicked.
            _customMargin = new CustomMargin();
            _textEditor.TextArea.LeftMargins.Insert(0, _customMargin);

            //cambiare tema - current theme nel viewmodel
            var documentVM = new DocumentViewModel();
            /* var mainWindowVM = new MainWindowViewModel(_textMateInstallation, _registryOptions);
                foreach (ThemeName themeName in Enum.GetValues<ThemeName>())
                {
                    var themeViewModel = new ThemeViewModel(themeName);
                    mainWindowVM.AllThemes.Add(themeViewModel);
                    if (themeName == ThemeName.DarkPlus)
                    {
                        mainWindowVM.SelectedTheme = themeViewModel;
                    }
                } */

            this.DataContext = documentVM;
        }
    }

    class ElementGenerator : VisualLineElementGenerator, IComparer<Pair>
    {
        public List<Pair> controls = new List<Pair>();

        /// <summary>
        /// Gets the first interested offset using binary search
        /// </summary>
        /// <returns>The first interested offset.</returns>
        /// <param name="startOffset">Start offset.</param>
        public override int GetFirstInterestedOffset(int startOffset)
        {
            int pos = controls.BinarySearch(new Pair(startOffset, null), this);
            if (pos < 0)
                pos = ~pos;
            if (pos < controls.Count)
                return controls[pos].Key;
            else
                return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            int pos = controls.BinarySearch(new Pair(offset, null), this);
            if (pos >= 0)
                return new InlineObjectElement(0, controls[pos].Value);
            else
                return null;
        }

        int IComparer<Pair>.Compare(Pair x, Pair y)
        {
            return x.Key.CompareTo(y.Key);
        }
    }
}

