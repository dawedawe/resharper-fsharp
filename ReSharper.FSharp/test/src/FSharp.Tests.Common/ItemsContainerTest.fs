module rec JetBrains.ReSharper.Plugins.FSharp.Tests.Common.ItemsContainerTest

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Text.RegularExpressions
open JetBrains.Application.BuildScript.Application.Zones
open JetBrains.Application.Environment
open JetBrains.Platform.MsBuildHost.Models
open JetBrains.ProjectModel
open JetBrains.ProjectModel.ProjectsHost
open JetBrains.ProjectModel.ProjectsHost
open JetBrains.ProjectModel.ProjectsHost.Impl
open JetBrains.ProjectModel.ProjectsHost.MsBuild
open JetBrains.ReSharper.Plugins.FSharp.Common.Util
open JetBrains.ReSharper.Plugins.FSharp.ProjectModel.ProjectItems.ItemsContainer
open JetBrains.ReSharper.TestFramework
open JetBrains.TestFramework
open JetBrains.TestFramework.Application.Zones
open JetBrains.Util
open Moq

type Assert = NUnit.Framework.Assert
type TestAttribute = NUnit.Framework.TestAttribute
type TestFixtureAttribute = NUnit.Framework.TestFixtureAttribute

let projectDirectory = FileSystemPath.Parse(@"C:\Solution\Project")
let solutionMark = SolutionMarkFactory.Create(projectDirectory.Combine("Solution.sln"))
let projectMark = DummyProjectMark(solutionMark, "Project", Guid.Empty, projectDirectory.Combine("Project.fsproj"))

let projectPath (relativePath: string) = projectDirectory / relativePath 

let (|NormalizedPath|) (path: FileSystemPath) =
    path.MakeRelativeTo(projectDirectory).NormalizeSeparators(FileSystemPathEx.SeparatorStyle.Unix)

let (|AbsolutePath|) (path: FileSystemPath) =
    path.MakeAbsoluteBasedOn(projectDirectory)

let removeIdentities path =
    Regex.Replace(path, @"\[\d+\]", String.Empty);


let createContainer items writer =
    let container = LoggingFSharpItemsContainer(writer, createRefresher writer)
    let rdItems =
        items |> List.map (fun { ItemType = itemType; EvaluatedInclude = evaluatedInclude; Link = link } ->
            let evaluatedInclude = removeIdentities evaluatedInclude
            let metadata =
                match link with
                | null -> []
                | _ -> [RdProjectMetadata("Link", link)]
            RdProjectItem(itemType, evaluatedInclude, RdThisProjectItemOrigin(), metadata.ToList(id)))

    let rdProject = RdProject(List(), rdItems.ToList(id), List(), List(), List(), List(), List())
    let rdProjectDescription =
        RdProjectDescription(projectDirectory.FullPath, projectMark.Location.FullPath, null, List(), List())
    let msBuildProject = MsBuildProject(projectMark, Dictionary(), [rdProject].ToList(id), rdProjectDescription)

    (container :> IFSharpItemsContainer).OnProjectLoaded(projectMark, msBuildProject)
    container


let createRefresher (writer: TextWriter) =
    { new IFSharpItemsContainerRefresher with
        member x.Refresh(_, initial) =
            if not initial then writer.WriteLine("Refresh whole project")

        member x.Refresh(_, NormalizedPath path, id) =
            writer.WriteLine(sprintf "Refresh %s[%O]" path id)

        member x.Update(_, NormalizedPath path) =
            writer.WriteLine(sprintf "Update view %s" path)

        member x.Update(_, NormalizedPath path, id) =
            writer.WriteLine(sprintf "Update view %s[%O]" path id)

        member x.ReloadProject(projectMark) =
            writer.WriteLine(sprintf "Reload project %s" projectMark.Name)

        member x.SelectItem(_, _) = () }


let createViewFile path (solutionItems: IDictionary<FileSystemPath, IProjectItem>) =
    FSharpViewFile(getOrCreateFile path solutionItems)

let createViewFolder path id solutionItems =
    FSharpViewFolder(getOrCreateFolder path solutionItems, { Identity = id })

