

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
            //  id del usuario de sesión
            int idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 0;
            if (idUsuario == 0)
                return RedirectToAction("Login"); // si no hay usuario mandar a login

            int idJuego;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                //  nuevo registro y obtener su id
                var cmd = new MySqlCommand(
                    "INSERT INTO juego (fecha, id_usuario, puntaje_total) VALUES (@fecha, @idUsuario, 0); SELECT LAST_INSERT_ID();",
                    conexion);
                cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);

                idJuego = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // reiniciar variables para la partida
            HttpContext.Session.SetInt32("idJuego", idJuego);
            HttpContext.Session.SetInt32("puntajeActual", 0); 
            HttpContext.Session.SetInt32("tiempoRestante", 60); // tiempo inicial
            HttpContext.Session.SetInt32("RespuestasCorrectas", 0);
            HttpContext.Session.SetInt32("RespuestasIncorrectas", 0);
            HttpContext.Session.SetInt32("IndicePregunta", 0); // indice de la primera pregunta

            // limpiar preguntas anteriores
            HttpContext.Session.Remove("Preguntas"); 
            HttpContext.Session.Remove("CategoriaSeleccionada");

            // vista de tarjetas para elegir categoría
            return RedirectToAction("Tarjetas");
        }



        public IActionResult Tarjetas()
        {
            // lista a guardar las categorias
            List<Categoria> categorias = new List<Categoria>();

            using (MySqlConnection conexion = _conexion.GetConnection())
            {
                conexion.Open();

                //  las categorias de la bd
                MySqlCommand comando = new MySqlCommand("SELECT * FROM categoria", conexion);
                MySqlDataReader lector = comando.ExecuteReader();

                while (lector.Read())
                {
                    // crear objeto categoria con lo que trae la bd
                    Categoria cat = new Categoria
                    {
                        id_categoria = lector.GetInt32("id_categoria"),
                        nombre_categoria = lector.GetString("nombre_categoria")
                    };
                    categorias.Add(cat); // agregar a la lista
                }

                lector.Close();
                conexion.Close();
            }

            // mezclar las categorias para que salgan en orden aleatorio
            var random = new Random();
            categorias = categorias.OrderBy(x => random.Next()).ToList();

            // enviar la lista a la vista
            return View(categorias);
        }

        public IActionResult Ronda()
        {
            // tiempo restante de la sesión
            int tiempoRestante = HttpContext.Session.GetInt32("tiempoRestante") ?? 60;

            // si se acabó el tiempo, terminar juego
            if (tiempoRestante <= 0)
                return RedirectToAction("FinalizarJuego");

            // tomar categoría seleccionada de la sesión
            int idCategoria = HttpContext.Session.GetInt32("CategoriaSeleccionada") ?? 0;
            if (idCategoria == 0)
                return RedirectToAction("Tarjetas"); // volver a tarjetas si no hay categoría

            // cargar preguntas de la categoría si no existen en sesión
            List<Pregunta> preguntas = null;
            string preguntasJson = HttpContext.Session.GetString("Preguntas");
            if (!string.IsNullOrEmpty(preguntasJson))
            {
                // si ya están en sesión, deserializar
                preguntas = System.Text.Json.JsonSerializer.Deserialize<List<Pregunta>>(preguntasJson);
            }
            else
            {
                preguntas = new List<Pregunta>();
                using (var conexion = _conexion.GetConnection())
                {
                    conexion.Open();

                    // traer preguntas aleatorias de la categoría
                    var comando = new MySqlCommand("SELECT * FROM pregunta WHERE id_categoria=@idCategoria ORDER BY RAND()", conexion);
                    comando.Parameters.AddWithValue("@idCategoria", idCategoria);
                    var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        // agregar cada pregunta a la lista
                        preguntas.Add(new Pregunta
                        {
                            id_pregunta = lector.GetInt32("id_pregunta"),
                            texto_pregunta = lector.GetString("texto_pregunta"),
                            id_categoria = lector.GetInt32("id_categoria")
                        });
                    }
                    lector.Close();
                }

                // guardar preguntas en sesión y no volver a consultar
                HttpContext.Session.SetString("Preguntas", System.Text.Json.JsonSerializer.Serialize(preguntas));
                HttpContext.Session.SetInt32("IndicePregunta", 0);

                // inicializar contadores al inicio de la ronda
                HttpContext.Session.SetInt32("RespuestasCorrectas", 0);
                HttpContext.Session.SetInt32("RespuestasIncorrectas", 0);
            }

            // índice de la pregunta actual
            int indice = HttpContext.Session.GetInt32("IndicePregunta") ?? 0;
            if (indice >= preguntas.Count)
                return RedirectToAction("FinalizarJuego"); // si se acaban las preguntas terminar juego

            Pregunta preguntaActual = preguntas[indice]; // pregunta actual

            // lista para las respuestas
            List<Respuesta> listaRespuestas = new List<Respuesta>();
            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open(); 

                // traer todas las respuestas de la pregunta actual
                var comando = new MySqlCommand("SELECT * FROM respuesta WHERE id_pregunta=@idPregunta", conexion);
                comando.Parameters.AddWithValue("@idPregunta", preguntaActual.id_pregunta);
                var lector = comando.ExecuteReader(); 
                while (lector.Read())
                {
                    // agregar cada respuesta a la lista
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

            // mezclar respuestas aleatoriamente
            Random rnd = new Random();
            listaRespuestas = listaRespuestas.OrderBy(x => rnd.Next()).ToList();

            // contadores acumulativos
            int correctas = HttpContext.Session.GetInt32("RespuestasCorrectas") ?? 0;
            int incorrectas = HttpContext.Session.GetInt32("RespuestasIncorrectas") ?? 0;

            // enviar datos a la vista
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
            // datos de la sesión
            int idJuego = HttpContext.Session.GetInt32("idJuego") ?? 0;
            int puntajeActual = HttpContext.Session.GetInt32("puntajeActual") ?? 0;
            int tiempoRestante = HttpContext.Session.GetInt32("tiempoRestante") ?? 60;
            int correctas = HttpContext.Session.GetInt32("RespuestasCorrectas") ?? 0;
            int incorrectas = HttpContext.Session.GetInt32("RespuestasIncorrectas") ?? 0;

            bool correcta = false;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                if (idRespuesta > 0)
                {
                    // si la respuesta es correcta 
                    var comando = new MySqlCommand(
                        "SELECT es_correcta FROM respuesta WHERE id_respuesta=@idRespuesta", conexion);
                    comando.Parameters.AddWithValue("@idRespuesta", idRespuesta);
                    var resultado = comando.ExecuteScalar();

                    if (resultado != null)
                        correcta = Convert.ToBoolean(resultado);

                    // detalle de la respuesta se guarda en bs
                    var insertarDetalle = new MySqlCommand(
                        "INSERT INTO detalleJuego (id_juego, id_pregunta, id_respuesta, es_correcta) " +
                        "VALUES(@idJuego, @idPregunta, @idRespuesta, @correcta)", conexion);
                    insertarDetalle.Parameters.AddWithValue("@idJuego", idJuego);
                    insertarDetalle.Parameters.AddWithValue("@idPregunta", idPregunta);
                    insertarDetalle.Parameters.AddWithValue("@idRespuesta", idRespuesta);
                    insertarDetalle.Parameters.AddWithValue("@correcta", correcta);
                    insertarDetalle.ExecuteNonQuery();

                    //  puntaje 10 puntos por correcta
                    if (correcta)
                    {
                        puntajeActual += 10;
                        correctas++;
                    }
                    else
                    {
                        incorrectas++;
                    }

                    // actualiza puntaje en la tabla juego
                    var actualizarJuego = new MySqlCommand(
                        "UPDATE juego SET puntaje_total=@puntaje WHERE id_juego=@idJuego", conexion);
                    actualizarJuego.Parameters.AddWithValue("@puntaje", puntajeActual);
                    actualizarJuego.Parameters.AddWithValue("@idJuego", idJuego);
                    actualizarJuego.ExecuteNonQuery();
                }
                else
                {
                    // pregunta omitida: id_respuesta = NULL, es_correcta = flalse
                    var insertarDetalle = new MySqlCommand(
                        "INSERT INTO detalleJuego (id_juego, id_pregunta, id_respuesta, es_correcta) " +
                        "VALUES(@idJuego, @idPregunta, @idRespuesta, @correcta)", conexion);

                    insertarDetalle.Parameters.AddWithValue("@idJuego", idJuego);
                    insertarDetalle.Parameters.AddWithValue("@idPregunta", idPregunta);
                    insertarDetalle.Parameters.AddWithValue("@idRespuesta", DBNull.Value); 
                    insertarDetalle.Parameters.AddWithValue("@correcta", false);          
                    insertarDetalle.ExecuteNonQuery();

                   
                    tiempoRestante -= 15;
                }


                conexion.Close();
            }

           
            if (correcta)
                tiempoRestante += 5;  // bonificación por correcta
            else if (idRespuesta > 0)
                tiempoRestante -= 10; // penalización por incorrecta

            if (tiempoRestante > 120) tiempoRestante = 120;
            if (tiempoRestante < 0) tiempoRestante = 0;

           
            HttpContext.Session.SetInt32("puntajeActual", puntajeActual);
            HttpContext.Session.SetInt32("tiempoRestante", tiempoRestante);
            HttpContext.Session.SetInt32("RespuestasCorrectas", correctas);
            HttpContext.Session.SetInt32("RespuestasIncorrectas", incorrectas);

          
            int indice = HttpContext.Session.GetInt32("IndicePregunta") ?? 0;
            indice++;
            HttpContext.Session.SetInt32("IndicePregunta", indice);

            
            int totalPreguntas = System.Text.Json.JsonSerializer
                .Deserialize<List<Pregunta>>(HttpContext.Session.GetString("Preguntas"))?.Count ?? 0;

            if (tiempoRestante <= 0 || indice >= totalPreguntas)
                return RedirectToAction("FinalizarJuego");

            return RedirectToAction("Ronda");
        }



        public IActionResult FinalizarJuego()
        {
            // id del juego de la sesión
            int idJuego = HttpContext.Session.GetInt32("idJuego") ?? 0;

            int correctas = 0;
            int incorrectas = 0;
            int puntajeTotal = 0;

            using (var conexion = _conexion.GetConnection())
            {
                conexion.Open();

                // traer datos de la partida y puntaje total
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
                    // guardar valores de correctas, incorrectas y puntaje
                    correctas = reader["correctas"] != DBNull.Value ? Convert.ToInt32(reader["correctas"]) : 0;
                    incorrectas = reader["incorrectas"] != DBNull.Value ? Convert.ToInt32(reader["incorrectas"]) : 0;
                    puntajeTotal = reader["puntaje_total"] != DBNull.Value ? Convert.ToInt32(reader["puntaje_total"]) : 0;
                }
            }

            // enviar a la vista para mostrar resultado
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
