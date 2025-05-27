using System;
using System.Data;
using System.Data.SqlClient;

namespace Negocio.BBDD
{
    internal class ReferenciaService
    {
        public void CrearReferencia(string referenciaID, string descripcion, decimal precio, DateTime fechaCreacion)
        {
            using (var conn = DBConnection.GetConnection())
            {
                conn.Open();
                string query = "INSERT INTO REFERENCIAS (Referencia, DES_REFERENCIA, PRECIO, F_CREACION, IMAGEN) " +
                               "VALUES (@Ref, @Desc, @Precio, @Fecha, NULL)";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Ref", referenciaID);
                    cmd.Parameters.AddWithValue("@Desc", descripcion);
                    cmd.Parameters.AddWithValue("@Precio", precio);
                    cmd.Parameters.AddWithValue("@Fecha", fechaCreacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable ObtenerReferencias()
        {
            using (var conn = DBConnection.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM REFERENCIAS";
                using (var adapter = new SqlDataAdapter(query, conn))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }
}