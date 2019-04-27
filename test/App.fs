module App 

open System
open Mocha
open Fable.Core
open Fable.Core.JsInterop
open Fable.SimpleJson
open Fable.SqlClient
open Fable.SqlClient.OptionWorkflow

let environmentVariables() : (string * string) [] = import "envVars" "./util.js" 

[<Emit("process.exit(1)")>]
let exit() = jsNative

let requiredVariables = [ "SQLCLIENT_DATABASE"; "SQLCLIENT_USER"; "SQLCLIENT_PASSWORD"; "SQLCLIENT_SERVER" ]
let variables = List.ofArray(environmentVariables()) |> List.map fst
 
requiredVariables
|> List.filter (fun variable -> not (List.contains variable variables))
|> function 
    | [ ] -> () 
    | missing -> 
        List.iter (printfn "Missing environment variable: '%s'") missing
        exit()

[<Emit("process.env[$0]")>]
let getVar name = jsNative

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
      SqlConfig.ConnectionTimeout 15000 ]

Fable.Core.JS.console.log(keyValueList CaseRules.LowerFirst defaultConfig)

module ByteArray =
    [<Emit("Buffer.from($0).toString('utf8')")>]
    let decodeUtf8 (bytes: byte[]) : string = jsNative
    
let sqlClientTests = 
    testList "Fable.SqlClient" [
        testCaseAsync "Sql.readRows works with primitive types" <| fun () ->
            async {
                let! values = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT GETDATE() as [Now], 1 as [Number], N'John' as [Name], 1.456 as [Float], cast(1 as bit) as [Logical]"
                    |> Sql.readRows (fun row -> 
                        option {
                            let! now = Sql.readDate "Now" row  
                            let! number = Sql.readInt "Number" row 
                            let! name = Sql.readString "Name" row
                            let! floatValue = Sql.readFloat "Float" row
                            let! logical = Sql.readBit "Logical" row
                            return (now, number, name, floatValue, logical)
                        })

                match values with 
                | Ok [ now, 1, "John", 1.456, true ] -> 
                    let yesterday = DateTime.Now.AddDays(-1.0)
                    isTrue (now > yesterday)

                | otherwise ->  
                    failTest (Json.stringify otherwise)
            }

        testCaseAsync "(Ab)use Sql.readJson to read binary" <| fun () ->
            async {
                let! json = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT 0x5468697320697320612074657374 as [Binary] FOR JSON PATH"
                    |> Sql.readJson 
                
                match json with 
                | Ok serialized ->
                    let deserialized = Json.parseAs<{| Binary: byte[] |} array> serialized 
                    let text = ByteArray.decodeUtf8 deserialized.[0].Binary
                    areEqual "This is a test" text 

                | otherwise -> failTest (sprintf "Unexpected result: %s" (Json.stringify otherwise))
            }

        testCaseAsync "Sql.readScalar: byte[] round trip" <| fun () ->
            async {
                let input = [| byte 1; byte 2; byte 3 |]
                let! output = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT @bytes"
                    |> Sql.parameters [ SqlParam.From("@bytes", input) ]
                    |> Sql.readScalar 
                
                match output with 
                | Ok (SqlValue.Binary output) -> areEqual output input
                | otherwise -> failTest (sprintf "Unexpected result: %s" (Json.stringify otherwise))
            }
        
        testCaseAsync "Sql.readScalar works with DateTime" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT GETDATE()"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.Date now) -> 
                    let yesterday = DateTime.Now.AddDays(-1.0)
                    isTrue (now > yesterday)

                | other -> failwith "Unexpected results"
            }

        testCaseAsync "Sql.readScalar works with named DateTime" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT GETDATE() as [Now]"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.Date now) -> 
                    let yesterday = DateTime.Now.AddDays(-1.0)
                    isTrue (now > yesterday)

                | other -> failwith "Unexpected results"
            }

        testCaseAsync "Sql.readScalar with parameterized query: DateTime roundtrip" <| fun () ->
            async {
                let! value =
                   defaultConfig
                   |> Sql.connect
                   |> Sql.query "SELECT @date"
                   |> Sql.parameters [ SqlParam.From("@date", DateTime.Now) ]
                   |> Sql.readScalar

                match value with 
                | Ok (SqlValue.Date now) ->  
                    let yesterday = DateTime.Now.AddDays(-1.0) 
                    isTrue (now > yesterday)

                | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readScalar with parameterized query: string roundtrip" <| fun () ->
            async {
                let! value =
                   defaultConfig
                   |> Sql.connect
                   |> Sql.query "SELECT @value"
                   |> Sql.parameters [ SqlParam.From("@value", "F# & Fable") ]
                   |> Sql.readScalar

                match value with 
                | Ok (SqlValue.String "F# & Fable") -> pass()
                | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readScalar with parameterized query: decimal roundtrip" <| fun () ->
            async {
                let! value =
                   defaultConfig
                   |> Sql.connect
                   |> Sql.query "SELECT @value"
                   |> Sql.parameters [ SqlParam.From("@value", 15.0M) ]
                   |> Sql.readScalar

                match value with 
                | Ok (SqlValue.Decimal 15.0M) -> pass()
                | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readScalar with parameterized query: Guid roundtrip" <| fun () ->
            async {
                let input = Guid.NewGuid()

                let! value =
                   defaultConfig
                   |> Sql.connect
                   |> Sql.query "SELECT @value"
                   |> Sql.parameters [ SqlParam.From("@value", input) ]
                   |> Sql.readScalar

                match value with 
                | Ok (SqlValue.UniqueIdentifier output) -> areEqual input output
                | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readScalar with parameterized query: Boolean roundtrip" <| fun () ->
            async {
                let! value =
                   defaultConfig
                   |> Sql.connect
                   |> Sql.query "SELECT @value"
                   |> Sql.parameters [ SqlParam.From("@value", true) ]
                   |> Sql.readScalar

                match value with 
                | Ok (SqlValue.Bool true) -> pass()
                | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readScalar works with Unique identifier" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT NEWID()"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.UniqueIdentifier guid) -> 
                    guid 
                    |> string
                    |> String.IsNullOrWhiteSpace
                    |> isFalse

                | other -> failwithf "Unexpected results %s" (Json.stringify other)
                    
            }

        testCaseAsync "Sql.readScalar works with bigint" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT CAST(42 as bigint)"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.BigInt value) -> areEqual 42L value
                | other ->  failwithf "Unexpected results %s" (Json.stringify other)
            }

        testCaseAsync "Sql.readScalar works with tinyint" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT CAST(42 as tinyint)"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.TinyInt value) -> areEqual 42 (int value)
                | other -> failwithf "Unexpected results %s" (Json.stringify other)
            }

        testCaseAsync "Sql.readScalar works with smallint" <| fun () -> 
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT CAST(42 as smallint)"
                    |> Sql.readScalar 
                
                match value with 
                | Ok (SqlValue.SmallInt value) -> areEqual 42 (int value)
                | other ->  failwith "Unexpected results"
            }

        testCaseAsync "Sql.readScalar: integer roundtrips" <| fun () -> 
            async {
                let queryConfig = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT @value"
                   
                let! integer32 = 
                    queryConfig
                    |> Sql.parameters [ SqlParam.From("@value", 42) ]
                    |> Sql.readScalar

                let! tinyInt = 
                    queryConfig
                    |> Sql.parameters [ SqlParam.From("@value", (uint8 42)) ]
                    |> Sql.readScalar 

                let! smallInt = 
                    queryConfig
                    |> Sql.parameters [ SqlParam.From("@value", (int16 42)) ]
                    |> Sql.readScalar
                
                let! bigInteger = 
                    queryConfig
                    |> Sql.parameters [ SqlParam.From("@value", 42L) ]
                    |> Sql.readScalar

                match integer32 with 
                | Ok (SqlValue.Int value) -> areEqual 42 value
                | other ->  failwith "Unexpected results when reading int32"

                match tinyInt with 
                | Ok (SqlValue.TinyInt value) -> areEqual 42 (int value)
                | other ->  failwith "Unexpected results when reading tinyint"

                match smallInt with 
                | Ok (SqlValue.SmallInt value) -> areEqual 42 (int value)
                | other ->  failwith "Unexpected results when reading smallint"

                match bigInteger with  
                | Ok (SqlValue.BigInt value) -> areEqual 42L value 
                | other ->  failwith "Unexpected results when reading bigint"
            }

        testCaseAsync "Sql.readJson works" <| fun () ->
            async {
                let! jsonResult = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT id, name FROM (VALUES (42, 'Fable'), (31415, 'F#')) AS Awesome(id, name) FOR JSON PATH"
                    |> Sql.readJson 
                
                match jsonResult with 
                | Ok jsonAsText -> 
                    let values = Json.parseNativeAs<{| id: int; name: string |} array> jsonAsText
                    areEqual 2 values.Length
                    areEqual 42 values.[0].id 
                    areEqual "Fable" values.[0].name 
                    areEqual 31415 values.[1].id 
                    areEqual "F#" values.[1].name
                
                | Error error -> failwithf "Unexpected error:\n%s" (Json.stringify error)
            }

        testCaseAsync "Sql.readJson does not work with generic table result" <| fun () ->
            async {
                let jsonQuery = "SELECT id, name FROM (VALUES (1, 'Fable'), (2, 'F#')) AS Awesome(id, name)"
                let! jsonValues = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query jsonQuery
                    |> Sql.readJson 
                
                match jsonValues with 
                | Error (SqlError.ApplicationError(_)) -> pass()
                | otherwise -> failwith ("Unexpected result:\n" + Json.stringify otherwise)
            }

        testCaseAsync "Sql.readAffectedRows works" <| fun () -> 
            async {
                let! rowCount = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT id, name FROM (VALUES (42, 'Fable'), (31415, 'F#')) AS Awesome(id, name)"
                    |> Sql.readAffectedRows 

                match rowCount with 
                | Ok 2 -> pass()
                | otherwise -> failwithf "Unpexpected results: %s" (Json.stringify otherwise)
            }

        testCaseAsync "Executing stored procedure works" <| fun () ->
            async {
                let! answer = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.storedProcedure "sp_executesql"
                    |> Sql.parameters [ SqlParam.From("@stmt", "SELECT 42") ]
                    |> Sql.readScalar

                match answer with  
                | Ok (SqlValue.Int 42) -> pass()
                | otherwise -> failwithf "Unexpected error: %s" (Json.stringify otherwise)
            }

        testCaseAsync "Executing stored procedure as named scalar" <| fun () ->
            async {
                let! answer = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.storedProcedure "sp_executesql"
                    |> Sql.parameters [ SqlParam.From("@stmt", "SELECT 42 as [ANSWER]") ]
                    |> Sql.readScalar

                match answer with  
                | Ok (SqlValue.Int 42) -> pass()
                | otherwise -> failwithf "Unexpected error: %s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readRows properly handles null values" <| fun () ->
            async {
                let! results =  
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT id, name FROM (VALUES (42, 'Fable'), (NULL, 'F#')) AS Awesome(id, name)"
                    |> Sql.readRows (fun row ->
                        option {
                            let! id = Sql.readInt "id" row 
                            let! name = Sql.readString "name" row
                            return (id, name)
                        })
                
                match results with 
                | Ok [ (42, "Fable") ] -> pass() 
                | other -> failwithf "Unexpected errror: %s" (Json.stringify other)
            }

        testCaseAsync "SqlValue.Null can be used a parameter" <| fun () ->
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT @value"
                    |> Sql.parameters [ SqlParam.Null("value") ]
                    |> Sql.readScalar 
                
                match value with 
                | Ok SqlValue.Null -> pass()
                | other -> failwithf "Unexpected errror: %s" (Json.stringify other)
            }

        testCaseAsync "Sql.readRows can use default values for null values" <| fun () ->
            async {
                let! results =  
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT id, name FROM (VALUES (42, 'Fable'), (31415, NULL)) AS Awesome(id, name)"
                    |> Sql.readRows (fun row ->
                        option {
                            let! id = Sql.readInt "id" row 
                            let name = Sql.readString "name" row
                            return (id, defaultArg name "F#")
                        })
                
                match results with 
                | Ok [ (42, "Fable"); (31415, "F#") ] -> pass() 
                | other -> failwithf "Unexpected errror: %s" (Json.stringify other)
            }

        testCaseAsync "Sql.readScalar properly handles NULL values" <| fun () ->
            async {
                let! value = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT NULL"
                    |> Sql.readScalar 
                
                match value with 
                | Ok SqlValue.Null -> pass() 
                | other -> failwithf "Unexpected errror: %s" (Json.stringify other)
            }
 
        testCaseAsync "Sql.readRows works with parameterized queries" <| fun () -> 
            async {
                let! values = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT id, name FROM (VALUES (@id, @name)) AS Awesome(id, name)"
                    |> Sql.parameters 
                        [ SqlParam.From("@id", 42)
                          SqlParam.From("@name", "Fable.SqlClient") ]
                    |> Sql.readRows (fun row -> 
                        option {
                            let! id = Sql.readInt "id" row
                            let! name = Sql.readString "name" row 
                            return (id, name)
                        })

                match values with 
                | Ok [ (42, "Fable.SqlClient") ] -> pass()
                | otherwise -> return! failwithf "Unexpected results: %s" (Json.stringify otherwise)
            }

        testCaseAsync "Sql.readJson works with parameterized queries" <| fun () ->
            async {
                let inputGuid = Guid.NewGuid()
                let! result = 
                    defaultConfig
                    |> Sql.connect
                    |> Sql.query "SELECT id, name, guid FROM (VALUES (@id, @name, @guid)) AS Awesome(id, name, guid) FOR JSON PATH"
                    |> Sql.parameters [
                        SqlParam.From("@id", 42)
                        SqlParam.From("@name", "Fable")
                        SqlParam.From("@guid", inputGuid) ]
                    |> Sql.readJson 
                
                match result with 
                | Ok jsonText -> 
                    let values = Json.parseNativeAs<{| id: int; name: string; guid: Guid |} array> jsonText 
                    areEqual 1 values.Length
                    areEqual 42 values.[0].id 
                    areEqual "Fable" values.[0].name 
                    areEqual (inputGuid.ToString().ToLower()) (values.[0].guid.ToString().ToLower())

                | otherwise -> failwith ("Unexpected result:\n" + Json.stringify otherwise)
            }

        testCaseAsync "Syntax error is catched as RequestError" <| fun () ->
            async {
                let! result = 
                    defaultConfig
                    |> Sql.connect 
                    |> Sql.query "SELECT TOP 1"
                    |> Sql.readScalar 

                match result with
                | Error (SqlError.RequestError(_)) -> pass()               
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
                    |> Sql.readRows (Sql.readDate "")

                match values with 
                | Error (SqlError.ConnectionError(_)) -> pass()
                | other ->  failTest "Unexpected results"
            }
    ]

Mocha.runModules [ 
  frameworkTests
  sqlClientTests 
]