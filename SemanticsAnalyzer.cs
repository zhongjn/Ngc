using System;
using System.Collections.Generic;
using System.Linq;


namespace Ngc
{

    public class SemanticsAnalyzer
    {

        private SyntaxTree m_Tree;

        private void SemanticsError(string msg)
        {
            throw new SemanticsException(msg); // TODO: position
        }

        private void Assert(bool b, string errMsg)
        {
            if (!b) SemanticsError(errMsg);
        }

        public void Analyze(SyntaxTree tree)
        {
            m_Tree = tree;
            VisitTree(tree);
            return;
        }

        private void LoadCPrimitiveTypeSymbols()
        {
            foreach (var type in CPrimitiveTypeUtil.Types)
            {
                m_Tree.GlobalSymbols.TryAdd(new TypeSymbol { Primitive = type, Name = CPrimitiveTypeUtil.GetName(type) });
            }
        }


        private Stack<Statement> m_StmtStack;

        private System.Type[] m_MustBeRoot = { typeof(FunctionStmt) };
        private System.Type[] m_CanBeRoot = { typeof(FunctionStmt), typeof(FunctionDeclarationStmt), typeof(VariableDeclarationStmt) };

        private T FindSymbol<T>(string name) where T : Symbol
        {
            foreach (var info in m_StmtStack)
            {
                if (info.ScopeSymbols == null) continue;
                var symbol = info.ScopeSymbols.Get(name);
                if (symbol != null) return symbol as T;
            }

            {
                var symbol = m_Tree.GlobalSymbols.Get(name);
                if (symbol != null) return symbol as T;
            }

            return null;
        }

        private TypeSymbol FindSymbol(CPrimitiveType basic)
        {
            return m_Tree.GlobalSymbols.Get(CPrimitiveTypeUtil.GetName(basic)) as TypeSymbol;
        }

        private SymbolTable GetNearestSymbolTable()
        {
            foreach (var info in m_StmtStack)
            {
                if (info.ScopeSymbols != null) return info.ScopeSymbols;
            }
            return m_Tree.GlobalSymbols;
        }

        private void LinkTypeSymbol(TypeName type)
        {
            type.Symbol = FindSymbol<TypeSymbol>(type.Name);
            Assert(type.Symbol != null, $"Can't find type '{type.Name}'.");
        }

        private void AssertCanApplyBinNumOp(TypeSymbol t1, TypeSymbol t2, string opName, out TypeSymbol wider) // 简陋版本
        {
            Assert(TypeSymbol.CanApplyBinNumOp(t1, t1, out wider), $"{opName} operators can't apply between type '{t1}' and '{t2}'.");
        }

        private void AssertCanConvert(TypeSymbol from, TypeSymbol to, out bool needConvert)
        {
            Assert(TypeSymbol.CanConvert(from, to, out needConvert), $"There are no conversions from type '{from.Name}' to '{to.Name}'.");
        }

        private Expression EliminateImplicitConv(Expression e, TypeSymbol expect)
        {
            var result = e;
            AssertCanConvert(e.DeducedType, expect, out bool needConvert);
            if (needConvert)
            {
                result = new ConvertExpr
                {
                    From = e,
                    DeducedType = expect,
                    TargetType = new TypeName
                    {
                        Name = expect.Name,
                        Symbol = expect
                    }
                };
            }
            return result;
        }

        private void VisitTree(SyntaxTree tree)
        {
            tree.GlobalSymbols = new SymbolTable(); // root symbol table
            LoadCPrimitiveTypeSymbols();

            BuildGlobalVarsInitFunction();

            m_StmtStack = new Stack<Statement>();
            foreach (var stmt in tree.Root)
            {
                VisitStmt(stmt, true);
            }
        }

        // 可能不需要？编译时就确定？
        private void BuildGlobalVarsInitFunction()
        {
            var globalVars = m_Tree.Root.OfType<VariableDeclarationStmt>();
            var body = new BlockStmt() { Statements = new List<Statement>() };
            var func = new FunctionStmt()
            {
                Declaration = new FunctionDeclarationStmt
                {
                    Name = "$init",
                    Signature = new FunctionSignature
                    {
                        Arguments = new List<Argument>(),
                        Return = "void"
                    }
                },
                Body = body
            };


            foreach (var g in globalVars)
            {
                body.Statements.Add(new ExpressionStmt
                {
                    Expression = new Op2Expr
                    {
                        Operator = Op2Expr.Type.Assign,
                        Left = new VariableValueExpr { Name = g.Name },
                        Right = g.Assignment
                    }
                });
                g.Assignment = null;
            }

            m_Tree.Root.Add(func);
        }

