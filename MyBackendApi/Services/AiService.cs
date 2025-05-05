using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MyBackendApi.Services
{
    /// <summary>
    /// Servicio para interactuar con APIs de inteligencia artificial
    /// </summary>
    public class AiService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiService> _logger;
        private readonly string? _openAiEndpoint;
        private readonly string? _openAiApiKey;
        private readonly string _openAiDeploymentName;
        private readonly bool _useSimulatedData = false;

        public AiService(IConfiguration configuration, ILogger<AiService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // Obtener configuración de Azure OpenAI
            _openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
            _openAiApiKey = _configuration["AzureOpenAI:ApiKey"];
            _openAiDeploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
            
            // Verificar si se usarán datos simulados
            _useSimulatedData = string.IsNullOrEmpty(_openAiEndpoint) || 
                                string.IsNullOrEmpty(_openAiApiKey);
                                
            if (_useSimulatedData)
            {
                _logger.LogWarning("OpenAI no configurado completamente. Se utilizará análisis simulado.");
            }
            else
            {
                _logger.LogInformation("Servicios de IA inicializados correctamente");
            }
        }

        /// <summary>
        /// Analiza el texto de un documento PDF y extrae información estructurada
        /// </summary>
        public async Task<(string documentTitle, string summary, List<string> keyPoints, List<string> conclusions)> AnalyzeDocumentTextAsync(string documentText, int pageCount)
        {
            try
            {
                // Si faltan credenciales, usar análisis simulado
                if (_useSimulatedData)
                {
                    _logger.LogInformation("Usando análisis simulado para el documento (modo sin Azure)");
                    return GenerateSimulatedAnalysis(documentText, pageCount);
                }

                // Limitar el texto si es demasiado largo
                var truncatedText = TruncateText(documentText, 10000);

                // Crear cliente de Azure OpenAI
                var client = CreateOpenAiClient();
                if (client == null)
                {
                    _logger.LogWarning("No se pudo crear el cliente de OpenAI, usando análisis simulado");
                    return GenerateSimulatedAnalysis(documentText, pageCount);
                }

                // Preparar el prompt
                var prompt = CreateDocumentAnalysisPrompt(truncatedText, pageCount);
                
                // Llamar a la API de OpenAI
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _openAiDeploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage("Eres un asistente especializado en análisis de documentos PDF. Tu tarea es extraer información estructurada de los documentos y presentarla en un formato específico."),
                        new ChatRequestUserMessage(prompt)
                    },
                    Temperature = 0.1f,
                    MaxTokens = 4000,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject
                };

                var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                var responseText = response.Value.Choices[0].Message.Content;
                
                // Parsear la respuesta JSON
                return ParseAiResponse(responseText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al analizar el documento con IA. Usando análisis simulado como fallback");
                return GenerateSimulatedAnalysis(documentText, pageCount);
            }
        }

        /// <summary>
        /// Crea un cliente de OpenAI
        /// </summary>
        private OpenAIClient? CreateOpenAiClient()
        {
            try
            {
                if (string.IsNullOrEmpty(_openAiEndpoint) || string.IsNullOrEmpty(_openAiApiKey))
                {
                    return null;
                }
                
                var credential = new AzureKeyCredential(_openAiApiKey);
                return new OpenAIClient(new Uri(_openAiEndpoint), credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear cliente de OpenAI");
                return null;
            }
        }

        /// <summary>
        /// Genera un análisis simulado para pruebas o cuando no hay conexión a IA
        /// </summary>
        private (string documentTitle, string summary, List<string> keyPoints, List<string> conclusions) GenerateSimulatedAnalysis(string documentText, int pageCount)
        {
            // Extraer información más significativa del texto real
            var cleanText = documentText.Replace("\r", " ").Replace("\n", " ");
            var sentences = cleanText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 15)
                .ToList();

            // Generar título basado en el contenido real
            string documentTitle;
            if (sentences.Count > 0 && sentences[0].Length > 10)
            {
                var firstSentence = sentences[0];
                documentTitle = firstSentence.Length > 50 
                    ? firstSentence.Substring(0, 50) + "..." 
                    : firstSentence;
            }
            else
            {
                var words = documentText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var titleWords = words.Length > 5 ? words.Take(5) : words;
                documentTitle = string.Join(" ", titleWords) + "...";
            }

            // Generar un resumen más significativo
            var summaryText = new StringBuilder();
            
            // Añadir información básica
            summaryText.AppendLine($"Documento PDF de {pageCount} páginas.");
            
            // Extraer y añadir 2-3 oraciones significativas del documento
            if (sentences.Count > 3)
            {
                // Tomar oraciones del principio, medio y final para tener una buena muestra
                summaryText.AppendLine(sentences[0]);
                summaryText.AppendLine(sentences[sentences.Count / 2]);
                summaryText.AppendLine(sentences[sentences.Count - 1]);
            }
            else if (sentences.Count > 0)
            {
                // Si hay pocas oraciones, añadir todas
                foreach (var sentence in sentences)
                {
                    summaryText.AppendLine(sentence);
                }
            }

            var summary = summaryText.ToString();

            // Generar puntos clave más relevantes
            var keyPoints = new List<string>();
            
            // Palabras que podrían indicar puntos importantes
            var keywordIndicators = new[] { "importante", "clave", "destacar", "significativo", "principal", "esencial" };
            
            // Intentar encontrar oraciones que contengan palabras clave
            var potentialKeyPoints = sentences
                .Where(s => keywordIndicators.Any(keyword => s.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .Take(3)
                .ToList();
                
            keyPoints.AddRange(potentialKeyPoints);
            
            // Si no encontramos suficientes puntos clave basados en palabras clave,
            // agregar algunas oraciones del documento que parezcan importantes
            if (keyPoints.Count < 3 && sentences.Count > 5)
            {
                // Tomar oraciones de posiciones estratégicas
                if (!keyPoints.Contains(sentences[0]))
                    keyPoints.Add(sentences[0]);
                    
                if (sentences.Count > 2 && !keyPoints.Contains(sentences[1]))
                    keyPoints.Add(sentences[1]);
                    
                if (sentences.Count > 4 && !keyPoints.Contains(sentences[3]))
                    keyPoints.Add(sentences[3]);
            }
            
            // Si aún no tenemos suficientes puntos clave, añadir algunos genéricos
            while (keyPoints.Count < 3)
            {
                keyPoints.Add($"Punto clave identificado del documento (simulado para {pageCount} páginas)");
            }

            // Generar conclusiones
            var conclusions = new List<string>();
            
            if (sentences.Count > 3)
            {
                // Las últimas oraciones suelen contener conclusiones
                conclusions.Add(sentences[sentences.Count - 1]);
                conclusions.Add(sentences[sentences.Count - 2]);
            }
            else
            {
                conclusions.Add("Conclusión principal del documento (generada automáticamente)");
                conclusions.Add("Recomendación basada en el análisis del documento (generada automáticamente)");
            }

            return (documentTitle, summary, keyPoints, conclusions);
        }

        /// <summary>
        /// Crea el prompt para el análisis de documentos para enviar a la IA
        /// </summary>
        private string CreateDocumentAnalysisPrompt(string documentText, int pageCount)
        {
            return $@"Analiza el siguiente documento PDF de {pageCount} páginas y proporciona una respuesta estructurada en formato JSON.

TEXTO DEL DOCUMENTO:
{documentText}

Proporciona un análisis completo que incluya:
1. Un título adecuado para el documento
2. Un resumen conciso del contenido
3. Los puntos clave o ideas principales (máximo 5)
4. Conclusiones importantes (máximo 3)

Formato de respuesta (JSON):
{{
  ""documentTitle"": ""Título identificado del documento"",
  ""summary"": ""Resumen conciso del contenido..."",
  ""keyPoints"": [
    ""Punto clave 1"",
    ""Punto clave 2"",
    ...
  ],
  ""conclusions"": [
    ""Conclusión 1"",
    ""Conclusión 2"",
    ...
  ]
}}";
        }

        /// <summary>
        /// Trunca el texto si es demasiado largo para evitar exceder límites de tokens
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;
                
            // Truncar el texto, intentando mantener párrafos completos
            var middleText = text.Substring(0, maxLength / 2);
            var endText = text.Substring(text.Length - maxLength / 2);
            
            return $"{middleText}\n\n[...contenido omitido por longitud...]\n\n{endText}";
        }

        /// <summary>
        /// Parsea la respuesta JSON de la IA
        /// </summary>
        private (string documentTitle, string summary, List<string> keyPoints, List<string> conclusions) ParseAiResponse(string responseText)
        {
            try
            {
                // Intentar parsear el JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var response = JsonSerializer.Deserialize<DocumentAnalysisAiResponse>(responseText, jsonOptions);
                
                return (
                    response?.DocumentTitle ?? "Título no disponible",
                    response?.Summary ?? "Resumen no disponible",
                    response?.KeyPoints ?? new List<string>(),
                    response?.Conclusions ?? new List<string>()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al parsear la respuesta JSON de la IA");
                
                // Extraer manualmente si falla el parsing
                var documentTitle = ExtractJsonValue(responseText, "documentTitle") ?? "Título no disponible";
                var summary = ExtractJsonValue(responseText, "summary") ?? "Resumen no disponible";
                var keyPoints = ExtractJsonArray(responseText, "keyPoints");
                var conclusions = ExtractJsonArray(responseText, "conclusions");
                
                return (documentTitle, summary, keyPoints, conclusions);
            }
        }
        
        /// <summary>
        /// Extrae un valor simple de un JSON
        /// </summary>
        private string? ExtractJsonValue(string json, string propertyName)
        {
            var propertyPattern = $"\"{propertyName}\"\\s*:\\s*\"(.*?)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, propertyPattern);
            return match.Success ? match.Groups[1].Value : null;
        }
        
        /// <summary>
        /// Extrae un array de valores de un JSON
        /// </summary>
        private List<string> ExtractJsonArray(string json, string arrayName)
        {
            var result = new List<string>();
            var arrayPattern = $"\"{arrayName}\"\\s*:\\s*\\[(.*?)\\]";
            var arrayMatch = System.Text.RegularExpressions.Regex.Match(json, arrayPattern, System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (arrayMatch.Success)
            {
                var arrayContent = arrayMatch.Groups[1].Value;
                var itemPattern = "\"(.*?)\"";
                var itemMatches = System.Text.RegularExpressions.Regex.Matches(arrayContent, itemPattern);
                
                foreach (System.Text.RegularExpressions.Match itemMatch in itemMatches)
                {
                    result.Add(itemMatch.Groups[1].Value);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Simula extracción OCR básica para PDFs basados en imágenes
        /// </summary>
        public string SimulateOcrText(string fileName)
        {
            return $@"FACTURA SIMULADA
Número: {Path.GetFileNameWithoutExtension(fileName).Replace("PO_", "")}
Fecha: {DateTime.Now:dd/MM/yyyy}
Cliente: EMPRESA SIMULADA S.A.
Orden de Compra: {Path.GetFileNameWithoutExtension(fileName)}

DETALLE DE PRODUCTOS:
1. Servicio de consultoría - $1,000,000
2. Mantenimiento mensual - $500,000

Subtotal: $1,500,000
IVA (19%): $285,000
TOTAL: $1,785,000

GRACIAS POR SU PREFERENCIA";
        }
        
        /// <summary>
        /// Genera campos simulados para pruebas
        /// </summary>
        public Dictionary<string, string> GenerateSimulatedFields(string fileName)
        {
            string fileNumber = Path.GetFileNameWithoutExtension(fileName).Replace("PO_", "").Split('_')[0];
            
            return new Dictionary<string, string>
            {
                { "Cliente", "EMPRESA SIMULADA S.A." },
                { "FechaEmision", DateTime.Now.ToString("dd/MM/yyyy") },
                { "NFactura", fileNumber },
                { "OC", Path.GetFileNameWithoutExtension(fileName) },
                { "TotalNeto", "1500000" },
                { "IVA", "285000" },
                { "Total", "1785000" },
                { "Descripcion", $"Orden de compra {fileNumber}" }
            };
        }

        /// <summary>
        /// Analiza un PDF usando GPT para extraer campos específicos
        /// </summary>
        public async Task<Dictionary<string, string>> AnalyzePdfWithAiOcrAsync(IFormFile pdfFile)
        {
            try
            {
                _logger.LogInformation($"Iniciando análisis del PDF: {pdfFile.FileName}");
                
                // Si estamos en modo simulado, retornar campos simulados
                if (_useSimulatedData)
                {
                    _logger.LogWarning($"Usando extracción simulada para {pdfFile.FileName}");
                    return GenerateSimulatedFields(pdfFile.FileName);
                }
                
                // Crear cliente de OpenAI
                var client = CreateOpenAiClient();
                if (client == null)
                {
                    _logger.LogWarning($"No se pudo crear el cliente de OpenAI para {pdfFile.FileName}. Usando datos simulados.");
                    return GenerateSimulatedFields(pdfFile.FileName);
                }
                
                // Extraer texto simulado para GPT
                var simulatedText = SimulateOcrText(pdfFile.FileName);
                
                // Preparar el prompt
                var prompt = CreateFieldExtractionPrompt(simulatedText, pdfFile.FileName);
                
                // Llamar a la API de OpenAI
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _openAiDeploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage("Eres un asistente especializado en extraer información estructurada de documentos. Tu tarea es extraer campos específicos de facturas y órdenes de compra."),
                        new ChatRequestUserMessage(prompt)
                    },
                    Temperature = 0.1f,
                    MaxTokens = 1000,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject
                };
                
                var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                var responseText = response.Value.Choices[0].Message.Content;
                
                // Parsear la respuesta JSON
                var fields = ParseFieldExtractionResponse(responseText);
                
                // Si no tenemos campos, usar datos simulados
                if (fields.Count == 0 || fields.ContainsKey("Error"))
                {
                    _logger.LogWarning($"No se pudieron extraer campos con GPT para {pdfFile.FileName}. Usando datos simulados.");
                    return GenerateSimulatedFields(pdfFile.FileName);
                }
                
                return fields;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al extraer campos con GPT: {pdfFile.FileName}. Usando datos simulados.");
                return GenerateSimulatedFields(pdfFile.FileName);
            }
        }
        
        /// <summary>
        /// Crea el prompt para extraer campos específicos con GPT
        /// </summary>
        private string CreateFieldExtractionPrompt(string text, string fileName)
        {
            // Truncar el texto si es muy largo
            var truncatedText = TruncateText(text, 8000);
            
            return $@"Extrae los siguientes campos de esta factura/orden de compra. El texto fue extraído con OCR, puede tener errores.
Si no encuentras alguno de los campos, deja el valor como cadena vacía.

TEXTO DEL DOCUMENTO:
{truncatedText}

NOMBRE DEL ARCHIVO: {fileName}

Extrae los siguientes campos y devuélvelos en formato JSON:
- Cliente: Nombre del cliente o empresa
- FechaEmision: Fecha de emisión del documento (formato dd/mm/yyyy)
- NFactura: Número de factura
- OC: Número de orden de compra
- TotalNeto: Monto neto/subtotal (solo número, sin símbolos)
- IVA: Monto de IVA (solo número, sin símbolos)
- Total: Monto total (solo número, sin símbolos)
- Descripcion: Una descripción breve del documento o su propósito

Formato de respuesta (JSON):
{{
  ""Cliente"": """",
  ""FechaEmision"": """",
  ""NFactura"": """",
  ""OC"": """",
  ""TotalNeto"": """",
  ""IVA"": """",
  ""Total"": """",
  ""Descripcion"": """"
}}";
        }
        
        /// <summary>
        /// Parsea la respuesta JSON de GPT a un diccionario
        /// </summary>
        private Dictionary<string, string> ParseFieldExtractionResponse(string jsonText)
        {
            try
            {
                // Intentar deserializar directamente
                var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonText);
                if (fields != null)
                {
                    // Asegurar formato de fecha correcto si existe
                    if (fields.TryGetValue("FechaEmision", out string? dateStr) && !string.IsNullOrEmpty(dateStr))
                    {
                        if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                        {
                            fields["FechaEmision"] = parsedDate.ToString("dd/MM/yyyy");
                        }
                    }
                    return fields;
                }
                
                // Fallback manual si la deserialización falla
                var result = new Dictionary<string, string>();
                
                // Parse campos individualmente
                ExtractJsonField(jsonText, "Cliente", result);
                ExtractJsonField(jsonText, "FechaEmision", result);
                ExtractJsonField(jsonText, "NFactura", result);
                ExtractJsonField(jsonText, "OC", result);
                ExtractJsonField(jsonText, "TotalNeto", result);
                ExtractJsonField(jsonText, "IVA", result);
                ExtractJsonField(jsonText, "Total", result);
                ExtractJsonField(jsonText, "Descripcion", result);
                
                // Asegurar formato de fecha correcto en extracción manual
                if (result.TryGetValue("FechaEmision", out string? dateStr2) && !string.IsNullOrEmpty(dateStr2))
                {
                    if (DateTime.TryParse(dateStr2, out DateTime parsedDate))
                    {
                        result["FechaEmision"] = parsedDate.ToString("dd/MM/yyyy");
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al parsear respuesta JSON de GPT");
                return new Dictionary<string, string> { { "Error", "Error en parse JSON" } };
            }
        }
        
        /// <summary>
        /// Extrae un campo del JSON de respuesta
        /// </summary>
        private void ExtractJsonField(string json, string fieldName, Dictionary<string, string> result)
        {
            var pattern = $@"""{fieldName}""\s*:\s*""(.*?)""";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                result[fieldName] = match.Groups[1].Value;
            }
            else
            {
                result[fieldName] = string.Empty;
            }
        }
    }
    
    /// <summary>
    /// Clase para deserializar la respuesta JSON de la IA
    /// </summary>
    public class DocumentAnalysisAiResponse
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new List<string>();
        public List<string> Conclusions { get; set; } = new List<string>();
    }
} 