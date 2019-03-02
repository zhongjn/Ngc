using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ngc
{
    public class LLVMIRGenerator
    {
        private SyntaxTree m_Tree;
        private Dictionary<VariableSymbol, LLVMValueRef> m_VariablePointers;
        private Dictionary<FunctionSymbol, LLVMValueRef> m_FunctionPointers;
        private Dictionary<TypeSymbol, LLVMTypeRef> m_Types;
        private Dictionary<Statement, LLVMBasicBlockRef> m_BreakableStatementSuccessor;
        private LLVMModuleRef m_Module;
        private LLVMValueRef m_Function;
        private LLVMBuilderRef m_Builder;

        public LLVMModuleRef Generate(SyntaxTree tree)
        {
            m_VariablePointers = new Dictionary<VariableSymbol, LLVMValueRef>();
            m_FunctionPointers = new Dictionary<FunctionSymbol, LLVMValueRef>();
            m_Types = new Dictionary<TypeSymbol, LLVMTypeRef>();
            m_BreakableStatementSuccessor = new Dictionary<Statement, LLVMBasicBlockRef>();

            m_Module = LLVM.ModuleCreateWithName("test");
            m_Builder = LLVM.CreateBuilder();

            m_Tree = tree;
            LoadTypes(m_Tree.GlobalSymbols);
            foreach (var stmt in m_Tree.Root)
            {
                VisitStmt(stmt);
            }

            LLVM.DisposeBuilder(m_Builder);

            LLVMPassManagerRef pm = LLVM.CreatePassManager();
            LLVM.AddPromoteMemoryToRegisterPass(pm);
            LLVM.AddInstructionCombiningPass(pm);
            LLVM.AddReassociatePass(pm);
            LLVM.AddGVNPass(pm);
            LLVM.AddCFGSimplificationPass(pm);
            LLVM.RunPassManager(pm, m_Module);

            return m_Module;
        }

        private struct CPrimitiveLLVMInfo
        {
            public CPrimitiveLLVMInfo(LLVMTypeRef type, LLVMValueRef? @default) : this()
            {
                Type = type;
                Default = @default;
            }
            public LLVMTypeRef Type { get; }
            public LLVMValueRef? Default { get; }
        }

        private Dictionary<CPrimitiveType, CPrimitiveLLVMInfo> m_PrimitiveLookup = new Dictionary<CPrimitiveType, CPrimitiveLLVMInfo>()
        {
            { CPrimitiveType.Void, new CPrimitiveLLVMInfo(LLVM.VoidType(), null) },
            { CPrimitiveType.Int, new CPrimitiveLLVMInfo(LLVM.Int32Type(), LLVM.ConstInt(LLVM.Int32Type(), 0, false)) },
            { CPrimitiveType.Double, new CPrimitiveLLVMInfo(LLVM.DoubleType(), LLVM.ConstReal(LLVM.DoubleType(), 0.0)) },
            { CPrimitiveType.String, new CPrimitiveLLVMInfo(LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.ConstPointerNull(LLVM.Int8Type())) }
        };

        private void LoadTypes(SymbolTable table)
        {
            if (table == null) return;
            foreach (var sym in table.Symbols.OfType<TypeSymbol>())
            {
                if (sym.IsPrimitive)
                {
                    m_Types.Add(sym, m_PrimitiveLookup[sym.Primitive.Value].Type);
                }
                else
                {
                    // TODO: compound type
                    throw new NotImplementedException();
                }
            }
        }

        private void VisitStmt(Statement stmt)
        {
            VisitStmt(stmt, out _);
        }

        private LLVMValueRef I32ToCond(LLVMValueRef i32)
        {
            return LLVM.BuildICmp(m_Builder, LLVMIntPredicate.LLVMIntNE, i32, LLVM.ConstInt(LLVM.Int32Type(), 0, false), "cond");
        }

        private void VisitStmt(Statement stmt, out bool terminate)
        {
            terminate = false;
            if (stmt == null) return;
            LoadTypes(stmt.ScopeSymbols);
            switch (stmt)
            {
                case FunctionDeclarationStmt fd:
                    {
                        break;
                    }

                case FunctionStmt f:
                    {
                        FunctionSymbol funcSym = f.Symbol;
                        TypeSymbol returnSym = funcSym.Signature.Return.Symbol;
                        LLVMTypeRef funcTy = LLVM.FunctionType(
                            ReturnType: m_Types[returnSym],
                            ParamTypes: funcSym.Signature.Arguments.Select(arg => m_Types[arg.Type.Symbol]).ToArray(),
                            IsVarArg: false);
                        m_Function = LLVM.AddFunction(m_Module, f.Declaration.Name, funcTy);
                        m_FunctionPointers.Add(funcSym, m_Function);
                        LLVMBasicBlockRef bb = LLVM.AppendBasicBlock(m_Function, "entry");
                        LLVM.PositionBuilderAtEnd(m_Builder, bb);

                        int paramIndex = 0;
                        foreach (var param in m_Function.GetParams())
                        {
                            LLVMValueRef ptrArg = LLVM.BuildAlloca(m_Builder, LLVM.TypeOf(param), "arg");
                            LLVM.BuildStore(m_Builder, param, ptrArg);
                            m_VariablePointers.Add(funcSym.Signature.Arguments[paramIndex].Symbol, ptrArg);
                            paramIndex++;
                        }

                        VisitStmt(f.Body, out bool term);

                        if (!term)
                        {
                            if (returnSym.IsPrimitive)
                            {
                                CPrimitiveType prim = returnSym.Primitive.Value;
                                if (prim == CPrimitiveType.Void)
                                {
                                    LLVM.BuildRetVoid(m_Builder);
                                }
                                else
                                {
                                    LLVM.BuildRet(m_Builder, m_PrimitiveLookup[prim].Default.Value);
                                }
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                        break;
                    }

                case BlockStmt block:
                    {
                        foreach (var childStmt in block.Statements)
                        {
                            VisitStmt(childStmt, out bool term);
                            if (term)
                            {
                                terminate = true;
                                break;
                            }
                        }
                        break;
                    }

                case VariableDeclarationStmt vd:
                    {
                        if (vd.Symbol.IsGlobal)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            LLVMValueRef ptr = LLVM.BuildAlloca(m_Builder, m_Types[vd.Type.Symbol], vd.Name);
                            m_VariablePointers.Add(vd.Symbol, ptr);
                            if (vd.Assignment != null)
                            {
                                LLVMValueRef value = VisitExpr(vd.Assignment);
                                LLVM.BuildStore(m_Builder, value, ptr);
                            }
                        }
                        break;
                    }

                case IfStmt ifs:
                    {
                        LLVMValueRef i32 = VisitExpr(ifs.Condition);
                        LLVMValueRef cond = I32ToCond(i32);

                        LLVMBasicBlockRef bbTrue = LLVM.AppendBasicBlock(m_Function, "if_true");
                        LLVMBasicBlockRef bbFalse = LLVM.AppendBasicBlock(m_Function, "if_false");
                        LLVMBasicBlockRef bbSucc = LLVM.AppendBasicBlock(m_Function, "if_succ");

                        LLVM.BuildCondBr(m_Builder, cond, bbTrue, bbFalse);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbTrue);
                        VisitStmt(ifs.True, out bool termTrue);
                        if (!termTrue) LLVM.BuildBr(m_Builder, bbSucc);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbFalse);
                        VisitStmt(ifs.False, out bool termFalse);
                        if (!termFalse) LLVM.BuildBr(m_Builder, bbSucc);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbSucc);
                        break;
                    }

                case ReturnStmt ret:
                    {
                        if (ret.Value == null)
                        {
                            LLVM.BuildRetVoid(m_Builder);
                        }
                        else
                        {
                            LLVMValueRef value = VisitExpr(ret.Value);
                            LLVM.BuildRet(m_Builder, value);
                        }
                        terminate = true;
                        break;
                    }

                case WhileStmt wh:
                    {
                        LLVMBasicBlockRef bbCond = LLVM.AppendBasicBlock(m_Function, "while_cond");
                        LLVMBasicBlockRef bbBody = LLVM.AppendBasicBlock(m_Function, "while_body");
                        LLVMBasicBlockRef bbSucc = LLVM.AppendBasicBlock(m_Function, "while_succ");
                        m_BreakableStatementSuccessor.Add(wh, bbSucc);

                        LLVM.BuildBr(m_Builder, bbCond);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbCond);
                        LLVMValueRef i32 = VisitExpr(wh.Condition);
                        LLVMValueRef cond = I32ToCond(i32);
                        LLVM.BuildCondBr(m_Builder, cond, bbBody, bbSucc);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbBody);
                        VisitStmt(wh.Body, out bool term);
                        if (!term) LLVM.BuildBr(m_Builder, bbCond);

                        LLVM.PositionBuilderAtEnd(m_Builder, bbSucc);
                        break;
                    }

                case BreakStmt brk:
                    {
                        terminate = true;
                        LLVM.BuildBr(m_Builder, m_BreakableStatementSuccessor[brk.Host]);
                        break;
                    }

                case ExpressionStmt exprStmt:
                    {
                        VisitExpr(exprStmt.Expression);
                        break;
                    }

                default:
                    {
                        throw new ArgumentException("Unrecognizable statement type.");
                    }
            }

        }


        private delegate LLVMValueRef LLVMBuildOp2(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string name);


        private LLVMValueRef VisitExpr(Expression expr)
        {

            switch (expr)
            {
                case Op2Expr op2:
                    {
                        if (op2.Operator == Op2Expr.Type.Assign)
                        {
                            var varSym = ((VariableValueExpr)op2.Left).Symbol;
                            var right = VisitExpr(op2.Right);
                            LLVM.BuildStore(m_Builder, right, m_VariablePointers[varSym]);
                            return right;
                        }
                        else
                        {
                            var left = VisitExpr(op2.Left);
                            var right = VisitExpr(op2.Right);
                            var primitive = op2.Left.DeducedType.Primitive.Value;

                            LLVMValueRef BuildBinIntFloat(LLVMOpcode? opInt, LLVMOpcode? opFloat) =>
                                LLVM.BuildBinOp(m_Builder, primitive == CPrimitiveType.Int ? opInt.Value : opFloat.Value, left, right, "tbin");

                            LLVMValueRef BuildCmpIntFloat(LLVMIntPredicate prdInt, LLVMRealPredicate prdFloat)
                            {
                                LLVMValueRef i1 = primitive == CPrimitiveType.Int ?
                                    LLVM.BuildICmp(m_Builder, prdInt, left, right, "cmpb") :
                                    LLVM.BuildFCmp(m_Builder, prdFloat, left, right, "cmpb");
                                return LLVM.BuildIntCast(m_Builder, i1, LLVM.Int32Type(), "cmp");
                            }

                            switch (op2.Operator)
                            {
                                case Op2Expr.Type.Add:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMAdd, LLVMOpcode.LLVMFAdd);
                                case Op2Expr.Type.Sub:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMSub, LLVMOpcode.LLVMFSub);
                                case Op2Expr.Type.Mul:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMMul, LLVMOpcode.LLVMFMul);
                                case Op2Expr.Type.Div:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMSDiv, LLVMOpcode.LLVMFDiv);
                                case Op2Expr.Type.Mod:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMSRem, LLVMOpcode.LLVMFRem);
                                case Op2Expr.Type.GT:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntSGT, LLVMRealPredicate.LLVMRealOGT);
                                case Op2Expr.Type.GE:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntSGE, LLVMRealPredicate.LLVMRealOGE);
                                case Op2Expr.Type.LT:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntSLT, LLVMRealPredicate.LLVMRealOLT);
                                case Op2Expr.Type.LE:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntSLE, LLVMRealPredicate.LLVMRealOLE);
                                case Op2Expr.Type.EQ:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntEQ, LLVMRealPredicate.LLVMRealOEQ);
                                case Op2Expr.Type.NEQ:
                                    return BuildCmpIntFloat(LLVMIntPredicate.LLVMIntNE, LLVMRealPredicate.LLVMRealONE);
                                case Op2Expr.Type.And:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMAnd, null);
                                case Op2Expr.Type.Or:
                                    return BuildBinIntFloat(LLVMOpcode.LLVMOr, null);
                            }
                        }

                        break;
                    }

                case LiteralExpr lit:
                    {
                        string origin = lit.Value;
                        if (origin.StartsWith("\""))
                        {
                            string value = origin.Substring(0, origin.Length - 2);
                            return LLVM.ConstString(value, (uint)value.Length, false);
                        }
                        else if (origin.Contains("."))
                        {
                            double value = double.Parse(origin);
                            return LLVM.ConstReal(LLVM.DoubleType(), value);
                        }
                        else
                        {
                            int value = int.Parse(origin);
                            return LLVM.ConstInt(LLVM.Int32Type(), (ulong)value, true);
                        }
                    }

                case FunctionCallExpr funcCall:
                    {
                        var funcSym = funcCall.Symbol;
                        var args = funcCall.Arguments.Select(arg => VisitExpr(arg)).ToArray();
                        return LLVM.BuildCall(m_Builder, m_FunctionPointers[funcSym], args, "call");
                    }

                case VariableValueExpr var:
                    {
                        return LLVM.BuildLoad(m_Builder, m_VariablePointers[var.Symbol], "var");
                    }
                case ConvertExpr conv:
                    {
                        var from = VisitExpr(conv.From);
                        var typeFrom = conv.From.DeducedType.Primitive.Value;
                        var typeTo = conv.DeducedType.Primitive.Value;
                        if (typeFrom == CPrimitiveType.Int && typeTo == CPrimitiveType.Double)
                        {
                            return LLVM.BuildSIToFP(m_Builder, from, LLVM.DoubleType(), "conv");
                        }
                        else if (typeFrom == CPrimitiveType.Double && typeTo == CPrimitiveType.Int)
                        {
                            return LLVM.BuildFPToSI(m_Builder, from, LLVM.Int32Type(), "conv");
                        }
                        break;
                    }

            }

            throw new ArgumentException("Unrecognizable expression type.");

        }



    }



}
