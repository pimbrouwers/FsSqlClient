# FsSequel

Idiomatic F# wrappers for ADO.NET & SQL Server.

> `async` methods have not yet been added.

## Getting Started

The simplest way to get started quickly is to use the one of the three helper methods:

```f#
open FsSequel.Sql

use connection = createOpenConnection "your_connection_string"

/// ExecuteNonQuery
connection 
|> execute "update dbo.Table set Col1 = @col1 where Col1 = 2" [("col1", 3)]

/// ExecuteScalar
connection
|> scalar "select 1 as N" [] Convert.ToInt32 

/// Execute Reader
connection
|> query "select 1 as N union all select 2" [] (fun rd -> rd.GetValue(0) |> Convert.ToInt32)

```

## Beyond the basics

### Connections

```f#
open FsSequel.Sql

/// The hard way
use connection = 
    "your_connection_string"
    |> createConnection
    |> passThrough openConnection

/// Arbitrarily create a connection factory (hooray for >>)
let connectionFactory = createOpenConnection 

/// Simple connection string
use connection = connectionFactory "your_connection_string"

/// OR using SqlConnectionStringBuilder
/// I find this method especially value for parsing command-line args as my connection string.
/// This defers its requirement to runtime and prevents it from needing to be stored anywhere.
let connectionString = createConnectionString "DataSource" "InitialCatalog" ["UserID"] ["Password"]
use connection = connectionFactory connectionString

```

### Commands

```f#
open FsSequel.Sql

let connectionString = createConnectionString "DataSource" "InitialCatalog" ["UserID"] ["Password"]
let connectionFactory = createOpenConnection 
use connection = connectionFactory connectrionString

use transaction = connection |> beginTransaction // NOTE: "transact" also created a new transaction

/// The direct way
use command = 
    transaction
    |> createCommand "update dbo.Table set Col1 = @col1 where Col1 = 2" 
    |> passThrough (setCommandType System.Data.CommandType.Text)
    |> passThrough (withParameters [("Col1", 1)]) // `passThrough` simply pipes turns 'a -> unit into 'a -> 'a

/// The "typed" way
let sqlText = Text "update dbo.Table set Col1 = @col1 where Col1 = 2"

use command = 
    transaction
    |> createTypedCommand sqlText
    |> passThrough (withParameters [("Col1", 1)]) // `passThrough` simply pipes turns 'a -> unit into 'a -> 'a

/// The "typed" helper
let sqlText = Text "update dbo.Table set Col1 = @col1 where Col1 = 2"
use command =
    transaction
    |> createTypedCommandWithParameters sqlText [("Col1", 1)])

/// And finally execute
command
|> executeNonQuery

transaction
|> commit   
```

#### Simple `Result<'a,'b>`-based Error Handling

```f#
use connection = createOpenConnection "your_connection_string"
let result = tryCatch(connection |> scalar "select 1 as N" [] Convert.ToInt32) 

match result with
| Ok res -> printfn "%A" res
| Error msg -> printfn "%s" msg
```

### Bulk Copy

```f#
open FsSequel.Sql

let connectionString = createConnectionString "DataSource" "InitialCatalog" ["UserID"] ["Password"]
let connectionFactory = createOpenConnection 
use connection = connectionFactory connectrionString
use reader = SOMETHING_IMPLEMENTING_IDataReader
let columnMappings = [("Col1", "Coll1")]

bulkInsert "dbo.Table" columnMappings reader connection
```