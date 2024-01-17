﻿namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Generate

open JetBrains.Application.Progress
open JetBrains.Diagnostics
open JetBrains.ReSharper.Feature.Services.Generate
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Generate
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Util
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Cache2
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util
open JetBrains.ReSharper.Plugins.FSharp.Util
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.DataContext
open JetBrains.ReSharper.Psi.ExtensionsAPI
open JetBrains.ReSharper.Psi.ExtensionsAPI.Tree
open JetBrains.ReSharper.Psi.Tree
open JetBrains.ReSharper.Psi.Util
open JetBrains.ReSharper.Resources.Shell

[<Language(typeof<FSharpLanguage>)>]
type FSharpGeneratorContextFactory() =
    interface IGeneratorContextFactory with
        member x.TryCreate(kind: string, psiDocumentRangeView: IPsiDocumentRangeView): IGeneratorContext =
            let psiView = psiDocumentRangeView.View<FSharpLanguage>()

            let treeNode = psiView.GetSelectedTreeNode()
            if isNull treeNode then null else

            let tryGetPreviousTypeDecl (treeNode: ITreeNode) =
                let prevToken = treeNode.GetPreviousMeaningfulToken()
                if isNull prevToken then null else

                prevToken.GetContainingNode<IFSharpTypeDeclaration>()

            let typeDeclaration: IFSharpTypeElementDeclaration =
                match psiView.GetSelectedTreeNode<IFSharpTypeDeclaration>() with
                | null ->
                    match psiView.GetSelectedTreeNode<IObjExpr>() with
                    | null -> tryGetPreviousTypeDecl treeNode
                    | objExpr -> objExpr
                | typeDeclaration -> typeDeclaration

            let anchor = GenerateOverrides.getAnchorNode psiView typeDeclaration
            FSharpGeneratorContext.Create(kind, treeNode, typeDeclaration, anchor) :> _

        member x.TryCreate(kind, treeNode, anchor) =
            let typeDecl = treeNode.As<IFSharpTypeElementDeclaration>()
            FSharpGeneratorContext.Create(kind, treeNode, typeDecl, anchor) :> _

        member x.TryCreate(_: string, _: IDeclaredElement): IGeneratorContext = null

[<GeneratorElementProvider(GeneratorStandardKinds.Overrides, typeof<FSharpLanguage>)>]
[<GeneratorElementProvider(GeneratorStandardKinds.MissingMembers, typeof<FSharpLanguage>)>]
type FSharpOverridableMembersProvider() =
    inherit GeneratorProviderBase<FSharpGeneratorContext>()

    let canHaveOverrides (typeElement: ITypeElement) =
        // todo: filter out union cases
        match typeElement with
        | :? FSharpClass as fsClass -> not (fsClass.IsAbstract && fsClass.IsSealed)
        | :? IStruct -> true
        | _ -> false // todo: interfaces with default impl

    let getTestDescriptor (overridableMember: ITypeMember) =
        GeneratorElementBase.GetTestDescriptor(overridableMember, overridableMember.IdSubstitution)

    override x.Populate(context: FSharpGeneratorContext) =
        let missingMembersOnly = context.Kind = GeneratorStandardKinds.MissingMembers
        GenerateOverrides.getOverridableMembers context.TypeDeclaration missingMembersOnly
        |> Seq.iter context.ProvidedElements.Add


