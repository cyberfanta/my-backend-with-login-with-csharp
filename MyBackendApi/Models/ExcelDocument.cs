using System;
using System.ComponentModel.DataAnnotations;

namespace MyBackendApi.Models
{
    /// <summary>
    /// Modelo para almacenar documentos Excel generados
    /// </summary>
    public class ExcelDocument
    {
        /// <summary>
        /// Identificador único del documento
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Nombre del archivo Excel
        /// </summary>
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo de contenido (MIME type)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        
        /// <summary>
        /// Contenido del archivo en bytes
        /// </summary>
        [Required]
        public byte[] Content { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// Identificadores de documentos PDF analizados (separados por comas)
        /// </summary>
        [StringLength(1000)]
        public string RelatedDocumentIds { get; set; } = string.Empty;
        
        /// <summary>
        /// Fecha y hora de creación
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Usuario que creó el documento
        /// </summary>
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        
        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long FileSize { get; set; }
    }
} 