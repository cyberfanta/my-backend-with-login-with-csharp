using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBackendApi.Models;
using MyBackendApi.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBackendApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerTag("Document")]
    public class DocumentController : ControllerBase
    {
        private readonly DocumentService _documentService;

        public DocumentController(DocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost("process-pdf")]
        [SwaggerOperation(
            Summary = "Procesar un archivo PDF", 
            Description = "Recibe un documento PDF y devuelve el texto extraído (requiere autenticación)",
            OperationId = "ProcessSinglePdf",
            Tags = new[] { "Document" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "PDF procesado correctamente", typeof(DocumentResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Archivo no proporcionado o inválido")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autorizado")]
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)] // 10MB max
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ProcessSinglePdf(
            [SwaggerParameter("Archivo PDF a procesar", Required = true)]
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No se ha proporcionado un archivo o el archivo está vacío");
            }

            if (!file.ContentType.Equals("application/pdf"))
            {
                return BadRequest("El archivo debe ser un PDF");
            }

            var result = await _documentService.ProcessSinglePdf(file);
            return Ok(result);
        }

        [HttpPost("process-pdfs")]
        [SwaggerOperation(
            Summary = "Procesar múltiples archivos PDF", 
            Description = "Recibe varios documentos PDF y devuelve un arreglo con los textos extraídos (requiere autenticación)",
            OperationId = "ProcessMultiplePdfs",
            Tags = new[] { "Document" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "PDFs procesados correctamente", typeof(List<DocumentResponse>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Archivos no proporcionados o inválidos")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autorizado")]
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB max
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ProcessMultiplePdfs(
            [SwaggerParameter("Archivos PDF a procesar (máximo 50MB en total)", Required = true)]
            [FromForm] List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                return BadRequest("No se han proporcionado archivos");
            }

            // Verificamos que todos los archivos sean PDFs
            foreach (var file in files)
            {
                if (!file.ContentType.Equals("application/pdf"))
                {
                    return BadRequest($"El archivo {file.FileName} no es un PDF");
                }
            }

            var results = await _documentService.ProcessMultiplePdfs(files);
            return Ok(results);
        }
    }
} 