[<GeneratorBuilder(GeneratorStandardKinds.Overrides, typeof<FSharpLanguage>)>]
[<GeneratorBuilder(GeneratorStandardKinds.MissingMembers, typeof<FSharpLanguage>)>]
type FSharpOverridingMembersBuilder() =
    inherit GeneratorBuilderBase<FSharpGeneratorContext>()

    let addNewLineBeforeReprIfNeeded (typeDecl: IFSharpTypeDeclaration) (typeRepr: ITypeRepresentation) =
        if isNull typeRepr || typeDecl.Identifier.StartLine <> typeRepr.StartLine then () else

        let indentSize = typeDecl.GetIndentSize()
        let desiredIndent = typeDecl.Indent + indentSize

        let origTypeReprIndent = typeRepr.Indent
        addNodesBefore typeRepr [
            NewLine(typeRepr.GetLineEnding())
            Whitespace(origTypeReprIndent)
        ] |> ignore

        let normalizeRepr (objRepr: IObjectModelTypeRepresentation) =
            if isNull objRepr.BeginKeyword then () else

            let diff =
                if objRepr.BeginKeyword.Indent > desiredIndent then -(objRepr.BeginKeyword.Indent - desiredIndent)
                else (desiredIndent - objRepr.BeginKeyword.Indent)
            shiftWithWhitespaceBefore diff objRepr

        let normalizeReprEnd (beginToken: ITokenNode) (endToken: ITokenNode) =
            if isNull beginToken || isNull endToken then () else

            if beginToken.Indent > endToken.Indent then
                let diff = beginToken.Indent - endToken.Indent
                addNodeBefore endToken (Whitespace(diff))

            elif endToken.Indent > beginToken.Indent && isFirstMeaningfulNodeOnLine endToken then
                let diff = endToken.Indent - beginToken.Indent
                shiftWithWhitespaceBefore -diff endToken

        let shiftToNext (token: ITokenNode) =
            if isNull token then () else

            let next = token.GetNextMeaningfulToken()
            let diff = if isNull next then 0 else token.Indent - next.Indent
            if diff > 0 then shiftWithWhitespaceBefore -diff token

        let beginToken, endToken =
            match typeRepr with
            | :? IObjectModelTypeRepresentation as objRepr ->
                // at this point the body of the repr can be before or after the begin keyword
                shiftToNext objRepr.BeginKeyword
                normalizeRepr objRepr
                normalizeReprEnd objRepr.BeginKeyword objRepr.EndKeyword
                objRepr.BeginKeyword, objRepr.EndKeyword

            | :? IRecordRepresentation as recordRepr ->
                let diff = origTypeReprIndent - desiredIndent
                if diff > 0 then shiftWithWhitespaceBefore -diff recordRepr
                normalizeReprEnd recordRepr.LeftBrace recordRepr.RightBrace
                recordRepr.LeftBrace, recordRepr.RightBrace

            | :? IUnionRepresentation
            | :? ITypeAbbreviationRepresentation as typeRepr ->
                let diff = origTypeReprIndent - desiredIndent
                if diff > 0 then shiftWithWhitespaceBefore -diff typeRepr
                null, null

            | _ -> null, null

        let reindentRange additionalIndent (range: TreeRange) =
            for node in range do
                if not (isFirstMeaningfulNodeOnLine node) then () else

                let diff =
                    if node == typeRepr then
                        origTypeReprIndent - (desiredIndent + additionalIndent)
                    else
                        node.Indent - (desiredIndent + additionalIndent)
                shiftWithWhitespaceBefore -diff node

        reindentRange indentSize (TreeRange(getNextSibling beginToken, getPrevSibling endToken))
        reindentRange 0 (TreeRange(typeDecl.TypeRepresentation.NextSibling, typeDecl.LastChild))

    override this.IsAvailable(context: FSharpGeneratorContext): bool =
        isNotNull context.TypeDeclaration && isNotNull context.TypeDeclaration.DeclaredElement

    override x.Process(context: FSharpGeneratorContext, _: IProgressIndicator) =
        use writeCookie = WriteLockCookie.Create(true)
        use disableFormatter = new DisableCodeFormatter()

        let typeDecl = context.Root :?> IFSharpTypeElementDeclaration

        let (anchor: ITreeNode), indent =
            match typeDecl with
            | :? IFSharpTypeDeclaration as typeDecl ->
                match typeDecl.TypeRepresentation with
                | :? IUnionRepresentation as unionRepr ->
                    unionRepr.UnionCasesEnumerable
                    |> Seq.tryHead
                    |> Option.iter EnumCaseLikeDeclarationUtil.addBarIfNeeded

                | :? ITypeAbbreviationRepresentation as abbrRepr when abbrRepr.CanBeUnionCase ->
                    let factory = typeDecl.CreateElementFactory()
                    let caseName = FSharpNamingService.mangleNameIfNecessary abbrRepr.AbbreviatedTypeOrUnionCase.SourceName
                    let declGroup = factory.CreateModuleMember($"type U = | {caseName}") :?> ITypeDeclarationGroup
                    let typeDeclaration = declGroup.TypeDeclarations[0] :?> IFSharpTypeDeclaration
                    let repr = typeDeclaration.TypeRepresentation
                    let newRepr = typeDecl.SetTypeRepresentation(repr)
                    if context.Anchor == abbrRepr then context.Anchor <- newRepr

                | _ -> ()

                let typeRepr = typeDecl.TypeRepresentation
                addNewLineBeforeReprIfNeeded typeDecl typeRepr

                let anchor: ITreeNode =
                    let deleteTypeRepr (typeDecl: IFSharpTypeDeclaration) : ITreeNode =
                        let equalsToken = typeDecl.EqualsToken.NotNull()

                        let equalsAnchor =
                            let afterComment = getLastMatchingNodeAfter isInlineSpaceOrComment equalsToken
                            let afterSpace = getLastMatchingNodeAfter isInlineSpace equalsToken
                            if afterComment != afterSpace then afterComment else equalsToken :> _

                        let prev = typeRepr.GetPreviousNonWhitespaceToken()
                        if prev.IsCommentToken() then
                            deleteChildRange prev.NextSibling typeRepr
                            prev
                        else
                            deleteChildRange equalsAnchor.NextSibling typeRepr
                            equalsAnchor

                    let anchor =
                        let isEmptyClassRepr =
                            match typeRepr with
                            | :? IClassRepresentation as classRepr ->
                                let classKeyword = classRepr.BeginKeyword
                                let endKeyword = classRepr.EndKeyword

                                isNotNull classKeyword && isNotNull endKeyword &&
                                classKeyword.GetNextNonWhitespaceToken() == endKeyword
                            | _ -> false

                        if isEmptyClassRepr then
                            deleteTypeRepr typeDecl
                        else
                            context.Anchor

                    if isNotNull anchor then anchor else

                    let typeMembers = typeDecl.TypeMembers
                    if not typeMembers.IsEmpty then typeMembers.Last() :> _ else

                    if isNull typeRepr then
                        typeDecl.EqualsToken.NotNull() else

                    let objModelTypeRepr = typeRepr.As<IObjectModelTypeRepresentation>()
                    if isNull objModelTypeRepr then typeRepr :> _ else

                    let typeMembers = objModelTypeRepr.TypeMembers
                    if not typeMembers.IsEmpty then typeMembers.Last() :> _ else

                    objModelTypeRepr

                match anchor with
                | :? IStructRepresentation as structRepr ->
                    structRepr.BeginKeyword :> _, structRepr.BeginKeyword.Indent + typeDecl.GetIndentSize()

                | :? ITypeRepresentation as typeRepr ->
                    typeRepr, typeRepr.Indent

                | treeNode ->
                    let parent =
                        if isNotNull typeRepr && typeRepr.Contains(treeNode) then typeRepr :> ITreeNode else treeNode.Parent
                    match parent with
                    | :? IObjectModelTypeRepresentation as repr when treeNode != repr.EndKeyword ->
                        let indent =
                            match repr.TypeMembersEnumerable |> Seq.tryHead with
                            | Some memberDecl -> memberDecl.Indent
                            | _ -> repr.BeginKeyword.Indent + typeDecl.GetIndentSize()
                        let treeNode =
                            let doOrLastLet =
                                repr.TypeMembersEnumerable
                                |> Seq.takeWhile (fun x -> x :? ILetBindingsDeclaration || x :? IDoStatement)
                                |> Seq.tryLast
                            match doOrLastLet with
                            | Some node -> node :> ITreeNode
                            | _ -> treeNode
                        treeNode, indent
                    | _ ->

                    let indent =
                        match typeDecl.TypeMembersEnumerable |> Seq.tryHead with
                        | Some memberDecl -> memberDecl.Indent
                        | _ ->

                        if isNotNull typeRepr then typeDecl.Indent + typeDecl.GetIndentSize() else

                        let typeDeclarationGroup = TypeDeclarationGroupNavigator.GetByTypeDeclaration(typeDecl).NotNull()
                        typeDeclarationGroup.Indent + typeDecl.GetIndentSize()

                    anchor, indent

            | :? IObjExpr as objExpr ->
                if isNull objExpr.WithKeyword then
                    addNodesAfter objExpr.TypeName [
                        Whitespace()
                        FSharpTokenType.WITH.CreateLeafElement()
                    ] |> ignore
            
                objExpr.WithKeyword, objExpr.GetIndentSize()

            | typeDecl -> failwith $"Unexpected typeDecl: {typeDecl}"

        let anchor =
            if isAtEmptyLine anchor then
                let first = getFirstMatchingNodeBefore isInlineSpace anchor |> getThisOrPrevNewLine
                let last = getLastMatchingNodeAfter isInlineSpace anchor

                let anchor = first.PrevSibling
                deleteChildRange first last
                anchor
            else
                anchor

        let anchor = GenerateOverrides.addEmptyLineBeforeIfNeeded anchor

        let missingMembersOnly = context.Kind = GeneratorStandardKinds.MissingMembers

        let inputElements =
            if missingMembersOnly then context.InputElements |> Seq.cast<FSharpGeneratorElement> else

            context.InputElements
            |> Seq.collect (fun generatorElement ->
                let e = generatorElement :?> FSharpGeneratorElement
                let mfv = e.Mfv
                let prop = e.Member.As<IProperty>()

                if isNull prop || not (mfv.IsNonCliEventProperty()) then [e] else

                [ if isNotNull prop.Getter && mfv.HasGetterMethod then
                      FSharpGeneratorElement(prop.Getter, { e.MfvInstance with Mfv = mfv.GetterMethod }, e.AddTypes)
                  if isNotNull prop.Setter && mfv.HasSetterMethod then
                      FSharpGeneratorElement(prop.Setter, { e.MfvInstance with Mfv = mfv.SetterMethod }, e.AddTypes) ])

        let lastNode =
            inputElements
            |> Seq.cast
            |> Seq.map (GenerateOverrides.generateMember typeDecl indent)
            |> Seq.collect (withNewLineAndIndentBefore indent)
            |> addNodesAfter anchor

        GenerateOverrides.addSpaceAfterIfNeeded lastNode

        let nodes = anchor.RightSiblings()
        let selectedRange = GenerateOverrides.getGeneratedSelectionTreeRange lastNode nodes
        context.SetSelectedRange(selectedRange)
