namespace JetBrains.ReSharper.Plugins.FSharp.Psi.LanguageService.Parsing

open System.Collections.Generic
open FSharp.Compiler.Ast
open FSharp.Compiler.PrettyNaming
open FSharp.Compiler.Range
open JetBrains.Diagnostics
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Parsing
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing
open JetBrains.ReSharper.Plugins.FSharp.Util
open JetBrains.ReSharper.Psi.ExtensionsAPI.Tree

type FSharpImplTreeBuilder(sourceFile, lexer, decls, lifetime, projectedOffset) =
    inherit FSharpTreeBuilderBase(sourceFile, lexer, lifetime, projectedOffset)

    let nextSteps = Stack<ITreeBuilderStep>()

    new (sourceFile, lexer, decls, lifetime) =
        FSharpImplTreeBuilder(sourceFile, lexer, decls, lifetime, 0) 

    member x.NextSteps = nextSteps

    override x.CreateFSharpFile() =
        let mark = x.Mark()
        for decl in decls do
            x.ProcessTopLevelDeclaration(decl)
        x.FinishFile(mark, ElementType.F_SHARP_IMPL_FILE)

    member x.ProcessTopLevelDeclaration(SynModuleOrNamespace(lid, _, moduleKind, decls, _, attrs, _, range)) =
        let mark, elementType = x.StartTopLevelDeclaration(lid, attrs, moduleKind, range)
        for decl in decls do
            x.ProcessModuleMemberDeclaration(decl)
        x.FinishTopLevelDeclaration(mark, range, elementType)

    member x.ProcessModuleMemberDeclaration(moduleMember) =
        match moduleMember with
        | SynModuleDecl.NestedModule(ComponentInfo(attrs, _, _, lid, _, _, _, _), _ ,decls, _, range) ->
            let mark = x.StartNestedModule attrs lid range
            for decl in decls do
                x.ProcessModuleMemberDeclaration(decl)
            x.Done(range, mark, ElementType.NESTED_MODULE_DECLARATION)

        | SynModuleDecl.Types(typeDefns, _) ->
            for typeDefn in typeDefns do
                x.ProcessTypeDefn(typeDefn)

        | SynModuleDecl.Exception(SynExceptionDefn(exn, memberDefns, range), _) ->
            let mark = x.StartException(exn)
            for memberDefn in memberDefns do
                x.ProcessTypeMember(memberDefn)
            x.Done(range, mark, ElementType.EXCEPTION_DECLARATION)

        | SynModuleDecl.Open(lidWithDots, range) ->
            let mark = x.MarkTokenOrRange(FSharpTokenType.OPEN, range)
            x.ProcessLongIdentifier(lidWithDots.Lid)
            x.Done(range, mark, ElementType.OPEN_STATEMENT)

        | SynModuleDecl.Let(_, bindings, range) ->
            let letStart = letStartPos bindings range
            let letMark = x.Mark(letStart)
            for binding in bindings do
                x.ProcessTopLevelBinding(binding)
            x.Done(range, letMark, ElementType.LET)

        | SynModuleDecl.HashDirective(hashDirective, _) ->
            x.ProcessHashDirective(hashDirective)

        | SynModuleDecl.DoExpr(_, expr, range) ->
            let mark = x.Mark(range)
            x.MarkChameleonExpression(expr)
            x.Done(range, mark, ElementType.DO)

        | decl ->
            x.MarkAndDone(decl.Range, ElementType.OTHER_MEMBER_DECLARATION)

    member x.ProcessHashDirective(ParsedHashDirective(id, _, range)) =
        let mark = x.Mark(range)
        let elementType =
            match id with
            | "l" | "load" -> ElementType.LOAD_DIRECTIVE
            | "r" | "reference" -> ElementType.REFERENCE_DIRECTIVE
            | "I" -> ElementType.I_DIRECTIVE
            | _ -> ElementType.OTHER_DIRECTIVE
        x.Done(range, mark, elementType)

    member x.ProcessTypeDefn(TypeDefn(ComponentInfo(attrs, typeParams, _, lid , _, _, _, _), repr, members, range)) =
        match repr with
        | SynTypeDefnRepr.ObjectModel(SynTypeDefnKind.TyconAugmentation, _, _) ->
            let mark = x.Mark(range)
            x.ProcessLongIdentifier(lid)
            x.ProcessTypeParametersOfType typeParams range false
            for extensionMember in members do
                x.ProcessTypeMember(extensionMember)
            x.Done(range, mark, ElementType.TYPE_EXTENSION_DECLARATION)
        | _ ->

        let mark = x.StartType attrs typeParams lid range
        let elementType =
            match repr with
            | SynTypeDefnRepr.Simple(simpleRepr, _) ->
                match simpleRepr with
                | SynTypeDefnSimpleRepr.Record(_, fields, _) ->
                    for field in fields do
                        x.ProcessField field ElementType.RECORD_FIELD_DECLARATION
                    ElementType.RECORD_DECLARATION

                | SynTypeDefnSimpleRepr.Enum(enumCases, _) ->
                    for case in enumCases do
                        x.ProcessEnumCase case
                    ElementType.ENUM_DECLARATION

                | SynTypeDefnSimpleRepr.Union(_, cases, range) ->
                    x.ProcessUnionCases(cases, range)
                    ElementType.UNION_DECLARATION

                | SynTypeDefnSimpleRepr.TypeAbbrev(_, synType, _) ->
                    x.ProcessType(synType)
                    ElementType.TYPE_ABBREVIATION_DECLARATION

                | SynTypeDefnSimpleRepr.None _ ->
                    ElementType.ABSTRACT_TYPE_DECLARATION

                | _ -> ElementType.OTHER_SIMPLE_TYPE_DECLARATION

            | SynTypeDefnRepr.Exception _ ->
                ElementType.EXCEPTION_DECLARATION

            | SynTypeDefnRepr.ObjectModel(SynTypeDefnKind.TyconAugmentation, _, _) ->
                ElementType.TYPE_EXTENSION_DECLARATION

            | SynTypeDefnRepr.ObjectModel(kind, members, _) ->
                for m in members do x.ProcessTypeMember m
                match kind with
                | TyconClass -> ElementType.CLASS_DECLARATION
                | TyconInterface -> ElementType.INTERFACE_DECLARATION
                | TyconStruct -> ElementType.STRUCT_DECLARATION

                | TyconDelegate(synType, _) ->
                    x.MarkOtherType(synType)                    
                    ElementType.DELEGATE_DECLARATION

                | _ -> ElementType.OBJECT_TYPE_DECLARATION

        for m in members do x.ProcessTypeMember m
        x.Done(range, mark, elementType)

    member x.ProcessTypeMember(typeMember: SynMemberDefn) =
        let attrs = typeMember.Attributes
        // todo: let/attrs range
        let rangeStart = x.GetStartOffset typeMember.Range
        let isMember =
            match typeMember with
            | SynMemberDefn.Member _ -> true
            | _ -> false

        if x.CurrentOffset <= rangeStart || (not isMember) then
            let mark = x.MarkAttributesOrIdOrRange(attrs, None, typeMember.Range)

            // todo: mark body exprs as synExpr
            let memberType =
                match typeMember with
                | SynMemberDefn.ImplicitCtor(_, _, args, selfId, _) ->
                    for arg in args do
                        x.ProcessImplicitCtorParam arg
                    if selfId.IsSome then x.ProcessLocalId selfId.Value
                    ElementType.IMPLICIT_CONSTRUCTOR_DECLARATION

                | SynMemberDefn.ImplicitInherit(baseType, args, _, _) ->
                    x.ProcessType(baseType)
                    x.MarkChameleonExpression(args)
                    ElementType.TYPE_INHERIT

                | SynMemberDefn.Interface(interfaceType, interfaceMembersOpt , _) ->
                    x.ProcessType(interfaceType)
                    match interfaceMembersOpt with
                    | Some members ->
                        for m in members do
                            x.ProcessTypeMember(m)
                    | _ -> ()
                    ElementType.INTERFACE_IMPLEMENTATION

                | SynMemberDefn.Inherit(baseType, _, _) ->
                    try x.ProcessType(baseType)
                    with _ -> () // Getting type range throws an exception if base type lid is empty.
                    ElementType.INTERFACE_INHERIT

                | SynMemberDefn.Member(Binding(_, _, _, _, _, _, valData, headPat, returnInfo, expr, _, _) ,range) ->
                    let elType =
                        match headPat with
                        | SynPat.LongIdent(LongIdentWithDots(lid, _), _, typeParamsOpt, memberParams, _, _) ->
                            match lid with
                            | [_] ->
                                match valData with
                                | SynValData(Some(flags), _, selfId) when flags.MemberKind = MemberKind.Constructor ->
                                    x.ProcessParams(memberParams, true, true) // todo: should check isLocal
                                    if selfId.IsSome then
                                        x.ProcessLocalId(selfId.Value)

                                    x.MarkChameleonExpression(expr)
                                    ElementType.CONSTRUCTOR_DECLARATION
                                | _ ->
                                    x.ProcessMemberDeclaration(typeParamsOpt, memberParams, returnInfo, expr, range)
                                    ElementType.MEMBER_DECLARATION

                            | selfId :: _ :: _ ->
                                x.ProcessLocalId(selfId)
                                x.ProcessMemberDeclaration(typeParamsOpt, memberParams, returnInfo, expr, range)
                                ElementType.MEMBER_DECLARATION

                            | _ -> ElementType.OTHER_TYPE_MEMBER
                        | _ -> ElementType.OTHER_TYPE_MEMBER
                    elType

                | SynMemberDefn.LetBindings(bindings, _, _, _) ->
                    for binding in bindings do
                        x.ProcessTopLevelBinding(binding)
                    ElementType.LET

                | SynMemberDefn.AbstractSlot(ValSpfn(_, _, typeParams, _, _, _, _, _, _, _, _), _, range) ->
                    match typeParams with
                    | SynValTyparDecls(typeParams, _, _) ->
                        x.ProcessTypeParametersOfType typeParams range true
                    ElementType.ABSTRACT_SLOT

                | SynMemberDefn.ValField(Field(_, _, _, _, _, _, _, _), _) ->
                    ElementType.VAL_FIELD

                | SynMemberDefn.AutoProperty(_, _, _, _, _, _, _, _, expr, _, _) ->
                    x.MarkChameleonExpression(expr)
                    ElementType.AUTO_PROPERTY

                | _ -> ElementType.OTHER_TYPE_MEMBER

            x.Done(typeMember.Range, mark, memberType)

    member x.ProcessReturnInfo(returnInfo) =
        // todo: mark return type attributes
        match returnInfo with
        | None -> ()
        | Some(SynBindingReturnInfo(returnType, range, _)) ->

        let startOffset = x.GetStartOffset(range)
        x.AdvanceToTokenOrOffset(FSharpTokenType.COLON, startOffset, range)

        let mark = x.Mark()
        x.ProcessType(returnType)
        x.Done(range, mark, ElementType.RETURN_TYPE_INFO)

    member x.ProcessMemberDeclaration(typeParamsOpt, memberParams, returnInfo, expr, range) =
        match typeParamsOpt with
        | Some(SynValTyparDecls(typeParams, _, _)) ->
            x.ProcessTypeParametersOfType typeParams range true // todo: of type?..
        | _ -> ()

        x.ProcessParams(memberParams, true, true) // todo: should check isLocal
        x.ProcessReturnInfo(returnInfo)
        x.MarkChameleonExpression(expr)

    // isTopLevelPat is needed to distinguish function definitions from other long ident pats:
    // let (Some x) = ...
    // let Some x = ...
    // When long pat is a function pat its args are currently mapped as local decls. todo: rewrite it to be params
    // Getting proper params (with right impl and sig ranges) isn't easy, probably a fix is needed in FCS.
    member x.ProcessPat(PatRange range as pat, isLocal, isTopLevelPat) =
        let mark = x.Mark(range)

        let elementType =
            match pat with
            | SynPat.Named(pat, id, _, _, _) ->
                match pat with
                | SynPat.Wild _ -> ()
                | _ -> x.ProcessPat(pat, isLocal, false)

                if IsActivePatternName id.idText then x.ProcessActivePatternId(id, isLocal)
                if isLocal then ElementType.LOCAL_NAMED_PAT else ElementType.TOP_NAMED_PAT

            | SynPat.LongIdent(lid, _, typars, args, _, _) ->
                match lid.Lid with
                | [id] when id.idText = "op_ColonColon" ->
                    match args with
                    | Pats pats ->
                        for pat in pats do
                            x.ProcessPat(pat, isLocal, false)
                    | NamePatPairs(pats, _) ->
                        for _, pat in pats do
                            x.ProcessPat(pat, isLocal, false)

                    ElementType.CONS_PAT

                | _ ->

                match lid.Lid with
                | [id] ->
                    if IsActivePatternName id.idText then
                        x.ProcessActivePatternId(id, isLocal)
    
                    match typars with
                    | None -> ()
                    | Some(SynValTyparDecls(typarDecls, _, _)) ->

                    for typarDecl in typarDecls do
                        x.ProcessTypeParameter(typarDecl, ElementType.TYPE_PARAMETER_OF_METHOD_DECLARATION)

                | lid ->
                    x.ProcessLongIdentifier(lid)

                x.ProcessParams(args, isLocal || isTopLevelPat, false)
                if isLocal then ElementType.LOCAL_LONG_IDENT_PAT else ElementType.TOP_LONG_IDENT_PAT

            | SynPat.Typed(pat, _, _) ->
                x.ProcessPat(pat, isLocal, false)
                ElementType.TYPED_PAT

            | SynPat.Or(pat1, pat2, _) ->
                x.ProcessPat(pat1, isLocal, false)
                x.ProcessPat(pat2, isLocal, false)
                ElementType.OR_PAT

            | SynPat.Ands(pats, _) ->
                for pat in pats do
                    x.ProcessPat(pat, isLocal, false)
                ElementType.ANDS_PAT

            | SynPat.Tuple(_, pats, _)
            | SynPat.ArrayOrList(_, pats, _) ->
                for pat in pats do
                    x.ProcessPat(pat, isLocal, false)
                ElementType.LIST_PAT

            | SynPat.Paren(pat, _) ->
                x.ProcessPat(pat, isLocal, false)
                ElementType.PAREN_PAT

            | SynPat.Record(pats, _) ->
                for _, pat in pats do
                    x.ProcessPat(pat, isLocal, false)
                ElementType.RECORD_PAT

            | SynPat.IsInst(typ, _) ->
                x.ProcessType(typ)
                ElementType.IS_INST_PAT

            | SynPat.Wild _ ->
                ElementType.WILD_PAT

            | SynPat.Attrib(pat, attrs, _) ->
                x.ProcessAttributes(attrs)
                x.ProcessPat(pat, isLocal, false)
                ElementType.ATTRIB_PAT

            | _ ->
                ElementType.OTHER_PAT

        x.Done(range, mark, elementType)

    member x.ProcessParams(args: SynConstructorArgs, isLocal, markMember) =
        match args with
        | Pats pats ->
            for pat in pats do
                x.ProcessParam(pat, isLocal, markMember)

        | NamePatPairs(idsAndPats, _) ->
            for _, pat in idsAndPats do
                x.ProcessParam(pat, isLocal, markMember)

    member x.ProcessParam(PatRange range as pat, isLocal, markMember) =
        if not markMember then x.ProcessPat(pat, isLocal, false) else

        let mark = x.Mark(range)
        x.ProcessPat(pat, isLocal, false)
        x.Done(range, mark, ElementType.MEMBER_PARAM)

    member x.FixExpresion(expr) =
        // A fake SynExpr.Typed node is added for binding with return type specification like in the following
        // member x.Prop: int = 1
        // where 1 is replaced with `1: int`. 
        // These fake nodes have original type specification ranges that are out of the actual expression ranges.
        match expr with
        | SynExpr.Typed(inner, synType, range) when not (rangeContainsRange range synType.Range) -> inner
        | _ -> expr

    member x.MarkChameleonExpression(expr) =
        let (ExprRange range as expr) = x.FixExpresion(expr)

        let mark = x.Mark(range)

        // Replace all tokens with single chameleon token.
        let tokenMark = x.Mark(range)
        x.AdvanceToEnd(range)
        x.Builder.AlterToken(tokenMark, FSharpTokenType.CHAMELEON)

        x.Done(range, mark, ChameleonExpressionNodeType.Instance, expr)

    member x.MarkOtherType(TypeRange range as typ) =
        let mark = x.Mark(range)
        x.ProcessType(typ)
        x.Done(range, mark, ElementType.OTHER_TYPE)

    member x.ProcessTopLevelBinding(Binding(_, kind, _, _, attrs, _, _ , headPat, returnInfo, expr, _, _) as binding) =
        let expr = x.FixExpresion(expr)

        match kind with
        | StandaloneExpression
        | DoBinding -> x.MarkChameleonExpression(expr)
        | _ ->

        let mark =
            match attrs with
            | [] -> x.Mark(binding.StartPos)
            | { Range = r } :: _ ->
                let mark = x.MarkTokenOrRange(FSharpTokenType.LBRACK_LESS, r)
                x.ProcessAttributes(attrs)
                mark

        x.ProcessPat(headPat, false, true)
        x.ProcessReturnInfo(returnInfo)
        x.MarkChameleonExpression(expr)

        x.Done(binding.RangeOfBindingAndRhs, mark, ElementType.TOP_BINDING)

    member x.ProcessLocalBinding(Binding(_, kind, _, _, attrs, _, _, headPat, returnInfo, expr, _, _) as binding) =
        let expr = x.FixExpresion(expr)

        match kind with
        | StandaloneExpression
        | DoBinding -> x.ProcessExpression(expr)
        | _ ->

        let mark =
            match attrs with
            | [] -> x.Mark(binding.StartPos)
            | { Range = r } :: _ ->
                let mark = x.MarkTokenOrRange(FSharpTokenType.LBRACK_LESS, r)
                x.ProcessAttributes(attrs)
                mark

        x.PushRangeForMark(binding.RangeOfBindingAndRhs, mark, ElementType.LOCAL_BINDING)
        x.ProcessPat(headPat, true, true)
        x.ProcessReturnInfo(returnInfo)
        x.ProcessExpression(expr)

    member x.PushRange(range: range, elementType) =
        x.PushRangeForMark(range, x.Mark(range), elementType)

    member x.PushRangeForMark(range, mark, elementType) =
        nextSteps.Push(EndRangeStep(range, mark, elementType))

    member x.PushRangeAndProcessExpression(expr, range, elementType) =
        x.PushRange(range, elementType)
        x.ProcessExpression(expr)

    member x.PushType(synType) =
        nextSteps.Push(ProcessTypeStep(synType))

    member x.PushLondIdentifier(lid) =
        nextSteps.Push(ProcessLidStep(lid))

    member x.PushExpression(synExpr) =
        nextSteps.Push(ProcessExpressionStep(synExpr))

    member x.PushExpressionList(exprs: SynExpr list) =
        nextSteps.Push(ExpressionListStep(exprs))

    member x.ProcessTopLevelExpression(expr) =
        x.ProcessExpression(expr)

        while nextSteps.Count > 0 do
            nextSteps.Peek().DoStep(x, nextSteps)

    member x.ProcessExpression(ExprRange range as expr) =
        match expr with
        | SynExpr.Paren(expr, _, _, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.PAREN_EXPR)

        | SynExpr.Quote(_, _, expr, _, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.QUOTE_EXPR)

        | SynExpr.Const(_, _) ->
            x.MarkAndDone(range, ElementType.CONST_EXPR)

        | SynExpr.Typed(expr, synType, _) ->
            Assertion.Assert(rangeContainsRange range synType.Range,
                             "rangeContainsRange range synType.Range; {0}; {1}", range, synType.Range)

            x.PushRange(range, ElementType.TYPED_EXPR)
            x.PushType(synType)
            x.ProcessExpression(expr)

        | SynExpr.Tuple(isStruct, exprs, _, _) ->
            let tupleRangeStart = x.GetStartOffset(range)
            if isStruct then
                x.AdvanceToTokenOrOffset(FSharpTokenType.STRUCT, tupleRangeStart, range)
            else
                x.AdvanceToOffset(tupleRangeStart)

            x.PushRangeForMark(range, x.Mark(), ElementType.TUPLE_EXPR)
            x.ProcessListExpr(exprs)

        | SynExpr.ArrayOrList(_, exprs, _) ->
            x.MarkListExpr(exprs, range, ElementType.ARRAY_OR_LIST_EXPR)

        | SynExpr.AnonRecd(_, copyInfo, fields, _) ->
            x.PushRange(range, ElementType.ANON_RECD_EXPR)

            match copyInfo with
            | Some(expr, _) -> x.ProcessExpression(expr)
            | _ -> ()

            for IdentRange idRange, expr in fields do
                let mark = x.Mark(idRange)
                x.ProcessExpression(expr)
                x.Done(mark, ElementType.RECORD_EXPR_BINDING)

        | SynExpr.Record(_, copyInfo, fields, _) ->
            x.PushRange(range, ElementType.RECORD_EXPR)
            nextSteps.Push(RecordFieldExprListStep(fields))
            match copyInfo with
            | Some(expr, _) -> x.ProcessExpression(expr)
            | _ -> ()

        | SynExpr.New(_, synType, expr, _) ->
            x.PushRange(range, ElementType.NEW_EXPR)
            x.ProcessType(synType)
            x.ProcessExpression(expr)

        | SynExpr.ObjExpr(synType, args, bindings, interfaceImpls, _, _) ->
            x.PushRange(range, ElementType.OBJ_EXPR)
            x.ProcessType(synType)
            nextSteps.Push(InterfaceImplementationListStep(interfaceImpls))
            nextSteps.Push(BindingListStep(bindings))

            match args with
            | Some(expr, _) -> x.ProcessExpression(expr)
            | _ -> ()

        | SynExpr.While(_, whileExpr, doExpr, _) ->
            x.PushRange(range, ElementType.WHILE_EXPR)
            x.PushExpression(doExpr)
            x.ProcessExpression(whileExpr)

        | SynExpr.For(_, id, idBody, _, toBody, doBody, _) ->
            x.PushRange(range, ElementType.FOR_EXPR)
            x.PushExpression(doBody)
            x.PushExpression(toBody)
            x.ProcessLocalId(id)
            x.ProcessExpression(idBody)

        | SynExpr.ForEach(_, _, _, pat, enumExpr, bodyExpr, _) ->
            x.PushRange(range, ElementType.FOR_EACH_EXPR)
            x.ProcessPat(pat, true, false)
            x.PushExpression(bodyExpr)
            x.ProcessExpression(enumExpr)

        | SynExpr.ArrayOrListOfSeqExpr(_, expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.ARRAY_OR_LIST_OF_SEQ_EXPR)

        | SynExpr.CompExpr(_, _, expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.COMP_EXPR)

        | SynExpr.Lambda(_, inLambdaSeq, _, bodyExpr, _) ->
            // Lambdas get "desugared" by converting to fake nested lambdas and match expressions.
            // Simple patterns like ids are preserved in lambdas and more complex ones are replaced
            // with generated placeholder patterns and go to generated match expressions inside lambda bodies.

            // Generated match expression have have a single generated clause with a generated id pattern.
            // Their ranges overlap with lambda param pattern ranges and they have the same start pos as lambdas. 

            Assertion.Assert(not inLambdaSeq, "Expecting non-generated lambda expression, got:\n{0}", expr)
            x.PushRange(range, ElementType.LAMBDA_EXPR)

            let skippedLambdas = skipGeneratedLambdas bodyExpr
            x.MarkLambdaParams(expr, skippedLambdas, true)
            x.ProcessExpression(skipGeneratedMatch skippedLambdas)

        | SynExpr.MatchLambda(_, _, clauses, _, _) ->
            x.PushRange(range, ElementType.MATCH_LAMBDA_EXPR)
            x.ProcessMatchClauses(clauses)

        | SynExpr.Match(_, expr, clauses, _) ->
            x.MarkMatchExpr(range, expr, clauses)

        | SynExpr.Do(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.DO_EXPR)

        | SynExpr.Assert(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.ASSERT_EXPR)

        | SynExpr.App(_, isInfix, funcExpr, argExpr, _) ->
            // todo: mark separate nodes for infix apps
            x.PushRange(range, ElementType.APP_EXPR)
            if isInfix then
                x.PushExpression(funcExpr)
                x.ProcessExpression(argExpr)
            else
                x.PushExpression(argExpr)
                x.ProcessExpression(funcExpr)

        | SynExpr.TypeApp(expr, lessRange, typeArgs, _, greaterRangeOpt, _, _) ->
            x.PushRange(range, ElementType.TYPE_APP_EXPR)
            x.ProcessExpression(expr)

            let mark = x.Mark(lessRange)
            for synType in typeArgs do
                x.ProcessType(synType)

            let endRange = if greaterRangeOpt.IsSome then greaterRangeOpt.Value else range
            x.Done(endRange, mark, ElementType.TYPE_ARGUMENT_LIST)

        | SynExpr.LetOrUse(_, _, bindings, bodyExpr, _) ->
            x.PushRange(range, ElementType.LET_OR_USE_EXPR)
            x.PushExpression(bodyExpr)
            x.ProcessBindings(bindings)

        | SynExpr.TryWith(tryExpr, _, withCases, _, _, _, _) ->
            x.PushRange(range, ElementType.TRY_WITH_EXPR)
            nextSteps.Push(MatchClauseListStep(withCases))
            x.ProcessExpression(tryExpr)

        | SynExpr.TryFinally(tryExpr, finallyExpr, _, _, _) ->
            x.PushRange(range, ElementType.TRY_FINALLY_EXPR)
            x.PushExpression(finallyExpr)
            x.ProcessExpression(tryExpr)

        | SynExpr.Lazy(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.LAZY_EXPR)

        | SynExpr.IfThenElse(ifExpr, thenExpr, elseExprOpt, _, _, _, _) ->
            x.PushRange(range, ElementType.IF_THEN_ELSE_EXPR)
            if elseExprOpt.IsSome then
                x.PushExpression(elseExprOpt.Value)
            x.PushExpression(thenExpr)
            x.ProcessExpression(ifExpr)

        | SynExpr.Ident _ ->
            x.MarkAndDone(range, ElementType.IDENT_EXPR)

        | SynExpr.LongIdent(_, lid, _, _) ->
            let mark = x.Mark(range)
            x.ProcessLongIdentifier(lid.Lid)
            x.Done(range, mark, ElementType.LONG_IDENT_EXPR)

        | SynExpr.LongIdentSet(lid, expr, _) ->
            x.PushRange(range, ElementType.LONG_IDENT_SET_EXPR)
            x.ProcessLongIdentifier(lid.Lid)
            x.ProcessExpression(expr)

        | SynExpr.DotGet(expr, _, lidWithDots, _) ->
            x.PushRange(range, ElementType.DOT_GET_EXPR)
            x.PushLondIdentifier(lidWithDots.Lid)
            x.ProcessExpression(expr)

        | SynExpr.DotSet(expr1, lidWithDots, expr2, _) ->
            x.PushRange(range, ElementType.DOT_SET_EXPR)
            x.PushExpression(expr2)
            x.PushLondIdentifier(lidWithDots.Lid)
            x.PushExpression(expr1)

        | SynExpr.Set(expr1, expr2, _) ->
            x.PushRange(range, ElementType.EXPR_SET_EXPR)
            x.PushExpression(expr2)
            x.ProcessExpression(expr1)

        | SynExpr.NamedIndexedPropertySet(_, expr1, expr2, _) ->
            x.PushRange(range, ElementType.NAMED_INDEXED_PROPERTY_SET)
            x.PushExpression(expr2)
            x.ProcessExpression(expr1)

        | SynExpr.DotNamedIndexedPropertySet(expr1, lidWithDots, expr2, expr3, _) ->
            x.PushRange(range, ElementType.DOT_NAMED_INDEXED_PROPERTY_SET)
            x.PushExpression(expr3)
            x.PushExpression(expr2)
            x.PushLondIdentifier(lidWithDots.Lid)
            x.PushExpression(expr1)

        | SynExpr.DotIndexedGet(expr, indexerArgs, _, _) ->
            x.PushRange(range, ElementType.DOT_INDEXED_GET_EXPR)
            x.ProcessExpression(expr) // todo
            for arg in indexerArgs do
                x.ProcessIndexerArg(arg)

        | SynExpr.DotIndexedSet(expr1, indexerArgs, expr2, _, _ , _) ->
            x.PushRange(range, ElementType.DOT_INDEXED_SET_EXPR)
            x.ProcessExpression(expr1)
            // todo: mark indexer expressions
            for arg in indexerArgs do
                x.ProcessIndexerArg(arg)
            x.ProcessExpression(expr2)

        | SynExpr.TypeTest(expr, synType, _) ->
            x.MarkTypeExpr(expr, synType, range, ElementType.TYPE_TEST_EXPR)

        | SynExpr.Upcast(expr, synType, _) ->
            x.MarkTypeExpr(expr, synType, range, ElementType.UPCAST_EXPR)

        | SynExpr.Downcast(expr, synType, _) ->
            x.MarkTypeExpr(expr, synType, range, ElementType.DOWNCAST_EXPR)

        | SynExpr.InferredUpcast(expr, _)
        | SynExpr.InferredDowncast(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.INFERRED_CAST_EXPR)

        | SynExpr.Null _ ->
            x.MarkAndDone(range, ElementType.NULL_EXPR)

        | SynExpr.AddressOf(_, expr, _, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.ADDRESS_OF_EXPR)

        | SynExpr.TraitCall(_, _, expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.TRAIT_CALL_EXPR)

        | SynExpr.JoinIn(expr1, _, expr2, _) ->
            x.PushRange(range, ElementType.JOIN_IN_EXPR)
            x.PushExpression(expr2)
            x.ProcessExpression(expr1)

        | SynExpr.ImplicitZero _ ->
            x.MarkAndDone(range, ElementType.IMPLICIT_ZERO_EXPR)

        | SynExpr.YieldOrReturn(_, expr, _)
        | SynExpr.YieldOrReturnFrom(_, expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.YIELD_OR_RETURN_EXPR)

        | SynExpr.LetOrUseBang(_, _, _, pat, expr, inExpr, _) ->
            x.PushRange(range, ElementType.LET_OR_USE_BANG_EXPR)
            x.ProcessPat(pat, true, false)
            x.PushExpression(inExpr)
            x.ProcessExpression(expr)

        | SynExpr.MatchBang(_, expr, clauses, _) ->
            x.MarkMatchExpr(range, expr, clauses)

        | SynExpr.DoBang(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.DO_EXPR)

        | SynExpr.LibraryOnlyILAssembly _
        | SynExpr.LibraryOnlyStaticOptimization _
        | SynExpr.LibraryOnlyUnionCaseFieldGet _
        | SynExpr.LibraryOnlyUnionCaseFieldSet _
        | SynExpr.LibraryOnlyILAssembly _ ->
            x.MarkAndDone(range, ElementType.LIBRARY_ONLY_EXPR)

        | SynExpr.ArbitraryAfterError _
        | SynExpr.FromParseError _
        | SynExpr.DiscardAfterMissingQualificationAfterDot _ ->
            x.MarkAndDone(range, ElementType.FROM_ERROR_EXPR)

        | SynExpr.Fixed(expr, _) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.FIXED_EXPR)

        | SynExpr.Sequential(_, _, expr1, expr2, _) ->
            // todo: concat nested sequential expressions
            x.PushRange(range, ElementType.SEQUENTIAL_EXPR)
            x.PushExpression(expr2)
            x.ProcessExpression(expr1)

    member x.MarkLambdaParams(expr, outerBodyExpr, topLevel) =
        match expr with
        | SynExpr.Lambda(_, inLambdaSeq, pats, bodyExpr, _) when inLambdaSeq <> topLevel ->
            x.MarkLambdaParams(pats, bodyExpr, outerBodyExpr)

        | _ -> ()
    
    member x.MarkLambdaParams(pats: SynSimplePats, lambdaBody: SynExpr, outerBodyExpr) =
        match pats with
        | SynSimplePats.SimplePats(pats, _) ->
            // `pats` can be empty for unit patterns.

            x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)

