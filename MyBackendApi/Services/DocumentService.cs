using Microsoft.AspNetCore.Http;
using MyBackendApi.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyBackendApi.Services
{
    /// <summary>
    /// Servicio para procesar documentos PDF
    /// </summary>
    public class DocumentService
    {
        /// <summary>
        /// Procesa un único documento PDF y extrae su texto
        /// </summary>
        /// <param name="file">Archivo PDF a procesar</param>
        /// <returns>Respuesta con el nombre del archivo y texto extraído</returns>
        public async Task<DocumentResponse> ProcessSinglePdf(IFormFile file)
        {
            // Aquí iría la lógica real de extracción de texto del PDF
            // Por ahora, simplemente devolvemos un texto dummy

            // Simulamos un procesamiento asíncrono
            await Task.Delay(500);
            
            return new DocumentResponse
            {
                FileName = file.FileName,
                FileSize = file.Length,
                Text = $"Este es un texto dummy extraído del archivo {file.FileName}."
            };
        }
        
        /// <summary>
        /// Procesa múltiples documentos PDF y extrae su texto
        /// </summary>
        /// <param name="files">Colección de archivos PDF a procesar</param>
        /// <returns>Lista de respuestas con nombres de archivo y textos extraídos</returns>
        public async Task<List<DocumentResponse>> ProcessMultiplePdfs(List<IFormFile> files)
        {
            var responses = new List<DocumentResponse>();
            
            foreach (var file in files)
            {
                // Simulamos un procesamiento asíncrono
                await Task.Delay(200);
                
                responses.Add(new DocumentResponse
                {
                    FileName = file.FileName,
                    FileSize = file.Length,
                    Text = $"Este es un texto dummy extraído del archivo {file.FileName}. " +
                           $"Procesado como parte de un lote de {files.Count} archivos."
                });
            }
            
            return responses;
        }
    }
} 