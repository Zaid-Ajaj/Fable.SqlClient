# Fable.SqlClient [![Nuget](https://img.shields.io/nuget/v/Fable.SqlClient.svg?colorB=green)](https://www.nuget.org/packages/Fable.SqlClient) [![Build status](https://ci.appveyor.com/api/projects/status/n7665851i24yh2d7?svg=true)](https://ci.appveyor.com/project/Zaid-Ajaj/fable-sqlclient)


[Fable](https://github.com/fable-compiler/Fable) binding for [node-mssql](https://github.com/tediousjs/node-mssql), Microsoft SQL Server client library with an idiomatic F# API to be used from Fable Node applications. 

### Installation
Install the Fable binding from Nuget
```
paket add Fable.SqlClient --project /path/to/project.fsproj

dotnet add package Fable.SqlClient
```
Install the actual node-mssql from Npm
```
npm install --save node-mssql
``` 
# Getting started
First of all, configure the connection:
```fs
open Fable.SqlClient

let connectionConfig = 
    [ SqlConfig.User "admin"
      SqlConfig.Password "str0ngPa$$word"
      SqlConfig.Host "localhost"
      SqlConfig.Database "AdventuresWorks"
      SqlConfog.Port 1433 ]
```


## SqlClient.query<'t>
Now let us query some values:
```fs
// to use promise api's
open Fable.PowerPack

// unit -> Promise<Result<User list, string>>
let getAllUsers() = 
    promise {
        // get the shared connection pool
        let! pool = SqlClient.connect connectionConfig
        
        let request = SqlClient.request pool
        let usersQuery = "SELECT Id, Name, DateModified from Users"
        
        let! usersResult  = SqlClient.query<User> usersQuery request
        match usersResult with
        | Ok users -> return Ok (List.ofArray users)
        | Error detailedSqlError -> 
            // log the error:
            printfn "%A" detailedSqlError
            return Error "Could not load users from the database"
    }
```
Notice how the type of the record is supplied to `SqlClient.query<'t>` function. It's signature is as follows:

```fs
query<'t> : string -> ISqlRequest -> Promise<Result<'t[], SqlError>>
```
`query<'t>` is safe and does not throw exceptions

## SqlClient.executeNonQuery
The same as .NET's `SqlReader` that returns the number of affected rows of a query:

```fs
// unit -> Promise<bool>
let deleteEvents() = 
    promise {
        let! pool = SqlClient.connect connectionConfig
        
        let request = SqlClient.request pool
        let query = "DELETE FROM Events"
        
        let! deletionResult = SqlClient.executeNonQuery query request
        match deletionResult with 
        | Ok rowsAffected -> return rowsAffected > 0
        | Error sqlError -> return false
    }
``` 
Signature of `SqlClient.executeNonQuery`:

```fs
executeNonQuery : string -> ISqlRequest -> Promise<Result<int, SqlError>>
```
`executeNonQuery` is safe and does not throw exceptions
## SqlClient.executeScalar<'t> 
Executes a query that returns a scalar value of type `'t`:

```fs
// unit -> Promise<Option<DateTime>>
let pingDatabase() = 
    promise {
        let! pool = SqlClient.connect connectionConfig
        
        let! serverTimeResult = 
            SqlClient.request pool  
            |> SqlClient.executeScalar<DateTime> "SELECT GETDATE()"  
        
        match serverTimeResult with 
        | Ok serverTime -> return Some serverTime
        | Error sqlError -> return None
    }
```
The signatue of `SqlClient.executeScalar<'t>`
```fs
executeScalar<'t> : string -> ISqlRequest -> Promise<Result<'t, SqlError>>
```
# Bonus

This repo contains an electron application with the UI written with Elmish and using the library to talk to my test databases: 

### Query result set
![app](screenshots/app.png)

### Scalar result
![scalar](screenshots/scalar.png)

### Error result
![error](screenshots/error.png)

### Requirements
 - Dotnet 2.0
 - Mono
 - Node


### Development  
To start building and testing the library using the Electron application, just run:
```sh
./build.sh StartApp 
```
it will start running the electron application as soon as everything is ready