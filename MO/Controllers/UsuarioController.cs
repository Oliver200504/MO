using Microsoft.AspNetCore.Mvc;
using MO.Data;
using MO.Models;
using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Http;

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
            using var conn = _conexion.GetConnection();
            conn.Open();

            string query = "SELECT * FROM usuario WHERE username=@user AND contrasena=@pass;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@pass", contrasena);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                // Guardar en sesión
                HttpContext.Session.SetInt32("IdUsuario", reader.GetInt32("id_usuario"));
                HttpContext.Session.SetString("Username", reader.GetString("username"));

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