//            match pats with
//            | [pat] ->
////                if posLt range.Start pat.Range.Start then
////                    let mark = x.Mark(range)
//                    x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)
////                    x.Done(range, mark, ElementType.PAREN_PAT)
////                else
////                    x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)
//
//            | _ ->
//                let mark = x.Mark(range) // todo: mark before lparen
//                x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)
//                x.Done(range, mark, ElementType.PAREN_PAT) // todo: marp tuple pat

        | SynSimplePats.Typed _ ->
            failwithf "Expecting SimplePats, got:\n%A" pats

    member x.MarkLambdaParam(pats: SynSimplePat list, lambdaBody: SynExpr, outerBodyExpr) =
        match pats with
        | [] -> x.MarkLambdaParams(lambdaBody, outerBodyExpr, false)
        | pat :: pats ->
            match pat with
            | SynSimplePat.Id(_, _, isGenerated, _, _, range) ->
                if not isGenerated then
                    x.MarkAndDone(range, ElementType.LOCAL_NAMED_PAT)
                    x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)
                else
                    match outerBodyExpr with
                    | SynExpr.Match(_, _, [ Clause(pat, whenExpr, innerExpr, clauseRange, _) ], matchRange) when
                            matchRange.Start = clauseRange.Start ->

                        Assertion.Assert(whenExpr.IsNone, "whenExpr.IsNone")
                        x.ProcessPat(pat, true, false)
                        x.MarkLambdaParam(pats, lambdaBody, innerExpr)

                    | _ ->
                        failwithf "Expecting generated match expression, got:\n%A" lambdaBody
            | _ ->
                x.MarkLambdaParam(pats, lambdaBody, outerBodyExpr)
    
    member x.MarkMatchExpr(range: range, expr, clauses) =
        x.PushRange(range, ElementType.MATCH_EXPR)
        nextSteps.Push(MatchClauseListStep(clauses))
        x.ProcessExpression(expr)

    member x.ProcessMatchClauses(clauses) =
        match clauses with
        | [] -> ()
        | [ clause ] ->
            x.ProcessMatchClause(clause)

        | clause :: clauses ->
            nextSteps.Push(MatchClauseListStep(clauses))
            x.ProcessMatchClause(clause)

    member x.ProcessBindings(clauses) =
        match clauses with
        | [] -> ()
        | [ binding ] ->
            x.ProcessLocalBinding(binding)

        | binding :: bindings ->
            nextSteps.Push(BindingListStep(bindings))
            x.ProcessLocalBinding(binding)
    
    member x.ProcessListExpr(exprs) =
        match exprs with
        | [] -> ()
        | [ expr ] ->
            x.ProcessExpression(expr)

        | [ expr1; expr2 ] ->
            x.PushExpression(expr2)
            x.ProcessExpression(expr1)

        | expr :: rest ->
            x.PushExpressionList(rest)
            x.ProcessExpression(expr)
    
    member x.ProcessInterfaceImplementation(InterfaceImpl(interfaceType, bindings, range)) =
        x.PushRange(range, ElementType.OBJ_EXPR_SECONDARY_INTERFACE)
        x.ProcessType(interfaceType)
        x.ProcessBindings(bindings)

    member x.ProcessAnonRecordFieldExpr(IdentRange idRange, (ExprRange range as expr)) =
        let mark = x.Mark(idRange)
        x.PushRangeForMark(range, mark, ElementType.RECORD_EXPR_BINDING)
        x.ProcessExpression(expr)

    member x.ProcessRecordFieldExpr(lid, expr) =
        match lid, expr with
        | [], None -> ()
        | [], Some(ExprRange range as expr) ->
            x.PushRangeAndProcessExpression(expr, range, ElementType.RECORD_EXPR_BINDING)

        | IdentRange headRange :: _, expr ->
            let mark = x.Mark(headRange)
            x.PushRangeForMark(headRange, mark, ElementType.RECORD_EXPR_BINDING)
            x.ProcessLongIdentifier(lid)
            if expr.IsSome then
                x.ProcessExpression(expr.Value)
    
    member x.MarkListExpr(exprs, range, elementType) =
        x.PushRange(range, elementType)
        x.ProcessListExpr(exprs)

    member x.MarkTypeExpr(expr, synType, range, elementType) =
        x.PushRange(range, elementType)
        x.PushType(synType)
        x.ProcessExpression(expr)

    member x.ProcessMatchClause(Clause(pat, whenExprOpt, expr, _, _) as clause) =
        let range = clause.Range
        let mark = x.MarkTokenOrRange(FSharpTokenType.BAR, range)
        x.PushRangeForMark(range, mark, ElementType.MATCH_CLAUSE)

        x.ProcessPat(pat, true, false)
        match whenExprOpt with
        | Some whenExpr ->
            x.PushExpression(expr)
            x.ProcessExpression(whenExpr)
        | _ ->
            x.ProcessExpression(expr)

    member x.ProcessIndexerArg(arg) =
        for expr in arg.Exprs do
            x.ProcessExpression(expr)