let getOrCreateFile (AbsolutePath path) (solutionItems): IProjectFile =
    solutionItems.GetOrCreateValue(path, fun () ->

    let file = Mock<IProjectFile>()
    file.Setup(fun x -> x.Name).Returns(path.Name) |> ignore
    file.Setup(fun x -> x.Location).Returns(path) |> ignore
    file.Setup(fun x -> x.ParentFolder).Returns(getOrCreateFolder path.Parent solutionItems) |> ignore
    file.Setup(fun x -> x.GetProject()).Returns(fun _ ->
        getOrCreateFolder projectDirectory solutionItems :?> _) |> ignore
    file.Object :> IProjectItem) :?> _


let getOrCreateFolder (AbsolutePath path) solutionItems: IProjectFolder =
    solutionItems.GetOrCreateValue(path, fun () ->

    let solution = Mock<ISolution>()
    solution.Setup(fun x -> x.FindProjectItemsByLocation(It.IsAny()))
        .Returns(fun path -> [solutionItems.TryGetValue(path)].AsCollection()) |> ignore

    let folder = Mock<IProjectFolder>()
    folder.Setup(fun x -> x.Name).Returns(path.Name) |> ignore
    folder.Setup(fun x -> x.Location).Returns(path) |> ignore
    folder.Setup(fun x -> x.ParentFolder).Returns(fun _ -> getOrCreateFolder path.Parent solutionItems) |> ignore
    folder.Setup(fun x -> x.GetSolution()).Returns(solution.Object) |> ignore
    folder.Setup(fun x -> x.GetProject()).Returns(fun _ ->
        getOrCreateFolder projectDirectory solutionItems :?> _) |> ignore

    if path.Equals(projectDirectory) then
        let project = folder.As<IProject>()
        project.Setup(fun x -> x.GetData(ProjectsHostExtensions.ProjectMarkKey)).Returns(projectMark) |> ignore

    folder.Object :> IProjectItem) :?> _


let createViewItems solutionItems item : seq<FSharpViewItem> = seq {
    let components = item.EvaluatedInclude.Split('/')

    let mutable path = projectDirectory
    for itemComponent in Seq.take (components.Length - 1) components do
        let matched = Regex.Match(itemComponent, @"(?<name>\w+)\[(?<identity>\d+)\]")
        Assert.IsTrue(matched.Success)

        let name = matched.Groups.["name"].Value
        let id = Int32.Parse(matched.Groups.["identity"].Value)
        path <- path.Combine(name)

        yield createViewFolder path id solutionItems :> _

    path <- path.Combine(Array.last components |> removeIdentities)
    yield
        match item.ItemType with
        | Folder -> createViewFolder path 1 solutionItems :> _
        | _ -> createViewFile path solutionItems :> _ }


let createItem itemType evaluatedInclude =
    { ItemType = itemType; EvaluatedInclude = evaluatedInclude; Link = null }

let link link item =
    { item with Link = link }


