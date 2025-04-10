using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace OracleSqlLanguageServer
{
    public class OracleSqlParserImpl : OracleSqlParser
    {
        public override ParseResult Parse(string text)
        {
            var result = new ParseResult { OriginalText = text };
            
            try
            {
                // Create the lexer and parser
                var inputStream = new AntlrInputStream(text);
                var lexer = new PlSqlLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new PlSqlParser(tokenStream);
                
                // Add error listener to collect syntax errors
                var errorListener = new SyntaxErrorListener();
                parser.RemoveErrorListeners();
                parser.AddErrorListener(errorListener);
                
                // Parse the input
                IParseTree tree = parser.sql_script();
                
                // Store syntax errors in the result
                result.HasSyntaxErrors = errorListener.Errors.Count > 0;
                result.SyntaxErrors = errorListener.Errors;
                
                // Store the parse tree for further analysis
                result.ParseTree = tree;
                result.Parser = parser;
                result.TokenStream = tokenStream;
            }
            catch (Exception ex)
            {
                // Handle parsing exceptions
                result.HasSyntaxErrors = true;
                result.SyntaxErrors.Add(new SyntaxError
                {
                    Message = $"Parser error: {ex.Message}",
                    Line = 0,
                    Column = 0
                });
            }
            
            return result;
        }
        
        public override CompletionList GetCompletions(string text, Position position, string currentLine)
        {
            var items = new List<CompletionItem>();
            
            // Basic completion for Oracle SQL
            var parseResult = Parse(text);
            
            // Add context-aware completions based on cursor position
            if (IsAfterKeyword("FROM", currentLine) || IsAfterKeyword("JOIN", currentLine))
            {
                // Add table suggestions (in a real implementation, these would come from schema)
                items.Add(new CompletionItem { Label = "EMPLOYEES", Kind = CompletionItemKind.Class });
                items.Add(new CompletionItem { Label = "DEPARTMENTS", Kind = CompletionItemKind.Class });
                items.Add(new CompletionItem { Label = "CUSTOMERS", Kind = CompletionItemKind.Class });
            }
            else if (IsAfterKeyword("SELECT", currentLine) || IsAfterComma(currentLine, position))
            {
                // Add column suggestions
                items.Add(new CompletionItem { 
                    Label = "employee_id", 
                    Kind = CompletionItemKind.Field,
                    Detail = "NUMBER",
                    Documentation = "Primary key for the EMPLOYEES table"
                });
                items.Add(new CompletionItem { 
                    Label = "first_name", 
                    Kind = CompletionItemKind.Field,
                    Detail = "VARCHAR2(50)"
                });
            }
            else if (IsInPLSQLBlock(parseResult, position))
            {
                // Add PLSQL specific completions
                items.Add(new CompletionItem { Label = "IF", Kind = CompletionItemKind.Keyword });
                items.Add(new CompletionItem { Label = "LOOP", Kind = CompletionItemKind.Keyword });
                items.Add(new CompletionItem { Label = "WHILE", Kind = CompletionItemKind.Keyword });
                items.Add(new CompletionItem { Label = "FOR", Kind = CompletionItemKind.Keyword });
                items.Add(new CompletionItem { Label = "EXCEPTION", Kind = CompletionItemKind.Keyword });
            }
            
            // Always include common Oracle SQL keywords
            items.Add(new CompletionItem { Label = "SELECT", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "FROM", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "WHERE", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "ORDER BY", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "GROUP BY", Kind = CompletionItemKind.Keyword });
            items.Add(new CompletionItem { Label = "HAVING", Kind = CompletionItemKind.Keyword });
            
            // Oracle specific SQL functions
            items.Add(new CompletionItem { 
                Label = "NVL", 
                Kind = CompletionItemKind.Function,
                Detail = "NVL(expr1, expr2)",
                Documentation = "Returns expr2 if expr1 is null, otherwise returns expr1"
            });
            items.Add(new CompletionItem { 
                Label = "DECODE", 
                Kind = CompletionItemKind.Function,
                Detail = "DECODE(expr, search1, result1, search2, result2, ..., default)",
                Documentation = "Compares expr to each search value one by one. If expr equals a search, returns the corresponding result."
            });
            
            return new CompletionList { IsIncomplete = false, Items = items.ToArray() };
        }
        
        public override Hover GetHoverInfo(string text, Position position)
        {
            // Parse the document
            var parseResult = Parse(text);
            
            // Get token at position
            var tokenAtPosition = GetTokenAtPosition(parseResult.TokenStream, position);
            if (tokenAtPosition == null)
            {
                return new Hover();
            }
            
            string hoverContent = string.Empty;
            
            // Provide hover info based on token type
            switch (tokenAtPosition.Type)
            {
                case PlSqlParser.K_SELECT:
                    hoverContent = "**SELECT**\n\nRetrieve data from a database table.";
                    break;
                case PlSqlParser.K_FROM:
                    hoverContent = "**FROM**\n\nSpecify the table(s) to query.";
                    break;
                case PlSqlParser.K_WHERE:
                    hoverContent = "**WHERE**\n\nFilter records based on a condition.";
                    break;
                case PlSqlParser.K_PROCEDURE:
                    hoverContent = "**PROCEDURE**\n\nDefine a PL/SQL procedure.";
                    break;
                case PlSqlParser.K_FUNCTION:
                    hoverContent = "**FUNCTION**\n\nDefine a PL/SQL function that returns a value.";
                    break;
                case PlSqlParser.K_BEGIN:
                    hoverContent = "**BEGIN**\n\nStart of a PL/SQL block.";
                    break;
                case PlSqlParser.K_END:
                    hoverContent = "**END**\n\nEnd of a PL/SQL block.";
                    break;
                case PlSqlParser.IDENTIFIER:
                    // In a real implementation, look up identifiers in schema
                    hoverContent = $"**{tokenAtPosition.Text}**\n\nObject identifier";
                    break;
                default:
                    hoverContent = tokenAtPosition.Text;
                    break;
            }
            
            return new Hover
            {
                Contents = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = hoverContent
                },
                Range = new Range
                {
                    Start = new Position { Line = tokenAtPosition.Line - 1, Character = tokenAtPosition.Column },
                    End = new Position { Line = tokenAtPosition.Line - 1, Character = tokenAtPosition.Column + tokenAtPosition.Text.Length }
                }
            };
        }
        
        private bool IsAfterKeyword(string keyword, string line)
        {
            return line.ToUpper().TrimEnd().EndsWith(keyword + " ", StringComparison.OrdinalIgnoreCase);
        }
        
        private bool IsAfterComma(string line, Position position)
        {
            int lastCommaPos = line.LastIndexOf(',', position.Character);
            return lastCommaPos >= 0 && line.Substring(lastCommaPos + 1, position.Character - lastCommaPos - 1).Trim().Length == 0;
        }
        
        private bool IsInPLSQLBlock(ParseResult parseResult, Position position)
        {
            // A real implementation would analyze the parse tree to determine if
            // the cursor is within a PLSQL block (between BEGIN and END)
            return false;
        }
        
        private IToken GetTokenAtPosition(CommonTokenStream tokenStream, Position position)
        {
            // Convert LSP position (0-based) to ANTLR position (1-based)
            int line = position.Line + 1;
            int column = position.Character;
            
            foreach (var token in tokenStream.GetTokens())
            {
                if (token.Line == line && 
                    column >= token.Column && 
                    column <= token.Column + token.Text.Length)
                {
                    return token;
                }
            }
            
            return null;
        }
    }
    
    public class SyntaxErrorListener : IAntlrErrorListener<IToken>
    {
        public List<SyntaxError> Errors { get; } = new List<SyntaxError>();
        
        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add(new SyntaxError 
            { 
                Message = msg, 
                Line = line - 1,  // Convert to 0-based indexing for LSP
                Column = charPositionInLine 
            });
        }
    }
}
