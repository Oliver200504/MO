using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;

namespace MO.Data
{
    public class ConexionBD
    {
        private readonly string _connectionString;

        public ConexionBD(IConfiguration configuration)
        {
            // Obtener cadena de conexión desde appsettings.json
            _connectionString = configuration.GetConnectionString("DefaultConnection");

        }

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString);
        }

       
    }
}
