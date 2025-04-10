using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace OracleSqlLanguageServer
{
    public class OracleSqlAnalyzerImpl : OracleSqlAnalyzer
    {
        public override List<Diagnostic> Analyze(ParseResult parseResult)
        {
            var diagnostics = new List<Diagnostic>();
            
            // Add syntax errors from the parser
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
            
            // If there are syntax errors, don't continue with semantic analysis
            if (parseResult.HasSyntaxErrors)
            {
                return diagnostics;
            }
            
            // Perform semantic analysis using the parse tree
            if (parseResult.ParseTree != null)
            {
                // Create a tree walker
                var walker = new OracleSqlTreeWalker(parseResult, diagnostics);
                
                // Walk the parse tree to collect semantic errors
                ParseTreeWalker.Default.Walk(walker, parseResult.ParseTree);
            }
            
            return diagnostics;
        }
    }
    
    public class OracleSqlTreeWalker : PlSqlParserBaseListener
    {
        private readonly ParseResult _parseResult;
        private readonly List<Diagnostic> _diagnostics;
        private readonly Stack<string> _context = new Stack<string>();
        
        public OracleSqlTreeWalker(ParseResult parseResult, List<Diagnostic> diagnostics)
        {
            _parseResult = parseResult;
            _diagnostics = diagnostics;
        }
        
        public override void EnterUpdate_statement(PlSqlParser.Update_statementContext context)
        {
            _context.Push("UPDATE");
            
            // Check for missing WHERE clause in UPDATE
            if (context.where_clause() == null)
            {
                AddWarning(
                    context.UPDATE().Symbol,
                    "UPDATE statement without WHERE clause can affect all rows in the table",
                    DiagnosticSeverity.Warning
                );
            }
        }
        
        public override void ExitUpdate_statement(PlSqlParser.Update_statementContext context)
        {
            _context.Pop();
        }
        
        public override void EnterDelete_statement(PlSqlParser.Delete_statementContext context)
        {
            _context.Push("DELETE");
            
            // Check for missing WHERE clause in DELETE
            if (context.where_clause() == null)
            {
                AddWarning(
                    context.DELETE().Symbol,
                    "DELETE statement without WHERE clause can delete all rows in the table",
                    DiagnosticSeverity.Warning
                );
            }
        }
        
        public override void ExitDelete_statement(PlSqlParser.Delete_statementContext context)
        {
            _context.Pop();
        }
        
        public override void EnterSelect_statement(PlSqlParser.Select_statementContext context)
        {
            _context.Push("SELECT");
            
            // Check for SELECT * 
            var selectList = context.select_list();
            if (selectList != null)
            {
                foreach (var item in selectList.select_list_item())
                {
                    if (item.GetText() == "*")
                    {
                        AddWarning(
                            item.Start,
                            "Using SELECT * is not recommended in production code",
                            DiagnosticSeverity.Information
                        );
                        break;
                    }
                }
            }
        }
        
        public override void ExitSelect_statement(PlSqlParser.Select_statementContext context)
        {
            _context.Pop();
        }
        
        public override void EnterCreate_procedure_body(PlSqlParser.Create_procedure_bodyContext context)
        {
            // Check procedure naming convention
            var procedureName = context.procedure_name()?.GetText();
            if (procedureName != null && !procedureName.StartsWith("P_", StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(
                    context.procedure_name().Start,
                    "Consider using P_ prefix for procedures (naming convention)",
                    DiagnosticSeverity.Information
                );
            }
        }
        
        public override void EnterCreate_function_body(PlSqlParser.Create_function_bodyContext context)
        {
            // Check function naming convention
            var functionName = context.function_name()?.GetText();
            if (functionName != null && !functionName.StartsWith("F_", StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(
                    context.function_name().Start,
                    "Consider using F_ prefix for functions (naming convention)",
                    DiagnosticSeverity.Information
                );
            }
            
            // Check return type
            var returnType = context.datatype()?.GetText();
            if (returnType != null && returnType.ToUpper().Contains("VARCHAR2") && 
                !context.GetText().ToUpper().Contains("RETURN NULL"))
            {
                AddWarning(
                    context.datatype().Start,
                    "Functions returning VARCHAR2 should handle NULL return values",
                    DiagnosticSeverity.Warning
                );
            }
        }
        
        public override void EnterVariable_declaration(PlSqlParser.Variable_declarationContext context)
        {
            // Check variable naming convention
            var varName = context.variable_name()?.GetText();
            if (varName != null && !varName.StartsWith("v_", StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(
                    context.variable_name().Start,
                    "Consider using v_ prefix for local variables (naming convention)",
                    DiagnosticSeverity.Information
                );
            }
        }
        
        public override void EnterNatural_join(PlSqlParser.Natural_joinContext context)
        {
            // Warn about NATURAL JOIN usage
            AddWarning(
                context.Start,
                "NATURAL JOIN is not recommended - use explicit join conditions",
                DiagnosticSeverity.Warning
            );
        }
        
        private void AddWarning(IToken token, string message, DiagnosticSeverity severity)
        {
            if (token == null) return;
            
            _diagnostics.Add(new Diagnostic
            {
                Range = new Range
                {
                    Start = new Position { Line = token.Line - 1, Character = token.Column },
                    End = new Position { Line = token.Line - 1, Character = token.Column + token.Text.Length }
                },
                Message = message,
                Severity = severity,
                Source = "OracleSqlLS"
            });
        }
    }
    
    // Extending the ParseResult class
    public partial class ParseResult
    {
        public IParseTree ParseTree { get; set; }
        public PlSqlParser Parser { get; set; }
        public CommonTokenStream TokenStream { get; set; }
    }
}
