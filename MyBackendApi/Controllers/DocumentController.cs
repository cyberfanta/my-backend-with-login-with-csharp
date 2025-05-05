using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBackendApi.Models;
using MyBackendApi.Services;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MyBackendApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerTag("Document")]
    public class DocumentController : ControllerBase
    {
        private readonly PdfAnalyzerService _pdfAnalyzerService;
        private readonly ExcelService _excelService;
        private readonly ExcelDocumentService _excelDocumentService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            PdfAnalyzerService pdfAnalyzerService,
            ExcelService excelService,
            ExcelDocumentService excelDocumentService,
            ILogger<DocumentController> logger)
        {
            _pdfAnalyzerService = pdfAnalyzerService;
            _excelService = excelService;
            _excelDocumentService = excelDocumentService;
            _logger = logger;
        }
        
        [HttpPost("process-pdf")]
        [SwaggerOperation(
            Summary = "Procesar un archivo PDF", 
            Description = "Recibe un documento PDF, lo analiza y devuelve un archivo Excel con la información estructurada (requiere autenticación)",
            OperationId = "ProcessPdf",
            Tags = new[] { "Document" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Excel generado correctamente", typeof(FileContentResult))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Archivo no proporcionado o inválido")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autorizado")]
        [RequestFormLimits(MultipartBodyLengthLimit = 20971520)] // 20MB max
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ProcessPdf(
            [SwaggerParameter("Archivo PDF a analizar", Required = true)]
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

            try
            {
                // Analizar PDF
                var analysisResult = await _pdfAnalyzerService.AnalyzePdfAsync(file);
                
                // Generar Excel con el nuevo método
                var results = new List<DocumentAnalysisResponse> { analysisResult };
                var excelStream = _excelService.GenerateExcelReport(results);
                
                // Guardar en la base de datos
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
                string excelFileName = Path.GetFileNameWithoutExtension(file.FileName) + "_analysis.xlsx";
                
                // El stream ya es una copia creada por el servicio, podemos usarlo directamente
                // Guardar en la base de datos
                await _excelDocumentService.SaveExcelDocumentAsync(
                    excelStream, 
                    excelFileName,
                    userId
                );
                
                // Resetear el stream original para lectura
                excelStream.Position = 0;
                byte[] fileBytes = excelStream.ToArray();
                
                // Devolver el Excel como archivo
                return File(
                    fileBytes, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    excelFileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al procesar el documento: {ex.Message}");
            }
        }
        
        [HttpPost("process-pdfs")]
        [SwaggerOperation(
            Summary = "Procesar múltiples archivos PDF", 
            Description = "Recibe varios documentos PDF, los analiza y devuelve un archivo Excel con la información estructurada de todos (requiere autenticación)",
            OperationId = "ProcessMultiplePdfs",
            Tags = new[] { "Document" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Excel generado correctamente", typeof(FileContentResult))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Archivos no proporcionados o inválidos")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autorizado")]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)] // 100MB max
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ProcessMultiplePdfs(
            [SwaggerParameter("Archivos PDF a analizar (máximo 100MB en total)", Required = true)]
            [FromForm] List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                return BadRequest("No se han proporcionado archivos");
            }

            // Verificamos que todos los archivos sean PDFs
            var validFiles = new List<IFormFile>();
            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning($"Se ha proporcionado un archivo vacío o nulo");
                    continue;
                }

                if (!file.ContentType.Equals("application/pdf"))
                {
                    _logger.LogWarning($"El archivo {file.FileName} no es un PDF. ContentType: {file.ContentType}");
                    // Opción 1: Rechazar todo el proceso si hay archivos no válidos
                    // return BadRequest($"El archivo {file.FileName} no es un PDF");
                    
                    // Opción 2: Ignorar los archivos no válidos y continuar con los válidos
                    continue;
                }
                
                validFiles.Add(file);
            }
            
            if (!validFiles.Any())
            {
                return BadRequest("No se han proporcionado archivos PDF válidos para procesar");
            }
            
            _logger.LogInformation($"Procesando {validFiles.Count} archivos PDF válidos de {files.Count} recibidos");

            try
            {
                // Analizar PDFs
                var analysisResults = await _pdfAnalyzerService.AnalyzeMultiplePdfsAsync(validFiles);
                _logger.LogInformation($"Análisis completado: {analysisResults.Count} resultados generados");
                
                // Generar Excel con el nuevo método
                var excelStream = _excelService.GenerateExcelReport(analysisResults);
                
                // Guardar en la base de datos
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
                string excelFileName = "multiple_pdfs_analysis.xlsx";
                
                // El stream ya es una copia creada por el servicio, podemos usarlo directamente
                // Guardar en la base de datos
                await _excelDocumentService.SaveExcelDocumentAsync(
                    excelStream, 
                    excelFileName,
                    userId
                );
                
                // Resetear el stream original para lectura
                excelStream.Position = 0;
                byte[] fileBytes = excelStream.ToArray();
                
                _logger.LogInformation($"Excel generado con éxito: {fileBytes.Length} bytes");
                
                // Devolver el Excel como archivo
                return File(
                    fileBytes, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    excelFileName
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar los documentos");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al procesar los documentos: {ex.Message}");
            }
        }
    }
} 