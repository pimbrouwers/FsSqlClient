namespace FsSequel 

module Sql =
    open System.Data
    open System.Data.SqlClient

    // ------------ Types ------------

    /// Used to differentiate between Text & StoredProcedure string input    
    type SqlText =
        | CommandText of string
        | StoredProcedure of string

    /// Differentiate the differents types of connection string security
    type SqlConnectionSecurity =
        | IntegratedSecurity of bool
        | UserIDAndPassword of (string * string)

    
    // ------------ Type Helpers ------------
    /// Create `CommandText` from string
    let newCmd input = CommandText input
    
    /// Create `StoredProcedure` from string
    let newSproc input = StoredProcedure input    


    // ------------ Railway adapters ------------
    /// Convert a dead-end function to a pass-through function
    let passThrough fn input = fn input |> ignore; input

    /// Encapsulate function in simple try/catch wrapper returning the more friendly `Result<'a,'b>`.
    let tryCatchAdv fn exnHandler =
        try 
            fn |> Ok            
        with 
        | ex -> exnHandler ex |> Error

    /// Helper method for `tryCatchAdv` to simply provide the `exn.Message` to the Error path
    let tryCatch fn =
        tryCatchAdv fn (fun ex -> ex.Message)
   

    // ----------- SqlConnectionStringBuilder ------------
    let createConnectionStringBuilder =
        SqlConnectionStringBuilder()

    let setConnectionStringDataSource dataSource (connectionStringBuilder : SqlConnectionStringBuilder) =
        connectionStringBuilder.DataSource <- dataSource

    let setConnectionStringInitialCatalog inititalCatalog (connectionStringBuilder : SqlConnectionStringBuilder) =
        connectionStringBuilder.InitialCatalog <- inititalCatalog

    let setConnectionStringSecurity (connectionSecurity : SqlConnectionSecurity) (connectionStringBuilder : SqlConnectionStringBuilder) =
        let setUserIdAndPassword (userId, password) =
            connectionStringBuilder.UserID <- userId;
            connectionStringBuilder.Password <- password;
        match connectionSecurity with
        | IntegratedSecurity i -> connectionStringBuilder.IntegratedSecurity <- i
        | UserIDAndPassword (u, p) -> setUserIdAndPassword |> ignore

    let getConnectionString (connectionStringBuilder : SqlConnectionStringBuilder) =
        connectionStringBuilder.ConnectionString

    let createConnectionString dataSource inititalCatalog connectionSecurity =
        createConnectionStringBuilder
        |> passThrough (setConnectionStringDataSource dataSource)
        |> passThrough (setConnectionStringInitialCatalog inititalCatalog)
        |> passThrough (setConnectionStringSecurity connectionSecurity)
        |> getConnectionString
    

    // ----------- Connection ------------
    
    /// Create a new `SqlConnection` from connection string
    let createConnection connectionString =
        new SqlConnection(connectionString)

    /// Open `SqlConnection` if it is not already open
    let openConnection (connection : SqlConnection) =
        if connection.State <> ConnectionState.Open then connection.Open()

    /// Close `SqlConnection` it is not already closed
    let closeConnection (connection : SqlConnection) =
        if connection.State <> ConnectionState.Closed then connection.Close()

    /// Create a new OPEN `SqlConnection` from connection string
    let createOpenConnection =
        createConnection 
        >> passThrough openConnection

    
    // ----------- Transactions  ------------

    /// Create a new `SqlTransaction`
    let beginTransaction (connection : SqlConnection) =
        connection.BeginTransaction()

    /// Commit `SqlTransaction`
    let commit (transaction : SqlTransaction) =
        transaction.Commit()

    /// Rollback `SqlTransaction`
    let rollback (transaction : SqlTransaction) =
        transaction.Rollback()

    /// Save (commit with name) `SqlTransaction`
    let save name (transaction : SqlTransaction) =
        transaction.Save(name); 

    /// Undo (rollback with name) `SqlTransaction`
    let undo name (transaction : SqlTransaction) =
        transaction.Rollback(name)

    /// Commit or Rollback `SqlTransaction` based on `Result<'a, 'b>`
    let commitOrRollback (transaction : SqlTransaction) =
        function
        | Ok _ -> commit transaction
        | Error _ -> rollback transaction
    // ----------- Commands -------------
    
    /// Create a new `SqlCommand`
    let createCommand text (transaction : SqlTransaction) =
        new SqlCommand(text, transaction.Connection, transaction)

    /// Configure CommandType for `SqlCommand` 
    let setCommandType commandType (command : SqlCommand) =
        command.CommandType <- commandType        

    /// Create a new Text type `SqlCommand`
    let createTextCommand text =
        createCommand text
        >> passThrough (setCommandType CommandType.Text)

    /// Create a new StoredProcedure type `SqlCommand`
    let createStoredProcedureCommand text   =
        createCommand text
        >> passThrough (setCommandType CommandType.StoredProcedure)

    /// Create a typed (Text or StoredProcedure) from `SqlText`
    let createTypedCommand (sqlText : SqlText) (transaction : SqlTransaction) =
        match sqlText with
        | CommandText text -> createTextCommand text transaction
        | StoredProcedure sproc -> createStoredProcedureCommand sproc transaction

    /// Create a `SqlParameter` from a name-value pair
    let createParameter (name, value) =
        SqlParameter(parameterName = name, value = value)

    /// Add `SqlParameter` to an existing `SqlCommand`
    let addParameter (command: SqlCommand) (parameter : SqlParameter)  =
        command.Parameters.Add(parameter) |> ignore

    /// Add parameter name-value pairs to existing `SqlCommand`
    let withParameters (parameters : seq<string * 'a>) (command : SqlCommand) =        
        parameters 
        |> Seq.iter (fun p -> createParameter p |> addParameter command)
                
    /// Create a typed (Text or StoredProcedure) `SqlCommand` with parameters        
    let createTypedCommandWithParameters sqlText parameters transaction  =
        createTypedCommand sqlText transaction
        |> passThrough (withParameters parameters)

    /// Execute a `SqlCommand` with no ouput (row #'s ignored)
    let executeNonQuery (command : SqlCommand) =
        command.ExecuteNonQuery() |> ignore

    /// Execute a `SqlCommand` with a scalar value result
    let executeScalar (command : SqlCommand) =
        command.ExecuteScalar()

    /// Execute a `SqlCommand` and open an IDbReader
    let executeReader (command : SqlCommand) =
        command.ExecuteReader()


    // ---------- Helpers -------------

    /// Helper method to directly perform a `ExecuteNonQuery` for an existing `SqlConnection`
    let execute text parameters transaction =       
        use cmd = createTypedCommandWithParameters text parameters transaction
        cmd 
        |> executeNonQuery 
        |> ignore
        

    /// Helper method to directly perform an `ExecuteScalar` for an existing `SqlConnection`
    let scalar text parameters mapScalar transaction =        
        use cmd = createTypedCommandWithParameters text parameters transaction
        cmd 
        |> executeScalar 
        |> mapScalar        

    /// Helper method to directly perform an `ExecuteReader` for an existing `SqlConnection`
    let query text parameters mapRecord transaction =
        seq {
            use cmd = createTypedCommandWithParameters text parameters transaction
            use reader = executeReader cmd

            while reader.Read() do
                yield mapRecord reader
        }


    // ----------- BulkCopy ------------

    /// Create a new `SqlBulkCopy` for the specified table
    let createBulkCopy tableName (transaction : SqlTransaction) =
        let setDestinationTableName (sqlBulkCopy : SqlBulkCopy) =
            sqlBulkCopy.DestinationTableName <- tableName
            sqlBulkCopy

        let sqlBulkCopyOptions =
            SqlBulkCopyOptions.CheckConstraints
            ||| SqlBulkCopyOptions.Default            
            ||| SqlBulkCopyOptions.KeepIdentity

        new SqlBulkCopy(transaction.Connection, sqlBulkCopyOptions, transaction)
        |> setDestinationTableName

    /// Commit (stream) `IDataReader` into `SqlBulkCopy` destination
    let writeToServer (reader : IDataReader) (bulkCopy : SqlBulkCopy) =
        bulkCopy.WriteToServer(reader)

    /// Apply `seq<(string * string)>` as `SqlColumnMapping` to `SqlBulkCopy`
    let applyColumnMappings (columnMappings : seq<string * string>) (bulkCopy : SqlBulkCopy) =
        let addColumnMapping ((sourceCol, destinationCol) : string * string) =
            bulkCopy.ColumnMappings.Add(sourceCol, destinationCol) |> ignore

        columnMappings 
        |> Seq.iter addColumnMapping

    /// Helper method to commit `IDataReader` into specified destination
    let bulkInsert tableName columnMappings reader transaction =
        createBulkCopy tableName transaction
        |> passThrough (applyColumnMappings columnMappings)
        |> writeToServer reader