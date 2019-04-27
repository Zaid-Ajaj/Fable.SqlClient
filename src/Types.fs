namespace Fable.SqlClient

open Fable.Core
open System

[<RequireQualifiedAccess>]
type PoolConfig = 
    /// The maximum number of connections there can be in the pool (default: 10).
    | Max of int
    /// The minimum of connections there can be in the pool (default: 0)
    | Min of int
    /// The Number of milliseconds before closing an unused connection (default: 30000).
    | [<CompiledName("idleTimeoutMillis")>] IdleTimeout of int

[<RequireQualifiedAccess>]
type SqlConfig = 
    /// User name to use for authentication
    | User of string 
    /// Password to use for authentication
    | Password of string
    /// Server to connect to. You can use 'localhost\instance' to connect to named instance
    | Server of string
    /// Port to connect to (default: 1433). Don't set when connecting to named instance.
    | Port of int
    /// Once you set domain, driver will connect to SQL Server using domain login
    | Domain of string
    /// Database to connect to (default: dependent on server configuration).
    | Database of string
    /// Connection timeout in ms (default: 15000)
    | ConnectionTimeout of int
    /// Request timeout in ms (default: 15000)
    | RequestTimeout of int
    /// Stream recordsets/rows instead of returning them all at once as an argument of callback (default: false). You can also enable streaming for each request independently (request.stream = true). Always set to true if you plan to work with large amount of rows.
    | Stream of bool
    /// Connection pool configuration
    | Pool of PoolConfig

[<StringEnum>]
type SqlType =
    | [<CompiledName("Float")>] Float
    | [<CompiledName("Int")>] Int
    | [<CompiledName("DateTime")>] DateTime
    | [<CompiledName("UniqueIdentifier")>] UniqueIdentifier
    | [<CompiledName("BigInt")>] BigInt
    | [<CompiledName("TinyInt")>] TinyInt
    | [<CompiledName("SmallInt")>] SmallInt 
    | [<CompiledName("DateTimeOffset")>] DateTimeOffset 
    | [<CompiledName("NVarChar")>] NVarChar 
    | [<CompiledName("VarChar")>] VarChar 
    | [<CompiledName("Decimal")>] Decimal 
    | [<CompiledName("Number")>] Number 
    | [<CompiledName("Bit")>] Bit
    | [<CompiledName("Money")>] Money 
    | [<CompiledName "VarBinary">] VarBinary

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

type SqlParam() = 
    static member inline From(name: string, value: int) = 
        unbox<SqlParam> (name, value, SqlType.Int)
    static member inline From(name: string, value: uint8) = 
        unbox<SqlParam> (name, value, SqlType.TinyInt)
    static member inline From(name: string, value: int16) = 
        unbox<SqlParam> (name, value, SqlType.SmallInt)
    static member inline From(name: string, value: int64) = 
        unbox<SqlParam> (name, value, SqlType.BigInt)
    static member inline From(name: string, value: bool) = 
        unbox<SqlParam> (name, value, SqlType.Bit) 
    static member inline From(name: string, value: string) = 
        unbox<SqlParam> (name, value, SqlType.NVarChar)
    static member inline From(name: string, value: decimal) = 
        unbox<SqlParam> (name, value, SqlType.Decimal)
    static member inline From(name: string, value: DateTime) = 
        unbox<SqlParam> (name, value, SqlType.DateTime)
    static member inline From(name: string, value: Guid) = 
        unbox<SqlParam> (name, value, SqlType.UniqueIdentifier)
    static member inline From(name: string, value: DateTimeOffset) = 
        unbox<SqlParam> (name, value, SqlType.DateTimeOffset)
    static member inline From(name: string, value: byte[]) = 
        unbox<SqlParam> (name, value, SqlType.VarBinary)
    static member inline Null(name: string) = 
        unbox<SqlParam> (name, null, "NullType")

type ISqlProps = {
    Config: SqlConfig list
    Query: string option 
    StoredProcedure: bool
    Parameters: SqlParam list
}

type NativeSqlError = {
    name : string
    code : string
    message : string 
    stack : string
}

type SqlError = 
    | ConnectionError of message: string * stack: string
    | TransactionError of message: string * stack: string 
    | RequestError of message: string * stack: string 
    | ApplicationError of message: string * stack: string 
    | GenericError of errorType: string * message: string * stack: string

    with 
        member this.Info() = 
            match this with 
            | ConnectionError(msg, stack) -> ("ConnectionError", msg, stack)
            | TransactionError(msg, stack) -> ("TransactionError", msg, stack)
            | RequestError(msg, stack) -> ("RequestError", msg, stack)
            | ApplicationError(msg, stack) -> ("RequestError", msg, stack)
            | GenericError(errorType, msg, stack) -> (errorType, msg, stack)
