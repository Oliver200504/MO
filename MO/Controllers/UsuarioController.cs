using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MO.Models;
using MySql.Data.MySqlClient;
using System.Dynamic;

namespace ProyectoMO2.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ConexionBD _conexion;

        public UsuarioController(ConexionBD conexion)
        {
            _conexion = conexion;
        }

        // GET: /Usuario/Login
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string contrasena)
        {
            
            if (username == "admin@MOO.com" && contrasena == "admin123")
            {
                HttpContext.Session.SetString("Rol", "Admin");
                HttpContext.Session.SetString("Username", "Administrador");

                return RedirectToAction("Index", "Admin");
            }

            
            using var conn = _conexion.GetConnection();
            conn.Open();

            string query = "SELECT * FROM usuario WHERE username=@user AND contrasena=@pass;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@pass", contrasena);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                HttpContext.Session.SetInt32("IdUsuario", reader.GetInt32("id_usuario"));
                HttpContext.Session.SetString("Username", reader.GetString("username"));
                HttpContext.Session.SetString("Rol", "Usuario");

                return RedirectToAction("MenuPrincipal", "Home");
            }

            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }


        public IActionResult Perfil()
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
                return RedirectToAction("Login");

            Usuario usuario = new Usuario();
            using (var conn = _conexion.GetConnection())
            {
                conn.Open();

                // Traer datos del usuario
                string queryUsuario = "SELECT * FROM usuario WHERE id_usuario=@id;";
                using (var cmd = new MySqlCommand(queryUsuario, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        usuario.Id_Usuario = reader.GetInt32("id_usuario");
                        usuario.Nombre = reader.GetString("nombre");
                        usuario.Username = reader.GetString("username");
                        usuario.Correo = reader.GetString("correo");
                        usuario.Contrasena = reader.GetString("contrasena");
                    }
                    reader.Close();
                }

                
                string queryUltima = @"
            SELECT 
                j.id_juego, 
                j.fecha, 
                j.puntaje_total,
                COALESCE(SUM(CASE WHEN d.es_correcta = 1 THEN 1 ELSE 0 END), 0) AS correctas,
                COALESCE(SUM(CASE WHEN d.es_correcta = 0 THEN 1 ELSE 0 END), 0) AS incorrectas
            FROM juego j
            LEFT JOIN detalleJuego d ON d.id_juego = j.id_juego
            WHERE j.id_usuario = @id
            GROUP BY j.id_juego, j.fecha, j.puntaje_total
            ORDER BY j.fecha DESC
            LIMIT 1;";

                using (var cmd = new MySqlCommand(queryUltima, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        dynamic ultimaPartida = new ExpandoObject();
                        ultimaPartida.IdJuego = reader.GetInt32("id_juego");
                        ultimaPartida.Fecha = reader.GetDateTime("fecha");
                        ultimaPartida.PuntajeTotal = reader.GetInt32("puntaje_total");
                        ultimaPartida.Correctas = reader.GetInt32("correctas");
                        ultimaPartida.Incorrectas = reader.GetInt32("incorrectas");

                        ViewBag.UltimaPartida = ultimaPartida;
                    }
                    reader.Close();
                }
            }

            return View(usuario);
        }


        // GET: Editar Perfil (muestra formulario con datos actuales)
        [HttpGet]
        public IActionResult Editar()
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
                return RedirectToAction("Login");

            Usuario usuario = new Usuario();
            using var conn = _conexion.GetConnection();
            conn.Open();
            string query = "SELECT * FROM usuario WHERE id_usuario=@id;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", idUsuario);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                usuario.Id_Usuario = reader.GetInt32("id_usuario");
                usuario.Nombre = reader.GetString("nombre");
                usuario.Username = reader.GetString("username");
                usuario.Correo = reader.GetString("correo");
                usuario.Contrasena = reader.GetString("contrasena");
            }

            return View(usuario);
        }

        // POST: Guardar cambios del perfil
        [HttpPost]
        public IActionResult Editar(Usuario usuario)
        {
            var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
            if (idUsuario == null)
                return RedirectToAction("Login");

            using var conn = _conexion.GetConnection();
            conn.Open();

            string query = @"UPDATE usuario 
                     SET nombre=@nom, username=@usr, correo=@cor, contrasena=@con 
                     WHERE id_usuario=@id;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@nom", usuario.Nombre);
            cmd.Parameters.AddWithValue("@usr", usuario.Username);
            cmd.Parameters.AddWithValue("@cor", usuario.Correo);
            cmd.Parameters.AddWithValue("@con", usuario.Contrasena);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.ExecuteNonQuery();

            // Limpiar sesión y redirigir al login
            HttpContext.Session.Clear();
            TempData["Mensaje"] = "Tu perfil fue actualizado correctamente. Inicia sesión nuevamente.";
            return RedirectToAction("Login");
        }

        // GET: /Usuario/Registro
        public IActionResult Registro()
        {
            return View();
        }

        // POST: /Usuario/Registro
        [HttpPost]
        public IActionResult Registro(Usuario usuario)
        {
            if (!ModelState.IsValid) return View(usuario);

            using var conn = _conexion.GetConnection();
            conn.Open();

            string query = "INSERT INTO usuario (nombre, username, correo, contrasena) VALUES (@nom, @usr, @cor, @con);";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@nom", usuario.Nombre);
            cmd.Parameters.AddWithValue("@usr", usuario.Username);
            cmd.Parameters.AddWithValue("@cor", usuario.Correo);
            cmd.Parameters.AddWithValue("@con", usuario.Contrasena);
            cmd.ExecuteNonQuery();

            ViewBag.Mensaje = "Usuario registrado correctamente.";
            return RedirectToAction("Login");
        }
        public IActionResult Logout()
        {
            // Elimina toda la información de la sesión
            HttpContext.Session.Clear();

            // Redirige al Login
            return RedirectToAction("Index", "Home");
        }
    }
}
