using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace OContabil.Services;

/// <summary>
/// Motor de inferência nativo ONNX para GLiNER 2.
/// </summary>
public class GlinerOnnxService : IDisposable
{
    private readonly string _modelPath;
    private readonly string _configPath;
    private readonly string _vocabPath;
    private InferenceSession? _session;
    private readonly List<string> _diagnosticLogs = new();

    public string DiagnosticReport => string.Join("\n", _diagnosticLogs);
    public bool IsModelAvailable => File.Exists(_modelPath);

    public GlinerOnnxService(string? modelDirectory = null)
    {
        var modelDir = modelDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        _modelPath = Path.Combine(modelDir, "model.onnx");
        _configPath = Path.Combine(modelDir, "config.json");
        _vocabPath = Path.Combine(modelDir, "tokenizer.json");
        
        Log($"Iniciando serviço ONNX. Pasta de modelos: {modelDir}");
        if (!IsModelAvailable) Log($"AVISO: Arquivo do modelo não encontrado em {_modelPath}");
    }

    private void Log(string msg) => _diagnosticLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");

    public void LoadModel()
    {
        try
        {
            if (!IsModelAvailable) return;
            
            Log("Carregando sessão ONNX...");
            _session = new InferenceSession(_modelPath);
            Log("Sessão ONNX carregada com sucesso.");
            
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                Log("Configuração do modelo carregada.");
            }
        }
        catch (Exception ex)
        {
            Log($"ERRO ao carregar modelo: {ex.Message}");
            throw;
        }
    }

    public async Task<GlinerResult> PredictAsync(
        string text, 
        string[] labels, 
        float threshold = 0.5f,
        bool flatNer = false)
    {
        if (_session == null) LoadModel();
        
        // TODO: Implementar tokenização quando a biblioteca ML.Tokenizers estiver estabilizada
        return await Task.FromResult(new GlinerResult
        {
            Success = false,
            Error = "Integração do Tokenizer nativo pendente. Usando motor Python por enquanto."
        });
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
