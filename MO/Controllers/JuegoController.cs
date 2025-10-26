

using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MySql.Data.MySqlClient;
using MO.Models;
using System;
using System.Collections.Generic;

namespace MO.Controllers
{
    public class JuegoController : Controller
    {
        private readonly ConexionBD _conexion;

        public JuegoController(ConexionBD conexion)
        {
            _conexion = conexion;
        }

        public IActionResult Iniciar()
        {
            int idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            if (idUsuario == 0)
                return RedirectToAction("Login");

            int idJuego;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                // Crear registro en juego
                var cmd = new MySqlCommand(
                    "INSERT INTO juego (fecha, id_usuario, puntaje_total) VALUES (@fecha, @idUsuario, 0); SELECT LAST_INSERT_ID();",
                    conexion);
                cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);

                // Obtener el id del juego recién creado
                idJuego = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Guardar en sesión
            HttpContext.Session.SetInt32("idJuego", idJuego);
            HttpContext.Session.SetInt32("puntajeActual", 0);
            HttpContext.Session.SetInt32("tiempoRestante", 60);
            HttpContext.Session.SetInt32("RespuestasCorrectas", 0);
            HttpContext.Session.SetInt32("RespuestasIncorrectas", 0);
            HttpContext.Session.SetInt32("IndicePregunta", 0);

            return RedirectToAction("Ronda");
        }




        public IActionResult Ruleta()
        {
            List<Categoria> categorias = new List<Categoria>();
            using (MySqlConnection conexion = _conexion.GetConnection())
            {
                conexion.Open();
                MySqlCommand comando = new MySqlCommand("SELECT * FROM categoria", conexion);
                MySqlDataReader lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    Categoria cat = new Categoria();
                    cat.id_categoria = lector.GetInt32("id_categoria");
                    cat.nombre_categoria = lector.GetString("nombre_categoria");
                    categorias.Add(cat);
                }
                lector.Close();
                conexion.Close();
            }

