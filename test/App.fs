module App 

open System
open Mocha
open Fable.Core
open Fable.Core.JsInterop
open Fable.SqlClient
open Fable.SqlClient.OptionWorkflow

let environmentVariables() : (string * string) [] = import "envVars" "./util.js" 

[<Emit("process.exit(1)")>]
let exit() = jsNative

let requiredVariables = [ "SQLCLIENT_DATABASE"; "SQLCLIENT_USER"; "SQLCLIENT_PASSWORD"; "SQLCLIENT_SERVER" ]
let variables = List.ofArray(environmentVariables())

requiredVariables
|> List.filter (fun variable -> variables |> List.exists (fun (key, value) -> key = variable) |> not)
|> function 
    | [ ] -> () 
    | missing -> 
        List.iter (printfn "Missing environment variable: '%s'") missing
        exit()

let getVar name = variables |> List.find (fun (key, value) -> key = name) |> snd

let frameworkTests = 
    testList "Mocha framework tests" [
        testCase "Simple testCase works" <| fun () -> 
            areEqual (1 + 1) 2
   
        testCase "isFalse works" <| fun () -> 
            isFalse (1 = 2)

        testCaseAsync "testCaseAsync works" <| fun () ->
            async {
                let! x = async { return 21 }
                let answer = x * 2
                areEqual 42 answer
            }
    ]

let defaultConfig = 
    [ SqlConfig.Database (getVar "SQLCLIENT_DATABASE")
      SqlConfig.Server (getVar "SQLCLIENT_SERVER")
      SqlConfig.User (getVar "SQLCLIENT_USER")
      SqlConfig.Password (getVar "SQLCLIENT_PASSWORD") 
      SqlConfig.ConnectionTimeout 5000 ]

Fable.Core.JS.console.log(keyValueList CaseRules.LowerFirst defaultConfig)

let sqlClientTests = 
    testList "Fable.SqlClient" [
        testCaseAsync "Simple query works with primitive types" <| fun () ->
            async {
                let! values = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT GETDATE() as [Now], 1 as [Number], N'John' as [Name]"
                    |> Sql.executeRows (fun row -> 
                        option {
                            let! now = Sql.readDate "Now" row  
                            let! number = Sql.readInt "Number" row 
                            let! name = Sql.readString "Name" row
                            return (now, number, name)
                        })

                match values with 
                | Ok [ now, 1, "John" ] -> 
                    let yesterday = DateTime.Now.AddDays(-1.0)
                    isTrue (now > yesterday)

                | otherwise ->  failTest "Unexpected results"
            }

        testCaseAsync "Sql.executeScalar works with DateTime" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT GETDATE()"
                    |> Sql.executeScalar 
                
                match value with 
                | Ok (SqlValue.Date now) -> 
                    let yesterday = DateTime.Now.AddDays(-1.0)
                    isTrue (now > yesterday)

                | other -> failwith "Unexpected results"
            }

        testCaseAsync "Sql.executeScalar works with Unique identifier" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT NEWID()"
                    |> Sql.executeScalar 
                
                match value with 
                | Ok (SqlValue.UniqueIdentifier guid) -> 
                    guid 
                    |> string
                    |> String.IsNullOrWhiteSpace
                    |> isFalse

                | other -> 
                    failwith "Unexpected results"
            }

        testCaseAsync "Sql.executeScalar works with bigint" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT CAST(42 as bigint)"
                    |> Sql.executeScalar 
                
                match value with 
                | Ok (SqlValue.BigInt value) -> 
                    areEqual 42L value
                | other -> 
                    failwith "Unexpected results"
            }

        testCaseAsync "Syntax error is catched as RequestError" <| fun () ->
            async {
                let! result = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT TOP 1"
                    |> Sql.executeScalar 

                match result with
                | Error error -> 
                    isTrue (SqlErrorType.RequestError = error.name)
                
                | otherwise -> failTest "Unexpected results"
            }

        testCaseAsync "Connection error is returned when unable to connect" <| fun () ->
            async {
                let! values = 
                    [ 
                       SqlConfig.Database "master"
                       SqlConfig.Server "1.1.1.1"
                       SqlConfig.User "NonExistingUser"
                       SqlConfig.Password "NonExistingPassword"
                       SqlConfig.ConnectionTimeout 3000
                    ]
                    |> Sql.connect
                    |> Sql.query "SELECT GETDATE()"
                    |> Sql.executeRows (Sql.readDate "")

                match values with 
                | Error sqlError -> isTrue (sqlError.name = SqlErrorType.ConnectionError)
                | other ->  failTest "Unexpected results"
            }
    ]

Mocha.runModules [ 
  frameworkTests
  sqlClientTests 
]