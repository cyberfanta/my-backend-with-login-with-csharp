using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MyBackendApi.Configuration
{
    /// <summary>
    /// Filtro de documentación para agregar descripciones a las etiquetas de Swagger
    /// </summary>
    public class CustomTagDescriptions : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Asegurarse de que la colección de etiquetas existe
            swaggerDoc.Tags = swaggerDoc.Tags ?? new List<OpenApiTag>();
            
            // Limpiar etiquetas existentes para evitar duplicados
            swaggerDoc.Tags.Clear();
            
            // Agregar etiquetas con descripciones personalizadas
            swaggerDoc.Tags.Add(new OpenApiTag { 
                Name = "Auth", 
                Description = "Authentication operations" 
            });
            
            swaggerDoc.Tags.Add(new OpenApiTag { 
                Name = "User", 
                Description = "User management operations" 
            });
        }
    }
} 