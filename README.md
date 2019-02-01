# FsSequel

Idiomatic F# wrappers for ADO.NET & SQL Server.

> `async` methods have not yet been added.

## Getting Started

The simplest way to get started quickly is to use the one of the three helper methods:

```f#
open FsSequel.Sql

use connection = createOpenConnection "your_connection_string"
use transaction = connection |> beginTransaction

/// ExecuteNonQuery
execute (newCmd "update dbo.Table set Col1 = @col1 where Col1 = 2") [("col1", 3)] transaction
execute (newSproc "dbo.MySproc") [("param1", 3)] transaction

/// ExecuteScalar
scalar (newCmd "select 1 as N") [] Convert.ToInt32 transaction

/// Execute Reader
query (newCmd "select 1 as N union all select 2") [] (fun rd -> rd.GetValue(0) |> Convert.ToInt32) transaction

/// AND Commit work
transaction |> commit

/// OR Use built-in error handling
let sproc = execute (newSproc "dbo.MySproc") [("param1", 3)]
trycatch (transaction |> sproc)
|> commitOrRollback transaction
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

### Transaction 

```f#
open FsSequel.Sql

use connection = createOpenConnection "your_connection_string"

/// Begin
use transaction = connection |> beginTransaction

/// Save
transaction |> save "name"

/// Undo 
transaction |> undo "name"

/// Commit
transaction |> commit

/// Rollback
transaction |> rollback

/// Commit or rollback based on Result<'a, 'b>
transaction |> commitOrRollback

```

### Commands

```f#
open FsSequel.Sql

let connectionString = createConnectionString "DataSource" "InitialCatalog" ["UserID"] ["Password"]
let connectionFactory = createOpenConnection 
use connection = connectionFactory connectrionString
use transaction = connection |> beginTransaction

/// The direct way
use command = 
    transaction
    |> createCommand "update dbo.Table set Col1 = @col1 where Col1 = 2" 
    |> passThrough (setCommandType System.Data.CommandType.Text)
    |> passThrough (withParameters [("Col1", 1)]) // `passThrough` simply pipes turns 'a -> unit into 'a -> 'a

/// The "typed" way
let sqlText = newCmd "update dbo.Table set Col1 = @col1 where Col1 = 2"
// OR let sprocName = newSproc "dbo.MySproc"

use command = 
    transaction
    |> createTypedCommand sqlText
    |> passThrough (withParameters [("Col1", 1)]) // `passThrough` simply pipes turns 'a -> unit into 'a -> 'a

/// The "typed" helper
let sqlText = newCmd "update dbo.Table set Col1 = @col1 where Col1 = 2"
use command = transaction |> createTypedCommandWithParameters sqlText [("Col1", 1)])

/// And finally, do the work
command |> executeNonQuery
transaction |> commit

/// Or if you want to handle exceptions
trycatch (command |> executeNonQuery)
|> commitOrRollback transaction
```

#### Simple `Result<'a,'b>`-based Error Handling

```f#
use connection = createOpenConnection "your_connection_string"
use transaction = connection |> beginTransaction

// Manual
let result = tryCatch(scalar "select 1 as N" [] Convert.ToInt32 transaction) 

match result with
| Ok res -> printfn "%A" res
| Error msg -> printfn "%s" msg

// Pre-handled
tryCatch(scalar "select 1 as N" [] Convert.ToInt32 transaction)
|> commitOrRollback transaction
```

### Bulk Copy

```f#
open FsSequel.Sql

let connectionString = createConnectionString "DataSource" "InitialCatalog" ["UserID"] ["Password"]
let connectionFactory = createOpenConnection 
use connection = connectionFactory connectrionString
use transaction = connection |> beginTransaction

use reader = SOMETHING_IMPLEMENTING_IDataReader
let columnMappings = [("Col1", "Col1WithAnotherName")] // (Source Column * Destination Column)

bulkInsert "dbo.Table" columnMappings reader connection
```