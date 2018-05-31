module Renderer

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Node.Exports
open Fable.PowerPack
open Fable.SqlClient
open Fable.Import.Browser
open Elmish
open Elmish.HMR
open Elmish.React
open Elmish.Debug

open Fable.Helpers.React 
open Fable.Helpers.React.Props 
open System
open Fable
open Elmish.ReactNative.Components
open Fable
open Fable

type Tab = Query | ParameterizedQuery | StoredProcedure

type ParamDirection = Input | Output

type QueryParam = {
    Id : int
    Type: ParamDirection
    SqlType : SqlTypes
    Name : string
}

type AppState = {
    Database: string 
    Port: int 
    User: string 
    Password: string 
    Query: string
    Host: string
    Result: Result<obj, obj>
    IsTabular: bool
    AvailableDatabases : string list option 
    CurrentTab : Tab
    QueryParameters : QueryParam list
}

type Msg = 
  | SetDatabase of string
  | SetPort of int 
  | SetUser of string 
  | SetPassword of string 
  | SetQuery of string
  | SetHost of string
  | SetTab of Tab
  | SetResult of Result<obj, obj> 
  | NoOp
  | ExecuteQuery  
  | ExecuteScalar
  | ExecuteNonQuery
  | RefreshDatabases 
  | SetDatabases of string list option

[<PassGenerics>]
let tryLoad<'t> name = 
    try BrowserLocalStorage.load<'t> name 
    with | _ -> FSharp.Core.None

let init () = 
    match tryLoad<AppState> "Model" with 
    | Some model -> model, Cmd.none
    | Option.None -> 
        { Database = ""; 
          Port = 0; 
          QueryParameters = []
          User = ""; 
          CurrentTab = Query
          Host = "";
          Password = ""
          Query = ""
          AvailableDatabases = None
          IsTabular = false
          Result = Result.Ok (obj()) }, Cmd.none  

module QueryResult = 
    type DbNames = { name: string }

let update msg state = 
    match msg with
    | SetDatabase db -> { state with Database = db }, Cmd.none 
    | SetPort port -> { state with Port = port }, Cmd.none
    | SetUser user -> { state with User = user }, Cmd.none 
    | SetHost host -> { state with Host = host }, Cmd.none
    | SetPassword password -> { state with Password = password }, Cmd.none 
    | SetResult result -> { state with IsTabular = false; Result = result }, Cmd.none
    | SetQuery query -> { state with Query = query }, Cmd.none
    | NoOp -> state, Cmd.none
    | SetTab tab -> { state with CurrentTab = tab }, Cmd.none
    | ExecuteQuery ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server state.Host ]

        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.query state.Query
                
                return results
            } 

        state, Cmd.ofPromise getResult () (fun x -> SetResult (unbox x)) (fun e -> SetResult (Result.Error (unbox e)))
    | ExecuteScalar ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server state.Host ]
        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.queryScalar state.Query
                return results   
            } 
        state, Cmd.ofPromise getResult () (function 
                                            | Result.Ok e -> SetResult (Result.Ok e)
                                            | Result.Error err -> SetResult (Result.Error (unbox err))) (fun e -> SetResult (Result.Error (unbox e)))        
    | RefreshDatabases ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Port state.Port
              SqlConfig.Server state.Host
              SqlConfig.Database "" ]   

        let getDatabases() = 
            promise {
                let! pool = SqlClient.connect config 
                let query = "SELECT name FROM master.sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')"
                let request = SqlClient.request pool
                let! results = SqlClient.query<QueryResult.DbNames> query request 
                match results with 
                | Result.Ok values -> 
                    let availableDatabases =
                        values 
                        |> Array.map (fun r -> r.name)
                        |> List.ofArray
                        |> Some 

                    printfn "%A" availableDatabases
                    return availableDatabases
                | Result.Error err -> return None  
            }

        state, Cmd.ofPromise 
                  getDatabases () 
                  SetDatabases
                  (unbox >> Result.Error >> SetResult)        
    
    | SetDatabases xs -> { state with AvailableDatabases = xs }, Cmd.none
    | ExecuteNonQuery ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server state.Host ]
        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.executeNonQuery state.Query
                return  results    
            } 
        state, Cmd.ofPromise getResult () (fun res -> SetResult (unbox res)) (fun e -> SetResult (Result.Error (unbox e)))        

let onChange (f: string -> unit) = 
    OnChange (fun e -> f (!!e.target?value))

