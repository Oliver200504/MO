using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MO.Models;
using MySql.Data.MySqlClient;

namespace MO.Controllers
{
   
        public class AdminController : Controller
        {
            private readonly ConexionBD _conexion;

            public AdminController(ConexionBD conexion)
            {
                _conexion = conexion;
            }

            private bool EsAdmin()
            {
                return HttpContext.Session.GetString("Rol") == "Admin";
            }

            public IActionResult Index()
            {
                if (!EsAdmin()) return RedirectToAction("Login", "Usuario");
                return View();
            }

            // Listar preguntas
            public IActionResult Preguntas()
            {
                if (!EsAdmin()) return RedirectToAction("Login", "Usuario");

                var lista = new List<Pregunta>();

                using var conn = _conexion.GetConnection();
                conn.Open();

                string query = "SELECT id_pregunta, texto_pregunta, id_categoria FROM pregunta ORDER BY id_pregunta DESC;";
                using var cmd = new MySqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    lista.Add(new Pregunta
                    {
                        id_pregunta = reader.GetInt32("id_pregunta"),
                        texto_pregunta = reader.IsDBNull(reader.GetOrdinal("texto_pregunta")) ? "" : reader.GetString("texto_pregunta"),
                        id_categoria = reader.GetInt32("id_categoria")
                    });
                }

                return View(lista);
            }

            // GET - Crear pregunta (carga categorías)
            [HttpGet]
            public IActionResult CrearPregunta()
            {
                if (!EsAdmin()) return RedirectToAction("Login", "Usuario");

                // Cargar categorías desde BD y pasarlas por ViewBag
                var categorias = new List<(int Id, string Nombre)>();
                using var conn = _conexion.GetConnection();
                conn.Open();

                string qCat = "SELECT id_categoria, nombre_categoria FROM categoria ORDER BY id_categoria;";
                using var cmd = new MySqlCommand(qCat, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    categorias.Add((reader.GetInt32("id_categoria"), reader.GetString("nombre_categoria")));
                }

                ViewBag.Categorias = categorias;
                return View();
            }

            
            [HttpPost]
            public IActionResult CrearPregunta(string textoPregunta, int id_categoria, List<string> respuestas, int correcta)
            {
                if (!EsAdmin()) return RedirectToAction("Login", "Usuario");

                using var conn = _conexion.GetConnection();
                conn.Open();

                // Insertar pregunta (usar nombres reales de columnas)
                string qPregunta = "INSERT INTO pregunta (texto_pregunta, id_categoria) VALUES (@texto, @cat);";
                using var cmdP = new MySqlCommand(qPregunta, conn);
                cmdP.Parameters.AddWithValue("@texto", textoPregunta);
                cmdP.Parameters.AddWithValue("@cat", id_categoria);
                cmdP.ExecuteNonQuery();

                
                long idPregunta = cmdP.LastInsertedId;
                if (idPregunta == 0)
                {
                    
                    using var cmdLast = new MySqlCommand("SELECT LAST_INSERT_ID();", conn);
                    idPregunta = Convert.ToInt64(cmdLast.ExecuteScalar());
                }

                
                for (int i = 0; i < respuestas.Count; i++)
                {
                    string qResp = @"INSERT INTO respuesta (texto_respuesta, es_correcta, id_pregunta)
                                 VALUES (@txt, @es, @preg);";
                    using var cmdR = new MySqlCommand(qResp, conn);
                    cmdR.Parameters.AddWithValue("@txt", respuestas[i]);
                    cmdR.Parameters.AddWithValue("@es", (i == correcta) ? 1 : 0);
                    cmdR.Parameters.AddWithValue("@preg", idPregunta);
                    cmdR.ExecuteNonQuery();
                }

                TempData["OK"] = "Pregunta registrada correctamente ✔";
                return RedirectToAction("Preguntas");
            }


