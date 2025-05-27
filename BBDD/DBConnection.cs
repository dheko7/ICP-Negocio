using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Negocio.BBDD
{
    public static class DBConnection
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["bdsaidEntities"].ConnectionString;

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(connectionString);
        }
    }
}
