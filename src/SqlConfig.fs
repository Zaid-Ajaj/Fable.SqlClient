namespace Fable.SqlClient

open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
type PoolConfig = 
    /// The maximum number of connections there can be in the pool (default: 10).
    | Max of int
    /// The minimum of connections there can be in the pool (default: 0)
    | Min of int
    /// The Number of milliseconds before closing an unused connection (default: 30000).
    | [<CompiledName("idleTimeoutMillis")>] IdleTimeout of int
    static member create (config: PoolConfig list) = 
        keyValueList CaseRules.LowerFirst config
        |> unbox<PoolConfig>

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

    static member create (config: SqlConfig list) = 
        keyValueList CaseRules.LowerFirst config
        |> unbox<SqlConfig>

