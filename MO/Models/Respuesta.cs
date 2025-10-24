namespace MO.Models
{
    public class Respuesta
    {
        public int id_respuesta { get; set; }
        public string texto_respuesta { get; set; }
        public bool es_correcta { get; set; }
        public int id_pregunta { get; set; }

    }
}
