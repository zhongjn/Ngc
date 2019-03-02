using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ngc
{
    public class Parser
    {
        public Parser()
        {

        }

        public SyntaxTree Parse(List<Token> tokens)
        {
            m_Pos = 0;
            m_Tokens = tokens;
            RootStmts(out List<Statement> stmts);
            return new SyntaxTree() { Root = stmts };
        }

        private void ParseError(string msg)
        {
            throw new ParserException($"{msg} (Pos={m_Pos})");
        }

        private int m_Pos = 0;
        private List<Token> m_Tokens;

        #region Parse Functions

        private bool RootStmts(out List<Statement> ls)
        {
            ls = new List<Statement>();
            int lastSucceededPos = -1;
            while (Stmt(out Statement s))
            {
                ls.Add(s);
                lastSucceededPos = m_Pos;
            }
            if (lastSucceededPos < m_Tokens.Count - 1)
            {
                ParseError("Unknown syntax error, unable to parse the rest of code.");
            }
            return true;
        }

        private bool Stmt(out Statement s)
        {
            int save = m_Pos;
            s = null;
            if (Reset(save) && Expr(out Expression e) && Consume(";"))
            {
                s = new ExpressionStmt { Expression = e };
            }
            else if (Reset(save) && Func(out Statement func))
            {
                s = func;
            }
            else if (Reset(save) && VarDecl(out Statement decl))
            {
                s = decl;
            }
            else if (Reset(save) && StmtBlock(out Statement block))
            {
                s = block;
            }
            else if (Reset(save) && IfStmt(out Statement sIf))
            {
                s = sIf;
            }
            else if (Reset(save) && WhileStmt(out Statement sWhile))
            {
                s = sWhile;
            }
            else if (Reset(save) && RetStmt(out Statement sRet))
            {
                s = sRet;
            }
            else if (Reset(save) && BreakStmt(out Statement sBreak))
            {
                s = sBreak;
            }

            return s != null;
        }

        private bool VarDecl(out Statement s)
        {
            int save = m_Pos;
            s = null;
            if (Reset(save) && Consume(out Token type, TokenType.Identifier) && Consume(out Token name, TokenType.Identifier))
            {
                var decl = new VariableDeclarationStmt { Type = type.Content, Name = name.Content };
                if (Consume("=")) // with intializer
                {
                    if (Expr(out Expression eAssign))
                    {
                        decl.Assignment = eAssign;
                    }
                    else
                    {
                        ParseError("There should be an expression after assign operator.");
                    }
                }
                if (Consume(";"))
                {
                    s = decl;
                }
                else
                {
                    ParseError("Expect a semicolon.");
                }
            }
            return s != null;
        }

        private bool StmtBlock(out Statement s)
        {
            s = null;
            if (Consume("{"))
            {
                var stmts = new List<Statement>();
                while (!Consume("}"))
                {
                    if (Stmt(out Statement st))
                    {
                        stmts.Add(st);
                    }
                    else
                    {
                        ParseError("Invalid statement.");
                    }
                }
                s = new BlockStmt { Statements = stmts };
            }
            return s != null;
        }

        private bool IfStmt(out Statement s)
        {
            s = null;
            if (Consume("if"))
            {
                if (Consume("(") && Expr(out Expression eCond) && Consume(")"))
                {
                    if (Stmt(out Statement sTrue))
                    {
                        var ifStmt = new IfStmt { Condition = eCond, True = sTrue };
                        if (Consume("else"))
                        {
                            if (Stmt(out Statement sFalse))
                            {
                                ifStmt.False = sFalse;
                            }
                            else
                            {
                                ParseError("Expect a false-branch statement.");
                            }
                        }
                        s = ifStmt;
                    }
                    else
                    {
                        ParseError("Expect a true-branch statement.");
                    }
                }
                else
                {
                    ParseError("'if' condition syntax error. Expect an expression with parens around.");
                }
            }
            return s != null;
        }

        private bool WhileStmt(out Statement s)
        {
            s = null;
            if (Consume("while"))
            {
                if (Consume("(") && Expr(out Expression eCond) && Consume(")"))
                {
                    if (Stmt(out Statement sBody))
                    {
                        var whileStmt = new WhileStmt { Condition = eCond, Body = sBody  };
                        s = whileStmt;
                    }
                    else
                    {
                        ParseError("Expect a loop body.");
                    }
                }
                else
                {
                    ParseError("'while' condition syntax error. Expect an expression with parens around.");
                }
            }
            return s != null;
        }

        private bool RetStmt(out Statement s)
        {
            s = null;
            if (Consume("return"))
            {
                var ret = new ReturnStmt();
                s = ret;
                if (Expr(out Expression v))
                {
                    ret.Value = v;
                }
                if (!Consume(";"))
                {
                    ParseError("Expect a ';'.");
                }
            }
            return s != null;
        }

        private bool BreakStmt(out Statement s)
        {
            s = null;
            if (Consume("break"))
            {
                var ret = new BreakStmt();
                s = ret;
                if (!Consume(";"))
                {
                    ParseError("Expect a ';'.");
                }
            }
            return s != null;
        }

        private bool Func(out Statement s)
        {
            s = null;
            if (Consume(out Token retType, TokenType.Identifier) && Consume(out Token funcName, TokenType.Identifier)
                && Consume("(") && ArgDeclList(out List<Argument> args) && Consume(")")) // declaration part
            {
                var decl = new FunctionDeclarationStmt
                {
                    Name = funcName.Content,
                    Signature = new FunctionSignature
                    {
                        Arguments = args,
                        Return = retType.Content
                    }
                };

                if (Consume(";"))
                {
                    s = decl;
                }
                else if (StmtBlock(out Statement body))
                {
                    s = new FunctionStmt { Declaration = decl, Body = body };
                }
                else
                {
                    ParseError("Expect a ';' for declaration, or a '{' for body.");
                }
            }
            return s != null;
        }

        private bool ArgDeclList(out List<Argument> args)
        {
            args = new List<Argument>(); ;
            bool first = true;
            while (first || Consume(","))
            {
                if (Consume(out Token type, TokenType.Identifier))
                {
                    var arg = new Argument { Type = type.Content };
                    if (Consume(out Token name, TokenType.Identifier))
                    {
                        arg.Name = name.Content;
                    }
                    args.Add(arg);
                }
                else
                {
                    if (!first) ParseError("Expect an argument declaration after ','.");
                    break;
                }
                first = false;
            }

            return args != null;
        }

        private delegate bool ExprGen(out Expression e);

        private bool Expr(out Expression e)
        {
            int save = m_Pos;
            e = null;

            if (Reset(save) && Factor(out Expression left) && Consume("=") && Expr(out Expression right)) // assign
            {
                e = new Op2Expr { Operator = Op2Expr.Type.Assign, Left = left, Right = right };
            }
            else if (Reset(save) && SeqOr(out Expression eOr))
            {
                e = eOr;
            }
            return e != null;
        }

        // Concat children terms with higher precedence into one term.
        // The operator must be left-associative.
        private bool LeftOp2ConcatChildren(ExprGen child, out Expression e, params string[] matchOps)
        {
            int save = m_Pos;
            e = null;

            if (Reset(save) && child(out e))
            {
                while (Consume(out Token match, matchOps))
                {
                    if (child(out Expression next))
                    {
                        var newE = new Op2Expr { Operator = Op2Expr.ParseType(match.Content), Left = e, Right = next };
                        e = newE;
                    }
                    else
                    {
                        ParseError("Expect an expression.");
                    }
                }
            }
            return e != null;
        }

        private bool SeqOr(out Expression e) => LeftOp2ConcatChildren(SeqAnd, out e, "||");

        private bool SeqAnd(out Expression e) => LeftOp2ConcatChildren(SeqEq, out e, "&&");

        private bool SeqEq(out Expression e) => LeftOp2ConcatChildren(SeqComp, out e, "==", "!=");

        private bool SeqComp(out Expression e) => LeftOp2ConcatChildren(SeqAddSub, out e, "<", "<=", ">", ">=");

        private bool SeqAddSub(out Expression e) => LeftOp2ConcatChildren(SeqMulDivMod, out e, "+", "-");

        private bool SeqMulDivMod(out Expression e) => LeftOp2ConcatChildren(Factor, out e, "*", "/", "%");

        private bool Factor(out Expression e)
        {
            e = null;
            int save = m_Pos;
            if (Reset(save) && Consume(out Token matchLiteral, TokenType.Literal)) // literal
            {
                e = new LiteralExpr { Value = matchLiteral.Content };
            }
            else if (Reset(save) && FuncCall(out Expression eFunc)) // function call
            {
                e = eFunc;
            }
            else if (Reset(save) && Consume(out Token matchVariable, TokenType.Identifier)) // variable
            {
                e = new VariableValueExpr { Name = matchVariable.Content };
            }
            else if (Reset(save) && Consume("(") && Expr(out Expression eInner) && Consume(")"))
            {
                e = eInner;
            }

            return e != null;
        }

        private bool FuncCall(out Expression e)
        {
            e = null;
            if (Consume(out Token matchFunc, TokenType.Identifier) && Consume("(") && ArgList(out List<Expression> args) && Consume(")"))
            {
                e = new FunctionCallExpr { Name = matchFunc.Content, Arguments = args };
            }
            return e != null;
        }

        private bool ArgList(out List<Expression> args)
        {
            args = new List<Expression>();
            if (Expr(out Expression first))
            {
                args.Add(first);
                while (Consume(","))
                {
                    if (Expr(out Expression next))
                    {
                        args.Add(next);
                    }
                    else
                    {
                        ParseError("Invalid expression.");
                    }
                }
            }

            return args != null;
        }

        private bool Reset(int p)
        {
            m_Pos = p;
            return true;
        }

        private bool IgnoreSpace()
        {
            while (m_Pos < m_Tokens.Count && m_Tokens[m_Pos].Type == TokenType.Space) m_Pos++;
            return true;
        }

        private bool Consume(string content)
        {
            return Token(out _, true, content);
        }

        private bool Consume(out Token match, params string[] contents)
        {
            return Token(out match, true, contents);
        }

        private bool Consume(out Token match, params TokenType[] types)
        {
            return Token(out match, true, types);
        }

        private bool Token(out Token match, bool consume, params string[] contents)
        {
            IgnoreSpace();
            match = null;
            if (m_Pos >= m_Tokens.Count) return false;
            foreach (var str in contents)
            {
                if (m_Tokens[m_Pos].Content == str)
                {
                    match = m_Tokens[m_Pos];
                    if (consume) m_Pos++;
                    return true;
                }
            }
            return false;
        }

        private bool Token(out Token match, bool consume, params TokenType[] types)
        {
            IgnoreSpace();
            match = null;
            if (m_Pos >= m_Tokens.Count) return false;

            foreach (var type in types)
            {
                if (type == TokenType.Space) throw new ArgumentException("Token method would ignore spaces.");
                if (m_Tokens[m_Pos].Type == type)
                {
                    match = m_Tokens[m_Pos];
                    if (consume) m_Pos++;
                    return true;
                }
            }
            return false;
        }

        #endregion
    }

    public class ParserException : Exception
    {
        public ParserException(string msg) : base(msg)
        {

        }
    }

}
