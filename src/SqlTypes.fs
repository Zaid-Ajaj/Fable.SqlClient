namespace Fable.SqlClient

[<RequireQualifiedAccess>]
type SqlTypes = 
    | Int
    | Bit
    | BigInt
    | Float
    | Money
    | Decimal of precision:int * scale:int
    | Numeric of precision:int * scale:int
    | Char of int
    | CharMax
    | NChar of int
    | NCharMax
    | NVarChar of int
    | NVarCharMax
    | VarChar of int
    | VarCharMax
    | Text
    | NText 
    | SmallInt
    | Time of scale:int
    | Date 
    | DateTime
    | DateTime2 of scale:int
    | DateTimeOffset of scale:int 
    | SmallDateTime 
    | UniqueIdentifier
    | Variant
    | Binary
    | VarBinary of int
    | VarBinaryMax