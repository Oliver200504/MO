using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MySql.Data.MySqlClient;
using MO.Data;

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
            var lista = new List<(string Usuario, int Puntaje)>();

            using var conn = _conexion.GetConnection();
            conn.Open();
            string query = "SELECT username, MAX(puntaje_total) AS puntaje FROM juego JOIN usuario USING(id_usuario) GROUP BY username ORDER BY puntaje DESC;";
            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add((reader.GetString("username"), reader.GetInt32("puntaje")));
            }

            ViewBag.Ranking = lista;
            return View();
        }
    }
}
