using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MyBackendApi.Models
{
    /// <summary>
    /// Modelo para la respuesta de análisis de documento avanzado con IA
    /// </summary>
    [SwaggerSchema(Description = "Resultado del análisis avanzado del documento PDF usando inteligencia artificial")]
    public class DocumentAnalysisResponse
    {
        /// <summary>
        /// Nombre original del archivo
        /// </summary>
        [SwaggerSchema(Description = "Nombre original del archivo procesado")]
        [Required]
        [DefaultValue("documento.pdf")]
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        [SwaggerSchema(Description = "Tamaño del archivo en bytes")]
        [Required]
        [DefaultValue(1024)]
        public long FileSize { get; set; }
        
        /// <summary>
        /// Título del documento identificado por IA
        /// </summary>
        [SwaggerSchema(Description = "Título del documento identificado mediante análisis de IA")]
        [DefaultValue("Título del documento")]
        public string DocumentTitle { get; set; } = string.Empty;
        
        /// <summary>
        /// Resumen ejecutivo del documento
        /// </summary>
        [SwaggerSchema(Description = "Resumen generado por IA del contenido del documento")]
        [Required]
        [DefaultValue("Este documento trata sobre...")]
        public string Summary { get; set; } = string.Empty;
        
        /// <summary>
        /// Ideas principales extraídas del documento
        /// </summary>
        [SwaggerSchema(Description = "Lista de ideas o puntos clave identificados en el documento mediante IA")]
        [Required]
        public List<string> KeyPoints { get; set; } = new List<string>();
        
        /// <summary>
        /// Tablas encontradas en el documento
        /// </summary>
        [SwaggerSchema(Description = "Tablas extraídas del documento, representadas como texto")]
        public List<TableData> Tables { get; set; } = new List<TableData>();
        
        /// <summary>
        /// Información sobre imágenes detectadas
        /// </summary>
        [SwaggerSchema(Description = "Información sobre imágenes encontradas en el documento")]
        public List<ImageInfo> Images { get; set; } = new List<ImageInfo>();
        
        /// <summary>
        /// Conclusiones generadas a partir del análisis
        /// </summary>
        [SwaggerSchema(Description = "Lista de conclusiones generadas a partir del análisis del documento")]
        public List<string> Conclusions { get; set; } = new List<string>();
        
        /// <summary>
        /// Metadatos adicionales del documento
        /// </summary>
        [SwaggerSchema(Description = "Metadatos extraídos del documento")]
        public DocumentMetadata Metadata { get; set; } = new DocumentMetadata();
    }
    
    /// <summary>
    /// Estructura de datos para representar una tabla extraída
    /// </summary>
    [SwaggerSchema(Description = "Estructura de datos para representar una tabla extraída")]
    public class TableData
    {
        /// <summary>
        /// Título de la tabla (si se pudo detectar)
        /// </summary>
        [SwaggerSchema(Description = "Título detectado de la tabla")]
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Datos de la tabla como una matriz de texto
        /// </summary>
        [SwaggerSchema(Description = "Datos de la tabla como matriz")]
        public List<List<string>> Data { get; set; } = new List<List<string>>();
        
        /// <summary>
        /// Representación ASCII/string de la tabla
        /// </summary>
        [SwaggerSchema(Description = "Representación de la tabla en formato de caracteres/ASCII")]
        public string TextRepresentation { get; set; } = string.Empty;
        
        /// <summary>
        /// Descripción generada de la tabla
        /// </summary>
        [SwaggerSchema(Description = "Descripción del contenido de la tabla")]
        public string Description { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Información sobre una imagen detectada
    /// </summary>
    [SwaggerSchema(Description = "Información sobre una imagen detectada en el documento")]
    public class ImageInfo
    {
        /// <summary>
        /// Posición aproximada de la imagen (número de página)
        /// </summary>
        [SwaggerSchema(Description = "Número de página donde se encontró la imagen")]
        [DefaultValue(1)]
        public int PageNumber { get; set; }
        
        /// <summary>
        /// Descripción generada para la imagen
        /// </summary>
        [SwaggerSchema(Description = "Descripción del contenido de la imagen")]
        [DefaultValue("La imagen muestra...")]
        public string Description { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Metadatos del documento
    /// </summary>
    [SwaggerSchema(Description = "Metadatos extraídos del documento PDF")]
    public class DocumentMetadata
    {
        /// <summary>
        /// Título del documento extraído de los metadatos
        /// </summary>
        [SwaggerSchema(Description = "Título del documento según los metadatos del PDF")]
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Autor del documento
        /// </summary>
        [SwaggerSchema(Description = "Autor del documento")]
        public string Author { get; set; } = string.Empty;
        
        /// <summary>
        /// Creador del documento (software usado)
        /// </summary>
        [SwaggerSchema(Description = "Software o aplicación usado para crear el documento")]
        public string Creator { get; set; } = string.Empty;
        
        /// <summary>
        /// Palabras clave del documento
        /// </summary>
        [SwaggerSchema(Description = "Palabras clave del documento")]
        public string Keywords { get; set; } = string.Empty;
        
        /// <summary>
        /// Número de páginas
        /// </summary>
        [SwaggerSchema(Description = "Número total de páginas")]
        [DefaultValue(1)]
        public int PageCount { get; set; }
        
        /// <summary>
        /// Fecha de creación
        /// </summary>
        [SwaggerSchema(Description = "Fecha de creación del documento")]
        public string CreationDate { get; set; } = string.Empty;
    }
} 