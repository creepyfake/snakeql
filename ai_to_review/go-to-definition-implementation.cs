using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace OracleSqlLanguageServer
{
    // First, let's update our main language server class to handle the definition request
    public partial class OracleSqlLanguageServer
    {
        private readonly DbObjectRepository _dbObjectRepository;
        
        public OracleSqlLanguageServer(DbObjectRepository dbObjectRepository)
        {
            _dbObjectRepository = dbObjectRepository;
        }
        
        [JsonRpcMethod(Methods.TextDocumentDefinitionName)]
        public async Task<LocationOrLocationLinks> TextDocumentDefinition(DefinitionParams @params)
        {
            var uri = @params.TextDocument.Uri;
            if (!_documents.TryGetValue(uri, out var text))
            {
                return new LocationOrLocationLinks();
            }
            
            var position = @params.Position;
            
            // Get definition information for the position
            var locations = _parser.GetDefinition(text, position, _dbObjectRepository);
            
            return new LocationOrLocationLinks { LocationLinks = locations };
        }
    }
    
    // Update our parser class to add the GetDefinition method
    public partial class OracleSqlParser
    {
        public virtual List<LocationLink> GetDefinition(string text, Position position, DbObjectRepository dbRepo)
        {
            // Default implementation (will be overridden by the actual parser)
            return new List<LocationLink>();
        }
    }
    
    // Implementation for the parser
    public partial class OracleSqlParserImpl : OracleSqlParser
    {
        public override List<LocationLink> GetDefinition(string text, Position position, DbObjectRepository dbRepo)
        {
            var result = new List<LocationLink>();
            
            // Parse the document
            var parseResult = Parse(text);
            
            // Get token at position
            var tokenAtPosition = GetTokenAtPosition(parseResult.TokenStream, position);
            if (tokenAtPosition == null || tokenAtPosition.Type != PlSqlParser.IDENTIFIER)
            {
                return result;
            }
            
            // Get the identifier name
            string identifierName = tokenAtPosition.Text;
            
            // Get context to determine what kind of identifier this is
            var context = DetermineIdentifierContext(parseResult.ParseTree, tokenAtPosition);
            
            // Find the object in the repository
            DbObject dbObject = context switch
            {
                IdentifierContext.Table => dbRepo.FindTable(identifierName),
                IdentifierContext.View => dbRepo.FindView(identifierName),
                IdentifierContext.Column => FindColumn(dbRepo, parseResult, tokenAtPosition),
                IdentifierContext.Procedure => dbRepo.FindProcedure(identifierName),
                IdentifierContext.Function => dbRepo.FindFunction(identifierName),
                IdentifierContext.Package => dbRepo.FindPackage(identifierName),
                IdentifierContext.Synonym => dbRepo.FindSynonym(identifierName),
                IdentifierContext.Sequence => dbRepo.FindSequence(identifierName),
                IdentifierContext.Type => dbRepo.FindType(identifierName),
                _ => dbRepo.FindAnyObject(identifierName)
            };
            
            if (dbObject != null)
            {
                // Create range for the token
                var tokenRange = new Range
                {
                    Start = new Position { Line = tokenAtPosition.Line - 1, Character = tokenAtPosition.Column },
                    End = new Position { Line = tokenAtPosition.Line - 1, Character = tokenAtPosition.Column + tokenAtPosition.Text.Length }
                };
                
                // If the object has a definition file
                if (!string.IsNullOrEmpty(dbObject.DefinitionFile))
                {
                    result.Add(new LocationLink
                    {
                        OriginSelectionRange = tokenRange,
                        TargetUri = dbObject.DefinitionFile,
                        TargetRange = dbObject.DefinitionRange,
                        TargetSelectionRange = dbObject.DefinitionSelectionRange
                    });
                }
                // If we only have object definitions in memory but not in files
                else
                {
                    // Generate a virtual file URI for database objects
                    string virtualUri = $"oracle-db://{dbObject.Owner}/{dbObject.Type}/{dbObject.Name}";
                    
                    // Create a basic range (will be refined in actual implementation)
                    var targetRange = new Range
                    {
                        Start = new Position { Line = 0, Character = 0 },
                        End = new Position { Line = 0, Character = dbObject.Name.Length }
                    };
                    
                    result.Add(new LocationLink
                    {
                        OriginSelectionRange = tokenRange,
                        TargetUri = virtualUri,
                        TargetRange = targetRange,
                        TargetSelectionRange = targetRange
                    });
                }
            }
            
            return result;
        }
        
        private DbObject FindColumn(DbObjectRepository dbRepo, ParseResult parseResult, IToken columnToken)
        {
            // Try to determine which table this column belongs to by analyzing context
            string tableName = FindTableForColumn(parseResult.ParseTree, columnToken);
            
            if (!string.IsNullOrEmpty(tableName))
            {
                return dbRepo.FindColumn(tableName, columnToken.Text);
            }
            
            // If we can't determine the table, try to find the column in any table
            return dbRepo.FindColumnInAnyTable(columnToken.Text);
        }
        
        private string FindTableForColumn(IParseTree parseTree, IToken columnToken)
        {
            // Create a visitor to find the table context for this column
            var visitor = new TableForColumnVisitor(columnToken);
            visitor.Visit(parseTree);
            return visitor.TableName;
        }
        
        private IdentifierContext DetermineIdentifierContext(IParseTree parseTree, IToken token)
        {
            // Create a visitor to determine the context of the identifier
            var visitor = new IdentifierContextVisitor(token);
            visitor.Visit(parseTree);
            return visitor.Context;
        }
    }
    
    // Visitor class to determine the identifier context (table, view, column, etc.)
    public class IdentifierContextVisitor : PlSqlParserBaseVisitor<object>
    {
        private readonly IToken _targetToken;
        
        public IdentifierContext Context { get; private set; } = IdentifierContext.Unknown;
        
        public IdentifierContextVisitor(IToken targetToken)
        {
            _targetToken = targetToken;
        }
        
        public override object VisitTable_element(PlSqlParser.Table_elementContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Table;
                return null;
            }
            return base.VisitTable_element(context);
        }
        
        public override object VisitColumn_name(PlSqlParser.Column_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Column;
                return null;
            }
            return base.VisitColumn_name(context);
        }
        
        public override object VisitProcedure_name(PlSqlParser.Procedure_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Procedure;
                return null;
            }
            return base.VisitProcedure_name(context);
        }
        
        public override object VisitFunction_name(PlSqlParser.Function_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Function;
                return null;
            }
            return base.VisitFunction_name(context);
        }
        
        public override object VisitPackage_name(PlSqlParser.Package_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Package;
                return null;
            }
            return base.VisitPackage_name(context);
        }
        
        public override object VisitView_name(PlSqlParser.View_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.View;
                return null;
            }
            return base.VisitView_name(context);
        }
        
        public override object VisitType_name(PlSqlParser.Type_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Type;
                return null;
            }
            return base.VisitType_name(context);
        }
        
        public override object VisitSequence_name(PlSqlParser.Sequence_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Sequence;
                return null;
            }
            return base.VisitSequence_name(context);
        }
        
        public override object VisitSynonym_name(PlSqlParser.Synonym_nameContext context)
        {
            if (TokenMatchesContext(context.Start))
            {
                Context = IdentifierContext.Synonym;
                return null;
            }
            return base.VisitSynonym_name(context);
        }
        
        private bool TokenMatchesContext(IToken contextToken)
        {
            return contextToken.Line == _targetToken.Line && 
                   contextToken.Column == _targetToken.Column &&
                   contextToken.Text == _targetToken.Text;
        }
    }
    
    // Visitor to find the table that a column belongs to
    public class TableForColumnVisitor : PlSqlParserBaseVisitor<object>
    {
        private readonly IToken _columnToken;
        private string _currentTable = null;
        
        public string TableName { get; private set; } = null;
        
        public TableForColumnVisitor(IToken columnToken)
        {
            _columnToken = columnToken;
        }
        
        public override object VisitSelect_list_item(PlSqlParser.Select_list_itemContext context)
        {
            // Check if this is our column token
            var columnName = context.column_name();
            if (columnName != null && IsTargetToken(columnName.Start))
            {
                // If the column has a table qualifier, use that
                var tableRef = context.table_name();
                if (tableRef != null)
                {
                    TableName = tableRef.GetText();
                }
                else
                {
                    // Otherwise use the current table from context
                    TableName = _currentTable;
                }
                return null;
            }
            return base.VisitSelect_list_item(context);
        }
        
        public override object VisitTable_reference(PlSqlParser.Table_referenceContext context)
        {
            // Save table name when entering a FROM clause or JOIN
            _currentTable = context.table_name()?.GetText();
            return base.VisitTable_reference(context);
        }
        
        public override object VisitJoin_clause(PlSqlParser.Join_clauseContext context)
        {
            // Handle join clauses
            var leftTable = context.table_reference_list(0)?.table_reference()?.table_name()?.GetText();
            if (leftTable != null)
            {
                _currentTable = leftTable;
            }
            
            var joinResult = base.VisitJoin_clause(context);
            
            // Reset current table after visiting join
            _currentTable = null;
            
            return joinResult;
        }
        
        public override object VisitWhere_clause(PlSqlParser.Where_clauseContext context)
        {
            // Handle columns in WHERE clause
            foreach (var expr in context.expr())
            {
                var column = FindColumnInExpression(expr);
                if (column != null && IsTargetToken(column.Start))
                {
                    // Try to determine table from the expression
                    var table = FindTableForColumnInExpression(expr);
                    if (table != null)
                    {
                        TableName = table;
                        return null;
                    }
                }
            }
            
            return base.VisitWhere_clause(context);
        }
        
        private PlSqlParser.Column_nameContext FindColumnInExpression(PlSqlParser.ExprContext expr)
        {
            // Simple implementation - a real one would be more comprehensive
            return expr.column_name();
        }
        
        private string FindTableForColumnInExpression(PlSqlParser.ExprContext expr)
        {
            // Simple implementation - a real one would be more comprehensive
            return expr.table_name()?.GetText();
        }
        
        private bool IsTargetToken(IToken token)
        {
            return token.Line == _columnToken.Line && 
                   token.Column == _columnToken.Column &&
                   token.Text == _columnToken.Text;
        }
    }
    
    // Enum to represent different types of identifiers
    public enum IdentifierContext
    {
        Unknown,
        Table,
        View,
        Column,
        Procedure,
        Function,
        Package,
        Synonym,
        Sequence,
        Type
    }
    
    // Database Object Repository interface/implementation
    public class DbObjectRepository
    {
        private readonly Dictionary<string, DbObject> _objects = new Dictionary<string, DbObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DbObject>> _columnsByTable = new Dictionary<string, List<DbObject>>(StringComparer.OrdinalIgnoreCase);
        
        public DbObject FindTable(string tableName)
        {
            return FindObjectByTypeAndName("TABLE", tableName);
        }
        
        public DbObject FindView(string viewName)
        {
            return FindObjectByTypeAndName("VIEW", viewName);
        }
        
        public DbObject FindProcedure(string procedureName)
        {
            return FindObjectByTypeAndName("PROCEDURE", procedureName);
        }
        
        public DbObject FindFunction(string functionName)
        {
            return FindObjectByTypeAndName("FUNCTION", functionName);
        }
        
        public DbObject FindPackage(string packageName)
        {
            return FindObjectByTypeAndName("PACKAGE", packageName);
        }
        
        public DbObject FindSynonym(string synonymName)
        {
            return FindObjectByTypeAndName("SYNONYM", synonymName);
        }
        
        public DbObject FindSequence(string sequenceName)
        {
            return FindObjectByTypeAndName("SEQUENCE", sequenceName);
        }
        
        public DbObject FindType(string typeName)
        {
            return FindObjectByTypeAndName("TYPE", typeName);
        }
        
        public DbObject FindColumn(string tableName, string columnName)
        {
            if (_columnsByTable.TryGetValue(tableName, out var columns))
            {
                return columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }
        
        public DbObject FindColumnInAnyTable(string columnName)
        {
            foreach (var columns in _columnsByTable.Values)
            {
                var column = columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                if (column != null)
                {
                    return column;
                }
            }
            return null;
        }
        
        public DbObject FindAnyObject(string objectName)
        {
            // First try exact match
            if (_objects.TryGetValue(objectName, out var dbObject))
            {
                return dbObject;
            }
            
            // Then check for a column with this name
            return FindColumnInAnyTable(objectName);
        }
        
        private DbObject FindObjectByTypeAndName(string objectType, string objectName)
        {
            return _objects.Values.FirstOrDefault(obj => 
                obj.Type.Equals(objectType, StringComparison.OrdinalIgnoreCase) && 
                obj.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase));
        }
        
        // Methods to populate the repository
        public void AddObject(DbObject dbObject)
        {
            _objects[dbObject.Name] = dbObject;
            
            // If it's a column, add to columns dictionary
            if (dbObject.Type.Equals("COLUMN", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(dbObject.TableName))
            {
                if (!_columnsByTable.ContainsKey(dbObject.TableName))
                {
                    _columnsByTable[dbObject.TableName] = new List<DbObject>();
                }
                _columnsByTable[dbObject.TableName].Add(dbObject);
            }
        }
        
        public void LoadFromDatabase(/* parameters to connect to Oracle DB */)
        {
            // In a real implementation, this method would query database metadata
            // and populate the repository with the results
            
            // Example:
            // - Query ALL_OBJECTS for tables, views, procedures, etc.
            // - Query ALL_TAB_COLUMNS for columns
            // - Create DbObject instances for each object
            // - Set definition files if objects are also stored in filesystem
        }
    }
    
    // Database Object class to store metadata
    public class DbObject
    {
        public string Name { get; set; }
        public string Type { get; set; }  // TABLE, VIEW, COLUMN, PROCEDURE, etc.
        public string Owner { get; set; }
        public string TableName { get; set; }  // For columns, the table they belong to
        
        // For objects that have a definition file
        public string DefinitionFile { get; set; }
        public Range DefinitionRange { get; set; }
        public Range DefinitionSelectionRange { get; set; }
        
        // Additional metadata
        public string DataType { get; set; }  // For columns
        public bool IsNullable { get; set; }  // For columns
        public string Status { get; set; }    // VALID, INVALID, etc.
        public DateTime CreatedDate { get; set; }
        public DateTime LastDDLTime { get; set; }
        
        // For PLSQL objects, the source code
        public string SourceCode { get; set; }
    }
}
