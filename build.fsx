#r "node_modules/fable-metadata/lib/Fable.Core.dll"

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.DynamicExtensions

module private Helpers =
    let readline: obj = importAll "readline"
    let path: obj = importAll "path"
    let fs: obj = importAll "fs"
    let childProcess: obj = importAll "child_process"
    let nodeProcess : obj = importAll "process"

    let inline (!>) x = ignore x
    let inline (~%) xs = createObj xs |> unbox

    type SingleObservable<'T>(?onDispose: unit->unit) =
        let mutable disposed = false
        let mutable listener: IObserver<'T> option = None
        member __.IsDisposed = disposed
        member __.Dispose() =
            if not disposed then
                onDispose |> Option.iter (fun d -> d())
                listener |> Option.iter (fun l -> l.OnCompleted())
                disposed <- true
                listener <- None
        member __.Trigger v =
            listener |> Option.iter (fun l -> l.OnNext v)
        interface IObservable<'T> with
            member this.Subscribe w =
                if disposed then failwith "Disposed"
                if Option.isSome listener then failwith "Busy"
                listener <- Some w
                { new IDisposable with
                    member __.Dispose() = this.Dispose() }

    let awaitWhileTrue (f: 'T->bool) (s: IObservable<'T>) =
        Async.FromContinuations <| fun (success,_,_) ->
            let mutable finished = false
            let mutable disp = Unchecked.defaultof<IDisposable>
            let observer =
                { new IObserver<'T> with
                    member __.OnNext v =
                        if not finished then
                            if not(f v) then
                                finished <- true
                                disp.Dispose()
                                success()
                    member __.OnError e = ()
                    member x.OnCompleted() =
                        success() }
            disp <- s.Subscribe(observer)

open Helpers

let (</>) (p1: string) (p2: string): string =
    path?join(p1, p2)

let args: string list =
    nodeProcess?argv
    |> Seq.skip 2
    |> Seq.toList

let fullPath (p: string): string =
  path?resolve(p)

let dirname (p: string): string =
  let parent = path?dirname(p)
  if parent = p then null else parent

let dirFiles (p: string): string[] =
    fs?readdirSync(p)

let isDirectory (p: string): bool =
    fs?lstatSync(p)?isDirectory()

let pathExists (p: string): bool =
    fs?existsSync(p)

let filename (p: string): string =
  path?basename(p)

let filenameWithoutExtension (p: string) =
    let name = filename p
    let i = name.LastIndexOf(".")
    if i > -1 then name.Substring(0, i) else name

let rec removeDirRecursive (p: string): unit =
    if fs?existsSync(p) then
        for file in dirFiles p do
            let curPath = p </> file
            if isDirectory curPath then
                removeDirRecursive curPath
            else
                printfn "Deleting file: %s" curPath
                fs?unlinkSync(curPath)
        printfn "Deleting directory: %s" p
        fs?rmdirSync(p)

let writeFile (filePath: string) (txt: string): unit =
    fs?writeFileSync(filePath, txt)

let readFile (filePath: string): string =
    fs?readFileSync(filePath)?toString()

let readAllLines (filePath: string): string[] =
    (readFile filePath).Split('\n')

let readLines (filePath: string): IObservable<string> =
    let rl = readline?createInterface %[
        "input" ==> fs?createReadStream(filePath)
        // Note: we use the crlfDelay option to recognize all instances of CR LF
        // ('\r\n') in input.txt as a single line break.
        "crlfDelay" ==> System.Double.PositiveInfinity
    ]
    let obs = SingleObservable(fun () -> rl?close())
    rl?on("line", fun line ->
        obs.Trigger(line))
    rl?on("close", fun _line ->
        obs.Dispose())
    obs :> _

let takeLines (numLines: int) (filePath: string) = async {
    let mutable i = -1
    let lines = ResizeArray()
    do! readLines filePath
        |> awaitWhileTrue (fun line ->
            i <- i + 1
            if i < numLines then lines.Add(line); true
            else false)
    return lines.ToArray()
}

let takeLinesWhile (predicate: string->bool) (filePath: string) = async {
    let lines = ResizeArray()
    do! readLines filePath
        |> awaitWhileTrue (fun line ->
            if predicate line then lines.Add(line); true
            else false)
    return lines.ToArray()
}
let run cmd: unit =
    printfn "> %s" cmd
    childProcess?execSync(cmd, %[
        "stdio" ==> "inherit"
    ])

let runList cmdParts =
    String.concat " " cmdParts |> run

let environVarOrNone (varName: string): string option =
    nodeProcess?env?(varName)
    |> Option.ofObj