[<TestFixture>]
type FSharpItemsContainerTest() =
    inherit BaseTestNoShell()

    override x.RelativeTestDataPath = "common/itemsContainer"

    [<Test>]
    member x.``Initialization 01``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "Folder[1]/SubFolder[1]/File1"
              createItem "Compile" "Folder[1]/SubFolder[1]/File2"
              createItem "Compile" "Folder[1]/OtherSubFolder[1]/Data[1]/File3"
              createItem "Compile" "Folder[1]/File4"
              createItem "Compile" "File5"
              createItem "Compile" "Folder[2]/SubFolder[2]/File6"
              createItem "Compile" "Folder[2]/SubFolder[2]/File7"
              createItem "Compile" "File8" ])

    [<Test>]
    member x.``Initialization 02 - CompileBefore``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile"       "File2"
              createItem "CompileBefore" "File1" ])

    [<Test>]
    member x.``Initialization 03 - CompileBefore folder``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile"       "Folder[1]/File2"
              createItem "Compile"       "File3"
              createItem "CompileBefore" "Folder[1]/File1"
              createItem "Compile"       "File4" ])

    [<Test>]
    member x.``Initialization 04 - CompileBefore, CompileAfter``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile"       "File3"
              createItem "CompileAfter"  "File4"
              createItem "CompileBefore" "File1"
              createItem "CompileAfter"  "File5"
              createItem "CompileBefore" "File2" ])

    [<Test>]
    member x.``Initialization 05 - CompileBefore, CompileAfter, folders``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile"       "Folder[1]/File3"
              createItem "CompileAfter"  "Folder[1]/File4"
              createItem "CompileBefore" "File1"
              createItem "CompileAfter"  "File5"
              createItem "CompileBefore" "Folder[1]/File2" ])

    [<Test>]
    member x.``Initialization 06 - CompileBefore, folders``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile"       "File3"
              createItem "Compile"       "Folder[2]/File4"
              createItem "Compile"       "File5"
              createItem "CompileBefore" "File1"
              createItem "CompileBefore" "Folder[1]/File2"
              createItem "CompileAfter"  "Folder[3]/File6"
              createItem "CompileAfter"  "File7" ])

    [<Test>]
    member x.``Initialization 07 - Linked files``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "..\\ExternalFolder\\File2"
              createItem "Compile" "..\\ExternalFolder\\File3" |> link "File3"
              createItem "Compile" "..\\ExternalFolder\\File4" |> link "LinkFolder\\File4" ])

    [<Test>]
    member x.``Initialization 08 - Empty folders``() =
        x.DoContainerInitializationTest(
            [ createItem "Compile" "File1"
              createItem "Folder"  "Empty1[1]"
              createItem "Compile" "File2"
              createItem "Compile" "Folder[1]/File3"
              createItem "Folder"  "Folder[1]/Empty2[1]"
              createItem "Compile" "Folder[1]/File4"
              createItem "Compile" "File5"
              createItem "Folder"  "Folder[2]/Empty3[1]" ])

    [<Test>]
    member x.``Add file 01 - Empty project``() =
        x.DoContainerModificationTest(([]: string list),
            fun container writer ->
                container.OnAddFile("Compile", "File1", null, None))
    
    [<Test>]
    member x.``Add file 02 - No relative``() =
        x.DoContainerModificationTest(([]: string list),
            fun container writer ->
                container.OnAddFile("Compile", "Folder/Subfolder/File1", null, None))

    [<Test>]
    member x.``Add file 03 - Split folders top level``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/File1"
              "Folder[1]/File2" ],
            "File3",
            "Folder/File2",
            "Folder/File1")

    [<Test>]
    member x.``Add file 04 - Split nested folders``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/SubFolder[1]/File2" ],
            "Folder/File3",
            "Folder/SubFolder/File2",
            "Folder/SubFolder/File1")

    [<Test>]
    member x.``Add file 05 - Split nested folders, add folders``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/SubFolder[1]/File2"
              "Folder[1]/Another[1]/SubFolder[1]/File3"
              "Folder[1]/File4" ],
            "Folder/Another/SubFolder/File5",
            "Folder/SubFolder/File2",
            "Folder/SubFolder/File1")

    [<Test>]
    member x.``Add file 06 - Add relative folders``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/SubFolder[1]/File1"],
            "Folder/Another/File2",
            null,
            "Folder/SubFolder/File1")

    [<Test>]
    member x.``Add file 07 - Top level``() =
        x.DoAddFileRelaviteToTests(
            [ "File1"
              "File2"
              "File3"
              "File4"
              "File5" ],
            "File6",
            "File4",
            "File3")

    [<Test>]
    member x.``Add file 08 - Top level, add folders``() =
        x.DoAddFileRelaviteToTests(
            [ "File1"
              "File2"
              "File3"
              "File4"
              "File5" ],
            "Folder/File6",
            "File4",
            "File3")

    [<Test>]
    member x.``Add file 09 - No relative, refresh``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/File2"],
            fun container writer ->
                container.OnAddFile("Compile", "Folder/Subfolder/File1", null, None))

    [<Test>]
    member x.``Add file 10 - No relative, refresh nested``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File2" ],
            fun container writer ->
                container.OnAddFile("Compile", "Folder/Subfolder/Another/File1", null, None))

    [<Test>]
    member x.``Add file 11 - Before first file in folder``() =
        x.DoAddFileRelativeBeforeTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "File4" ],
            "File5",
            "Folder/File3")
    
    [<Test>]
    member x.``Add file 12 - Before first file in nested folder``() =
        x.DoAddFileRelativeBeforeTest(
            [ "File1"
              "File2"
              "Folder[1]/Subfolder[1]/File3"
              "File4" ],
            "File5",
            "Folder/Subfolder/File3")

    [<Test>]
    member x.``Add file 13 - Before first file in nested folders``() =
        x.DoAddFileRelativeBeforeTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Folder/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Add file 14 - Before first file in nested folders, different parent``() =
        x.DoAddFileRelativeBeforeTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Folder/Another/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Add file 15 - Before first file in nested folder, different parent``() =
        x.DoAddFileRelativeBeforeTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Another/Subfolder/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Add file 16 - Split nested folders``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/SubFolder[1]/File2" ],
            "File3",
            "Folder/SubFolder/File2",
            "Folder/SubFolder/File1")

    [<Test>]
    member x.``Add file 17 - Split nested folders``() =
        x.DoAddFileRelaviteToTests(
            [ "Folder[1]/File1"
              "Folder[1]/File2"
              "Folder[1]/SubFolder[1]/File3"
              "Folder[1]/File4" ],
            "File5",
            "Folder/SubFolder/File3",
            "Folder/File2")

    [<Test>]
    member x.``Add file 18 - Split nested folders``() =
        x.DoAddFileRelaviteToTests(
            [ "File1"
              "Folder[1]/File2"
              "Folder[1]/File3"
              "Folder[1]/SubFolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "File7",
            "Folder/SubFolder/File4",
            "Folder/File3")

    [<Test>]
    member x.``Add file 19 - After last file in folder``() =
        x.DoAddFileRelativeAfterTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "File4" ],
            "File5",
            "Folder/File3")

    [<Test>]
    member x.``Add file 20 - After last file in folder``() =
        x.DoAddFileRelativeAfterTest(
            [ "File1"
              "File2"
              "Folder[1]/Subfolder[1]/File3"
              "File4" ],
            "File5",
            "Folder/Subfolder/File3")

    [<Test>]
    member x.``Add file 21 - After last file in nested folder``() =
        x.DoAddFileRelativeAfterTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Folder/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Add file 22 - After last file in nested folder, different parent``() =
        x.DoAddFileRelativeAfterTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Folder/Another/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Add file 23 - After last file in nested folder, different parent``() =
        x.DoAddFileRelativeAfterTest(
            [ "File1"
              "File2"
              "Folder[1]/File3"
              "Folder[1]/Subfolder[1]/File4"
              "Folder[1]/File5"
              "File6" ],
            "Another/Subfolder/File7",
            "Folder/Subfolder/File4")

    [<Test>]
    member x.``Remove file 01 - Top level``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/Subfolder[1]/File1"
              "Folder[1]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "File1"))

    [<Test>]
    member x.``Remove file 02 - Remove file in subfolder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/Subfolder/File1"))

    [<Test>]
    member x.``Remove file 03 - Remove file in nested subfolder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/Subfolder[1]/File1"
              "Folder[1]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/Subfolder/Subfolder/File1"))

    [<Test>]
    member x.``Remove file 04 - Remove empty splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/File2"
              "File3"
              "Folder[2]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/File2"))

    [<Test>]
    member x.``Remove file 05 - Remove empty splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/File2"
              "File3"
              "Folder[2]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/File4"))

    [<Test>]
    member x.``Remove file 06 - Join splitted folders``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/Subfolder[1]/File1"
              "Folder[1]/File2"
              "Folder[1]/Subfolder[2]/File3"
              "Folder[1]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/File2"))

    [<Test>]
    member x.``Remove file 07 - Remove nested empty splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File2"
              "File3"
              "Folder[2]/SubFolder[2]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/SubFolder/File4"))

    [<Test>]
    member x.``Remove file 08 - Remove empty splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/File2"
              "File3"
              "Folder[2]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/File2"))

    [<Test>]
    member x.``Remove file 09 - Remove nested empty splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File2"
              "File3"
              "Folder[2]/SubFolder[2]/File4" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Folder/SubFolder/File2"))

    [<Test>]
    member x.``Remove file 10 - Remove splitted folder and join relative splitted``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File2"
              "Another[1]/File3"
              "Folder[2]/SubFolder[2]/File4"
              "Another[2]/File5" ],
            fun container writer ->
                container.OnRemoveFile("Compile", "Another/File3"))

    [<Test>]
    member x.``Modification 01 - Move file``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/File4" ],
            fun container writer ->
                writer.WriteLine("Move 'Folder/File4' after 'File1':")
                container.OnRemoveFile("Compile", "Folder/File4");
                container.OnAddFile("Compile", "File4", "File1", Some RelativeToType.After))

    [<Test>]
    member x.``Create modification context 01 - No modification``() =
        x.DoCreateModificationContextTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "File2"
              createItem "Compile" "File3" ])

    [<Test>]
    member x.``Create modification context 02 - Single file folder``() =
        x.DoCreateModificationContextTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "Folder[1]/File2"
              createItem "Compile" "File3" ])

    [<Test>]
    member x.``Create modification context 03 - Multiple files folder``() =
        x.DoCreateModificationContextTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "Folder[1]/File2"
              createItem "Compile" "Folder[1]/File3"
              createItem "Compile" "Folder[1]/File4"
              createItem "Compile" "File3" ])

    [<Test>]
    member x.``Create modification context 04 - Nested folders``() =
        x.DoCreateModificationContextTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "Folder[1]/SubFolder[1]/File2"
              createItem "Compile" "Folder[1]/SubFolder[1]/File3"
              createItem "Compile" "Folder[1]/SubFolder[1]/File4"
              createItem "Compile" "File3" ])

    [<Test>]
    member x.``Create modification context 05 - Multiple nested folders``() =
        x.DoCreateModificationContextTest(
            [ createItem "Compile" "File1"
              createItem "Compile" "Folder[1]/SubFolder[1]/File2"
              createItem "Compile" "Folder[1]/SubFolder[1]/File3"
              createItem "Compile" "Folder[1]/SubFolder[1]/File4"
              createItem "Compile" "Folder[1]/File5"
              createItem "Compile" "Folder[1]/SubFolder[2]/File6"
              createItem "Compile" "File7" ])

    [<Test>]
    member x.``Create modification context 06 - CompileBefore``() =
        x.DoCreateModificationContextTest( // todo: fix
            [ createItem "Compile"       "Folder[1]/File3"
              createItem "CompileAfter"  "File5" 
              createItem "CompileAfter"  "File6" 
              createItem "CompileBefore" "File1"
              createItem "Compile"       "Folder[1]/File4"
              createItem "CompileBefore" "File2" ])

    [<Test>]
    member x.``Update 01 - Rename files``() =
        x.DoContainerModificationTest(
            [ "Folder[1]/File1"
              "File2"
              "File3"
              "Folder[1]/File4" ],
            fun container writer ->
                container.OnUpdateFile("Compile", "File2", "Compile", "NewName1")
                container.OnUpdateFile("Compile", "File3", "Compile", "NewName2"))

    [<Test>]
    member x.``Update 02 - Rename files in folder``() =
        x.DoContainerModificationTest( // todo: change to separate tests
            [ "File1"
              "Folder[1]/File2"
              "Folder[1]/File3"
              "Folder[1]/File4"
              "File5" ],
            fun container writer ->
                container.OnUpdateFile("Compile", "Folder/File2", "Compile", "NewName1")
                container.OnUpdateFile("Compile", "Folder/File3", "Compile", "NewName2")
                container.OnUpdateFile("Compile", "Folder/File4", "Compile", "NewName3"))

    [<Test>]
    member x.``Update 03 - Rename folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/SubFolder[1]/File2"
              "Folder[1]/File3"
              "Folder[1]/SubFolder[2]/File4"
              "File5" ],
            fun container writer ->
                container.OnUpdateFolder("Folder", "NewName"))

    [<Test>]
    member x.``Update 04 - Rename splitted folder``() =
        x.DoContainerModificationTest(
            [ "File1"
              "Folder[1]/File2"
              "Folder[1]/SubFolder[1]/File3"
              "Folder[1]/File4"
              "File5"
              "Folder[2]/File6"
              "Folder[2]/SubFolder/File7"
              "Folder[2]/File8" ],
            fun container writer ->
                container.OnUpdateFolder("Folder", "NewName"))

    [<Test>]
    member x.``Update 05 - Rename nested splitted folder``() =
        x.DoContainerModificationTest(
            [ "Folder[1]/SubFolder[1]/File1"
              "File2"
              "Folder[2]/SubFolder[2]/File3" ],
            fun container writer ->
                container.OnUpdateFolder("Folder/SubFolder", "Folder/NewName"))

    [<Test>]
    member x.``Update 06 - Rename nested splitted folder and splitted folder``() =
        x.DoContainerModificationTests(
            [ "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/File2"
              "File3"
              "Folder[2]/SubFolder[2]/File4" ],
            [ fun (container: LoggingFSharpItemsContainer) (writer: TextWriter) ->
                  writer.WriteLine("Rename 'Folder' to 'NewName'")
                  container.OnUpdateFolder("Folder", "NewName")
              fun container writer ->
                  writer.WriteLine("Rename 'NewName/SubFolder' to 'NewName/SubFolderNewName'")
                  container.OnUpdateFolder("NewName/SubFolder", "NewName/SubFolderNewName") ])

    [<Test>]
    member x.``Update 07 - Rename splitted folder and nested splitted folder``() =
        x.DoContainerModificationTests(
            [ "Folder[1]/SubFolder[1]/File1"
              "Folder[1]/File2"
              "File3"
              "Folder[2]/SubFolder[2]/File4" ],
            [ fun (container: LoggingFSharpItemsContainer) (writer: TextWriter) ->
                  writer.WriteLine("Rename 'Folder/SubFolder' to 'Folder/SubFolderNewName'")
                  container.OnUpdateFolder("Folder/SubFolder", "Folder/SubFolderNewName")
              fun container writer ->
                  writer.WriteLine("Rename 'Folder' to 'NewName'")
                  container.OnUpdateFolder("Folder", "NewName") ])

    [<Test>]
    member x.``Update 08 - Change item type``() =
        x.DoContainerModificationTest(
            [ createItem "Compile"       "File1"
              createItem "Compile"       "File2"
              createItem "CompileAfter"  "File3"
              createItem "CompileAfter"  "File4"
              createItem "CompileBefore" "File5"
              createItem "CompileAfter"  "File6" ],
            (fun container writer ->
                 container.OnUpdateFile("Compile", "File1", "Resource", "File1")
                 writer.WriteLine()

                 container.OnUpdateFile("Compile", "File2", "CompileBefore", "File2")
                 writer.WriteLine()

                 container.OnUpdateFile("CompileAfter", "File3", "Resource", "File3")
                 writer.WriteLine()

                 container.OnUpdateFile("Compile", "File4", "Compile", "File4")
                 writer.WriteLine()

                 container.OnUpdateFile("CompileBefore", "File5", "CompileAfter", "File5")
                 writer.WriteLine()

                 container.OnUpdateFile("CompileAfter", "File6", "CompileBefore", "File6")),
            false)

    member x.DoCreateModificationContextTest(items: AnItem list) =
        let relativeToTypes = [ RelativeToType.Before; RelativeToType.After ]
        x.ExecuteWithGold(fun writer ->
            let container = createContainer items writer
            container.Dump(writer)
            writer.WriteLine()
    
            let solutionItems = Dictionary<FileSystemPath, IProjectItem>()
            let contextProvider = FSharpItemModificationContextProvider(container)
            let itemTypes = Dictionary<FileSystemPath, string>()
            for item in items do
                let path =
                    FileSystemPath.Parse(removeIdentities item.EvaluatedInclude).MakeAbsoluteBasedOn(projectDirectory)
                itemTypes.[path] <- item.ItemType
    
            let viewItems = HashSet(items |> Seq.collect (createViewItems solutionItems))
            let viewFiles = viewItems.OfType<FSharpViewFile>().ToList()
            for modifiedViewFile in viewFiles do
                for relativeViewItem in viewItems do
                    for relativeToType in relativeToTypes do
                        let modifiedFile = modifiedViewFile.ProjectFile :> IProjectItem
                        let relativeItem = relativeViewItem.ProjectItem
                        let modifiedFileItemType = itemTypes.TryGetValue(modifiedFile.Location)
                        let relativeFileItemType = itemTypes.TryGetValue(relativeItem.Location)
                        let sameItemType =
                            isNotNull modifiedFileItemType &&
                            equalsIgnoreCase modifiedFileItemType relativeFileItemType

                        let context =
                            contextProvider.CreateModificationContext(modifiedViewFile, relativeViewItem, relativeToType)
                        match relativeViewItem, context with
                        | (:? FSharpViewFile), Some context when
                                modifiedFile <> relativeItem && sameItemType ->

                            let relativeItemPath = relativeItem.Location
                            let contextRelativeItemPath = context.RelativeTo.NotNull().ReferenceItem.Location
                            Assertion.Assert(relativeItemPath.Equals(contextRelativeItemPath),
                                sprintf "%O <> %O" relativeItem contextRelativeItemPath)

                        | _ ->
                            let (NormalizedPath path) = modifiedViewFile.Location
                            let (NormalizedPath relativePath) = relativeViewItem.Location
                            writer.Write(path)
                            let folderInfo =
                                match relativeViewItem with
                                | :? FSharpViewFolder as folder -> sprintf " (%s [%O])" folder.Name folder.Identitiy
                                | _ -> ""
                            writer.Write(sprintf " %O %s%s -> " relativeToType relativePath folderInfo)
                            writer.WriteLine(
                                match context with
                                | Some context ->
                                    let (NormalizedPath path) = context.RelativeTo.ReferenceItem.Location
                                    sprintf "%O %s" context.RelativeTo.Type path
                                | _ -> "null")
                writer.WriteLine()) |> ignore


    member x.DoAddFileRelativeBeforeTest(items: string list, filePath, relativeBefore) =
        x.DoAddFileRelaviteToTests(items, filePath, relativeBefore, null)

    member x.DoAddFileRelativeAfterTest(items: string list, filePath, relativeAfter) =
        x.DoAddFileRelaviteToTests(items, filePath, null, relativeAfter)
    
    member x.DoAddFileRelaviteToTests(items: string list, filePath, relativeBefore, relativeAfter) =
        x.ExecuteWithGold(fun writer ->
            let mutable addBeforeDump: string = null
            let mutable addAfterDump: string = null
    
            if isNotNull relativeBefore then
              addBeforeDump <- x.DoAddFileImpl(items, filePath, relativeBefore, RelativeToType.Before, writer, true);
    
            if isNotNull relativeAfter then
              addAfterDump <- x.DoAddFileImpl(items, filePath, relativeAfter, RelativeToType.After, writer, isNull relativeBefore )
    
            if (isNotNull addBeforeDump && isNotNull addAfterDump) then
              writer.WriteLine(sprintf "Dumps are equal: %O" (addBeforeDump.Equals(addAfterDump, StringComparison.Ordinal))))
        |> ignore

    member x.DoAddFileImpl(items, filePath, relativeTo, relativeToType, writer: TextWriter, shouldDumpInitial) =
        let container = createContainer (items |> List.map (createItem "Compile")) writer
        if shouldDumpInitial then
            container.Dump(writer)
            writer.WriteLine()

        writer.WriteLine("=======")
        container.OnAddFile("Compile", filePath, relativeTo, Some relativeToType)

        let stringWriter = new StringWriter()
        container.Dump(stringWriter)
        writer.WriteLine()
        writer.WriteLine(stringWriter.ToString())
    
        stringWriter.ToString()

    member x.DoContainerModificationTest(items: string list, action: LoggingFSharpItemsContainer -> TextWriter -> unit, ?dump) =
        x.DoContainerModificationTest(items |> List.map (createItem "Compile"), action, ?dump = dump)
    
    member x.DoContainerModificationTests(items: string list, actions: (LoggingFSharpItemsContainer -> TextWriter -> unit) list, ?dump) =
        x.DoContainerModificationTests(items |> List.map (createItem "Compile"), actions, ?dump = dump)

    member x.DoContainerModificationTest(items: AnItem list, action: LoggingFSharpItemsContainer -> TextWriter -> unit, ?dump) =
        x.DoContainerModificationTests(items, [action], ?dump = dump)

    member x.DoContainerModificationTests(items: AnItem list, actions: (LoggingFSharpItemsContainer -> TextWriter -> unit) list, ?dump) =
        let dump = defaultArg dump true
        x.ExecuteWithGold(fun writer ->
            let container = createContainer items writer
            container.Dump(writer)
    
            writer.WriteLine()
            writer.WriteLine("=======")
    
            for action in actions do
                action container writer
                writer.WriteLine()
                if dump then
                    container.Dump(writer)
                    writer.WriteLine()) |> ignore

    member x.DoContainerInitializationTest(items: AnItem list) =
        x.ExecuteWithGold(fun writer ->
            let container = createContainer items writer
            let solutionItems = Dictionary()

            writer.WriteLine()
            writer.WriteLine("=== Container Dump ===")
            container.Dump(writer)

            let dumpStructure items solutionItems =
                writer.WriteLine()
                writer.WriteLine("=== Structure API ===")

                for item in items do
                    let mutable identString = ""
                    let ident () =
                        identString <- identString + "  "
            
                    for viewItem in createViewItems solutionItems item do
                        let name = viewItem.ProjectItem.Name
                        let sortKey = container.TryGetSortKey(viewItem)
                        writer.Write(sprintf "%s%s SortKey=%O" identString name (Option.get sortKey))
                        match viewItem with
                        | :? FSharpViewFolder as viewFolder->
                            writer.WriteLine()
                            ident ()
                        | :? FSharpViewFile as viewFile ->
                            container.TryGetParentFolderIdentity(viewItem)
                            |> sprintf " ParentFolderIdentity=%O" 
                            |> writer.WriteLine
                        | _ -> ()

            let dumpParentFolders items solutionItems =
                writer.WriteLine()
                writer.WriteLine("=== Parent Folders API ===")

                let emptyFolders =
                    items |> List.choose (fun item ->
                        match item.ItemType with
                        | Folder -> Some (FileSystemPath.Parse(removeIdentities item.EvaluatedInclude))
                        | _ -> None)

                let folders =
                    items |> Seq.collect (fun item ->
                        FileSystemPath.Parse(removeIdentities item.EvaluatedInclude).GetParentDirectories())
                    |> Seq.append emptyFolders
                    |> HashSet

                for path in folders.OrderBy(fun x -> x.FullPath) do
                    writer.WriteLine(path)
                    for folder, parent in container.CreateFoldersWithParents(getOrCreateFolder path solutionItems) do
                        writer.WriteLine(sprintf "  %O -> %O" folder parent)

            dumpStructure items solutionItems
            dumpParentFolders items solutionItems) |> ignore