            return View(categorias);
        }

        public IActionResult Ronda()
        {
            // Recuperar tiempo restante
            int tiempoRestante = HttpContext.Session.GetInt32("tiempoRestante") ?? 60;

            if (tiempoRestante <= 0)
                return RedirectToAction("FinalizarJuego");

            int idCategoria = HttpContext.Session.GetInt32("CategoriaSeleccionada") ?? 0;
            if (idCategoria == 0)
                return RedirectToAction("Ruleta");

            // Cargar preguntas de la categoría si no existen
            List<Pregunta> preguntas = null;
            string preguntasJson = HttpContext.Session.GetString("Preguntas");
            if (!string.IsNullOrEmpty(preguntasJson))
                preguntas = System.Text.Json.JsonSerializer.Deserialize<List<Pregunta>>(preguntasJson);
            else
            {
                preguntas = new List<Pregunta>();
                using (var conexion = _conexion.GetConnection())
                {
                    conexion.Open();
                    var comando = new MySqlCommand("SELECT * FROM pregunta WHERE id_categoria=@idCategoria ORDER BY RAND()", conexion);
                    comando.Parameters.AddWithValue("@idCategoria", idCategoria);
                    var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        preguntas.Add(new Pregunta
                        {
                            id_pregunta = lector.GetInt32("id_pregunta"),
                            texto_pregunta = lector.GetString("texto_pregunta"),
                            id_categoria = lector.GetInt32("id_categoria")
                        });
                    }
                    lector.Close();
                }

                HttpContext.Session.SetString("Preguntas", System.Text.Json.JsonSerializer.Serialize(preguntas));
                HttpContext.Session.SetInt32("IndicePregunta", 0);

                // Inicializar contadores solo al inicio de la ronda
                HttpContext.Session.SetInt32("RespuestasCorrectas", 0);
                HttpContext.Session.SetInt32("RespuestasIncorrectas", 0);
            }

            int indice = HttpContext.Session.GetInt32("IndicePregunta") ?? 0;
            if (indice >= preguntas.Count)
                return RedirectToAction("FinalizarJuego");

            Pregunta preguntaActual = preguntas[indice];

            // Traer respuestas
            List<Respuesta> listaRespuestas = new List<Respuesta>();
            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();
                var comando = new MySqlCommand("SELECT * FROM respuesta WHERE id_pregunta=@idPregunta", conexion);
                comando.Parameters.AddWithValue("@idPregunta", preguntaActual.id_pregunta);
                var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    listaRespuestas.Add(new Respuesta
                    {
                        id_respuesta = lector.GetInt32("id_respuesta"),
                        texto_respuesta = lector.GetString("texto_respuesta"),
                        es_correcta = lector.GetBoolean("es_correcta"),
                        id_pregunta = lector.GetInt32("id_pregunta")
                    });
                }
                lector.Close();
            }

            // Barajar las respuestas aleatoriamente
            Random rnd = new Random();
            listaRespuestas = listaRespuestas.OrderBy(x => rnd.Next()).ToList();


            // Contadores acumulativos
            int correctas = HttpContext.Session.GetInt32("RespuestasCorrectas") ?? 0;
            int incorrectas = HttpContext.Session.GetInt32("RespuestasIncorrectas") ?? 0;

            ViewBag.Pregunta = preguntaActual;
            ViewBag.Respuestas = listaRespuestas;
            ViewBag.Tiempo = tiempoRestante;
            ViewBag.Correctas = correctas;
            ViewBag.Incorrectas = incorrectas;

            return View();
        }

        [HttpPost]
        public IActionResult Responder(int idPregunta, int idRespuesta, int tiempoTomado)
        {
            int idJuego = HttpContext.Session.GetInt32("idJuego") ?? 0;
            int puntajeActual = HttpContext.Session.GetInt32("puntajeActual") ?? 0;
            int tiempoRestante = HttpContext.Session.GetInt32("tiempoRestante") ?? 60;
            int correctas = HttpContext.Session.GetInt32("RespuestasCorrectas") ?? 0;
            int incorrectas = HttpContext.Session.GetInt32("RespuestasIncorrectas") ?? 0;

            bool correcta = false;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                // Revisar si la respuesta es correcta
                var comando = new MySqlCommand("SELECT es_correcta FROM respuesta WHERE id_respuesta=@idRespuesta", conexion);
                comando.Parameters.AddWithValue("@idRespuesta", idRespuesta);
                correcta = Convert.ToBoolean(comando.ExecuteScalar());

                // Guardar detalle de la respuesta
                var insertarDetalle = new MySqlCommand(
                    "INSERT INTO detalleJuego (id_juego, id_pregunta, id_respuesta, es_correcta) VALUES(@idJuego, @idPregunta, @idRespuesta, @correcta)",
                    conexion);
                insertarDetalle.Parameters.AddWithValue("@idJuego", idJuego);
                insertarDetalle.Parameters.AddWithValue("@idPregunta", idPregunta);
                insertarDetalle.Parameters.AddWithValue("@idRespuesta", idRespuesta);
                insertarDetalle.Parameters.AddWithValue("@correcta", correcta);
                insertarDetalle.ExecuteNonQuery();

                // Actualizar puntaje total: cada correcta vale 1
                if (correcta) puntajeActual = correctas + 10; // correctas aún no se incrementan
                var actualizarJuego = new MySqlCommand("UPDATE juego SET puntaje_total=@puntaje WHERE id_juego=@idJuego", conexion);
                actualizarJuego.Parameters.AddWithValue("@puntaje", puntajeActual);
                actualizarJuego.Parameters.AddWithValue("@idJuego", idJuego);
                actualizarJuego.ExecuteNonQuery();
            }

            // Ajustes de tiempo y contadores
            if (correcta)
            {
                tiempoRestante += 5;
                correctas++;
            }
            else
            {
                tiempoRestante -= 10;
                incorrectas++;
            }

            if (tiempoRestante > 120) tiempoRestante = 120;
            if (tiempoRestante < 0) tiempoRestante = 0;

            // Guardar en sesión
            HttpContext.Session.SetInt32("puntajeActual", correctas); // puntaje = respuestas correctas
            HttpContext.Session.SetInt32("tiempoRestante", tiempoRestante);
            HttpContext.Session.SetInt32("RespuestasCorrectas", correctas);
            HttpContext.Session.SetInt32("RespuestasIncorrectas", incorrectas);

            // Avanzar índice
            int indice = HttpContext.Session.GetInt32("IndicePregunta") ?? 0;
            indice++;
            HttpContext.Session.SetInt32("IndicePregunta", indice);

            // Revisar fin de juego
            int totalPreguntas = System.Text.Json.JsonSerializer.Deserialize<List<Pregunta>>(HttpContext.Session.GetString("Preguntas"))?.Count ?? 0;
            if (tiempoRestante <= 0 || indice >= totalPreguntas)
                return RedirectToAction("FinalizarJuego");

            return RedirectToAction("Ronda");
        }


        public IActionResult FinalizarJuego()
        {
            int idJuego = HttpContext.Session.GetInt32("idJuego") ?? 0;

            int correctas = 0;
            int incorrectas = 0;
            int puntajeTotal = 0;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                // Obtener estadísticas y puntaje total de la partida
                var cmd = new MySqlCommand(
                    @"SELECT 
                SUM(CASE WHEN es_correcta=1 THEN 1 ELSE 0 END) as correctas,
                SUM(CASE WHEN es_correcta=0 THEN 1 ELSE 0 END) as incorrectas,
                puntaje_total
              FROM juego j
              LEFT JOIN detalleJuego d ON j.id_juego=d.id_juego
              WHERE j.id_juego=@idJuego",
                    conexion);
                cmd.Parameters.AddWithValue("@idJuego", idJuego);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    correctas = reader["correctas"] != DBNull.Value ? Convert.ToInt32(reader["correctas"]) : 0;
                    incorrectas = reader["incorrectas"] != DBNull.Value ? Convert.ToInt32(reader["incorrectas"]) : 0;
                    puntajeTotal = reader["puntaje_total"] != DBNull.Value ? Convert.ToInt32(reader["puntaje_total"]) : 0;
                }
            }

            ViewBag.Correctas = correctas;
            ViewBag.Incorrectas = incorrectas;
            ViewBag.Puntaje = puntajeTotal;

            return View();
        }



        public IActionResult SeleccionarCategoria(int idCategoria)
        {
            HttpContext.Session.SetInt32("CategoriaSeleccionada", idCategoria);

            // Reiniciar contadores al empezar una nueva ronda
            HttpContext.Session.SetInt32("puntajeActual", 0);
            HttpContext.Session.SetInt32("tiempoRestante", 60);
            HttpContext.Session.SetInt32("RespuestasCorrectas", 0);
            HttpContext.Session.SetInt32("RespuestasIncorrectas", 0);

            HttpContext.Session.Remove("Preguntas");
            HttpContext.Session.Remove("IndicePregunta");

            return RedirectToAction("Ronda");
        }


    }
}