let configForm state dispatch = 
  form [ ]
       [ div [ ClassName "form-group" ] 
             [ label [ ] [ str "Host" ]
               input [ ClassName "form-control"
                       Placeholder "Host"; 
                       DefaultValue state.Host
                       onChange (SetHost >> dispatch)  ] ]
           
         div [ ClassName "form-group" ] 
             [ label [ ] [ str "Username" ]
               input [ ClassName "form-control"
                       Placeholder "Username"; 
                       DefaultValue state.User
                       onChange (SetUser >> dispatch)  ] ]
         
         div [ ClassName "form-group" ] 
             [ label [ ] [ str "Password" ]
               input [ ClassName "form-control"
                       Placeholder "Password"; 
                       DefaultValue state.Password
                       onChange (SetPassword >> dispatch) ] ]
         
         div [ ClassName "form-group" ] 
             [ label [ ] [ str "Port" ]
               input [ ClassName "form-control"
                       Placeholder "Port"; 
                       DefaultValue (string state.Port)
                       onChange (int >> SetPort >> dispatch) ] ]
         
         div [ ClassName "form-group" ] 
             [ div [ ClassName "btn btn-success"
                     OnClick (fun _ -> dispatch RefreshDatabases) ] 
                   [ str "Refresh Databases" ] ]
         
         div [ ClassName "form-group" ] 
             [ label [ ] [ str "Selected database" ]
               select [ ClassName "form-control"
                        Placeholder "Database"; 
                        DefaultValue state.Database
                        onChange (SetDatabase >> dispatch) ] 
                      [ match state.AvailableDatabases with 
                        | None -> () 
                        | Some values -> 
                            for value in values do 
                                yield option [ ] [ str value ] ] ] ] 

[<Emit("JSON.stringify(JSON.parse($0), null, 4)")>]
let beautify (input: string) : string = jsNative

[<Emit("JSON.stringify($0)")>]
let toString (x: obj) : string = jsNative

let resultView state dispatch = 
 let color = match state.Result with  | Result.Ok _ -> "green" | Result.Error _ -> "crimson"
 textarea [ Cols 70.0; 
            Rows 10.0; 
            Style [ Color color ]
            ClassName "form-control"
            ReadOnly true
            Value (beautify (toJson state.Result)) ] [  ]

let tab state dispatch tab name = 
    let isActive = state.CurrentTab = tab
    let className = if isActive then "nav-link active" else "nav-link"
    a [ ClassName className;
        OnClick (fun _ -> dispatch (SetTab tab)) 
        Style [ Cursor "pointer" ] ] 
      [ str name ]
    
let tabs state dispatch = 
    ul [ ClassName "nav nav-tabs" ] 
       [ li [ ClassName "nav-item" ] 
            [ tab state dispatch Query "Query" ]
         li [ ClassName "nav-item" ] 
            [ tab state dispatch ParameterizedQuery "Parameterized Query" ]
         li [ ClassName "nav-item" ] 
            [ tab state dispatch StoredProcedure "Stored Procedure" ] ]

let queryButtons state dispatch = 
    div [ ClassName "row"
          Style [PaddingLeft 10] ] 
          [ div [ Style [ Margin 5; ]
                  ClassName "btn btn-info"
                  OnClick (fun _ -> dispatch ExecuteQuery) ] 
                [ str "Execute Query" ]
            br [ ]
            div [ ClassName "btn btn-info"
                  Style [ Margin 5;  ]
                  OnClick (fun _ -> dispatch ExecuteScalar) ] 
                [ str "Execute Scalar" ]
            br [ ]
            div [ ClassName "btn btn-info"
                  Style [ Margin 5; ]
                  OnClick (fun _ -> dispatch ExecuteNonQuery) ] 
                [ str "Execute Non Query" ] ]

let query state dispatch = 
  div 
    [ ]
    [ queryButtons state dispatch         
      br [ ]
      textarea [ 
        Rows 3.0
        Cols 70.0
        ClassName "form-control"
        Placeholder "Query"
        DefaultValue state.Query
        onChange (SetQuery >> dispatch) ] [ ] 
      br [ ]
      h3 [ ] [ str "Results" ]
      br [ ]
      resultView state dispatch ] 

let main state dispatch = 
    let view = 
        match state.CurrentTab with
        | Query -> query state dispatch 
        | ParameterizedQuery -> h3 [ ] [ str "Parameterized query" ]
        | StoredProcedure -> h3 [ ] [ str "Stored Procedure" ]

    div [ Style [ Padding 20 ] ]
        [ div [ ]
              [ tabs state dispatch
                br [ ]
                view ] ]
   
let view (state: AppState) dispatch = 
    div [ Style [ Padding 20 ] ] 
        [ h1 [ ] [ str "Fable.SqlClient" ]
          hr [ ]
          div [ ClassName "row" ] 
              [ div [ ClassName "col-md-3" ] 
                    [ configForm state dispatch ] 
                div [ ClassName "col-md-9" ] 
                    [ main state dispatch ] ] ]

// App
Program.mkProgram init update view 
|> Program.withReact "root"
|> Program.withConsoleTrace
|> Program.withTrace (fun msg model -> BrowserLocalStorage.save "Model" model)
|> Program.run