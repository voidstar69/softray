using System;
using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Web;

/// <summary>
/// A wrapper around a database.
/// </summary>
public class Database : IDisposable
{
  // The connection to the database.
  private IDbConnection connection = null;

  // Has Dispose already been called?
  private bool disposed = false;

  public enum Engine
  {
    MSAccess,
    MySQL,
    SQLServer
  }

  private static string dataRoot;

  public static string GetDataRoot(HttpServerUtility server)
  {
    if (dataRoot != null)
    {
        return dataRoot;
    }

    string root;
    try
    {
      // Running on webserver?
      // TODO: On dev box, this sometimes throws; sometimes maps to web host path! Bizarre!
      root = server.MapPath("/");

      // Remove last two parts of path.
      //root = root.Substring(0, root.LastIndexOf('\\', root.LastIndexOf('\\') - 1));
    }
    catch(InvalidOperationException)
    {
      // No, we are running on a dev box.
      // TODO: Because of the bug above, we hack this on dev box.
      //root = @"C:\Users\Gavin\Documents\My Websites\Brinkster";
      root = @"C:\Src\SVN\website";
      //root = @"C:\Src\Model Library\website";
    }

    //dataRoot = root + @"\database\";
    dataRoot = root + '\\';
    return dataRoot;
  }

  public static Database OpenMySqlDatabase()
  {
    return new Database(null, Engine.MySQL, "gavinm");
  }

  public Database(HttpServerUtility server) : this(server, "Finance.mdb")
  {
  }

  public Database(HttpServerUtility server, string databaseName)
  {
    string dbPath = GetDataRoot(server) + databaseName;
    string connectionString = "PROVIDER=Microsoft.Jet.OLEDB.4.0; Data Source=" + dbPath + ";Persist Security Info=False";

    connection = new OleDbConnection(connectionString);
    connection.Open();
  }

  public Database(HttpServerUtility server, Engine dbEngine, string databaseName)
  {
    switch(dbEngine)
    {
      case Engine.MSAccess:
        string dbPath = GetDataRoot(server) + databaseName;
        string connectionString = "PROVIDER=Microsoft.Jet.OLEDB.4.0; Data Source=" + dbPath + ";Persist Security Info=False";
        connection = new OleDbConnection(connectionString);
        connection.Open();
        break;

      case Engine.MySQL:
//        connection = new OdbcConnection("Driver={MySQL ODBC 3.51 Driver};Server=localhost;Database=" + databaseName + ";user id=gavinm;password=");
        connection = new OdbcConnection("Driver={MySQL ODBC 3.51 Driver};Server=mysql7.brinkster.com;Database=" + databaseName + ";user id=gavinm;password=k0kek0la");
        connection.Open();
        break;

      case Engine.SQLServer:
        throw new NotImplementedException("No support for SQL Server databases yet");
    }
  }

  public int ExecuteNonQuery(string cmdText)
  {
    using (IDbCommand command = connection.CreateCommand())
    {
      command.CommandType = CommandType.Text;
      command.CommandText = cmdText;
      return command.ExecuteNonQuery();
    }
  }

  public IDataReader ExecuteReader(string cmdText)
  {
    using (IDbCommand command = connection.CreateCommand())
    {
      command.CommandType = CommandType.Text;
      command.CommandText = cmdText;
      return command.ExecuteReader();
    }
  }

  public object ExecuteScalar(string cmdText)
  {
    using (IDbCommand command = connection.CreateCommand())
    {
      command.CommandType = CommandType.Text;
      command.CommandText = cmdText;
      return command.ExecuteScalar();
    }
  }

  /// <summary>
  /// Execute a database function on a MySQL database.
  /// </summary>
  /// <param name="functionName">The name of the database function to execute.</param>
  /// <param name="parameters">Parameters to the database function.</param>
  /// <returns>The result of the database function.</returns>
  public object ExecuteFunction(string functionName, params object[] parameters)
  {
    return ExecuteStoredProc("SELECT " + functionName, parameters);
  }

  /// <summary>
  /// Execute a database procedure on a MySQL database.
  /// </summary>
  /// <param name="procedureName">The name of the database procedure to execute.</param>
  /// <param name="parameters">Parameters to the database procedure.</param>
  /// <returns>The result of the database procedure.</returns>
  public void ExecuteProcedure(string procedureName, params object[] parameters)
  {
    ExecuteStoredProc("CALL " + procedureName, parameters);
  }

  /// <summary>
  /// Execute a stored procedure on a MySQL database.
  /// </summary>
  /// <param name="storedProcName">The name of the stored proc to execute,
  /// prefixed by 'SELECT' for functions or 'CALL' for procedures.</param>
  /// <param name="parameters">Parameters to the stored procedure.</param>
  /// <returns>The result of the stored procedure.</returns>
  public object ExecuteStoredProc(string storedProcName, params object[] parameters)
  {
    // TODO: Allow named parameters to be passed to the stored proc.
    // Quick-and-dirty: build up a query string to exec the stored proc.
    // Better: build up a list of parameter objects to the stored proc (seems to break MySQL).
    using (IDbCommand command = connection.CreateCommand())
    {
      command.CommandType = CommandType.StoredProcedure;
      command.CommandText = storedProcName + '(';
      for (int i = 0; i < parameters.Length; i++)
      {
        // TODO: Assuming string type here.
        command.CommandText += "'" + parameters[i].ToString() + "'";
        if (i < parameters.Length - 1)
        {
          command.CommandText += ',';
        }

        // TODO: This seems to break MySQL.
/*
        IDataParameter param = command.CreateParameter();
        param.ParameterName = ':' + (string)parameters[i * 2];
        param.Value = parameters[i * 2 + 1];
        command.Parameters.Add(param);
        command.CommandText += param.ParameterName;
        if (i < parameters.Length - 2)
        {
          command.CommandText += ',';
        }
 */
      }
      command.CommandText += ')';
      return command.ExecuteScalar();
    }
  }

  public IDbCommand CreateCommand()
  {
    return connection.CreateCommand();
  }

  // Implement IDisposable.
  public void Dispose()
  {
    Dispose(true);
    // Call GC.SupressFinalize to take this object off the finalization queue
    // and prevent finalization code for this object from executing a second time.
    GC.SuppressFinalize(this);
  }

  // If disposing equals true, managed and unmanaged resources can be disposed.
  // If disposing equals false, only unmanaged resources can be disposed.
  private void Dispose(bool disposing)
  {
    // Check to see if Dispose has already been called.
    if (!disposed)
    {
      // If disposing equals true, dispose all managed and unmanaged resources.
      if (disposing)
      {
        // Dispose managed resources.
        connection.Dispose();
        connection = null;
      }

      disposed = true;
    }
  }
}
