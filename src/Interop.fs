namespace Fable.SqlClient

open Fable.Core
open Fable.Core.JsInterop
open Fable

[<Erase>]
type private ISqlType = ISqlType 

type private ISqlRequest = 
    abstract query : string -> (SqlError -> obj -> unit) -> unit
    abstract execute : string -> (SqlError -> obj -> unit) -> unit
    abstract rowsAffected : int 
    abstract multiple : bool with get, set
    abstract stream : bool with set, get
    abstract input : string -> obj -> obj -> unit
    abstract output : string -> obj -> unit
    abstract on : string -> (obj -> unit) -> unit

[<AllowNullLiteral>]
type private ISqlConnectionPool = 
    abstract request : unit -> ISqlRequest
    abstract close : unit -> unit
    abstract connect : (exn -> unit) -> unit

type private IMSSql = 
    abstract connect : SqlConfig -> Fable.Core.JS.Promise<ISqlConnectionPool>
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

