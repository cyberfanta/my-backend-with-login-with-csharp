using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using MyBackendApi.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace MyBackendApi.Services
{
    /// <summary>
    /// Servicio para generar reportes en formato Excel
    /// </summary>
    public class ExcelService
    {
        private readonly ILogger<ExcelService> _logger;
        private readonly string _templatePath;
        
        // Constantes para estilos de Excel
        private static readonly Color ERROR_BACKGROUND = Color.FromArgb(255, 200, 200);  // Rojo claro para errores
        private static readonly Color WARNING_BACKGROUND = Color.FromArgb(255, 235, 156); // Amarillo para advertencias
        private static readonly Color HEADER_BACKGROUND = Color.FromArgb(91, 155, 213);   // Azul para encabezados
        private static readonly Color SUCCESS_BACKGROUND = Color.FromArgb(198, 239, 206); // Verde claro para éxito

        public ExcelService(ILogger<ExcelService> logger)
        {
            _logger = logger;
            
            // Ruta del template Excel
            _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Templates", "Planilla.xlsx");
            
            // Configuración de EPPlus para uso no comercial
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Genera un informe Excel con el análisis de múltiples documentos PDF
        /// </summary>
        /// <param name="documentAnalyses">Lista de análisis de documentos</param>
        /// <returns>Archivo Excel generado en memoria</returns>
        public MemoryStream GenerateExcelReport(List<DocumentAnalysisResponse> documentAnalyses)
        {
            _logger.LogInformation($"Generando informe Excel para {documentAnalyses.Count} documentos");
            
            var stream = new MemoryStream();
            
            try
            {
                using (var package = new ExcelPackage(stream))
                {
                    // Crear hoja principal con resumen
                    var summaryWorksheet = package.Workbook.Worksheets.Add("Resumen de Documentos");
                    CreateSummarySheet(summaryWorksheet, documentAnalyses);
                    
                    // Crear hoja de detalles para cada documento
                    for (int i = 0; i < documentAnalyses.Count; i++)
                    {
                        var analysis = documentAnalyses[i];
                        var docName = Path.GetFileNameWithoutExtension(analysis.FileName);
                        
                        // Limitar el nombre de la hoja a 31 caracteres (límite de Excel)
                        var sheetName = docName.Length > 28 
                            ? docName.Substring(0, 25) + "..." 
                            : docName;
                            
                        // Asegurar que el nombre de la hoja sea único
                        sheetName = EnsureUniqueSheetName(package.Workbook.Worksheets, sheetName, i + 1);
                        
                        var detailWorksheet = package.Workbook.Worksheets.Add(sheetName);
                        CreateDetailSheet(detailWorksheet, analysis);
                    }
                    
                    // Auto ajustar columnas en todas las hojas
                    foreach (var worksheet in package.Workbook.Worksheets)
                    {
                        worksheet.Cells.AutoFitColumns();
                    }
                    
                    _logger.LogInformation("Excel con resumen y detalles generado correctamente");
                    package.Save();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el informe Excel");
                
                // Generar un Excel básico de error en caso de fallo
                stream = GenerateErrorExcel(documentAnalyses, ex.Message);
            }
            
            // Reposicionar el stream al inicio para lectura
            stream.Position = 0;
            return stream;
        }
        
        /// <summary>
        /// Genera un Excel básico de error cuando falla la generación normal
        /// </summary>
        private MemoryStream GenerateErrorExcel(List<DocumentAnalysisResponse> documentAnalyses, string errorMessage)
        {
            var stream = new MemoryStream();
            
            using (var package = new ExcelPackage(stream))
            {
                var worksheet = package.Workbook.Worksheets.Add("Error en Informe");
                
                // Estilo para el título
                worksheet.Cells[1, 1].Value = "ERROR EN LA GENERACIÓN DEL INFORME";
                worksheet.Cells[1, 1, 1, 5].Merge = true;
                worksheet.Cells[1, 1, 1, 5].Style.Font.Bold = true;
                worksheet.Cells[1, 1, 1, 5].Style.Font.Size = 14;
                worksheet.Cells[1, 1, 1, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1, 1, 5].Style.Fill.BackgroundColor.SetColor(ERROR_BACKGROUND);
                worksheet.Cells[1, 1, 1, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                
                // Mensaje de error
                worksheet.Cells[3, 1].Value = "Mensaje de error:";
                worksheet.Cells[3, 2].Value = errorMessage;
                worksheet.Cells[3, 1].Style.Font.Bold = true;
                worksheet.Cells[3, 2, 3, 5].Merge = true;
                
                // Lista de documentos
                worksheet.Cells[5, 1].Value = "Documentos procesados:";
                worksheet.Cells[5, 1].Style.Font.Bold = true;
                
                int row = 6;
                foreach (var doc in documentAnalyses)
                {
                    worksheet.Cells[row, 1].Value = doc.FileName;
                    worksheet.Cells[row, 2].Value = doc.Summary?.StartsWith("Error") == true ? "Error" : "Procesado";
                    worksheet.Cells[row, 3].Value = doc.Metadata?.PageCount ?? 0;
                    
                    if (doc.Summary?.StartsWith("Error") == true)
                    {
                        worksheet.Cells[row, 1, row, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(ERROR_BACKGROUND);
                    }
                    
                    row++;
                }
                
                // Encabezados de columna
                worksheet.Cells[5, 2].Value = "Estado";
                worksheet.Cells[5, 3].Value = "Páginas";
                worksheet.Cells[5, 1, 5, 3].Style.Font.Bold = true;
                worksheet.Cells[5, 1, 5, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[5, 1, 5, 3].Style.Fill.BackgroundColor.SetColor(HEADER_BACKGROUND);
                worksheet.Cells[5, 1, 5, 3].Style.Font.Color.SetColor(Color.White);
                
                worksheet.Cells.AutoFitColumns();
                package.Save();
            }
            
            stream.Position = 0;
            return stream;
        }
        
        /// <summary>
        /// Asegura que el nombre de la hoja sea único dentro del libro
        /// </summary>
        private string EnsureUniqueSheetName(ExcelWorksheets worksheets, string baseName, int index)
        {
            string name = baseName;
            int suffix = 1;
            
            // Verificar si el nombre ya existe
            while (worksheets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName}_{suffix}";
                suffix++;
                
                // Si el nombre sigue siendo demasiado largo, truncarlo más
                if (name.Length > 31)
                {
                    name = $"{baseName.Substring(0, 25)}_{suffix}";
                }
            }
            
            return name;
        }
        
        /// <summary>
        /// Crea la hoja de resumen con la información de todos los documentos
        /// </summary>
        private void CreateSummarySheet(ExcelWorksheet worksheet, List<DocumentAnalysisResponse> documentAnalyses)
        {
            _logger.LogInformation("Creando hoja de resumen del Excel");
            
            // Encabezado
            worksheet.Cells[1, 1].Value = "RESUMEN DE ANÁLISIS DE DOCUMENTOS";
            worksheet.Cells[1, 1, 1, 8].Merge = true;
            worksheet.Cells[1, 1, 1, 8].Style.Font.Bold = true;
            worksheet.Cells[1, 1, 1, 8].Style.Font.Size = 14;
            worksheet.Cells[1, 1, 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1, 1, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1, 1, 8].Style.Fill.BackgroundColor.SetColor(HEADER_BACKGROUND);
            worksheet.Cells[1, 1, 1, 8].Style.Font.Color.SetColor(Color.White);
            
            // Información general
            worksheet.Cells[2, 1].Value = $"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            worksheet.Cells[2, 1, 2, 8].Merge = true;
            worksheet.Cells[2, 1, 2, 8].Style.Font.Italic = true;
            
            // Encabezados de tabla
            string[] headers = new[] 
            { 
                "N°", "Archivo", "Título", "Fecha", "Páginas", "Estado", "Cliente", "Total" 
            };
            
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[4, i + 1].Value = headers[i];
                worksheet.Cells[4, i + 1].Style.Font.Bold = true;
                worksheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(HEADER_BACKGROUND);
                worksheet.Cells[4, i + 1].Style.Font.Color.SetColor(Color.White);
                worksheet.Cells[4, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
            
            // Datos de cada documento
            int row = 5;
            for (int i = 0; i < documentAnalyses.Count; i++)
            {
                var analysis = documentAnalyses[i];
                bool hasError = analysis.Summary?.StartsWith("Error") == true;
                
                worksheet.Cells[row, 1].Value = i + 1; // Número secuencial
                worksheet.Cells[row, 2].Value = analysis.FileName;
                worksheet.Cells[row, 3].Value = analysis.DocumentTitle ?? Path.GetFileNameWithoutExtension(analysis.FileName);
                
                // Manejar fecha de creación, verificando posibles errores
                string creationDate = "N/A";
                if (!string.IsNullOrEmpty(analysis.Metadata?.CreationDate))
                {
                    creationDate = analysis.Metadata.CreationDate;
                    
                    // Verificar si la fecha tiene formato válido
                    if (!DateTime.TryParseExact(creationDate, "dd/MM/yyyy", null, 
                                               System.Globalization.DateTimeStyles.None, out _))
                    {
                        // Intentar convertir al formato correcto si es posible
                        if (DateTime.TryParse(creationDate, out DateTime parsedDate))
                        {
                            creationDate = parsedDate.ToString("dd/MM/yyyy");
                        }
                    }
                }
                worksheet.Cells[row, 4].Value = creationDate;
                worksheet.Cells[row, 5].Value = analysis.Metadata?.PageCount ?? 0;
                
                // Estado del procesamiento
                string status = hasError ? "Error" : "Procesado";
                worksheet.Cells[row, 6].Value = status;
                
                // Si se procesó con OCR, agregar indicador
                bool usedOcr = analysis.KeyPoints?.Any(k => k.Contains("OCR")) == true;
                if (usedOcr && !hasError)
                {
                    worksheet.Cells[row, 6].Value = "Procesado con OCR";
                }
                
                // Datos financieros (si están disponibles)
                // Cliente
                var clientInfo = analysis.KeyPoints?.FirstOrDefault(k => k.Contains("Cliente:"));
                worksheet.Cells[row, 7].Value = clientInfo != null 
                    ? clientInfo.Replace("Cliente:", "").Trim() 
                    : "N/A";
                
                // Total
                var totalInfo = analysis.KeyPoints?.FirstOrDefault(k => k.Contains("Total:"));
                worksheet.Cells[row, 8].Value = totalInfo != null 
                    ? totalInfo.Replace("Total:", "").Trim() 
                    : "N/A";
                
                // Aplicar estilo según el estado
                if (hasError)
                {
                    // Estilo para errores
                    worksheet.Cells[row, 1, row, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(ERROR_BACKGROUND);
                }
                else if (usedOcr)
                {
                    // Estilo para procesamiento con OCR (advertencia)
                    worksheet.Cells[row, 1, row, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(WARNING_BACKGROUND);
                }
                else
                {
                    // Estilo para procesamiento exitoso
                    worksheet.Cells[row, 1, row, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(SUCCESS_BACKGROUND);
                }
                
                // Bordes para todas las celdas
                worksheet.Cells[row, 1, row, 8].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                
                row++;
            }
            
            // Ajustar columnas
            worksheet.Cells.AutoFitColumns();
            
            // Congelar paneles (filas de encabezado)
            worksheet.View.FreezePanes(5, 1);
            
            _logger.LogInformation("Hoja de resumen creada con éxito");
        }
        
        /// <summary>
        /// Crea una hoja de detalle para un documento específico
        /// </summary>
        private void CreateDetailSheet(ExcelWorksheet worksheet, DocumentAnalysisResponse analysis)
        {
            bool hasError = analysis.Summary?.StartsWith("Error") == true;
            
            // Título del documento
            worksheet.Cells[1, 1].Value = analysis.DocumentTitle ?? Path.GetFileNameWithoutExtension(analysis.FileName);
            worksheet.Cells[1, 1, 1, 5].Merge = true;
            worksheet.Cells[1, 1, 1, 5].Style.Font.Bold = true;
            worksheet.Cells[1, 1, 1, 5].Style.Font.Size = 14;
            
            // Estilo diferente si hay error
            if (hasError)
            {
                worksheet.Cells[1, 1, 1, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1, 1, 5].Style.Fill.BackgroundColor.SetColor(ERROR_BACKGROUND);
            }
            else
            {
                worksheet.Cells[1, 1, 1, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1, 1, 5].Style.Fill.BackgroundColor.SetColor(HEADER_BACKGROUND);
                worksheet.Cells[1, 1, 1, 5].Style.Font.Color.SetColor(Color.White);
            }
            
            // Información básica del documento
            worksheet.Cells[3, 1].Value = "Nombre de archivo:";
            worksheet.Cells[3, 2].Value = analysis.FileName;
            
            worksheet.Cells[4, 1].Value = "Tamaño:";
            worksheet.Cells[4, 2].Value = $"{analysis.FileSize / 1024.0:N2} KB";
            
            worksheet.Cells[5, 1].Value = "Fecha:";
            worksheet.Cells[5, 2].Value = analysis.Metadata?.CreationDate ?? "N/A";
            
            worksheet.Cells[6, 1].Value = "Páginas:";
            worksheet.Cells[6, 2].Value = analysis.Metadata?.PageCount ?? 0;
            
            // Formatear columnas de etiquetas
            worksheet.Cells[3, 1, 6, 1].Style.Font.Bold = true;
            
            // Resumen del documento
            worksheet.Cells[8, 1].Value = "RESUMEN:";
            worksheet.Cells[8, 1].Style.Font.Bold = true;
            
            worksheet.Cells[9, 1].Value = analysis.Summary;
            worksheet.Cells[9, 1, 9, 5].Merge = true;
            worksheet.Cells[9, 1, 9, 5].Style.WrapText = true;
            
            // Altura fija para el resumen (múltiples líneas)
            worksheet.Row(9).Height = 60;
            
            // Puntos clave
            worksheet.Cells[11, 1].Value = "PUNTOS CLAVE:";
            worksheet.Cells[11, 1].Style.Font.Bold = true;
            
            int row = 12;
            if (analysis.KeyPoints != null && analysis.KeyPoints.Any())
            {
                foreach (var point in analysis.KeyPoints)
                {
                    worksheet.Cells[row, 1].Value = "•";
                    worksheet.Cells[row, 2].Value = point;
                    worksheet.Cells[row, 2, row, 5].Merge = true;
                    row++;
                }
            }
            else
            {
                worksheet.Cells[row, 1].Value = "• No se encontraron puntos clave";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                row++;
            }
            
            // Espacio entre secciones
            row += 1;
            
            // Conclusiones
            worksheet.Cells[row, 1].Value = "CONCLUSIONES:";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            row++;
            
            if (analysis.Conclusions != null && analysis.Conclusions.Any())
            {
                foreach (var conclusion in analysis.Conclusions)
                {
                    worksheet.Cells[row, 1].Value = "•";
                    worksheet.Cells[row, 2].Value = conclusion;
                    worksheet.Cells[row, 2, row, 5].Merge = true;
                    row++;
                }
            }
            else
            {
                worksheet.Cells[row, 1].Value = "• No se encontraron conclusiones";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                row++;
            }
            
            // Espacio entre secciones
            row += 1;
            
            // Datos de tablas (si existen)
            if (analysis.Tables != null && analysis.Tables.Any())
            {
                worksheet.Cells[row, 1].Value = "TABLAS DETECTADAS:";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;
                
                int tableCount = 1;
                foreach (var table in analysis.Tables)
                {
                    worksheet.Cells[row, 1].Value = $"Tabla {tableCount}:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Value = table;
                    worksheet.Cells[row, 2, row, 5].Merge = true;
                    row++;
                    tableCount++;
                }
            }
            
            // Datos de imágenes (si existen)
            if (analysis.Images != null && analysis.Images.Any())
            {
                worksheet.Cells[row, 1].Value = "IMÁGENES DETECTADAS:";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;
                
                int imageCount = 1;
                foreach (var image in analysis.Images)
                {
                    worksheet.Cells[row, 1].Value = $"Imagen {imageCount}:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Value = image;
                    worksheet.Cells[row, 2, row, 5].Merge = true;
                    row++;
                    imageCount++;
                }
            }
            
            // Metadatos adicionales (si existen)
            if (analysis.Metadata != null)
            {
                row += 1;
                worksheet.Cells[row, 1].Value = "METADATOS ADICIONALES:";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;
                
                if (!string.IsNullOrEmpty(analysis.Metadata.Author))
                {
                    worksheet.Cells[row, 1].Value = "Autor:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Value = analysis.Metadata.Author;
                    row++;
                }
                
                // Mostrar Creator si existe
                if (!string.IsNullOrEmpty(analysis.Metadata.Creator))
                {
                    worksheet.Cells[row, 1].Value = "Creador:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Value = analysis.Metadata.Creator;
                    row++;
                }
                
                // Mostrar Keywords si existen
                if (!string.IsNullOrEmpty(analysis.Metadata.Keywords))
                {
                    worksheet.Cells[row, 1].Value = "Palabras clave:";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 2].Value = analysis.Metadata.Keywords;
                    row++;
                }
            }
            
            // Mensaje de advertencia si se usó OCR
            if (analysis.KeyPoints?.Any(k => k.Contains("OCR")) == true)
            {
                row += 2;
                worksheet.Cells[row, 1].Value = "Nota: Este documento fue procesado con OCR. La información extraída puede requerir verificación manual.";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1, row, 5].Style.Font.Italic = true;
                worksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(WARNING_BACKGROUND);
            }
            
            // Ajustar columnas
            worksheet.Cells.AutoFitColumns();
        }
    }
} 