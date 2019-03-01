#pragma warning disable CS0660, CS0661

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ngc
{
    #region Symbol

    public class SymbolTable
    {
        private Dictionary<string, Symbol> m_Symbols = new Dictionary<string, Symbol>();

        public bool TryAdd(Symbol sym)
        {
            if (Exists(sym.Name)) return false;
            m_Symbols.Add(sym.Name, sym);
            return true;
        }

        public bool Exists(string name)
        {
            return m_Symbols.ContainsKey(name);
        }

        public Symbol Get(string name)
        {
            return m_Symbols.TryGetValue(name, out Symbol v) ? v : null;
        }

        public IEnumerable<Symbol> Symbols => m_Symbols.Values;
    }

    public abstract class Symbol
    {
        public string Name { get; set; }
    }

    public class VariableSymbol : Symbol
    {
        public bool IsGlobal { get; set; }
        public TypeName Type { get; set; }
    }

    public class FunctionSymbol : Symbol
    {
        public bool HasBody { get; set; } = false;
        public FunctionSignature Signature { get; set; }
    }

    public class TypeSymbol : Symbol
    {
        public bool IsPrimitive => Primitive != null;
        public bool IsCompound => Compound != null;
        public CPrimitiveType? Primitive { get; set; }
        public TypeSymbol[] Compound { get; set; }

        private static List<CPrimitiveType> m_NumTypes = new List<CPrimitiveType> { CPrimitiveType.Int, CPrimitiveType.Double }; // 暂时只支持这个
        public static bool CanConvert(TypeSymbol from, TypeSymbol to, out bool needConvert)
        {
            needConvert = false;
            if (from.Primitive == null || to.Primitive == null) return false;
            if (from.Primitive == to.Primitive && from.Primitive != CPrimitiveType.Void) return true;
            if (!(m_NumTypes.Contains(from.Primitive.Value) && m_NumTypes.Contains(to.Primitive.Value))) return false;
            needConvert = true;
            return true;
        }

        public static bool CanApplyBinNumOp(TypeSymbol t1, TypeSymbol t2, out TypeSymbol tResult)
        {
            tResult = null;
            if (t1.Primitive == null || t2.Primitive == null) return false;
            int i1 = m_NumTypes.IndexOf(t1.Primitive.Value);
            int i2 = m_NumTypes.IndexOf(t2.Primitive.Value);
            if (i1 < 0 || i2 < 0)
            {
                return false;
            }
            tResult = i1 >= i2 ? t1 : t2;
            return true;
        }

    }

    #endregion

    #region Statement

    public abstract class Statement
    {
        // public object Tag { get; set; }
        public SymbolTable ScopeSymbols { get; set; }
    }



    public class FunctionDeclarationStmt : Statement
    {
        public string Name { get; set; }
        public FunctionSignature Signature { get; set; }
    }


    public class FunctionStmt : Statement
    {
        public FunctionSymbol Symbol { get; set; }
        public FunctionDeclarationStmt Declaration { get; set; }
        public Statement Body { get; set; }
    }


    public class BlockStmt : Statement
    {
        public List<Statement> Statements { get; set; }
    }

    public class VariableDeclarationStmt : Statement
    {
        public VariableSymbol Symbol { get; set; }
        public TypeName Type { get; set; }
        public string Name { get; set; }
        public Expression Assignment { get; set; }
    }

    public class IfStmt : Statement
    {
        public Expression Condition { get; set; }
        public Statement True { get; set; }
        public Statement False { get; set; }
    }

    public class ReturnStmt : Statement
    {
        public Expression Value { get; set; }
    }

    public class BreakStmt : Statement
    {

    }

    public class ExpressionStmt : Statement
    {
        public Expression Expression { get; set; }
    }



    #endregion

    #region Expression

    public abstract class Expression
    {
        public TypeSymbol DeducedType { get; set; }
        // public TypeSymbol ExpectedType { get; set; }
    }

    public class Op2Expr : Expression
    {
        public Type Operator { get; set; }
        public Expression Left { get; set; }
        public Expression Right { get; set; }

        public enum Type
        {
            Add,
            Sub,
            Mul,
            Div,
            Mod,
            Assign,
            GT,
            GE,
            LT,
            LE,
            EQ,
            NEQ,
            And,
            Or
        }

        private static Dictionary<string, Type> OpLookup = new Dictionary<string, Type>() {
            { "+", Type.Add },
            { "-", Type.Sub },
            { "*", Type.Mul },
            { "/", Type.Div },
            { "%", Type.Mod },
            { "=", Type.Assign },
            { ">", Type.GT },
            { ">=", Type.GE },
            { "<", Type.LT },
            { "<=", Type.LE },
            { "==", Type.EQ },
            { "!=", Type.NEQ },
            { "&&", Type.And },
            { "||", Type.Or }
        };

        public static Type ParseType(string s)
        {
            return OpLookup[s];
        }
    }

    public class LiteralExpr : Expression
    {
        public string Value { get; set; }
    }

    public class FunctionCallExpr : Expression
    {
        public FunctionSymbol Symbol { get; set; }
        public string Name { get; set; }
        public List<Expression> Arguments { get; set; }
    }

    public class VariableValueExpr : Expression
    {
        public VariableSymbol Symbol { get; set; }
        public string Name { get; set; }
    }

    public class ConvertExpr : Expression
    {
        public TypeName TargetType { get; set; }
        public Expression From { get; set; }
    }

    #endregion

    #region Misc

    public enum CPrimitiveType : int
    {
        Void, Int, Double, String
    }

    public static class CPrimitiveTypeUtil
    {
        static CPrimitiveTypeUtil()
        {
            Types = m_CPrimitiveTypeNameLookup.Keys.ToList();
        }

        public static IReadOnlyList<CPrimitiveType> Types { get; private set; }

        private static Dictionary<CPrimitiveType, string> m_CPrimitiveTypeNameLookup = new Dictionary<CPrimitiveType, string>()
        {
            { CPrimitiveType.Void, "void" },
            { CPrimitiveType.Int, "int" },
            { CPrimitiveType.Double, "double" },
            { CPrimitiveType.String, "string" }
        };

        public static string GetName(CPrimitiveType t)
        {
            return m_CPrimitiveTypeNameLookup[t];
        }
    }



    public class SyntaxTree
    {
        public List<Statement> Root { get; set; }
        public SymbolTable GlobalSymbols { get; set; }
    }

    public class Argument
    {
        public VariableSymbol Symbol { get; set; }
        public TypeName Type { get; set; }
        public string Name { get; set; }
    }

    public class FunctionSignature
    {
        public List<Argument> Arguments { get; set; }
        public TypeName Return { get; set; }

        public static bool operator ==(FunctionSignature s1, FunctionSignature s2)
        {
            if (s1.Return != s2.Return) return false;
            if (s1.Arguments.Count != s2.Arguments.Count) return false;
            for (int i = 0; i < s1.Arguments.Count; i++)
            {
                if (s1.Arguments[i].Type != s2.Arguments[i].Type) return false;
            }
            return true;
        }

        public static bool operator !=(FunctionSignature signature1, FunctionSignature signature2)
        {
            return !(signature1 == signature2);
        }
    }

    public class TypeName
    {
        public string Name { get; set; }
        public TypeSymbol Symbol { get; set; }


        public static implicit operator string(TypeName t)
        {
            return t.Name;
        }

        public static implicit operator TypeName(string name)
        {
            return new TypeName { Name = name };
        }

    }

    #endregion
}
