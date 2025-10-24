using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MO.Models;
using System.Data;

namespace MO.Controllers
{
    public class JuegoController : Controller
    {
        private readonly string connectionString = "server=localhost;database=mo;uid=root;pwd=root;";

        // Verifica que haya sesión iniciada
        public IActionResult Iniciar()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Usuario");

            ViewBag.Username = username;
            return View();
        }

        // Vista principal del módulo de juego
        public IActionResult Index()
        {
            return View();
        }

        // Mostrar la ruleta de categorías
        public IActionResult Ruleta()
        {
            List<Categoria> categorias = new List<Categoria>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT * FROM categoria", conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    categorias.Add(new Categoria
                    {
                        id_categoria = reader.GetInt32("id_categoria"),
                        nombre_categoria = reader.GetString("nombre_categoria")
                    });
                }
            }

            return View(categorias);
        }

        // Inicia nueva partida (crea registro en la tabla juego)
        public IActionResult NuevoJuego()
        {
            int idUsuario = Convert.ToInt32(HttpContext.Session.GetInt32("IdUsuario"));
            int idJuego = 0;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand(
                    "INSERT INTO juego (fecha, id_usuario, puntaje_total) VALUES (NOW(), @id_usuario, 0); SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@id_usuario", idUsuario);
                idJuego = Convert.ToInt32(cmd.ExecuteScalar());
            }

            HttpContext.Session.SetInt32("id_juego", idJuego);
            HttpContext.Session.SetInt32("puntaje_actual", 0);
            HttpContext.Session.SetInt32("rondas_completadas", 0);

            return RedirectToAction("Ruleta");
        }

        // Inicia una ronda con 10 preguntas aleatorias de la categoría seleccionada
        public IActionResult IniciarRonda(int idCategoria)
        {
            List<Pregunta> preguntas = new List<Pregunta>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM pregunta WHERE id_categoria = @cat ORDER BY RAND() LIMIT 10";
                var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cat", idCategoria);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    preguntas.Add(new Pregunta
                    {
                        id_pregunta = reader.GetInt32("id_pregunta"),
                        texto_pregunta = reader.GetString("texto_pregunta"),
                        id_categoria = reader.GetInt32("id_categoria")
                    });
                }
            }

            HttpContext.Session.SetString("Preguntas", System.Text.Json.JsonSerializer.Serialize(preguntas));
            HttpContext.Session.SetInt32("IndiceActual", 0);

            return RedirectToAction("Ronda");
        }

        // Muestra la pregunta actual
        public IActionResult Ronda()
        {
            var preguntasJson = HttpContext.Session.GetString("Preguntas");
            if (preguntasJson == null)
                return RedirectToAction("Ruleta");

            var preguntas = System.Text.Json.JsonSerializer.Deserialize<List<Pregunta>>(preguntasJson);
            int indice = HttpContext.Session.GetInt32("IndiceActual") ?? 0;

            if (indice >= preguntas.Count)
                return RedirectToAction("FinalizarRonda");

            Pregunta preguntaActual = preguntas[indice];
            List<Respuesta> respuestas = new List<Respuesta>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT * FROM respuesta WHERE id_pregunta = @p", conn);
                cmd.Parameters.AddWithValue("@p", preguntaActual.id_pregunta);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    respuestas.Add(new Respuesta
                    {
                        id_respuesta = reader.GetInt32("id_respuesta"),
                        texto_respuesta = reader.GetString("texto_respuesta"),
                        es_correcta = reader.GetBoolean("es_correcta"),
                        id_pregunta = reader.GetInt32("id_pregunta")
                    });
                }
            }

            ViewBag.Pregunta = preguntaActual;
            ViewBag.Respuestas = respuestas;
            return View();
        }

        // Procesa la respuesta del jugador (ahora con tiempo y puntos dinámicos)
        [HttpPost]
        public IActionResult Responder(int idPregunta, int idRespuesta, int tiempoRespuesta)
        {
            int idJuego = HttpContext.Session.GetInt32("id_juego") ?? 0;
            int puntaje = HttpContext.Session.GetInt32("puntaje_actual") ?? 0;
            bool esCorrecta = false;
            int puntosGanados = 0;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Verificar si la respuesta fue correcta
                var cmd = new MySqlCommand("SELECT es_correcta FROM respuesta WHERE id_respuesta = @r", conn);
                cmd.Parameters.AddWithValue("@r", idRespuesta);
                esCorrecta = Convert.ToBoolean(cmd.ExecuteScalar());

                // Calcular puntos según el tiempo de respuesta
                if (esCorrecta)
                {
                    if (tiempoRespuesta <= 5)
                        puntosGanados = 3;
                    else if (tiempoRespuesta <= 10)
                        puntosGanados = 2;
                    else if (tiempoRespuesta <= 30)
                        puntosGanados = 1;
                }

                // Guardar detalle de la respuesta
                var insert = new MySqlCommand(
                    "INSERT INTO detalleJuego (id_juego, id_pregunta, id_respuesta, es_correcta) VALUES (@j, @p, @r, @c)", conn);
                insert.Parameters.AddWithValue("@j", idJuego);
                insert.Parameters.AddWithValue("@p", idPregunta);
                insert.Parameters.AddWithValue("@r", idRespuesta);
                insert.Parameters.AddWithValue("@c", esCorrecta);
                insert.ExecuteNonQuery();

                // Actualizar puntaje total si aplica
                if (puntosGanados > 0)
                {
                    puntaje += puntosGanados;
                    var update = new MySqlCommand("UPDATE juego SET puntaje_total = puntaje_total + @pts WHERE id_juego = @j", conn);
                    update.Parameters.AddWithValue("@pts", puntosGanados);
                    update.Parameters.AddWithValue("@j", idJuego);
                    update.ExecuteNonQuery();
                }
            }

            // Avanzar a la siguiente pregunta
            var preguntas = System.Text.Json.JsonSerializer.Deserialize<List<Pregunta>>(HttpContext.Session.GetString("Preguntas"));
            int indice = HttpContext.Session.GetInt32("IndiceActual") ?? 0;
            indice++;

            HttpContext.Session.SetInt32("IndiceActual", indice);
            HttpContext.Session.SetInt32("puntaje_actual", puntaje);

            if (indice >= preguntas.Count)
                return RedirectToAction("FinalizarRonda");

            return RedirectToAction("Ronda");
        }

        // Al terminar las 10 preguntas de una categoría
        public IActionResult FinalizarRonda()
        {
            int rondas = HttpContext.Session.GetInt32("rondas_completadas") ?? 0;
            rondas++;
            HttpContext.Session.SetInt32("rondas_completadas", rondas);

            if (rondas >= 5)
                return RedirectToAction("FinalizarJuego");

            return View();
        }

        // Muestra el resultado final del juego
        public IActionResult FinalizarJuego()
        {
            int idJuego = HttpContext.Session.GetInt32("id_juego") ?? 0;
            int puntajeFinal = 0;

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT puntaje_total FROM juego WHERE id_juego = @j", conn);
                cmd.Parameters.AddWithValue("@j", idJuego);
                puntajeFinal = Convert.ToInt32(cmd.ExecuteScalar());
            }

            ViewBag.Puntaje = puntajeFinal;
            return View("ResultadoFinal");
        }

        // Ranking global
        public IActionResult Ranking()
        {
            List<JuegoRanking> ranking = new List<JuegoRanking>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand(@"SELECT u.username, j.puntaje_total, j.fecha
                                            FROM juego j
                                            JOIN usuario u ON j.id_usuario = u.id_usuario
                                            ORDER BY j.puntaje_total DESC, j.fecha ASC", conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    ranking.Add(new JuegoRanking
                    {
                        Username = reader.GetString("username"),
                        Puntaje = reader.GetInt32("puntaje_total"),
                        Fecha = reader.GetDateTime("fecha")
                    });
                }
            }

            return View(ranking);
        }
    }
}
