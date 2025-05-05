using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyBackendApi.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MyBackendApi.Services
{
    /// <summary>
    /// Servicio para análisis avanzado de documentos PDF
    /// Extrae metadatos, texto, y detecta tablas e imágenes.
    /// </summary>
    public class PdfAnalyzerService
    {
        private readonly ILogger<PdfAnalyzerService> _logger;
        private readonly AiService _aiService;
        private const int MIN_CHARACTERS_FOR_TEXT_PDF = 20; // Umbral mínimo de caracteres para considerar un PDF con texto

        public PdfAnalyzerService(ILogger<PdfAnalyzerService> logger, AiService aiService)
        {
            _logger = logger;
            _aiService = aiService;
        }

        /// <summary>
        /// Analiza un documento PDF y extrae información estructurada
        /// </summary>
        /// <param name="pdfFile">Archivo PDF a analizar</param>
        /// <returns>Respuesta con la información estructurada extraída del PDF</returns>
        public async Task<DocumentAnalysisResponse> AnalyzePdfAsync(IFormFile pdfFile)
        {
            _logger.LogInformation($"Iniciando análisis del documento: {pdfFile.FileName}");
            
            try
            {
                var response = new DocumentAnalysisResponse
                {
                    FileName = pdfFile.FileName,
                    FileSize = pdfFile.Length,
                };
                
                // Primero intentar extraer contenido con PdfPig
                bool isPdfWithExtractableText = false;
                int letterCount = 0;
                
                try
                {
                    // Abrir el PDF para verificar si tiene texto extraíble
                    using (var stream = pdfFile.OpenReadStream())
                    using (var pdfDocument = PdfDocument.Open(stream))
                    {
                        // Verificar si el PDF tiene texto extraíble
                        letterCount = pdfDocument.GetPages().Sum(p => p.Letters.Count);
                        isPdfWithExtractableText = letterCount > MIN_CHARACTERS_FOR_TEXT_PDF;
                        
                        if (isPdfWithExtractableText)
                        {
                            _logger.LogInformation($"PDF con texto extraíble: {pdfFile.FileName}. Letras encontradas: {letterCount}");
                            
                            // Extraer metadatos
                            response.Metadata = ExtractMetadata(pdfDocument);
                            
                            // Extraer texto completo para análisis
                            var fullText = new StringBuilder();
                            var pageTexts = new List<string>();
                            
                            foreach (var page in pdfDocument.GetPages())
                            {
                                var pageText = string.Join(" ", page.Letters.Select(l => l.Value));
                                pageTexts.Add(pageText);
                                fullText.AppendLine(pageText);
                            }
                            
                            // Detectar tablas potenciales
                            var tablesDetected = DetectTables(pdfDocument, pageTexts);
                            
                            // Convertir a formato TableData para mantener compatibilidad
                            response.Tables = tablesDetected.Select(t => new TableData
                            {
                                Description = t,
                                Title = "Tabla detectada",
                                TextRepresentation = "Formato automático"
                            }).ToList();
                            
                            // Detectar imágenes
                            var imagesDetected = DetectImages(pdfDocument);
                            
                            // Convertir a formato ImageInfo para mantener compatibilidad
                            response.Images = imagesDetected.Select(i => {
                                var parts = i.Split(':');
                                return new ImageInfo
                                {
                                    PageNumber = int.TryParse(parts[0].Replace("Página ", ""), out int page) ? page : 1,
                                    Description = i
                                };
                            }).ToList();
                            
                            // Analizar el contenido con IA
                            await AnalyzeContentWithAiAsync(fullText.ToString(), response);
                        }
                        else
                        {
                            _logger.LogWarning($"PDF con poco o ningún texto extraíble: {pdfFile.FileName}. Solo se encontraron {letterCount} caracteres. Utilizando análisis simulado.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error al extraer texto con PdfPig, posiblemente el PDF es una imagen escaneada: {pdfFile.FileName}");
                    isPdfWithExtractableText = false;
                }
                
                // Si el PDF no tiene texto extraíble o hubo un error, usar simulación
                if (!isPdfWithExtractableText)
                {
                    _logger.LogInformation($"Usando análisis simulado para PDF: {pdfFile.FileName}");
                    await ProcessPdfWithSimulatedDataAsync(pdfFile, response);
                }
                
                // Verificar que tenemos al menos algunos datos básicos
                EnsureBasicData(response);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error crítico al analizar el documento PDF: {pdfFile.FileName}");
                
                // Crear una respuesta básica que permita mostrar algo en el Excel
                return new DocumentAnalysisResponse
                {
                    FileName = pdfFile.FileName,
                    FileSize = pdfFile.Length,
                    Summary = $"Error al procesar el documento: {ex.Message}",
                    KeyPoints = new List<string> { "No se pudo analizar correctamente" },
                    Metadata = new DocumentMetadata
                    {
                        PageCount = 1,
                        Title = Path.GetFileNameWithoutExtension(pdfFile.FileName),
                        Author = string.Empty,
                        Creator = "Error de Procesamiento",
                        Keywords = "error, procesamiento fallido",
                        CreationDate = DateTime.Now.ToString("dd/MM/yyyy")
                    }
                };
            }
        }
        
        /// <summary>
        /// Asegura que la respuesta contiene datos básicos mínimos
        /// </summary>
        private void EnsureBasicData(DocumentAnalysisResponse response)
        {
            // Asegurar que hay metadatos
            if (response.Metadata == null)
            {
                response.Metadata = new DocumentMetadata
                {
                    PageCount = 1,
                    Title = Path.GetFileNameWithoutExtension(response.FileName),
                    Author = string.Empty,
                    Creator = string.Empty,
                    Keywords = string.Empty,
                    CreationDate = DateTime.Now.ToString("dd/MM/yyyy")
                };
            }
            else
            {
                // Verificar propiedades individuales
                if (string.IsNullOrEmpty(response.Metadata.Creator))
                {
                    response.Metadata.Creator = string.Empty;
                }
                
                if (string.IsNullOrEmpty(response.Metadata.Keywords))
                {
                    response.Metadata.Keywords = string.Empty;
                }
                
                // Verificar el formato de fecha
                if (string.IsNullOrEmpty(response.Metadata.CreationDate))
                {
                    response.Metadata.CreationDate = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
            
            // Asegurar que hay un título
            if (string.IsNullOrEmpty(response.DocumentTitle))
            {
                response.DocumentTitle = !string.IsNullOrEmpty(response.Metadata.Title) 
                    ? response.Metadata.Title 
                    : Path.GetFileNameWithoutExtension(response.FileName);
            }
            
            // Asegurar que hay un resumen
            if (string.IsNullOrEmpty(response.Summary))
            {
                response.Summary = $"Documento: {Path.GetFileNameWithoutExtension(response.FileName)}";
            }
            
            // Asegurar que hay puntos clave
            if (response.KeyPoints == null || !response.KeyPoints.Any())
            {
                response.KeyPoints = new List<string> { "Información extraída automáticamente" };
            }
            
            // Asegurar que hay conclusiones
            if (response.Conclusions == null || !response.Conclusions.Any())
            {
                response.Conclusions = new List<string> { "Documento procesado automáticamente" };
            }
        }
        
        /// <summary>
        /// Procesa un PDF usando datos simulados cuando el texto no puede ser extraído directamente
        /// </summary>
        private async Task ProcessPdfWithSimulatedDataAsync(IFormFile pdfFile, DocumentAnalysisResponse response)
        {
            try
            {
                // Obtener campos extraídos usando análisis simulado
                var extractedFields = await _aiService.AnalyzePdfWithAiOcrAsync(pdfFile);
                
                // Verificar si hay errores
                if (extractedFields.TryGetValue("Error", out string? errorMessage) && !string.IsNullOrEmpty(errorMessage))
                {
                    _logger.LogWarning($"Error en análisis simulado: {errorMessage}");
                    response.Summary = $"Error en procesamiento con análisis simulado: {errorMessage}";
                    response.KeyPoints = new List<string> { "Documento procesado con errores", "Se recomienda revisión manual" };
                    return;
                }
                
                // Extraer texto simulado para análisis
                var simulatedText = _aiService.SimulateOcrText(pdfFile.FileName);
                
                // Analizar contenido con AI usando texto simulado
                var (documentTitle, summary, keyPoints, conclusions) = await _aiService.AnalyzeDocumentTextAsync(simulatedText, 1);
                
                // Asignar los datos obtenidos
                response.DocumentTitle = documentTitle;
                response.Summary = summary;
                response.KeyPoints = keyPoints;
                response.Conclusions = conclusions;
                
                // Establecer metadatos básicos
                response.Metadata = new DocumentMetadata
                {
                    PageCount = 1, // No podemos saber el número exacto de páginas sin procesar el PDF completamente
                    Title = extractedFields.TryGetValue("Descripcion", out string? title) && !string.IsNullOrEmpty(title)
                        ? title 
                        : Path.GetFileNameWithoutExtension(pdfFile.FileName),
                    Author = extractedFields.TryGetValue("Cliente", out string? author) && !string.IsNullOrEmpty(author)
                        ? author
                        : string.Empty,
                    Creator = "Procesamiento Simulado",
                    Keywords = "OCR, procesamiento simulado",
                    CreationDate = extractedFields.TryGetValue("FechaEmision", out string? dateStr) && !string.IsNullOrEmpty(dateStr)
                        ? FormatValidDate(dateStr)
                        : DateTime.Now.ToString("dd/MM/yyyy")
                };
                
                // Agregar indicador de procesamiento simulado
                response.KeyPoints.Add("Documento procesado con análisis simulado (sin OCR)");
                response.Conclusions.Add("La información puede no ser precisa debido a limitaciones del análisis simulado");
                
                _logger.LogInformation($"Procesamiento simulado completado para {pdfFile.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en procesamiento simulado: {pdfFile.FileName}");
                response.Summary = $"Error en procesamiento simulado: {ex.Message}";
                response.KeyPoints = new List<string> { "Documento no procesado correctamente", "Se requiere revisión manual" };
            }
        }
        
        /// <summary>
        /// Analiza múltiples documentos PDF
        /// </summary>
        /// <param name="pdfFiles">Lista de archivos PDF a analizar</param>
        /// <returns>Lista de respuestas con la información estructurada de cada PDF</returns>
        public async Task<List<DocumentAnalysisResponse>> AnalyzeMultiplePdfsAsync(List<IFormFile> pdfFiles)
        {
            var responses = new List<DocumentAnalysisResponse>();
            
            _logger.LogInformation($"Comenzando análisis de {pdfFiles.Count} archivos PDF");
            
            // Verificar que todos los archivos sean válidos
            if (pdfFiles == null || !pdfFiles.Any())
            {
                _logger.LogWarning("No se proporcionaron archivos para analizar");
                return responses;
            }
            
            // Procesar cada archivo PDF
            for (int i = 0; i < pdfFiles.Count; i++)
            {
                var file = pdfFiles[i];
                _logger.LogInformation($"Procesando archivo {i+1} de {pdfFiles.Count}: {file.FileName}");
                
                try
                {
                    var response = await AnalyzePdfAsync(file);
                    responses.Add(response);
                    _logger.LogInformation($"Archivo {i+1}/{pdfFiles.Count} procesado correctamente: {file.FileName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error al analizar el documento: {file.FileName}");
                    // Añadir respuesta de error para este archivo
                    responses.Add(new DocumentAnalysisResponse
                    {
                        FileName = file.FileName,
                        FileSize = file.Length,
                        Summary = $"Error al procesar el documento: {ex.Message}",
                        KeyPoints = new List<string> { "No se pudo analizar correctamente" },
                        Metadata = new DocumentMetadata
                        {
                            PageCount = 1,
                            Title = Path.GetFileNameWithoutExtension(file.FileName),
                            Author = string.Empty,
                            Creator = "Error de Procesamiento",
                            Keywords = "error, procesamiento fallido",
                            CreationDate = DateTime.Now.ToString("dd/MM/yyyy")
                        }
                    });
                    _logger.LogInformation($"Se añadió una respuesta de error para el archivo: {file.FileName}");
                }
            }
            
            _logger.LogInformation($"Análisis completado. Procesados {responses.Count} de {pdfFiles.Count} archivos PDF");
            
            return responses;
        }
        
        /// <summary>
        /// Analiza el contenido del documento con IA
        /// </summary>
        private async Task AnalyzeContentWithAiAsync(string documentText, DocumentAnalysisResponse response)
        {
            try
            {
                var (documentTitle, summary, keyPoints, conclusions) = await _aiService.AnalyzeDocumentTextAsync(
                    documentText, 
                    response.Metadata?.PageCount ?? 1
                );
                
                response.DocumentTitle = documentTitle;
                response.Summary = summary;
                response.KeyPoints = keyPoints;
                response.Conclusions = conclusions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al analizar el contenido con IA");
                
                // Usar datos básicos en caso de error
                response.DocumentTitle = response.Metadata?.Title ?? Path.GetFileNameWithoutExtension(response.FileName);
                response.Summary = "Error al analizar el contenido con IA. " + ex.Message;
                response.KeyPoints = new List<string> { "Error en análisis de contenido" };
                response.Conclusions = new List<string> { "No se pudieron generar conclusiones automáticas" };
            }
        }
        
        /// <summary>
        /// Extrae metadatos del documento PDF
        /// </summary>
        private DocumentMetadata ExtractMetadata(PdfDocument document)
        {
            var metadata = new DocumentMetadata
            {
                PageCount = document.NumberOfPages
            };
            
            // Extraer información del diccionario de información del documento
            var info = document.Information;
            metadata.Title = info.Title;
            metadata.Author = info.Author;
            metadata.Creator = info.Creator;
            metadata.Keywords = info.Keywords;
            
            // Formatear fecha de creación, evitando error de conversión
            if (info.CreationDate.HasValue)
            {
                try
                {
                    metadata.CreationDate = info.CreationDate.Value.ToString("dd/MM/yyyy");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al formatear la fecha de creación del documento. Usando fecha actual.");
                    metadata.CreationDate = DateTime.Now.ToString("dd/MM/yyyy");
                }
            }
            else
            {
                metadata.CreationDate = DateTime.Now.ToString("dd/MM/yyyy");
            }
            
            // Si no hay título, intentar generarlo a partir del nombre del documento
            if (string.IsNullOrEmpty(metadata.Title))
            {
                // Intentar extraer de la primera página
                if (document.NumberOfPages > 0)
                {
                    var firstPage = document.GetPage(1);
                    // Extraer primeras palabras para intentar formar un título
                    var words = firstPage.Text.Split(' ').Take(10);
                    metadata.Title = string.Join(" ", words);
                }
            }
            
            return metadata;
        }
        
        /// <summary>
        /// Detecta posibles tablas en el documento
        /// </summary>
        private List<string> DetectTables(PdfDocument document, List<string> pageTexts)
        {
            var tables = new List<string>();
            
            // Detección muy básica de tablas (patrón de filas y columnas similares)
            for (int i = 0; i < document.NumberOfPages && i < pageTexts.Count; i++)
            {
                var pageText = pageTexts[i];
                var lines = pageText.Split('\n');
                
                // Buscar líneas que podrían ser tablas (contienen varios espacios consecutivos o tabulaciones)
                var potentialTableLines = lines.Where(l => l.Contains("\t") || l.Contains("  ")).ToList();
                
                if (potentialTableLines.Count >= 3) // Mínimo para considerar como tabla (encabezado + 2 filas)
                {
                    var tableDescription = $"Tabla detectada en página {i + 1} con aproximadamente {potentialTableLines.Count} filas";
                    tables.Add(tableDescription);
                }
            }
            
            return tables;
        }
        
        /// <summary>
        /// Detecta imágenes en el documento
        /// </summary>
        private List<string> DetectImages(PdfDocument document)
        {
            var images = new List<string>();
            
            try
            {
                // Detectar imágenes en cada página
                for (int i = 1; i <= document.NumberOfPages; i++)
                {
                    var page = document.GetPage(i);
                    int imageCount = 0;
                    
                    // Intento alternativo para detectar imágenes sin usar XObjects directamente
                    try
                    {
                        // Intento obtener imágenes (si hay alguna excepción, asumimos que hay al menos una imagen)
                        var pageImages = page.GetImages().ToList();
                        imageCount = pageImages.Count;
                    }
                    catch
                    {
                        // Si falla al obtener imágenes, asumimos que puede haber una imagen no extraíble
                        imageCount = 1;
                    }
                    
                    if (imageCount > 0)
                    {
                        images.Add($"Página {i}: {imageCount} imágenes detectadas");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al detectar imágenes en el documento");
                
                // En caso de error, agregar una entrada genérica
                images.Add("Posibles imágenes detectadas (no extraíbles)");
            }
            
            return images;
        }

        private string FormatValidDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime date))
            {
                return date.ToString("dd/MM/yyyy");
            }
            else
            {
                _logger.LogWarning($"Formato de fecha inválido: {dateStr}. Usando fecha actual.");
                return DateTime.Now.ToString("dd/MM/yyyy");
            }
        }
    }
} 