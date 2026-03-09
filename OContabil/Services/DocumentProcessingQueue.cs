using System.Collections.Concurrent;
using System.Windows;
using OContabil.Data;
using OContabil.Models;
using System.Text.Json;

namespace OContabil.Services;

/// <summary>
/// Background document processing queue. Runs AI analysis on a separate thread
/// so the UI never freezes. Documents are enqueued and processed one at a time.
/// </summary>
public sealed class DocumentProcessingQueue
{
    private static DocumentProcessingQueue? _instance;
    public static DocumentProcessingQueue Instance => _instance ??= new DocumentProcessingQueue();

    private readonly ConcurrentQueue<QueueItem> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly GlinerService _gliner = new();
    private readonly GlinerOnnxService _onnx = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isRunning;
    private bool _isNativeReady;

    public int PendingCount => _queue.Count;

    /// <summary>
    /// Event fired when a document finishes processing (success or error).
    /// Called on UI thread.
    /// </summary>
    public event Action<int>? DocumentProcessed;

    private DocumentProcessingQueue()
    {
        // Try to initialize native ONNX
        try
        {
            if (_onnx.IsModelAvailable)
            {
                _onnx.LoadModel();
                _isNativeReady = true;
            }
        }
        catch { /* Fallback to Python is handled in ProcessItem */ }

        // Start the background consumer thread
        StartConsumer();
    }

    /// <summary>
    /// Enqueue a document for AI processing. Returns immediately.
    /// </summary>
    public void Enqueue(int docId, string filePath, string docType)
    {
        // Mark as Processing immediately in DB
        try
        {
            using var db = new AppDbContext();
            var doc = db.Documents.Find(docId);
            if (doc != null)
            {
                doc.Status = DocumentStatus.Processing;
                doc.OcrText = null;
                doc.ExtractedJson = null;
                doc.ConfidenceScore = null;
                doc.ProcessedAt = null;
                db.SaveChanges();
            }
        }
        catch { /* DB error will be caught in processing */ }

        _queue.Enqueue(new QueueItem(docId, filePath, docType));
        _signal.Release(); // Signal the consumer

        Application.Current?.Dispatcher.Invoke(() =>
            ToastService.ShowInfo($"Documento enfileirado para processamento ({_queue.Count} na fila)."));
    }

