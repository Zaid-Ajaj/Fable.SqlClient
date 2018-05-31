namespace Fable.SqlClient

open Fable.Core
open Fable.Core.JsInterop
open Fable

[<Erase>]
type ISqlType = ISqlType 

type ISqlRequest = 
    abstract query<'a> : string -> Fable.Import.JS.Promise<'a []>
    abstract rowsAffected : int 
    abstract multiple : bool with get, set
    abstract stream : bool with set, get
    abstract input : string -> obj -> obj -> unit
    abstract output : string -> obj -> unit
    abstract on : string -> (obj -> unit) -> unit

[<AllowNullLiteral>]
type ISqlConnectionPool = 
    abstract request : unit -> ISqlRequest
    abstract close : unit -> unit
    abstract connect : unit -> Fable.Import.JS.Promise<unit>

type IMSSql = 
    abstract connect : SqlConfig -> Fable.Import.JS.Promise<ISqlConnectionPool>
    abstract Bit : ISqlType with get 
    abstract BigInt : ISqlType with get 
    abstract Decimal : int -> int -> ISqlType
    abstract Float : ISqlType
    abstract Int : ISqlType
    abstract Money : ISqlType 
    abstract Numeric : int -> int -> ISqlType
    abstract SmallInt : ISqlType 
    abstract SmallMoney : ISqlType
    abstract Real : ISqlType
    abstract TinyInt : ISqlType 
    abstract Char : int -> ISqlType
    abstract NChar : int -> ISqlType
    abstract Text : ISqlType with get
    abstract NText : ISqlType with get
    abstract VarChar : int -> ISqlType
    abstract NVarChar : int -> ISqlType
    abstract Time : int -> ISqlType
    abstract Date : ISqlType with get
    abstract DateTime : ISqlType with get 
    abstract DateTime2 : int -> ISqlType
    abstract DateTimeOffset : int -> ISqlType
    abstract SmallDateTime : ISqlType with get
    abstract UniqueIdentifier : ISqlType with get
    abstract Variant : ISqlType with get 
    abstract Binary : ISqlType with get
    abstract VarBinary : int -> ISqlType 
    abstract MAX : int with get

module ConnectionPool = 
    let create (config: SqlConfig) : ISqlConnectionPool = import "createConnectionPool" "./createPool.js"

[<StringEnumAttribute>]
type SqlErrorType =
    | [<CompiledName("ConnectionError")>] ConnectionError
    | [<CompiledName("TransactionError")>]  TransactionError
    | [<CompiledName("RequestError")>] RequestError
    | [<CompiledName("PreparedStatementError")>] PreparedStatementError

[<Pojo>]
type SqlError = {
    name : SqlErrorType
    code : string
    message : string 
    stack : string
}

