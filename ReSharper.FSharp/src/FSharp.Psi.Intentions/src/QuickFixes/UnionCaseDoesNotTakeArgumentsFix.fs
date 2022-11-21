namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes

open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Highlightings
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util
open JetBrains.ReSharper.Psi.ExtensionsAPI
open JetBrains.ReSharper.Psi.ExtensionsAPI.Tree
open JetBrains.ReSharper.Resources.Shell

type UnionCaseDoesNotTakeArgumentsFix(error: UnionCaseDoesNotTakeArgumentsError) =
    inherit FSharpQuickFixBase()

    let pat = error.Pattern.As<IParametersOwnerPat>()

    override x.Text = "This union case does not take arguments"

    override x.IsAvailable _ =
        isValid pat

    override x.ExecutePsiTransaction _ =
        use writeCookie = WriteLockCookie.Create(pat.IsPhysical())
        use disableFormatter = new DisableCodeFormatter()

        let isTopLevel = pat.GetBindingFromHeadPattern() :? ITopBinding
        let nodeType = if isTopLevel then ElementType.TOP_REFERENCE_PAT else ElementType.LOCAL_REFERENCE_PAT

        let oldNode = pat.IgnoreParentParens() // possibly containing parens (or the old param owner pat)
        let topReferencePat = nodeType.Create() // new node
        let topReferencePat = ModificationUtil.AddChildBefore(oldNode, topReferencePat) // new node inserted into the tree before old pattern
        ModificationUtil.AddChild(topReferencePat, pat.ReferenceName) |> ignore // reference name node moved to the new one
        ModificationUtil.DeleteChild(oldNode) // remove the old node