        [HttpPost]
        public IActionResult EliminarPregunta(int id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin")
                return RedirectToAction("Login", "Usuario");

            using var conn = _conexion.GetConnection();
            conn.Open();

            
            string qCheck = @"
        SELECT COUNT(*) 
        FROM detallejuego dj
        INNER JOIN respuesta r ON dj.id_respuesta = r.id_respuesta
        WHERE r.id_pregunta = @id;
    ";

            long usos = 0;

            using (var cmd = new MySqlCommand(qCheck, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                usos = (long)cmd.ExecuteScalar();
            }

            
            if (usos > 0)
            {
                TempData["Error"] = "No se puede eliminar esta pregunta porque ya fue utilizada en partidas del juego.";
                return RedirectToAction("Preguntas");
            }

            
            string qDelRespuestas = "DELETE FROM respuesta WHERE id_pregunta=@id;";
            using (var cmdDelR = new MySqlCommand(qDelRespuestas, conn))
            {
                cmdDelR.Parameters.AddWithValue("@id", id);
                cmdDelR.ExecuteNonQuery();
            }

            
            string qDelPregunta = "DELETE FROM pregunta WHERE id_pregunta=@id;";
            using (var cmdDelP = new MySqlCommand(qDelPregunta, conn))
            {
                cmdDelP.Parameters.AddWithValue("@id", id);
                cmdDelP.ExecuteNonQuery();
            }

            TempData["OK"] = "Pregunta eliminada correctamente ✔";
            return RedirectToAction("Preguntas");
        }


        [HttpGet]
        public IActionResult EditarPregunta(int id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin")
                return RedirectToAction("Login", "Usuario");

            Pregunta pregunta = null;
            List<string> respuestas = new();
            int correcta = -1;

            using var conn = _conexion.GetConnection();
            conn.Open();

            // Obtener la pregunta
            string q = "SELECT * FROM pregunta WHERE id_pregunta=@id;";
            using (var cmd = new MySqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    pregunta = new Pregunta
                    {
                        id_pregunta = reader.GetInt32("id_pregunta"),
                        texto_pregunta = reader.GetString("texto_pregunta"),
                        id_categoria = reader.GetInt32("id_categoria")
                    };
                }
                reader.Close();
            }

            // Obtener respuestas de la pregunta
            string qResp = "SELECT texto_respuesta, es_correcta FROM respuesta WHERE id_pregunta=@id;";
            using (var cmd = new MySqlCommand(qResp, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    respuestas.Add(reader.GetString("texto_respuesta"));
                    if (reader.GetInt32("es_correcta") == 1)
                        correcta = respuestas.Count - 1;
                }
            }

            // Cargar categorías
            List<(int, string)> categorias = new();
            using (var cmd = new MySqlCommand("SELECT id_categoria, nombre_categoria FROM categoria;", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    categorias.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            ViewBag.Pregunta = pregunta;
            ViewBag.Respuestas = respuestas;
            ViewBag.Correcta = correcta;
            ViewBag.Categorias = categorias;

            return View();
        }

        [HttpPost]
        public IActionResult EditarPregunta(int id_pregunta, string textoPregunta, int id_categoria, List<string> respuestas, int correcta)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin")
                return RedirectToAction("Login", "Usuario");

            using var conn = _conexion.GetConnection();
            conn.Open();

            // -----------------------------------------------------------------
            // 1. Actualizar la pregunta
            // -----------------------------------------------------------------
            string qUpdatePregunta = @"UPDATE pregunta 
                               SET texto_pregunta=@t, id_categoria=@c 
                               WHERE id_pregunta=@id;";

            using (var cmd = new MySqlCommand(qUpdatePregunta, conn))
            {
                cmd.Parameters.AddWithValue("@t", textoPregunta);
                cmd.Parameters.AddWithValue("@c", id_categoria);
                cmd.Parameters.AddWithValue("@id", id_pregunta);
                cmd.ExecuteNonQuery();
            }

            // -----------------------------------------------------------------
            // 2. Obtener actuales respuestas
            // -----------------------------------------------------------------
            var respuestasBD = new List<(int id_respuesta, string texto, bool correcta)>();

            string qGetRespuestas = @"SELECT id_respuesta, texto_respuesta, es_correcta 
                              FROM respuesta 
                              WHERE id_pregunta=@id;";

            using var cmdSelect = new MySqlCommand(qGetRespuestas, conn);
            cmdSelect.Parameters.AddWithValue("@id", id_pregunta);

            using (var reader = cmdSelect.ExecuteReader())
            {
                while (reader.Read())
                {
                    respuestasBD.Add((
                        reader.GetInt32("id_respuesta"),
                        reader.GetString("texto_respuesta"),
                        reader.GetInt32("es_correcta") == 1
                    ));
                }
            }

            // -----------------------------------------------------------------
            // 3. Actualizar respuestas existentes o agregar nuevas
            // -----------------------------------------------------------------

            // Caso: siempre deben ser 4 respuestas
            for (int i = 0; i < respuestas.Count; i++)
            {
                if (i < respuestasBD.Count)
                {
                    // EXISTE EN BD → UPDATE
                    string qUpdateResp = @"UPDATE respuesta 
                                   SET texto_respuesta=@t, es_correcta=@c
                                   WHERE id_respuesta=@idResp;";

                    using var cmdUpdate = new MySqlCommand(qUpdateResp, conn);
                    cmdUpdate.Parameters.AddWithValue("@t", respuestas[i]);
                    cmdUpdate.Parameters.AddWithValue("@c", i == correcta ? 1 : 0);
                    cmdUpdate.Parameters.AddWithValue("@idResp", respuestasBD[i].id_respuesta);
                    cmdUpdate.ExecuteNonQuery();
                }
                else
                {
                    // NO EXISTE → INSERT
                    string qInsert = @"INSERT INTO respuesta(texto_respuesta, es_correcta, id_pregunta)
                               VALUES (@t, @c, @idP);";

                    using var cmdInsert = new MySqlCommand(qInsert, conn);
                    cmdInsert.Parameters.AddWithValue("@t", respuestas[i]);
                    cmdInsert.Parameters.AddWithValue("@c", i == correcta ? 1 : 0);
                    cmdInsert.Parameters.AddWithValue("@idP", id_pregunta);
                    cmdInsert.ExecuteNonQuery();
                }
            }

            // -----------------------------------------------------------------
            // 4. BORRAR solo respuestas sobrantes sin referencias
            // -----------------------------------------------------------------
            if (respuestasBD.Count > respuestas.Count)
            {
                for (int i = respuestas.Count; i < respuestasBD.Count; i++)
                {
                    int id_resp = respuestasBD[i].id_respuesta;

                    // Verificar si está en detalleJuego
                    string qCheck = "SELECT COUNT(*) FROM detallejuego WHERE id_respuesta=@id;";
                    using var cmdCheck = new MySqlCommand(qCheck, conn);
                    cmdCheck.Parameters.AddWithValue("@id", id_resp);
                    long usos = (long)cmdCheck.ExecuteScalar();

                    if (usos == 0)
                    {
                        // BORRAR SOLO SI NO ESTÁ REFERENCIADA
                        using var cmdDel = new MySqlCommand("DELETE FROM respuesta WHERE id_respuesta=@id;", conn);
                        cmdDel.Parameters.AddWithValue("@id", id_resp);
                        cmdDel.ExecuteNonQuery();
                    }
                }
            }

            TempData["OK"] = "Pregunta editada correctamente ✔";
            return RedirectToAction("Preguntas");
        }


    }
}