let (|IgnoreCase|_|) (pattern: string) (input: string) =
    if String.Equals(input, pattern, StringComparison.OrdinalIgnoreCase) then
        Some IgnoreCase
    else None

let (|Regex|_|) (pattern: string) (input: string) =
    let m = Regex.Match(input, pattern)
    if m.Success then
        let mutable groups = []
        for i = m.Groups.Count - 1 downto 0 do
            groups <- m.Groups.[i].Value::groups
        Some groups
    else None

let replaceRegex (pattern: string) (replacement: string list) (input: string) =
    Regex.Replace(input, pattern, String.concat "" replacement)

module private Publish =
    let NUGET_VERSION = @"(<Version>)(.*?)(<\/Version>)"
    let NUGET_PACKAGE_VERSION = @"(<PackageVersion>)(.*?)(<\/PackageVersion>)"
    let NPM_VERSION = @"""version"":\s*""(.*?)"""
    let VERSION = @"\d+\.\d+\.\d+\S*"

    let splitPrerelease (version: string) =
        let i = version.IndexOf("-")
        if i > 0
        then version.Substring(0, i), Some(version.Substring(i + 1))
        else version, None

    let rec findFileUpwards fileName dir =
        let fullPath = dir </> fileName
        if pathExists fullPath
        then fullPath
        else
            let parent = dirname dir
            if isNull parent then
                failwithf "Couldn't find %s directory" fileName
            findFileUpwards fileName parent

    let loadReleaseVersion projFile =
        let projDir = if isDirectory projFile then projFile else dirname projFile
        let releaseNotes = findFileUpwards "RELEASE_NOTES.md" projDir
        match readFile releaseNotes with
        | Regex VERSION [version] -> version
        | _ -> failwithf "Couldn't find version in %s" releaseNotes

    let needsPublishing (checkPkgVersion: string->string option) (releaseVersion: string) projFile =
        let print msg =
            let projName =
                let projName = filename projFile
                if projName = "package.json"
                then dirname projFile |> filename
                else projName
            printfn "%s > %s" projName msg
        match readFile projFile |> checkPkgVersion with
        | None -> failwithf "Couldn't find package version in %s" projFile
        | Some version ->
            let sameVersion = version = releaseVersion
            if sameVersion then
                sprintf "Already version %s, no need to publish" releaseVersion |> print
            not sameVersion

    let pushNuget (projFile: string) =
        let checkPkgVersion = function
            | Regex NUGET_PACKAGE_VERSION [_;_;pkgVersion;_] -> Some pkgVersion
            | _ -> None
        let releaseVersion = loadReleaseVersion projFile
        if needsPublishing checkPkgVersion releaseVersion projFile then
            let projDir = dirname projFile
            let nugetKey =
                match environVarOrNone "NUGET_KEY" with
                | Some nugetKey -> nugetKey
                | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
            // Restore dependencies here so they're updated to latest project versions
            runList ["dotnet restore"; projDir]
            // Update the project file
            readFile projFile
            |> replaceRegex NUGET_VERSION ["$1"; splitPrerelease releaseVersion |> fst; "$3"]
            |> replaceRegex NUGET_PACKAGE_VERSION ["$1"; releaseVersion; "$3"]
            |> writeFile projFile
            try
                let tempDir = projDir </> "temp"
                removeDirRecursive tempDir
                runList ["dotnet pack"; projDir; sprintf "-c Release -o %s" tempDir]
                let pkgName = filenameWithoutExtension projFile
                let nupkg =
                    dirFiles tempDir
                    |> Seq.tryPick (fun path ->
                        if path.Contains(pkgName) then Some(tempDir </> path) else None)
                    |> function
                        | Some x -> x
                        | None -> failwithf "Cannot find .nupgk with name %s" pkgName
                runList ["dotnet nuget push"; nupkg; "-s nuget.org -k"; nugetKey]
                removeDirRecursive tempDir
            with _ ->
                filenameWithoutExtension projFile
                |> printfn "There's been an error when pushing project: %s"
                printfn "Please revert the version change in .fsproj"
                reraise()

let pushNuget projFile =
    Publish.pushNuget projFile

printfn "Args: %s" (String.concat ", " args)

let executeTarget = function 
    | IgnoreCase "publish" -> 
        pushNuget "src/Fable.SqlClient.fsproj"
    
    | IgnoreCase "clean" -> 
        removeDirRecursive "src/temp"
        removeDirRecursive "src/bin"
        removeDirRecursive "src/obj"
        removeDirRecursive "test/bin"
        removeDirRecursive "test/obj"
        removeDirRecursive "temp"
    | _ -> 
        ignore() 

args |> List.iter executeTarget