and ITreeBuilderStep =
    abstract DoStep: FSharpImplTreeBuilder * Stack<ITreeBuilderStep> -> unit

and ExpressionListStep(exprs) =
    inherit ProcessListStepBase<SynExpr>(exprs)

    override x.DoStep(builder, expr) =
        builder.ProcessExpression(expr)

and MatchClauseListStep(clauses) =
    inherit ProcessListStepBase<SynMatchClause>(clauses)

    override x.DoStep(builder, clause) =
        builder.ProcessMatchClause(clause)

and BindingListStep(bindings) =
    inherit ProcessListStepBase<SynBinding>(bindings)

    override x.DoStep(builder, binding) =
        builder.ProcessLocalBinding(binding)

and AnonRecordFieldExprListStep(fields) =
    inherit ProcessListStepBase<Ident * SynExpr>(fields)

    override x.DoStep(builder, field) =
        builder.ProcessAnonRecordFieldExpr(field)

and RecordFieldExprListStep(fields) =
    inherit ProcessListStepBase<RecordFieldName * (SynExpr option) * BlockSeparator option>(fields)

    override x.DoStep(builder, field) =
        let (lid, _), expr, _ = field 
        builder.ProcessRecordFieldExpr(lid.Lid, expr)

and InterfaceImplementationListStep(interfaceImpls) =
    inherit ProcessListStepBase<SynInterfaceImpl>(interfaceImpls)

    override x.DoStep(builder, interfaceImpl) =
        builder.ProcessInterfaceImplementation(interfaceImpl)

