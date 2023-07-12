namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes

open FSharp.Compiler.Symbols
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Highlightings
open JetBrains.ReSharper.Psi.ExtensionsAPI
open JetBrains.ReSharper.Psi.Tree
open JetBrains.ReSharper.Resources.Shell

type UpdateLiteralConstantFix(error: LiteralConstantValuesDifferError) =
    inherit FSharpQuickFixBase()
    let errorRefPat = error.Pat.As<IReferencePat>()
    
    let tryFindDeclarationFromSignature () =
        let containingTypeDecl = errorRefPat.GetContainingTypeDeclaration()
        let decls = containingTypeDecl.DeclaredElement.GetDeclarations()
        decls |> Seq.tryFind (fun d -> d.GetSourceFile().IsFSharpSignatureFile)
        
    let tryFindSigBindingSignature sigMembers =
        let p = errorRefPat.Binding.HeadPattern.As<IFSharpPattern>()
        if Seq.length p.Declarations = 1 then
            let implDec = Seq.head p.Declarations
            let declName = implDec.DeclaredName
            sigMembers
            |>  Seq.tryPick(fun m ->
                let bindingSignature = m.As<IBindingSignature>()
                match bindingSignature with
                | null -> None
                | _ ->
                    match bindingSignature.HeadPattern with
                    | :? IReferencePat as sigRefPat when
                        declName = sigRefPat.DeclaredName -> Some bindingSignature
                    | _ -> None
                )
        else
            None

    let mutable sigRefPat = null

    override x.Text = $"Update literal constant {errorRefPat.Identifier.Name} in signature"

    override x.IsAvailable _ =
        // Todo reuse/extend SignatureFixUtil
        if isNull errorRefPat then false else

        match tryFindDeclarationFromSignature () with
        | Some sigDecl ->
            let sigMembers = sigDecl.As<IModuleDeclaration>().Members
            let sigBindingSignature = tryFindSigBindingSignature sigMembers
            match sigBindingSignature with
            | None -> false
            | Some s ->
                match s.HeadPattern with
                | :? IReferencePat as sRefPat ->
                    sigRefPat <- sRefPat
                    let refExpr = errorRefPat.Binding.Expression.As<IReferenceExpr>()
                    if isNull refExpr then true else
                    
                    refExpr.Reference.ResolveWithFcs(sRefPat, System.String.Empty, false, refExpr.IsQualified)
                    |> Option.isSome
                
                | _ -> false
        
        | _ -> false

    override x.ExecutePsiTransaction _ =
        use writeCookie = WriteLockCookie.Create(sigRefPat.IsPhysical())
        use disableFormatter = new DisableCodeFormatter()
        
        let sigSymbolUse = sigRefPat.GetFcsSymbolUse()
        let implSymbolUse = errorRefPat.GetFcsSymbolUse()
        let implMfv = implSymbolUse.Symbol :?> FSharpMemberOrFunctionOrValue
        let sigMfv = sigSymbolUse.Symbol :?> FSharpMemberOrFunctionOrValue
        if implMfv.FullType.BasicQualifiedName <> sigMfv.FullType.BasicQualifiedName then
            let returnTypeString = implMfv.ReturnParameter.Type.Format(sigSymbolUse.DisplayContext)
            let factory = sigRefPat.CreateElementFactory()
            let typeUsage = factory.CreateTypeUsage(returnTypeString, TypeUsageContext.TopLevel)
            sigRefPat.Binding.ReturnTypeInfo.SetReturnType(typeUsage) |> ignore

        sigRefPat.Binding.SetExpression(errorRefPat.Binding.Expression.Copy()) |> ignore
