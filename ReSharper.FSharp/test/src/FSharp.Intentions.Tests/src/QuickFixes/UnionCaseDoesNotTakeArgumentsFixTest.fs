namespace JetBrains.ReSharper.Plugins.FSharp.Tests.Intentions.QuickFixes

open JetBrains.ReSharper.Plugins.FSharp
open JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.QuickFixes
open JetBrains.ReSharper.Plugins.FSharp.Tests
open NUnit.Framework

[<FSharpTest>]
type UnionCaseDoesNotTakeArgumentsFixTest() =
    inherit FSharpQuickFixTestBase<UnionCaseDoesNotTakeArgumentsFix>()
    override x.RelativeTestDataPath = "features/quickFixes/unionCaseDoesNotTakeArgumentsFix"

        
    [<Test>] member x.``Two names`` () = x.DoNamedTest()
    [<Test; DumpPsiTree>] member x.``Local 01`` () = x.DoNamedTest()
    [<Test; DumpPsiTree>] member x.``Local 02`` () = x.DoNamedTest()
    [<Test>] member x.``Single parameter`` () = x.DoNamedTest()
    
    [<Test>] member x.``Two parameters`` () = x.DoNamedTest()