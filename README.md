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