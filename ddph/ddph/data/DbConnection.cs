using Microsoft.Data.SqlClient;

public class DbConnection
{
    private readonly string connectionString =
        @"Server=.\SQLEXPRESS;Database=ddph;Trusted_Connection=True;";

    public SqlConnection GetConnection()
    {   
        return new SqlConnection(connectionString);
    }
}