    private void StartConsumer()
    {
        if (_isRunning) return;
        _isRunning = true;

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Wait for a signal (new item in queue)
                await _signal.WaitAsync(_cts.Token);

                if (_queue.TryDequeue(out var item))
                {
                    await ProcessItemAsync(item);
                    
                    // Notify UI thread that processing is done
                    Application.Current?.Dispatcher.Invoke(() =>
                        DocumentProcessed?.Invoke(item.DocId));
                }
            }
        }, _cts.Token);
    }

    private async Task ProcessItemAsync(QueueItem item)
    {
        // 1. Check if we can use native ONNX
        bool useNative = _isNativeReady && _onnx.IsModelAvailable;
        
        // 2. Fallback check for Python
        if (!useNative && (!_gliner.IsPythonAvailable || !_gliner.IsScriptAvailable))
        {
            MarkAsError(item.DocId, _gliner.IsPythonAvailable
                ? "Motores de IA (ONNX/Python) indisponíveis."
                : "Python não instalado e modelo ONNX não encontrado.");
            return;
        }

        try
        {
            float threshold = (float)AppSettings.GlinerThreshold;
            GlinerResult result;

            if (useNative)
            {
                // Prioritize Native ONNX (faster, self-contained)
                // We need to provide the labels based on document type
                var labels = GetLabelsForType(item.DocType);
                
                // OCR is still needed as ONNX expects text
                // Since OCR logic is in the Python bridge, we use its basic OCR capabilities
                // if the document isn't plain text already.
                string textToProcess = await GetTextForProcessing(item.FilePath);
                
                result = await _onnx.PredictAsync(textToProcess, labels, threshold);
            }
            else
            {
                // Fallback to Python Bridge
                result = await _gliner.ProcessFileAsync(item.FilePath, item.DocType);
            }

            using var db = new AppDbContext();
            var doc = db.Documents.Find(item.DocId);
            if (doc == null) return;

            doc.ProcessedAt = DateTime.Now;

            if (result.Success)
            {
                // ── USE REAL CONFIDENCE FROM GLINER ──
                // The Python script now returns avg_confidence from GLiNER itself.
                // Use it if available; fallback to extraction-key-based heuristic.
                double confidence;
                if (result.AvgConfidence > 0)
                {
                    // Real GLiNER confidence!
                    confidence = result.AvgConfidence;
                }
                else
                {
                    // Fallback: key-count heuristic (for regex fallback mode)
                    confidence = 0.50;
                    if (result.Extraction.HasValue && result.Extraction.Value.ValueKind == JsonValueKind.Object)
                    {
                        var root = result.Extraction.Value;
                        int keys = root.EnumerateObject().Count();
                        
                        // Se houver apenas 1 chave (ex: "nota_fiscal"), conte o que está dentro dela
                        if (keys == 1)
                        {
                            var firstProp = root.EnumerateObject().First().Value;
                            if (firstProp.ValueKind == JsonValueKind.Object)
                                keys = firstProp.EnumerateObject().Count();
                            else if (firstProp.ValueKind == JsonValueKind.Array)
                                keys = firstProp.GetArrayLength();
                        }
                        
                        confidence = Math.Min(0.96, 0.60 + (keys * 0.05));
                    }
                }

                bool isCrossValidated = false;
                var api = new BrasilApiService();

                // ── CROSS VALIDATION: Boleto ──
                if (doc.DocumentType == "Boleto" && !string.IsNullOrEmpty(result.OcrText))
                {
                    var digitsOnly = System.Text.RegularExpressions.Regex.Replace(result.OcrText, @"\D", "");
                    var matches = System.Text.RegularExpressions.Regex.Matches(digitsOnly, @"\d{47,48}");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var boletoInfo = BrasilApiService.ParseBoleto(match.Value);
                        if (boletoInfo != null && boletoInfo.IsValid)
                        {
                            isCrossValidated = true;
                            doc.ExtractedJson = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                Valor = boletoInfo.Value,
                                VencimentoOriginal = boletoInfo.DueDate?.ToString("yyyy-MM-dd"),
                                LinhaDigitavel = match.Value,
                                Banco = boletoInfo.BankCode
                            });
                            break;
                        }
                    }
                }

                // ── CROSS VALIDATION: CNPJ ──
                if (!isCrossValidated && !string.IsNullOrEmpty(result.OcrText))
                {
                    var cnpjs = System.Text.RegularExpressions.Regex.Matches(result.OcrText, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}");
                    foreach (System.Text.RegularExpressions.Match match in cnpjs)
                    {
                        try
                        {
                            var apiResult = await api.LookupCnpjAsync(match.Value);
                            if (apiResult != null && !string.IsNullOrEmpty(apiResult.RazaoSocial) && !apiResult.RazaoSocial.StartsWith("ERRO_API"))
                            {
                                var mainName = apiResult.RazaoSocial.Split(' ').First();
                                if (result.OcrText.Contains(mainName, StringComparison.OrdinalIgnoreCase))
                                {
                                    isCrossValidated = true;
                                    break;
                                }
                            }
                        }
                        catch { /* API failure ≠ processing failure */ }
                    }
                }

                // ── AUTO CATEGORIZATION ──
                if (!string.IsNullOrEmpty(result.OcrText))
                {
                    var accounts = db.Accounts.Where(a => !string.IsNullOrEmpty(a.AutoClassificationRegex)).ToList();
                    foreach (var acc in accounts)
                    {
                        try
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(result.OcrText, acc.AutoClassificationRegex!))
                            {
                                doc.ChartOfAccountId = acc.Id;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // ── FINAL STATUS ──
                // O threshold ja foi obtido no inicio do metodo
                
                if (isCrossValidated)
                {
                    doc.Status = DocumentStatus.Validated;
                    doc.ConfidenceScore = 1.0;
                }
                else if (confidence >= (double)threshold)
                {
                    doc.Status = DocumentStatus.Validated;
                    doc.ConfidenceScore = confidence;
                }
                else
                {
                    doc.Status = DocumentStatus.ReadyForReview;
                    doc.ConfidenceScore = confidence;
                }

                doc.OcrText = result.OcrText;
                if (!isCrossValidated) doc.ExtractedJson = result.Extraction?.ToString();
            }
            else
            {
                doc.Status = DocumentStatus.Error;
                doc.OcrText = result.Error;
            }

            db.SaveChanges();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var statusMsg = doc.Status switch
                {
                    DocumentStatus.Validated => $"✓ Validado ({doc.ConfidenceScore:P0})",
                    DocumentStatus.ReadyForReview => $"⚠ Para revisão ({doc.ConfidenceScore:P0})",
                    DocumentStatus.Error => "✗ Erro",
                    _ => doc.StatusDisplay
                };
                ToastService.ShowInfo($"{doc.Filename}: {statusMsg}");
            });
        }
        catch (Exception ex)
        {
            MarkAsError(item.DocId, $"Erro no processamento: {ex.Message}");
        }
    }

    private static void MarkAsError(int docId, string errorMessage)
    {
        try
        {
            using var db = new AppDbContext();
            var doc = db.Documents.Find(docId);
            if (doc != null)
            {
                doc.Status = DocumentStatus.Error;
                doc.OcrText = errorMessage;
                doc.ProcessedAt = DateTime.Now;
                db.SaveChanges();
            }
        }
        catch { }

        Application.Current?.Dispatcher.Invoke(() =>
            ToastService.ShowError($"Erro no processamento do documento: {errorMessage}"));
    }

    public void Shutdown()
    {
        _cts.Cancel();
    }

    private async Task<string> GetTextForProcessing(string filePath)
    {
        // Simple helper to get text.
        try { return await System.IO.File.ReadAllTextAsync(filePath); } catch { return ""; }
    }

    private string[] GetLabelsForType(string docType)
    {
        return docType switch
        {
            "Boleto" => new[] { "valor", "vencimento", "beneficiario", "pagador", "codigo_barras" },
            "NotaFiscal" => new[] { "cnpj_emitente", "nome_emitente", "valor_total", "data_emissao", "numero_nota" },
            _ => new[] { "entidade", "data", "valor", "organizacao" }
        };
    }

    private record QueueItem(int DocId, string FilePath, string DocType);
}