        private void VisitStmt(Statement stmt, bool root = false)
        {
            if (stmt == null) return;

            m_StmtStack.Push(stmt);

            Assert(!root || m_CanBeRoot.Contains(stmt.GetType()), $"Only function and variable declaration can be a root statement.");
            Assert(root || !m_MustBeRoot.Contains(stmt.GetType()), $"Function defination must be a root statement.");

            switch (stmt)
            {
                case FunctionDeclarationStmt fd:
                    {
                        VisitFunctionDeclaration(fd, false);
                        break;
                    }

                case FunctionStmt f:
                    {
                        Assert(root, "Functions can only be the root statements.");

                        f.Symbol = VisitFunctionDeclaration(f.Declaration, true);

                        f.ScopeSymbols = f.Body.ScopeSymbols = new SymbolTable();// function body has the same scope with arguments
                        foreach (var arg in f.Declaration.Signature.Arguments)
                        {
                            if (string.IsNullOrEmpty(arg.Name)) continue;
                            var argSym = new VariableSymbol { Name = arg.Name, Type = arg.Type };
                            Assert(f.ScopeSymbols.TryAdd(argSym),
                                $"Argument name '{arg.Name}' duplicated.");
                            arg.Symbol = argSym;
                        }
                        VisitStmt(f.Body);
                        break;
                    }

                case BlockStmt block:
                    {
                        block.ScopeSymbols = block.ScopeSymbols ?? new SymbolTable(); // initialize a symbol table if not explicitly specified
                        foreach (var childStmt in block.Statements)
                        {
                            VisitStmt(childStmt);
                        }
                        break;
                    }

                case VariableDeclarationStmt vd:
                    {

                        LinkTypeSymbol(vd.Type);

                        Assert(vd.Type != "void", $"'void' can't be used as a type.");

                        var sym = new VariableSymbol { Name = vd.Name, Type = vd.Type, IsGlobal = root };
                        Assert(GetNearestSymbolTable().TryAdd(sym),
                            $"Variable name '{vd.Name}' duplicated with another symbol under the same scope.");
                        vd.Symbol = sym;

                        VisitExpr(vd.Assignment);
                        vd.Assignment = EliminateImplicitConv(vd.Assignment, vd.Type.Symbol);

                        break;
                    }

                case IfStmt i:
                    {
                        VisitExpr(i.Condition);
                        i.Condition = EliminateImplicitConv(i.Condition, FindSymbol(CPrimitiveType.Int));
                        i.True.ScopeSymbols = new SymbolTable();
                        VisitStmt(i.True);
                        if (i.False != null) i.False.ScopeSymbols = new SymbolTable();
                        VisitStmt(i.False);

                        break;
                    }

                case ReturnStmt ret:
                    {
                        VisitExpr(ret.Value);
                        FunctionStmt func = null;
                        foreach (var ss in m_StmtStack.Reverse())
                        {
                            if (ss is FunctionStmt)
                            {
                                func = (FunctionStmt)ss;
                                break;
                            }
                        }

                        Assert(func.Declaration.Signature.Return != "void" || ret.Value == null, "This function does not return a value.");

                        Assert(ret.Value != null, "This function needs to return a value.");

                        ret.Value = EliminateImplicitConv(ret.Value, func.Declaration.Signature.Return.Symbol);

                        break;
                    }

                case BreakStmt brk:
                    {
                        // TODO
                        throw new NotImplementedException();
                        break;
                    }

                case ExpressionStmt exprStmt:
                    {
                        switch (exprStmt.Expression)
                        {
                            case Op2Expr op2 when op2.Operator == Op2Expr.Type.Assign:
                            case FunctionCallExpr funcCall:
                                break;
                            default:
                                SemanticsError("Only assignment and function call expression can be a statement.");
                                break;
                        }
                        VisitExpr(exprStmt.Expression);
                        break;
                    }

                default:
                    {
                        throw new ArgumentException("Unrecognizable statement type.");
                    }
            }

            m_StmtStack.Pop();
        }

        private FunctionSymbol VisitFunctionDeclaration(FunctionDeclarationStmt fd, bool hasDefination)
        {
            foreach (var arg in fd.Signature.Arguments)
            {
                Assert(arg.Type != "void", $"'void' can't be used as a type.");
                LinkTypeSymbol(arg.Type);
            }
            LinkTypeSymbol(fd.Signature.Return);

            var funcSym = FindSymbol<FunctionSymbol>(fd.Name);
            if (funcSym != null)
            {
                Assert(funcSym.Signature == fd.Signature, $"Function '{fd.Name}' signature mismatch.");
                if (hasDefination)
                {
                    funcSym.HasBody = true;
                }
            }
            else
            {
                funcSym = new FunctionSymbol { Name = fd.Name, Signature = fd.Signature, HasBody = hasDefination };
                Assert(GetNearestSymbolTable().TryAdd(funcSym),
                    $"There's another type of symbol with the same name '{fd.Name}'.");
            }
            return funcSym;
        }

