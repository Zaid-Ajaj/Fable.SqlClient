#r @"packages/build/FAKE/tools/FakeLib.dll"

open System
open System.IO
open Fake

let libPath = "./src"
let testsPath = "./test"
let mutable dotnetCli = "dotnet"

let run fileName args workingDir =
    printfn "CWD: %s" workingDir
    let fileName, args =
        if EnvironmentHelper.isUnix
        then fileName, args else "cmd", ("/C " + fileName + " " + args)
    let ok =
        execProcess (fun info ->
            info.FileName <- fileName
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if not ok then failwith (sprintf "'%s> %s %s' task failed" workingDir fileName args)

let delete file = 
    if File.Exists(file) 
    then DeleteFile file
    else () 

let cleanBundles() = 
    [ "main"; "renderer" ]
    |> List.map (fun file -> file + ".js")
    |> List.collect (fun file -> [ file; file + ".map" ])
    |> List.map (fun file -> Path.GetFullPath(Path.Combine("dist", file)))
    |> List.iter delete

let cleanCacheDirs() = 
    [ testsPath </> "bin" 
      testsPath </> "obj" 
      libPath </> "bin"
      libPath </> "obj" ]
    |> CleanDirs

Target "Clean" <| fun _ ->
    cleanCacheDirs()
    cleanBundles()

Target "InstallNpmPackages" (fun _ ->
  printfn "Node version:"
  run "node" "--version" __SOURCE_DIRECTORY__
  run "npm" "--version" __SOURCE_DIRECTORY__
  run "npm" "install" __SOURCE_DIRECTORY__
)

Target "RestoreFableTestProject" <| fun _ ->
  run dotnetCli "restore" testsPath

let publish projectPath = fun () ->
    [ projectPath </> "bin"
      projectPath </> "obj" ] |> CleanDirs
    run dotnetCli "restore --no-cache" projectPath
    run dotnetCli "pack -c Release" projectPath
    let nugetKey =
        match environVarOrNone "NUGET_KEY" with
        | Some nugetKey -> nugetKey
        | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
    let nupkg = 
        Directory.GetFiles(projectPath </> "bin" </> "Release") 
        |> Seq.head 
        |> Path.GetFullPath

    let pushCmd = sprintf "nuget push %s -s nuget.org -k %s" nupkg nugetKey
    run dotnetCli pushCmd projectPath

Target "PublishNuget" (publish libPath)

Target "BuildTestProject" <| fun _ ->
    run dotnetCli "fable npm-run build --port free" testsPath

Target "DotnetRestore" <| fun _ ->
    run dotnetCli "restore --no-cache" libPath

Target "RunTests" <| fun _ ->
    run "npm" "run test" "."

"Clean"
  ==> "InstallNpmPackages"
  ==> "RestoreFableTestProject"
  ==> "BuildTestProject"
  ==> "RunTests"


Target "BuildApp" <| fun _ ->
    run dotnetCli "restore --no-cache" ("app" </> "Main")
    run dotnetCli "restore --no-cache" ("app" </> "Renderer") 
    run dotnetCli "fable npm-run build-app --port free" ("app" </> "Main")
    run "npm" "run build-app" "."

Target "StartApp" <| fun _ ->
    run dotnetCli "restore" ("app" </> "Main")
    run dotnetCli "restore" ("app" </> "Renderer") 
    [ async { run dotnetCli "fable npm-run start" ("app" </> "Main") }
      async { 
          // sleep for 10 seconds to let fable does it's thing
          do! Async.Sleep (10 * 1000)
          
          // a function that long-polls whether Fable finished working or not
          let stillCompiling() = 
            [ "main.js"; "renderer.js" ]
            |> List.map (fun file -> Path.GetFullPath(Path.Combine("dist", file)))
            |> List.forall fileExists 
            |> not

          while stillCompiling() do
            printfn "Still compiling..."
            do! Async.Sleep 3000
          
          run "npm" "run launch" "." 
      } ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore    

"Clean" ==> "InstallNpmPackages" ==> "BuildApp" 
"Clean" ==> "InstallNpmPackages" ==> "StartApp" 

RunTargetOrDefault "RunTests"