[<Struct>]
type AnItem =
    { ItemType: string
      EvaluatedInclude: string
      Link: string }

    static member Create(itemType, evaluatedInclude, ?link) =
        { ItemType = itemType; EvaluatedInclude = evaluatedInclude; Link = defaultArg link null } 


type LoggingFSharpItemsContainer(writer, refresher) as this =
    inherit FSharpItemsContainer(refresher)

    let container = this :> IFSharpItemsContainer

    member x.OnAddFile(itemType, location, relativeTo, relativeToType: RelativeToType option) =
        let output, relativeToPath =
            match relativeTo with
            | null -> "", null
            | relativeTo -> sprintf " %O '%O'" (relativeToType.NotNull().Value) relativeTo, projectPath relativeTo
        writer.Write(sprintf "Add '%O'" location)
        writer.WriteLine(output)
        let path = projectPath location
        container.OnAddFile(projectMark, itemType, path, path, relativeToPath, Option.toNullable relativeToType)

    member x.OnRemoveFile(itemType, location) =
        writer.WriteLine(sprintf "Remove '%O'" location)
        container.OnRemoveFile(projectMark, itemType, projectPath location)

    member x.OnUpdateFile(oldItemType, oldLocation, newItemType, newLocation) =
        writer.WriteLine(sprintf "Update file: '%O' (%O) -> '%O' (%O)" oldLocation oldItemType newLocation newItemType)
        container.OnUpdateFile(projectMark, oldItemType, projectPath oldLocation, newItemType, projectPath newLocation)

    member x.OnUpdateFolder(oldLocation, newLocation) =
        writer.WriteLine(sprintf "Update folder: '%O' -> '%O'" oldLocation newLocation)
        container.OnUpdateFolder(projectMark, projectPath oldLocation, projectPath newLocation)

    member x.Dump(writer) = container.Dump(writer)
    member x.TryGetSortKey(viewItem) = container.TryGetSortKey(viewItem)
    member x.TryGetParentFolderIdentity(viewItem) = container.TryGetParentFolderIdentity(viewItem)
    member x.CreateFoldersWithParents(folder) = container.CreateFoldersWithParents(folder)
