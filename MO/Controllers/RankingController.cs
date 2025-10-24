using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MO.Models;
using MySql.Data.MySqlClient;

namespace MO.Controllers
{
    public class RankingController : Controller
    {
        private readonly ConexionBD _conexion;

        public RankingController(ConexionBD conexion)
        {
            _conexion = conexion;
        }

        public IActionResult Index()
        {
            var lista = new List<JuegoRanking>();

            using var conn = _conexion.GetConnection();
            conn.Open();

            string query = @"SELECT u.username, MAX(j.puntaje_total) AS puntaje, MAX(j.fecha) AS fecha
                             FROM juego j
                             JOIN usuario u USING(id_usuario)
                             GROUP BY u.username
                             ORDER BY puntaje DESC;";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new JuegoRanking
                {
                    Username = reader.GetString("username"),
                    Puntaje = reader.GetInt32("puntaje"),
                    Fecha = reader.GetDateTime("fecha")
                });
            }

            return View(lista); // ✅ Enviamos el modelo correctamente
        }
    }
}
