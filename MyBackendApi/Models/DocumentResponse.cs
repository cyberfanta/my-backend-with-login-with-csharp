using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Swashbuckle.AspNetCore.Annotations;

namespace MyBackendApi.Models
{
    /// <summary>
    /// Modelo de respuesta para procesamiento de documentos
    /// </summary>
    [SwaggerSchema(Description = "Resultado del procesamiento de un documento")]
    public class DocumentResponse
    {
        /// <summary>
        /// Nombre original del archivo
        /// </summary>
        [SwaggerSchema(Description = "Nombre original del archivo procesado")]
        [Required]
        [DefaultValue("documento.pdf")]
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Texto extraído o procesado del documento
        /// </summary>
        [SwaggerSchema(Description = "Texto extraído o procesado del documento")]
        [Required]
        [DefaultValue("Este es el texto extraído del documento...")]
        public string Text { get; set; } = string.Empty;
        
        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        [SwaggerSchema(Description = "Tamaño del archivo en bytes")]
        [Required]
        [DefaultValue(1024)]
        public long FileSize { get; set; }
    }
} 