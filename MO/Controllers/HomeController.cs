using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MO.Models;
using System.Diagnostics;

using MySql.Data.MySqlClient;



namespace MO.Controllers
{
    public class HomeController : Controller
    {
        
        private readonly ConexionBD _conexion;

        public HomeController(ConexionBD conexion)
        {
            _conexion = conexion;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult MenuPrincipal()
        {
            ViewBag.Username = HttpContext.Session.GetString("Username");

            var top5 = ObtenerTop5Jugadores();
            ViewBag.TopJugadores = top5;

            return View();
        }

        private List<JugadorRanking> ObtenerTop5Jugadores()
        {
            var topJugadores = new List<JugadorRanking>();  // lista donde guardo top 5

            using (var conn = _conexion.GetConnection())
            {
                conn.Open();

                string query = @"
            SELECT 
                u.username AS Nombre,
                COALESCE(SUM(CASE WHEN d.es_correcta=1 THEN 1 ELSE 0 END),0) AS RespuestasCorrectas,
                COALESCE(SUM(j.puntaje_total),0) AS PuntajeTotal
            FROM usuario u
            LEFT JOIN juego j ON u.id_usuario = j.id_usuario
            LEFT JOIN detalleJuego d ON j.id_juego = d.id_juego
            GROUP BY u.id_usuario, u.username
            ORDER BY PuntajeTotal DESC
            LIMIT 5;";
                // une usuario con sus juegos y luego con los detalles; con LEFT JOIN
                
                using var cmd = new MySqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();
                // recorrer resultados y llenar la lista
                while (reader.Read())
                {
                    topJugadores.Add(new JugadorRanking
                    {
                        Nombre = reader.GetString("Nombre"),
                        RespuestasCorrectas = reader.GetInt32("RespuestasCorrectas"),
                        PuntajeTotal = reader.GetInt32("PuntajeTotal")
                    });
                }
            }

            return topJugadores;
        }

    }
}