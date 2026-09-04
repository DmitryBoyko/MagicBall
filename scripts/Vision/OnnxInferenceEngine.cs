using System.Reflection;
using System.Runtime.InteropServices;
using CrystalBall.App;
using Godot;
using Microsoft.ML.OnnxRuntime;

namespace CrystalBall.Vision;

/// <summary>
/// Синглтон сессии MobileNetV2. Прогрев на экране загрузки, native .so защищены от DllNotFoundException.
/// </summary>
public partial class OnnxInferenceEngine : Node
{
    public const string PackedModelPath = "res://models/mobilenetv2-7.onnx";
    public const string UserModelPath = "user://models/mobilenetv2-7.onnx";
    public const string ModelFileName = "mobilenetv2-7.onnx";

    public static OnnxInferenceEngine? Instance { get; private set; }

    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Task? _initTask;
    private string _inputName = "data";
    private string _outputName = "mobilenetv20_output_flatten0_reshape0";

    public InferenceSession? Session => _session;
    public bool IsAvailable => _session != null;
    public bool IsInitialized { get; private set; }
    public string? LastError { get; private set; }
    public string InputName => _inputName;
    public string OutputName => _outputName;
    public string ExecutionProvider { get; private set; } = "None";

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        DisposeSession();
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Один раз на экране загрузки: копия модели в user://, NNAPI/CPU, прогревочный прогон.
    /// </summary>
    public Task InitializeEngineAsync()
    {
        if (_initTask != null)
            return _initTask;

        _initTask = InitializeEngineCoreAsync();
        return _initTask;
    }

    private async Task InitializeEngineCoreAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(true);
        try
        {
            if (IsInitialized && _session != null)
                return;

            LastError = null;
            // FileAccess — на main; тяжёлый InferenceSession — в фоне.
            var osPath = EnsureModelOnDisk();
            if (string.IsNullOrEmpty(osPath))
            {
                LastError = "Файл mobilenetv2-7.onnx не найден в res://models и user://models.";
                GD.PushWarning($"[OnnxInferenceEngine] {LastError}");
                IsInitialized = true;
                return;
            }

            try
            {
                var options = CreateSessionOptions();
                var provider = ExecutionProvider;
                var built = await Task.Run(() =>
                {
                    var session = new InferenceSession(osPath, options);
                    string input = "data";
                    string output = "mobilenetv20_output_flatten0_reshape0";
                    try
                    {
                        using var enIn = session.InputMetadata.Keys.GetEnumerator();
                        if (enIn.MoveNext())
                            input = enIn.Current;
                        using var enOut = session.OutputMetadata.Keys.GetEnumerator();
                        if (enOut.MoveNext())
                            output = enOut.Current;
                    }
                    catch
                    {
                        // keep defaults
                    }

                    WarmupGraph(session);
                    return (session, input, output, provider);
                }).ConfigureAwait(true);

                _session = built.session;
                _sessionOptions = options;
                _inputName = built.input;
                _outputName = built.output;
                IsInitialized = true;
                GD.Print($"[OnnxInferenceEngine] Сессия готова. Провайдер: {ExecutionProvider}. Вход: {_inputName}");
            }
            catch (DllNotFoundException ex)
            {
                HandleNativeFailure("DllNotFoundException", ex);
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException inner)
            {
                HandleNativeFailure("TypeInitializationException/DllNotFoundException", inner);
            }
            catch (BadImageFormatException ex)
            {
                HandleNativeFailure("BadImageFormatException", ex);
            }
            catch (OnnxRuntimeException ex)
            {
                HandleNativeFailure("OnnxRuntimeException", ex);
            }
            catch (Exception ex)
            {
                HandleNativeFailure(ex.GetType().Name, ex);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public InferenceSession RequireSession()
    {
        if (_session == null)
            throw new InvalidOperationException(LastError ?? "ONNX-сессия не инициализирована.");
        return _session;
    }

    private void HandleNativeFailure(string kind, Exception ex)
    {
        DisposeSession();
        LastError = $"{kind}: native ONNX Runtime недоступен ({ex.Message}). Инференс отключён, гадание идёт без фото-тега.";
        IsInitialized = true;
        ExecutionProvider = "Unavailable";
        GD.PushError($"[OnnxInferenceEngine] {LastError}");
    }

    private static string? EnsureModelOnDisk()
    {
        if (FileAccess.FileExists(UserModelPath) && FileAccess.GetFileAsBytes(UserModelPath) is { Length: > 0 })
            return ProjectSettings.GlobalizePath(UserModelPath);

        if (!FileAccess.FileExists(PackedModelPath))
            return FileAccess.FileExists(UserModelPath) ? ProjectSettings.GlobalizePath(UserModelPath) : null;

        var userDir = ProjectSettings.GlobalizePath("user://models");
        DirAccess.MakeDirRecursiveAbsolute(userDir);

        using var source = FileAccess.Open(PackedModelPath, FileAccess.ModeFlags.Read);
        using var dest = FileAccess.Open(UserModelPath, FileAccess.ModeFlags.Write);
        if (source == null || dest == null)
            return null;

        dest.StoreBuffer(source.GetBuffer((long)source.GetLength()));
        dest.Flush();
        return ProjectSettings.GlobalizePath(UserModelPath);
    }

    private SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = 2,
            InterOpNumThreads = 1,
        };

        if (TryAppendAndroidNnapi(options))
        {
            ExecutionProvider = "NNAPI";
            return options;
        }

        TryAppendCpu(options);
        ExecutionProvider = "CPU";
        return options;
    }