and EndRangeStep(range: range, mark, elementType: NodeType) =
    interface ITreeBuilderStep with
        member x.DoStep(builder, nextSteps) =
            nextSteps.Pop() |> ignore
            builder.Done(range, mark, elementType)

and ProcessExpressionStep(expr: SynExpr) =
    interface ITreeBuilderStep with
        member x.DoStep(builder, nextSteps) =
            nextSteps.Pop() |> ignore
            builder.ProcessExpression(expr)

and ProcessTypeStep(synType: SynType) =
    interface ITreeBuilderStep with
        member x.DoStep(builder, nextSteps) =
            nextSteps.Pop() |> ignore
            builder.ProcessType(synType)

and ProcessLidStep(lid: LongIdent) =
    interface ITreeBuilderStep with
        member x.DoStep(builder, nextSteps) =
            nextSteps.Pop() |> ignore
            builder.ProcessLongIdentifier(lid)

and
    [<AbstractClass>]
    ProcessListStepBase<'T>(items: 'T list) =
        let mutable items = items

        abstract DoStep: FSharpImplTreeBuilder * 'T -> unit

        interface ITreeBuilderStep with
            member x.DoStep(builder, nextSteps) =
                match items with
                | [] -> nextSteps.Pop() |> ignore

                | [ item ] ->
                    nextSteps.Pop() |> ignore
                    x.DoStep(builder, item)

                | item :: rest ->
                    items <- rest
                    x.DoStep(builder, item)
