module Tests

open QUnit
open Fable.SqlClient
open Fable.PowerPack
open Fable.Core
open Fable
open System

[<Import("*", "process")>]
let proc : Fable.Import.Node.Process.Process = jsNative

registerModule "Fable.SqlClient"

[<Pojo>]
type Student = {
    Name: string
    Id: int
}

setTimeout 5000

let config = 
  [ SqlConfig.User "sa"
    SqlConfig.Password "VeryStr0ng!Password"
    SqlConfig.Port 1433
    SqlConfig.Server "localhost"
    SqlConfig.Database "Tests" ]

testCasePromise "SqlClient.query works" <| fun test ->
    promise {
      let! pool = SqlClient.connect config
      let! studentsResult = 
        SqlClient.request pool
        |> SqlClient.query<Student> "SELECT * FROM Student"

      match studentsResult with
      | Ok students ->
          test.areEqual 2 (Array.length students)

          let firstStudent = students.[0]
          test.areEqual "Zaid" firstStudent.Name 
          test.areEqual 1 firstStudent.Id 

          let secondStudent = students.[1]
          test.areEqual "Anna" secondStudent.Name 
          test.areEqual 2 secondStudent.Id
      | Error sqlError ->
          test.unexpected sqlError
    }

testCasePromise "SqlClient.queryScalar works" <| fun test ->
    promise {
        let! pool = SqlClient.connect config
        let! serverTime =
            SqlClient.request pool 
            |> SqlClient.queryScalar<DateTime> "SELECT GETDATE()"
        
        match serverTime with
        | Ok time ->
            let now = DateTime.Now
            test.areEqual time.Day now.Day
        | Error sqlError -> 
            test.unexpected sqlError
    }

testCasePromise "SqlClient.queryScalar works with named column" <| fun test ->
    promise {
        let! pool = SqlClient.connect config
        let! serverTime =
            SqlClient.request pool 
            |> SqlClient.queryScalar<int> "SELECT 999 as [StudentId]"
        
        match serverTime with
        | Ok id -> test.areEqual 999 id
        | Error sqlError -> test.unexpected sqlError
    }

testCasePromise "SqlClient.executeNonQuery" <| fun test ->
    promise {
        let! pool = SqlClient.connect config
        let! result = 
          SqlClient.request pool
          |> SqlClient.executeNonQuery "DELETE FROM Student WHERE Id = 999" 

        match result with 
        /// 0 rows affected
        | Ok 0 -> test.pass() 
        | otherwise -> test.unexpected otherwise 
    }


testCasePromise "SqlClient.executeNonQuery with input" <| fun test ->
    promise {
        let! pool = SqlClient.connect config
        let! result = 
          SqlClient.request pool
          |> SqlClient.input "studentId" SqlTypes.Int 999
          |> SqlClient.executeNonQuery "DELETE FROM Student WHERE Id = @studentId" 

        match result with 
        /// 0 rows affected
        | Ok 0 -> test.pass() 
        | otherwise -> test.unexpected otherwise 
    }

Fable.Import.JS.setTimeout (fun _ -> 
    SqlClient.close()
    proc.exit(0)) 10000
|> ignore