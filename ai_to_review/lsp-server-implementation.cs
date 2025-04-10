using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace OracleSqlLanguageServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            var server = new OracleSqlLanguageServer();
            var messageHandler = new HeaderDelimitedMessageHandler(Console.OpenStandardInput(), Console.OpenStandardOutput());
            var jsonRpc = new JsonRpc(messageHandler);
            
            jsonRpc.AddLocalRpcTarget(server);
            jsonRpc.StartListening();
            
            await jsonRpc.Completion;
        }
    }

    class OracleSqlLanguageServer
    {
        private readonly Dictionary<string, string> _documents = new Dictionary<string, string>();
        private readonly OracleSqlParser _parser = new OracleSqlParser();
        private readonly OracleSqlAnalyzer _analyzer = new OracleSqlAnalyzer();

        [JsonRpcMethod(Methods.InitializeName)]
        public InitializeResult Initialize(InitializeParams @params)
        {
            return new InitializeResult
            {
                Capabilities = new ServerCapabilities
                {
                    TextDocumentSync = new TextDocumentSyncOptions
                    {
                        OpenClose = true,
                        Change = TextDocumentSyncKind.Full
                    },
                    DiagnosticProvider = new DiagnosticRegistrationOptions
                    {
                        InterFileDependencies = false,
                        WorkspaceDiagnostics = false
                    },
                    CompletionProvider = new CompletionOptions
                    {
                        TriggerCharacters = new[] { ".", " " }
                    },
                    HoverProvider = true
                }
            };
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void TextDocumentDidOpen(DidOpenTextDocumentParams @params)
        {
            var document = @params.TextDocument;
            _documents[document.Uri] = document.Text;
            
            AnalyzeDocument(document.Uri, document.Text);
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void TextDocumentDidChange(DidChangeTextDocumentParams @params)
        {
            var document = @params.TextDocument;
            var changes = @params.ContentChanges;
            
            if (changes.Count > 0)
            {
                _documents[document.Uri] = changes[0].Text;
                AnalyzeDocument(document.Uri, changes[0].Text);
            }
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void TextDocumentDidClose(DidCloseTextDocumentParams @params)
        {
            _documents.Remove(@params.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList TextDocumentCompletion(CompletionParams @params)
        {
            var uri = @params.TextDocument.Uri;
            if (!_documents.TryGetValue(uri, out var text))
            {
                return new CompletionList();
            }

            // Get context for completion
            var position = @params.Position;
            var line = GetLine(text, position.Line);
            
            // Provide completions based on Oracle SQL/PLSQL
            return _parser.GetCompletions(text, position, line);
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover TextDocumentHover(TextDocumentPositionParams @params)
        {
            var uri = @params.TextDocument.Uri;
            if (!_documents.TryGetValue(uri, out var text))
            {
                return new Hover();
            }

            var position = @params.Position;
            return _parser.GetHoverInfo(text, position);
        }

        private void AnalyzeDocument(string uri, string text)
        {
            // Parse the document
            var parseResult = _parser.Parse(text);
            
            // Analyze for errors and warnings
            var diagnostics = _analyzer.Analyze(parseResult);
            
            // Publish diagnostics
            PublishDiagnostics(uri, diagnostics);
        }

        private void PublishDiagnostics(string uri, List<Diagnostic> diagnostics)
        {
            // In a real implementation, you would send this via JsonRpc
            var diagnosticsParams = new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = diagnostics.ToArray()
            };
            
            // This would be sent via the JsonRpc instance
            // _jsonRpc.NotifyAsync(Methods.TextDocumentPublishDiagnosticsName, diagnosticsParams);
        }

        private string GetLine(string text, int lineNumber)
        {
            var lines = text.Split('\n');
            return lineNumber < lines.Length ? lines[lineNumber] : string.Empty;
        }
    }

    // Oracle SQL Parser class (placeholder)
    class OracleSqlParser
    {
        public ParseResult Parse(string text)
        {
            // In a real implementation, this would use ANTLR or another parsing library
            // to parse the Oracle SQL/PLSQL code
            return new ParseResult { OriginalText = text };
        }

        public CompletionList GetCompletions(string text, Position position, string currentLine)
        {
            // This would analyze the context and provide relevant Oracle SQL completions
            var items = new List<CompletionItem>();
            
            // Add common Oracle SQL keywords
            items.Add(new CompletionItem { Label = "SELECT", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "FROM", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "WHERE", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "JOIN", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "BEGIN", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "END", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "PROCEDURE", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "FUNCTION", Kind = CompletionItemKind.Keyword });
            
            // Add Oracle-specific functions
            items.Add(new CompletionItem { Label = "NVL", Kind = CompletionItemKind.Function });
            items.Add(new CompletionItem { Label = "DECODE", Kind = CompletionItemKind.Function });
            items.Add(new CompletionItem { Label = "TO_DATE", Kind = CompletionItemKind.Function });
            items.Add(new CompletionItem { Label = "TO_CHAR", Kind = CompletionItemKind.Function });
            
            return new CompletionList { IsIncomplete = false, Items = items.ToArray() };
        }

        public Hover GetHoverInfo(string text, Position position)
        {
            // In a real implementation, this would provide context-specific hover information
            // about Oracle SQL keywords, functions, etc.
            return new Hover
            {
                Contents = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Oracle SQL/PLSQL Help"
                }
            };
        }
    }

    // Simple parse result class (placeholder)
    class ParseResult
    {
        public string OriginalText { get; set; }
        public bool HasSyntaxErrors { get; set; }
        public List<SyntaxError> SyntaxErrors { get; set; } = new List<SyntaxError>();
    }

    // Syntax error class (placeholder)
    class SyntaxError
    {
        public string Message { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    // Oracle SQL Analyzer (placeholder)
    class OracleSqlAnalyzer
    {
        public List<Diagnostic> Analyze(ParseResult parseResult)
        {
            var diagnostics = new List<Diagnostic>();
            
            // Convert syntax errors to diagnostics
            foreach (var error in parseResult.SyntaxErrors)
            {
                diagnostics.Add(new Diagnostic
                {
                    Message = error.Message,
                    Range = new Range
                    {
                        Start = new Position { Line = error.Line, Character = error.Column },
                        End = new Position { Line = error.Line, Character = error.Column + 1 }
                    },
                    Severity = DiagnosticSeverity.Error,
                    Source = "OracleSqlLS"
                });
            }
            
            // In a real implementation, add linting rules here
            // Check for:
            // - Missing WHERE clauses in UPDATE/DELETE
            // - Oracle specific best practices
            // - PLSQL block structure
            
            return diagnostics;
        }
    }
}
