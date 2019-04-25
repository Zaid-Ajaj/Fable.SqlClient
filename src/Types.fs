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
    | [<CompiledName("Int")>] Int
    | [<CompiledName("DateTime")>] DateTime
    | [<CompiledName("UniqueIdentifier")>] UniqueIdentifier
    | [<CompiledName("BigInt")>] BigInt
    | [<CompiledName("TinyInt")>] TinyInt
    | [<CompiledName("SmallInt")>] SmallInt 
    | [<CompiledName("DateTimeOffset")>] DateTimeOffset 
    | [<CompiledName("NVarChar")>] NVarChar 
    | [<CompiledName("Decimal")>] Decimal 
    | [<CompiledName("Number")>] Number 
    | [<CompiledName("Bit")>] Bit
    | [<CompiledName("Money")>] Money 

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

[<Erase>]
type SqlParam = 
    | SqlParam
    
    static member inline From(value: int) = unbox<SqlParam> [ value, SqlType.Int ]
    static member inline From(value: string) = unbox<SqlParam> [ value, SqlType.NVarChar ]
    static member inline From(value: decimal) = unbox<SqlParam> [ value, SqlType.NVarChar ]
    static member inline From(value: DateTime) = unbox<SqlParam> [ value, SqlType.NVarChar ]
    static member inline From(value: Guid) = unbox<SqlParam> [ value, SqlType.NVarChar ]

type ISqlProps = {
    Config: SqlConfig list
    Query: string option 
    Parameters: SqlParam list
}

[<StringEnumAttribute>]
type SqlErrorType =
    | [<CompiledName("ConnectionError")>] ConnectionError
    | [<CompiledName("TransactionError")>]  TransactionError
    | [<CompiledName("RequestError")>] RequestError
    | [<CompiledName("PreparedStatementError")>] PreparedStatementError

type SqlError = {
    name : SqlErrorType
    code : string
    message : string 
    stack : string
}