using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace OContabil.Services;

public class GlinerService
{
    private readonly string _scriptPath;
    private readonly string _pythonPath;

    /// <summary>
    /// Maximum time to wait for the Python process to complete (in seconds).
    /// The large model can take up to 90+ seconds to download on first use.
    /// </summary>
    private const int PROCESS_TIMEOUT_SECONDS = 120;

    public GlinerService()
    {
        _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "gliner_bridge.py");

        // Also check relative to project directory for development
        if (!File.Exists(_scriptPath))
        {
            var devPath = Path.Combine(
                Directory.GetCurrentDirectory(), "Scripts", "gliner_bridge.py");
            if (File.Exists(devPath))
                _scriptPath = devPath;
        }

        // Try to find Python
        _pythonPath = FindPython();
    }

    public bool IsPythonAvailable => !string.IsNullOrEmpty(_pythonPath);
    public bool IsScriptAvailable => File.Exists(_scriptPath);

    private static string FindPython()
    {
        // Known local installations first, then system PATH
        string[] candidates = [
            @"C:\Users\Oxta\Desktop\Python-3.11.15\python.exe",
            @"C:\Users\Oxta\Desktop\Python-3.11.15\Scripts\python.exe",
            "python",
            "python3",
            "py"
        ];

        foreach (var cmd in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0)
                    return cmd;
            }
            catch { }
        }
        return "";
    }

    public async Task<GlinerResult> ProcessFileAsync(string filePath, string docType = "")
    {
        if (!IsPythonAvailable)
        {
            return new GlinerResult
            {
                Success = false,
                Error = "Python nao encontrado. Instale Python 3.10+ e execute: pip install -r Scripts/requirements.txt"
            };
        }

        if (!IsScriptAvailable)
        {
            return new GlinerResult
            {
                Success = false,
                Error = $"Script nao encontrado: {_scriptPath}"
            };
        }

        try
        {
            var modelName = AppSettings.GlinerModelName;
            var threshold = AppSettings.GlinerThreshold;
            
            // Build args: script_path file_path doc_type model_name threshold
            var args = $"\"{_scriptPath}\" \"{filePath}\"";
            args += $" \"{docType}\"";
            args += $" \"{modelName}\"";
            args += $" {threshold:F2}";

            var psi = new ProcessStartInfo(_pythonPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_scriptPath) ?? ""
            };
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            var process = Process.Start(psi)!;
            
            // Read output asynchronously
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // TIMEOUT PROTECTION: Kill process if it takes too long
            var completed = await Task.Run(() => process.WaitForExit(PROCESS_TIMEOUT_SECONDS * 1000));
            
            if (!completed)
            {
                // Process is stuck (likely downloading large model or out of memory)
                try { process.Kill(entireProcessTree: true); } catch { }
                
                return new GlinerResult
                {
                    Success = false,
                    Error = $"Timeout: o modelo '{modelName}' demorou mais de {PROCESS_TIMEOUT_SECONDS}s para processar. " +
                            "Possíveis causas: modelo grande sendo baixado pela primeira vez, falta de RAM, ou erro no script Python. " +
                            "Tente novamente ou use o modelo base."
                };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Extract only the JSON part from stdout
            int jsonStart = stdout.IndexOf('{');
            int jsonEnd = stdout.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                stdout = stdout.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            if (string.IsNullOrWhiteSpace(stdout) || !stdout.StartsWith("{"))
            {
                return new GlinerResult
                {
                    Success = false,
                    Error = $"Sem resposta válida do processador (code {process.ExitCode}): {stderr}\nOutput: {stdout}"
                };
            }

            var result = JsonSerializer.Deserialize<GlinerResult>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new GlinerResult { Success = false, Error = "Falha ao deserializar resultado" };
        }
        catch (Exception ex)
        {
            return new GlinerResult
            {
                Success = false,
                Error = $"Erro ao processar: {ex.Message}"
            };
        }
    }
}
