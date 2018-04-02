module Renderer

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Electron
open Node.Exports
open Fable.PowerPack
open Fable.SqlClient

open Elmish
open Elmish.HMR
open Elmish.React
open Elmish.Debug

open Fable.Helpers.React 
open Fable.Helpers.React.Props 
open System

type AppState = {
    Database: string 
    Port: int 
    User: string 
    Password: string 
    Query: string
    Result: obj
}

type Msg = 
  | SetDatabase of string
  | SetPort of int 
  | SetUser of string 
  | SetPassword of string 
  | SetQuery of string
  | SetResult of obj 
  | NoOp
  | ExecuteQuery  
  | ExecuteScalar
  | ExecuteNonQuery

let init () = { 
    Database = "Tests"; 
    Port = 1433; 
    User = "sa"; 
    Password = "VeryStr0ng!Password"
    Query = ""
    Result = obj() }, Cmd.none 

let update msg state = 
    match msg with
    | SetDatabase db -> { state with Database = db }, Cmd.none 
    | SetPort port -> { state with Port = port }, Cmd.none
    | SetUser user -> { state with User = user }, Cmd.none 
    | SetPassword password -> { state with Password = password }, Cmd.none 
    | SetResult result -> { state with Result = result }, Cmd.none
    | SetQuery query -> { state with Query = query }, Cmd.none
    | NoOp -> state, Cmd.none
    | ExecuteQuery ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server "localhost" ]
        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.query state.Query
                return (unbox<obj> results)    
            } 
        state, Cmd.ofPromise getResult () SetResult (fun e -> SetResult e)
    | ExecuteScalar ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server "localhost" ]
        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.queryScalar state.Query
                return (unbox<obj> results)    
            } 
        state, Cmd.ofPromise getResult () SetResult (fun e -> SetResult e)        
    | ExecuteNonQuery ->
        let config = 
            [ SqlConfig.User state.User
              SqlConfig.Password state.Password 
              SqlConfig.Database state.Database
              SqlConfig.Port state.Port
              SqlConfig.Server "localhost" ]
        let getResult() = 
            promise {
                let! pool = SqlClient.connect config 
                let! results = 
                    SqlClient.request pool 
                    |> SqlClient.executeNonQuery state.Query
                return (unbox<obj> results)    
            } 
        state, Cmd.ofPromise getResult () SetResult (fun e -> SetResult e)        

let onChange (f: string -> unit) = 
    OnChange (fun e -> f (!!e.target?value))

let configForm state dispatch = 
  form [ ]
       [ div [ ClassName "form-group" ] 
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
             [ label [ ] [ str "Database" ]
               input [ ClassName "form-control"
                       Placeholder "Database"; 
                       DefaultValue state.Database
                       onChange (SetDatabase >> dispatch) ] ] ] 

let main state dispatch = 
    div [ Style [ Padding 20 ] ]
        [ div [ ClassName "row" ]
              [ div [ ClassName "col-md-3" ] 
                    [ div [ Style [ Margin 10 ]
                            ClassName "btn btn-info"
                            OnClick (fun _ -> dispatch ExecuteQuery) ] 
                          [ str "Execute Query" ]
                      br [ ]
                      div [ ClassName "btn btn-info"
                            Style [ Margin 10 ]
                            OnClick (fun _ -> dispatch ExecuteScalar) ] 
                          [ str "Execute Scalar" ]
                      br [ ]
                      div [ ClassName "btn btn-info"
                            Style [ Margin 10 ]
                            OnClick (fun _ -> dispatch ExecuteNonQuery) ] 
                          [ str "Execute Non Query" ] ]
                div [ ClassName "col-md-7" ] 
                    [ textarea [ Rows 3.0
                                 Cols 70.0
                                 ClassName "form-control"
                                 Placeholder "Query"
                                 DefaultValue state.Query
                                 onChange (SetQuery >> dispatch) ] [ ] ] ]
          hr [ ]
          div [ ] 
              [ textarea [ Cols 70.0; 
                           Rows 10.0; 
                           Value (sprintf "%A" state.Result) 
                           DefaultValue (sprintf "%A" state.Result) ] [  ] ] 
        ] 
   

let view (state: AppState) dispatch = 
    div [ Style [ Padding 20 ] ] 
        [ h1 [ ] [ str "Fable.SqlClient" ]
          hr [ ]
          div [ ClassName "row" ] 
              [ div [ ClassName "col-md-3" ] 
                    [ configForm state dispatch ] 
                div [ ClassName "col-md-7" ] 
                    [ main state dispatch ] ] ]

// App
Program.mkProgram init update view 
|> Program.withReact "root"
|> Program.withConsoleTrace
|> Program.run