        private void VisitExpr(Expression expr)
        {
            if (expr == null) return;
            switch (expr)
            {
                case Op2Expr op2:
                    {
                        VisitExpr(op2.Left);
                        VisitExpr(op2.Right);
                        var tLeft = op2.Left.DeducedType;
                        var tRight = op2.Right.DeducedType;

                        switch (op2.Operator)
                        {
                            case Op2Expr.Type.Add:
                            case Op2Expr.Type.Sub:
                            case Op2Expr.Type.Mul:
                            case Op2Expr.Type.Div:
                            case Op2Expr.Type.Mod:
                                {
                                    Assert(op2.Operator != Op2Expr.Type.Mod || tLeft == FindSymbol(CPrimitiveType.Int) && tRight == FindSymbol(CPrimitiveType.Int), "'%' can't apply on non-integral types");
                                    AssertCanApplyBinNumOp(tLeft, tRight, "Arithmetic", out TypeSymbol wider);
                                    op2.Left = EliminateImplicitConv(op2.Left, wider);
                                    op2.Right = EliminateImplicitConv(op2.Right, wider);
                                    expr.DeducedType = wider;
                                    break;
                                }

                            case Op2Expr.Type.Assign:
                                {
                                    Assert(op2.Left is VariableValueExpr, "Require a lvalue before '='.");
                                    op2.Right = EliminateImplicitConv(op2.Right, tLeft);
                                    expr.DeducedType = tLeft;
                                    break;
                                }

                            case Op2Expr.Type.GT:
                            case Op2Expr.Type.GE:
                            case Op2Expr.Type.LT:
                            case Op2Expr.Type.EQ:
                            case Op2Expr.Type.NEQ:
                                {
                                    AssertCanApplyBinNumOp(tLeft, tRight, "Comparasion", out TypeSymbol wider);
                                    op2.Left = EliminateImplicitConv(op2.Left, wider);
                                    op2.Right = EliminateImplicitConv(op2.Right, wider);
                                    expr.DeducedType = FindSymbol(CPrimitiveType.Int);
                                    break;
                                }

                            case Op2Expr.Type.And:
                            case Op2Expr.Type.Or:
                                {
                                    op2.Left = EliminateImplicitConv(op2.Left, FindSymbol(CPrimitiveType.Int));
                                    op2.Right = EliminateImplicitConv(op2.Right, FindSymbol(CPrimitiveType.Int));
                                    expr.DeducedType = FindSymbol(CPrimitiveType.Int);
                                    break;
                                }
                        }
                        break;
                    }

                case LiteralExpr lit:
                    {
                        if (lit.Value.StartsWith("\"")) // 字符串
                        {
                            expr.DeducedType = FindSymbol(CPrimitiveType.String); // 暂时不支持指针操作，所以要内建string类型
                        }
                        else if (lit.Value.Contains(".")) // double
                        {
                            expr.DeducedType = FindSymbol(CPrimitiveType.Double);
                        }
                        else
                        {
                            expr.DeducedType = FindSymbol(CPrimitiveType.Int);
                        }
                        break;
                    }

                case FunctionCallExpr funcCall:
                    {
                        var funcSym = FindSymbol<FunctionSymbol>(funcCall.Name);

                        Assert(funcSym != null, $"There is no such a function '{funcCall.Name}'.");

                        Assert(funcSym.Signature.Arguments.Count == funcCall.Arguments.Count,
                            $"Function '{funcCall.Name}' expect {funcSym.Signature.Arguments.Count} argument(s).");

                        for (int i = 0; i < funcCall.Arguments.Count; i++)
                        {
                            VisitExpr(funcCall.Arguments[i]);
                            funcCall.Arguments[i] = EliminateImplicitConv(funcCall.Arguments[i], funcSym.Signature.Arguments[i].Type.Symbol);
                        }

                        funcCall.Symbol = funcSym;
                        funcCall.DeducedType = funcSym.Signature.Return.Symbol;

                        break;
                    }

                case VariableValueExpr var:
                    {
                        var varSym = FindSymbol<VariableSymbol>(var.Name);

                        Assert(varSym != null, $"There is no such a variable '{var.Name}'.");

                        var.Symbol = varSym;
                        var.DeducedType = varSym.Type.Symbol;

                        break;
                    }

                case ConvertExpr conv:
                    {
                        VisitExpr(conv.From);

                        LinkTypeSymbol(conv.TargetType);
                        AssertCanConvert(conv.From.DeducedType, conv.TargetType.Symbol, out bool _);
                        conv.DeducedType = conv.TargetType.Symbol;
                        break;
                    }

                default:
                    {
                        throw new ArgumentException("Unrecognizable expression type.");
                    }
            }
        }

    }

    public class SemanticsException : Exception
    {
        public SemanticsException(string msg) : base(msg)
        {

        }
    }





}
