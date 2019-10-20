# Fable.SqlClient [![Nuget](https://img.shields.io/nuget/v/Fable.SqlClient.svg?colorB=green)](https://www.nuget.org/packages/Fable.SqlClient) [![Build status](https://ci.appveyor.com/api/projects/status/n7665851i24yh2d7?svg=true)](https://ci.appveyor.com/project/Zaid-Ajaj/fable-sqlclient)


[Fable](https://github.com/fable-compiler/Fable) binding for [node-mssql](https://github.com/tediousjs/node-mssql), Microsoft SQL Server client library with an idiomatic and type-safe F# API to be used from Fable Node applications. 

### Installation
Install the Fable binding from Nuget
```bash
# using nuget
dotnet add package Fable.SqlClient

# or with paket
paket add Fable.SqlClient --project /path/to/project.fsproj
```
Install the actual node-mssql from Npm
```
npm install --save node-mssql
``` 
# Getting started
First of all, configure the connection using `SqlConfig`:
```fs
open Fable.SqlClient

let connectionConfig = 
    [ SqlConfig.User "admin"
      SqlConfig.Password "str0ngPa$$word"
      SqlConfig.Host "localhost"
      SqlConfig.Database "AdventuresWorks"
      SqlConfog.Port 1433 ]
```

# `Sql.readRows` querying a tabular result set 

```fs
open Fable.SqlClient
open Fable.SqlClient.OptionWorkflow

type User = { Id: int; Name: string }
 
let getUsers() : Async<Option<User list>> = 
    async {
        let! usersResult = 
          connectionConfig
          |> Sql.connect
          |> Sql.query "SELECT id, name from [dbo].[Users]" 
          |> Sql.readRows (fun row -> 
                option {
                    let! id = Sql.readInt "id" row
                    let! name = Sql.readString "name" row
                    return { Id = id; Name = name; }   
                })

        match usersResult with 
        | Ok users -> return Some users 
        | Error sqlError -> return None
    }
```
Here `Sql.readRows` takes a transformer function that maps every row into an `Option<'t>`. Because we are using the `option` workflow, the mapping is done safely and if either `id` or `name` is null, then the row is skipped. 

If you want to handle the null values manually, then simply use `let` instead of `let!` in combination with `defaultArg`. For example, if you want to use an empty string as a default for the `name` column, then you simply write the following:  

```fs
async {
    let! usersResult = 
      connectionConfig
      |> Sql.connect
      |> Sql.query "SELECT id, name from [dbo].[Users]" 
      |> Sql.readRows (fun row -> 
            option {
                let! id = Sql.readInt "id" row
                // notice: no ! in the let binding
                let name = Sql.readString "name" row
                let nameOrEmpty = defaultArg name ""
                return { Id = id; Name = nameOrEmpty; }   
            })

    match usersResult with 
    | Ok users -> return Some users 
    | Error sqlError -> return None
}
```
# `Sql.readRows` from a parameterized query
Queries can be parameterized with named parameters to avoid SQL injections: 
```fs
open Fable.SqlClient
open Fable.SqlClient.OptionWorkflow

let userByUsername (username: string) : Async<User option> = 
    async {
        let! results = 
            connectionConfig
            |> Sql.connect
            |> Sql.query "SELECT TOP 1 id, name from [dbo].[Users] where name = @name"
            |> Sql.parameters [ SqlParam.From ("@name", username) ]
            |> Sql.readRows (fun row ->
                option {
                    let! id = Sql.readInt "id" row
                    let! name = Sql.readString "name" row
                    return { Id = id; Name = name }
                })

        match results with 
        | Ok (user :: _) -> return Some user 
        | _ -> return None
    }
```
# `Sql.readScalar` querying a scalar value
```fs
open Fable.SqlClient

let pingDatabase() : Async<DateTime option> = 
    async {
        let! serverTime = 
            connectionConfig
            |> Sql.connect
            |> Sql.query "SELECT GETDATE()"
            |> Sql.readScalar 

        match serverTime with 
        | Ok (SqlValue.Date time) -> return Some time
        | _ -> return None
    }
```
# `Sql.storedProcedure` 
executing a stored procedure with parameters
```fs
let userExists (name: string) : Async<bool> = 
    async {
        let! exists = 
            connectionConfig
            |> Sql.connect
            |> Sql.storedProcedure "user_exists"
            |> Sql.parameters [ SqlParam.From("@name", name) ]
            |> Sql.readScalar 

        match exists with 
        | Ok (SqlValue.Bool value) -> return value
        | _ -> return false
    }
```
# `Sql.readAffectedRows`
Returns the number of rows affected. For example when you execute a `DELETE`, `INSERT` or `UPDATE` statements, the rows affected will the ones that were deleted, inserted or updated. If you read the rows affected by a `SELECT` statements, the row count is returned. 
```fs
/// delete events older than 2 months
let deleteOldEvents() : Async<Result<int, string>> = 
    async {
        let! eventsDeleted = 
            connectionConfig
            |> Sql.connect
            |> Sql.query "DELETE FROM [dbo].[Events] WHERE DateAdded < @TwoMonthsAgo"
            |> Sql.parameters [ SqlParam.From("@TwoMonthsAgo", DateTime.Now.AddMonths(-2)) ]
            |> Sql.readAffectedRows 

        match eventsDeleted with 
        | Ok count -> return Ok count
        | Error error -> 
            // Extract info from the SqlError
            let (errType, errMsg, errStack) = error.Info()
            return Error errMsg      
    }
```
# `Sql.readJson` 
Since Microsoft SQL Server supports JSON natively, you can query the database and have it return the result set as a single JSON string. `Sql.readJson` is a utility function that extracts the JSON from the scalar value. You can then parse the resulting serialized JSON using your favorite Json library:
```fs
async {
    let! json =
        connectionConfig
        |> Sql.connect
        |> Sql.query "SELECT id, name FROM (VALUES(42, N'Fable')) as TableName(id, name) FOR JSON PATH"
        |> Sql.readJson 
    
    match json with 
    | Ok serialized = 
        let values = Json.parseAs<{| id: int; name: string |} array> serialized
        let value = values.[0] 
        printfn "Id = %d and Name = %s" value.id value.name
    
    | Error error -> 
        printfn "Something went wrong..."
}
```
# API definitions 
```fs
val Sql.readRows : ((string * SqlValue) list -> Option<'t>) -> (props: ISqlProps) -> Async<Result<'t list, SqlError>>

val Sql.readAffectedRows (props: ISqlProps) -> Async<Result<int, SqlError>>

val Sql.readJson (props: ISqlProps) -> Async<Result<string, SqlError>> 

val Sql.readScalar (props: ISqlProps) -> Async<Result<SqlValue, SqlError>>
```
where the important types are defined as follows. The types within `SqlValue` are those that you can read from Sql Server.
```fs
type SqlValue = 
    | TinyInt of uint8
    | SmallInt of int16
    | Int of int 
    | Bool of bool
    | Date of DateTime
    | UniqueIdentifier of Guid 
    | BigInt of int64
    | Decimal of decimal  
    | String of string 
    | Number of float 
    | Binary of byte[]
    | Null

    
type SqlError = 
    | ConnectionError of message: string * stack: string
    | TransactionError of message: string * stack: string 
    | RequestError of message: string * stack: string 
    | ApplicationError of message: string * stack: string 
    | GenericError of errorType: string * message: string * stack: string
```
the type `ISqlProps` is just a helper type that accumulate the configuration for a query using a fluent syntax
# Development and Testing
The test project is in the `test` directory and includes *integration* tests. To run these tests on your local machine, you will need to setup a couple of environment variables:
 - `SQLCLIENT_DATABASE`: the name of the database to run the tests against. The tests don't require a specific database, you can just use `master`. 
 - `SQLCLIENT_SERVER`: the IP address of the hosting machine, if you have a local MSSQL server, then `local/{instance}` will do
 - `SQLCLIENT_USER`: the username to log in with 
 - `SQLCLIENT_PASSWORD`: the password of the user

After you have set up these variables, you can run the commands:
```bash
npm install 
npm test
```
This will compile the test the project and runs tests using `Mocha`. 

# Known issues
Reading `DateTimeOffset` directly is not supported. It has to be converted to `nvarchar` first and parsed from `SqlValue.String value`. However, a `DateTimeOffset` value can still be used as a parameter value directly. The following test demonstrates what's supported:
```fs
testCaseAsync "DateTimeOffset round trip works" <| fun () -> 
    async {
        let input = DateTimeOffset.UtcNow
        let! value = 
            defaultConfig
            |> Sql.connect 
            // convert the value to nvarchar
            |> Sql.query "SELECT CONVERT(nvarchar(100), @DateTimeOffset) as [Value]"
            |> Sql.parameters [ SqlParam.From("@DateTimeOffset", input) ]
            |> Sql.readScalar 

        match value with 
        // parse the value here
        | Ok (SqlValue.String serialized) -> 
            let deserialized = DateTimeOffset.Parse serialized 
            areEqual input deserialized
        
        | otherwise -> return! failwithf "Unexpected results:\n%s" (Json.stringify otherwise)
    }
```
