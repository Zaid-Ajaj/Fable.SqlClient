namespace Fable.SqlClient 

open System
open Fable.Core
open Fable.Core.JsInterop

module Sql = 

    [<Import("*", "mssql")>] 
    let private mssql: IMSSql = jsNative

    [<Emit("$1[$0]")>]
    let private get<'a> (prop: string) (literal: obj) : 'a = jsNative

    let private defaultProps : ISqlProps = 
        { Config = []; Query = None; Parameters = [ ] }

    let connect (initialConfig: SqlConfig list) = 
        { defaultProps with Config = initialConfig }

    let query (query: string) (config: ISqlProps) = 
        { config with Query = Some query }

    let paramters (values: SqlParam list) (config: ISqlProps) = 
        { config with Parameters = values }
    
    let private createPool (config: SqlConfig) : ISqlConnectionPool = import "createConnectionPool" "./createPool.js"
    
    let private applicationError (ex: exn) : NativeSqlError = 
        { name = "ApplicationError"
          code = "APPERROR"
          message = ex.Message
          stack = ex.StackTrace }

    let private toSqlError (nativeError: NativeSqlError) = 
        match nativeError.name with 
        | "ConnectionError" -> SqlError.ConnectionError (nativeError.message, nativeError.stack)
        | "TransactionError" -> SqlError.TransactionError (nativeError.message, nativeError.stack)
        | "RequestError" -> SqlError.RequestError (nativeError.message, nativeError.stack)
        | "ApplicationError" -> SqlError.ApplicationError (nativeError.message, nativeError.stack)
        | otherTypeOfError -> SqlError.GenericError (otherTypeOfError, nativeError.message, nativeError.stack) 
    
    /// Creates and connects to a new connection
    let private connectToPool (config: SqlConfig list) : Async<Result<ISqlConnectionPool, SqlError>> =
        Async.FromContinuations <| fun (resolve, reject, _) ->
            let connectionConfig = unbox<SqlConfig> (keyValueList CaseRules.LowerFirst config) 
            let connection = createPool connectionConfig
            connection.connect(fun err -> 
                if isNull err 
                then resolve (Ok connection)
                else
                    connection.close() 
                    resolve (Error (toSqlError (unbox err)))
            )

    let private request (pool: ISqlConnectionPool) : ISqlRequest = 
        pool.request() 
    
    let private rawQuery (query: string) (req: ISqlRequest) : Async<Result<obj, SqlError>> = 
        Async.FromContinuations <| fun (resolve, reject, _) ->
            req.query query (fun err results -> 
                if isNull (box err) 
                then resolve (Ok results)
                else resolve (Error (toSqlError (unbox err)))
            )

    let private columnDefinitions (resultset: obj) : (string * SqlType) array = import "columnDefinitions" "./InspectSchema.js"

    let readDate (name: string) (row: (string * SqlValue) list) = 
        match List.tryFind (fun (columnName, value) -> name = columnName) row with 
        | Some (_, SqlValue.Date date) -> Some date 
        | _ -> None

    let readInt (name: string) (row: (string * SqlValue) list) = 
        match List.tryFind (fun (columnName, value) -> name = columnName) row with 
        | Some (_, SqlValue.Int value) -> Some value  
        | Some (_, SqlValue.TinyInt value) -> Some (int32 value)
        | Some (_, SqlValue.SmallInt value) -> Some (int32 value)
        | _ -> None

    let readString (name: string) (row: (string * SqlValue) list) = 
        match List.tryFind (fun (columnName, value) -> name = columnName) row with 
        | Some (_, SqlValue.String value) -> Some value  
        | _ -> None
   
    let readBit (name: string) (row: (string * SqlValue) list) = 
        match List.tryFind (fun (columnName, value) -> name = columnName) row with 
        | Some (_, SqlValue.Bool value) -> Some value  
        | Some (_, SqlValue.Int 1) -> Some true 
        | Some (_, SqlValue.Int 0) -> Some false
        | Some (_, SqlValue.String ("true" | "True" | "TRUE")) -> Some true 
        | Some (_, SqlValue.String ("false" | "False" | "FALSE")) -> Some false
        | _ -> None 
        
    let readFloat (name: string) (row: (string * SqlValue) list) = 
        match List.tryFind (fun (columnName, value) -> name = columnName) row with 
        | Some (_, SqlValue.Int value) -> Some (float value)
        | Some (_, SqlValue.TinyInt value) -> Some (float value)
        | Some (_, SqlValue.SmallInt value) -> Some (float value)
        | Some (_, SqlValue.Number value) -> Some value  
        | _ -> None 

    let readRows<'t> (map: (string * SqlValue) list -> Option<'t>) (config: ISqlProps) : Async<Result<'t list, SqlError>> = 
        async {
            let! connectionResult = connectToPool config.Config
            match connectionResult with 
            | Error connectionError -> return Error connectionError  
            | Ok connection ->
                let queryRequest = request connection
                let sqlQuery = defaultArg config.Query ""
                let! results = rawQuery sqlQuery queryRequest
                match results with 
                | Error requestErr -> 
                    connection.close()
                    return Error requestErr
                | Ok resultset ->
                    try 
                        let metadata = columnDefinitions resultset
                        let recordset : obj[] = get "recordset" resultset
                        let rows = ResizeArray()
                        for row in recordset do 
                            let rowValues = ResizeArray<string * SqlValue>()
                            for (columnName, columnType) in metadata do
                                match columnType with 
                                | SqlType.Float -> rowValues.Add(columnName, SqlValue.Number (unbox (get columnName row)))
                                | SqlType.TinyInt -> rowValues.Add(columnName, SqlValue.TinyInt (unbox (get columnName row)))
                                | SqlType.SmallInt -> rowValues.Add(columnName, SqlValue.SmallInt (unbox (get columnName row)))
                                | SqlType.Int -> rowValues.Add (columnName, SqlValue.Int (unbox (get columnName row)))
                                | SqlType.DateTime -> rowValues.Add (columnName, SqlValue.Date (unbox (get columnName row)))
                                | SqlType.Number -> rowValues.Add(columnName, SqlValue.Number (get columnName row))
                                | SqlType.NVarChar -> rowValues.Add(columnName, SqlValue.String (get columnName row))
                                | SqlType.Decimal -> rowValues.Add(columnName, SqlValue.Decimal (decimal (get columnName row)))
                                | SqlType.Money -> rowValues.Add(columnName, SqlValue.Decimal (decimal (get columnName row)))
                                | SqlType.BigInt -> rowValues.Add(columnName, SqlValue.BigInt (int64 (get<string> columnName row)))
                                | SqlType.Bit -> rowValues.Add(columnName, SqlValue.Bool (get columnName row))
                                | SqlType.DateTimeOffset -> rowValues.Add(columnName, SqlValue.Date (unbox (get columnName row)))
                                | SqlType.UniqueIdentifier -> rowValues.Add(columnName, SqlValue.UniqueIdentifier (Guid.Parse(get<string> columnName row)))
                            match map (List.ofSeq rowValues) with  
                            | Some value -> rows.Add value  
                            | None -> ()

                        connection.close()
                        return Ok (List.ofSeq rows)
                    
                    with 
                    | ex -> 
                        connection.close()
                        return Error (toSqlError (applicationError ex))
        }

    let readScalar (config: ISqlProps) : Async<Result<SqlValue, SqlError>> = 
        async {
            let! connectionResult = connectToPool config.Config
            match connectionResult with 
            | Error err -> return Error err 
            | Ok connection ->
                let queryRequest = request connection
                let sqlQuery = defaultArg config.Query ""
                let! results = rawQuery sqlQuery queryRequest
                match results with 
                | Error requestErr -> 
                    connection.close()
                    return Error requestErr 
                | Ok resultset ->
                    try 
                        let metadata = columnDefinitions resultset
                        let scalarType = metadata |> Array.map snd |> Array.item 0 
                        let value : obj = get "" (Array.item 0 (get "recordset" resultset))
                        let parsedValue = 
                            match scalarType with 
                            | SqlType.Float -> SqlValue.Number (unbox value)
                            | SqlType.TinyInt -> SqlValue.TinyInt (unbox value) 
                            | SqlType.SmallInt -> SqlValue.SmallInt (unbox value)
                            | SqlType.BigInt -> SqlValue.BigInt (int64 (unbox<string> value))
                            | SqlType.Int -> SqlValue.Int (unbox value)
                            | SqlType.Number -> SqlValue.Number (unbox value)
                            | SqlType.DateTime -> SqlValue.Date (unbox value)
                            | SqlType.DateTimeOffset -> SqlValue.Date (unbox value)
                            | SqlType.Decimal -> SqlValue.Decimal (decimal (unbox<float> value))
                            | SqlType.Money -> SqlValue.Decimal (decimal (unbox<float> value))
                            | SqlType.UniqueIdentifier -> SqlValue.UniqueIdentifier (Guid.Parse (unbox<string> value))
                            | SqlType.Bit -> SqlValue.Bool (unbox value)
                            | SqlType.NVarChar -> SqlValue.String (unbox value)

                        connection.close()
                        return Ok parsedValue 
                    with 
                    | ex -> 
                        connection.close() 
                        return Error (toSqlError (applicationError ex))
        } 

    let readJson (config: ISqlProps) : Async<Result<string, SqlError>> = 
        async {
            let! connectionResult = connectToPool config.Config
            match connectionResult with 
            | Error err -> return Error err 
            | Ok connection ->
                let queryRequest = request connection
                let sqlQuery = defaultArg config.Query ""
                let! results = rawQuery sqlQuery queryRequest
                match results with 
                | Error requestErr -> 
                    connection.close()
                    return Error requestErr 
                | Ok resultset ->
                    try 
                        let metadata = columnDefinitions resultset
                        let scalarType = metadata |> Array.map snd |> Array.item 0 
                        let value : obj = get "JSON_F52E2B61-18A1-11d1-B105-00805F49916B" (Array.item 0 (get "recordset" resultset))
                        let parsedValue = 
                            match scalarType with 
                            | SqlType.NVarChar -> unbox<string> value
                            | otherwise -> failwithf "Expected the return type of a scalar value to be a string, instead got %s" (unbox scalarType)

                        connection.close()
                        return Ok parsedValue 
                    with 
                    | ex -> 
                        connection.close() 
                        return Error (toSqlError (applicationError ex))
        } 