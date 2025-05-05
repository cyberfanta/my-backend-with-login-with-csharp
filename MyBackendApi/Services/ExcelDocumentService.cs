using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyBackendApi.Data;
using MyBackendApi.Models;

namespace MyBackendApi.Services
{
    /// <summary>
    /// Servicio para gestionar documentos Excel en la base de datos
    /// </summary>
    public class ExcelDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ExcelDocumentService> _logger;

        public ExcelDocumentService(ApplicationDbContext dbContext, ILogger<ExcelDocumentService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Guarda un documento Excel en la base de datos
        /// </summary>
        /// <param name="stream">Stream con el contenido del Excel</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="userId">ID del usuario que lo crea</param>
        /// <param name="relatedDocumentIds">IDs de documentos relacionados (opcional)</param>
        /// <returns>ID del documento guardado</returns>
        public async Task<Guid> SaveExcelDocumentAsync(MemoryStream stream, string fileName, string userId, string relatedDocumentIds = "")
        {
            try
            {
                _logger.LogInformation($"Guardando documento Excel '{fileName}' en la base de datos");
                
                var content = stream.ToArray();
                
                var excelDocument = new ExcelDocument
                {
                    Id = Guid.NewGuid(),
                    FileName = fileName,
                    Content = content,
                    FileSize = content.Length,
                    CreatedBy = userId,
                    RelatedDocumentIds = relatedDocumentIds,
                    CreatedAt = DateTime.UtcNow
                };
                
                _dbContext.ExcelDocuments.Add(excelDocument);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Documento Excel guardado con ID: {excelDocument.Id}");
                
                return excelDocument.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al guardar documento Excel '{fileName}'");
                throw;
            }
        }

        /// <summary>
        /// Obtiene un documento Excel de la base de datos
        /// </summary>
        /// <param name="id">ID del documento</param>
        /// <returns>Documento Excel</returns>
        public async Task<ExcelDocument?> GetExcelDocumentAsync(Guid id)
        {
            try
            {
                _logger.LogInformation($"Obteniendo documento Excel con ID: {id}");
                
                var document = await _dbContext.ExcelDocuments.FindAsync(id);
                
                if (document == null)
                {
                    _logger.LogWarning($"No se encontró el documento Excel con ID: {id}");
                }
                
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener documento Excel con ID: {id}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene los documentos Excel más recientes
        /// </summary>
        /// <param name="count">Número de documentos a obtener</param>
        /// <returns>Lista de documentos Excel (sin el contenido)</returns>
        public async Task<List<ExcelDocument>> GetRecentExcelDocumentsAsync(int count = 10)
        {
            try
            {
                _logger.LogInformation($"Obteniendo los {count} documentos Excel más recientes");
                
                // Obtener los documentos sin incluir el contenido para no sobrecargar la respuesta
                var documents = await _dbContext.ExcelDocuments
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(count)
                    .Select(d => new ExcelDocument
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        ContentType = d.ContentType,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        FileSize = d.FileSize,
                        RelatedDocumentIds = d.RelatedDocumentIds,
                        // No incluir el contenido
                        Content = Array.Empty<byte>()
                    })
                    .ToListAsync();
                
                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documentos Excel recientes");
                throw;
            }
        }

        /// <summary>
        /// Elimina un documento Excel de la base de datos
        /// </summary>
        /// <param name="id">ID del documento</param>
        /// <returns>True si se eliminó correctamente, false si no existía</returns>
        public async Task<bool> DeleteExcelDocumentAsync(Guid id)
        {
            try
            {
                _logger.LogInformation($"Eliminando documento Excel con ID: {id}");
                
                var document = await _dbContext.ExcelDocuments.FindAsync(id);
                
                if (document == null)
                {
                    _logger.LogWarning($"No se encontró el documento Excel con ID: {id} para eliminar");
                    return false;
                }
                
                _dbContext.ExcelDocuments.Remove(document);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Documento Excel con ID: {id} eliminado correctamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar documento Excel con ID: {id}");
                throw;
            }
        }
    }
} 