    private static bool TryAppendAndroidNnapi(SessionOptions options)
    {
        if (!IsAndroidBuild() && !OperatingSystem.IsAndroid())
            return false;

#if GODOT_ANDROID || ANDROID || __ANDROID__
        try
        {
            options.AppendExecutionProvider_Nnapi();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
#else
        try
        {
            var method = typeof(SessionOptions).GetMethod("AppendExecutionProvider_Nnapi", Type.EmptyTypes);
            if (method == null)
                return false;
            method.Invoke(options, null);
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
#endif
    }

    private static void TryAppendCpu(SessionOptions options)
    {
        try
        {
            options.AppendExecutionProvider_CPU();
        }
        catch (Exception)
        {
            // Дефолтный провайдер сессии — CPU, даже если явный Append упал.
        }
    }

    private static bool IsAndroidBuild()
    {
#if GODOT_ANDROID || ANDROID || __ANDROID__
        return true;
#else
        try
        {
            return OS.GetName().Equals("Android", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID"));
        }
#endif
    }

    private void CacheIoNames(InferenceSession session)
    {
        if (session.InputMetadata.Count > 0)
            _inputName = session.InputMetadata.Keys.First();
        if (session.OutputMetadata.Count > 0)
            _outputName = session.OutputMetadata.Keys.First();
    }

    private static void WarmupGraph(InferenceSession session)
    {
        var zeros = new float[AppConfig.TensorLength];
        OrtValue? inputValue = null;
        RunOptions? runOptions = null;
        IDisposableReadOnlyCollection<OrtValue>? results = null;
        try
        {
            var inputName = session.InputMetadata.Keys.First();
            var outputName = session.OutputMetadata.Keys.First();
            inputValue = OrtValue.CreateTensorValueFromMemory(zeros, [1, 3, AppConfig.ImageSize, AppConfig.ImageSize]);
            runOptions = new RunOptions();
            results = session.Run(runOptions, [inputName], [inputValue], [outputName]);
        }
        finally
        {
            results?.Dispose();
            inputValue?.Dispose();
            runOptions?.Dispose();
        }
    }

    private void DisposeSession()
    {
        try
        {
            _session?.Dispose();
        }
        catch (Exception)
        {
            // native dispose не должен ронять выход
        }

        try
        {
            _sessionOptions?.Dispose();
        }
        catch (Exception)
        {
        }

        _session = null;
        _sessionOptions = null;
    }
}
