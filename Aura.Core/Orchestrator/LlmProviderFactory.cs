using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Aura.Core.Configuration;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Factory for creating and managing LLM provider instances
/// </summary>
public class LlmProviderFactory
{
    private readonly ILogger<LlmProviderFactory> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderSettings _providerSettings;
    private readonly IKeyStore _keyStore;

    public LlmProviderFactory(
        ILogger<LlmProviderFactory> logger,
        IHttpClientFactory httpClientFactory,
        ProviderSettings providerSettings,
        IKeyStore keyStore)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _providerSettings = providerSettings;
        _keyStore = keyStore;
    }

    /// <summary>
    /// Creates all available LLM providers based on configuration
    /// </summary>
    public Dictionary<string, ILlmProvider> CreateAvailableProviders(ILoggerFactory loggerFactory)
    {
        var providers = new Dictionary<string, ILlmProvider>();

        // Always available: RuleBased provider (GUARANTEED - never allow this to fail)
        try
        {
            _logger.LogInformation("Attempting to register RuleBased provider...");
            providers["RuleBased"] = CreateRuleBasedProvider(loggerFactory);
            _logger.LogInformation("✓ RuleBased provider registered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "✗ CRITICAL: Failed to create RuleBased provider");
        }

        // Try to create Ollama provider (local)
        try
        {
            _logger.LogInformation("Attempting to register Ollama provider...");
            var ollamaProvider = CreateOllamaProvider(loggerFactory);
            if (ollamaProvider != null)
            {
                providers["Ollama"] = ollamaProvider;
                _logger.LogInformation("✓ Ollama provider registered successfully");
            }
            else
            {
                _logger.LogDebug("✗ Ollama provider not available (returned null)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "✗ Ollama provider registration failed");
        }

        // Load API keys from secure store
        Dictionary<string, string> apiKeys;
        try
        {
            // Ensure we always have the latest keys (handles updates during runtime)
            _keyStore.Reload();
            apiKeys = _keyStore.GetAllKeys();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load API keys from secure storage, continuing with local/RuleBased providers only");
            apiKeys = new Dictionary<string, string>();
        }

        // Try to create Pro providers if API keys are available
        try
        {
            if (apiKeys.TryGetValue("openai", out var openAiKey) && !string.IsNullOrWhiteSpace(openAiKey))
            {
                _logger.LogInformation("Attempting to register OpenAI provider...");
                var openAiProvider = CreateOpenAiProvider(loggerFactory, openAiKey);
                if (openAiProvider != null)
                {
                    providers["OpenAI"] = openAiProvider;
                    _logger.LogInformation("✓ OpenAI provider registered successfully");
                }
                else
                {
                    _logger.LogDebug("✗ OpenAI provider not available (returned null)");
                }
            }
            else
            {
                _logger.LogDebug("✗ OpenAI provider skipped (no API key configured)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "✗ OpenAI provider registration failed");
        }

        try
        {
            if (apiKeys.TryGetValue("azure_openai_key", out var azureKey) && 
                apiKeys.TryGetValue("azure_openai_endpoint", out var azureEndpoint) &&
                !string.IsNullOrWhiteSpace(azureKey) && 
                !string.IsNullOrWhiteSpace(azureEndpoint))
            {
                _logger.LogInformation("Attempting to register Azure OpenAI provider...");
                var azureProvider = CreateAzureOpenAiProvider(loggerFactory, azureKey, azureEndpoint);
                if (azureProvider != null)
                {
                    providers["Azure"] = azureProvider;
                    _logger.LogInformation("✓ Azure OpenAI provider registered successfully");
                }
                else
                {
                    _logger.LogDebug("✗ Azure OpenAI provider not available (returned null)");
                }
            }
            else
            {
                _logger.LogDebug("✗ Azure OpenAI provider skipped (missing API key or endpoint)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "✗ Azure OpenAI provider registration failed");
        }

        try
        {
            if (apiKeys.TryGetValue("gemini", out var geminiKey) && !string.IsNullOrWhiteSpace(geminiKey))
            {
                _logger.LogInformation("Attempting to register Gemini provider...");
                var geminiProvider = CreateGeminiProvider(loggerFactory, geminiKey);
                if (geminiProvider != null)
                {
                    providers["Gemini"] = geminiProvider;
                    _logger.LogInformation("✓ Gemini provider registered successfully");
                }
                else
                {
                    _logger.LogDebug("✗ Gemini provider not available (returned null)");
                }
            }
            else
            {
                _logger.LogDebug("✗ Gemini provider skipped (no API key configured)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "✗ Gemini provider registration failed");
        }

        _logger.LogInformation("========================================");
        _logger.LogInformation("Registered {Count} LLM providers: {Providers}", 
            providers.Count, string.Join(", ", providers.Keys));
        _logger.LogInformation("========================================");

        return providers;
    }

    private ILlmProvider CreateRuleBasedProvider(ILoggerFactory loggerFactory)
    {
        var type = Type.GetType("Aura.Providers.Llm.RuleBasedLlmProvider, Aura.Providers");
        if (type == null)
        {
            throw new Exception("RuleBasedLlmProvider type not found");
        }

        // Use generic CreateLogger<T> method via reflection
        var createLoggerMethod = typeof(LoggerFactoryExtensions)
            .GetMethod("CreateLogger", new[] { typeof(ILoggerFactory) });
        if (createLoggerMethod == null)
        {
            throw new Exception("CreateLogger<T> method not found");
        }
        
        var genericMethod = createLoggerMethod.MakeGenericMethod(type);
        var logger = genericMethod.Invoke(null, new object[] { loggerFactory });
        
        return (ILlmProvider)Activator.CreateInstance(type, logger)!;
    }

    private ILlmProvider? CreateOllamaProvider(ILoggerFactory loggerFactory)
    {
        var ollamaUrl = _providerSettings.GetOllamaUrl();
        var ollamaModel = _providerSettings.GetOllamaModel();
        var httpClient = _httpClientFactory.CreateClient();
        
        var type = Type.GetType("Aura.Providers.Llm.OllamaLlmProvider, Aura.Providers");
        if (type == null)
        {
            _logger.LogWarning("OllamaLlmProvider type not found");
            return null;
        }

        // Use generic CreateLogger<T> method via reflection
        var createLoggerMethod = typeof(LoggerFactoryExtensions)
            .GetMethod("CreateLogger", new[] { typeof(ILoggerFactory) });
        if (createLoggerMethod == null)
        {
            _logger.LogWarning("CreateLogger<T> method not found");
            return null;
        }
        
        var genericMethod = createLoggerMethod.MakeGenericMethod(type);
        var logger = genericMethod.Invoke(null, new object[] { loggerFactory });

        return (ILlmProvider)Activator.CreateInstance(
            type, 
            logger, 
            httpClient, 
            ollamaUrl, 
            ollamaModel, 
            2, // maxRetries
            120 // timeoutSeconds
        )!;
    }

    private ILlmProvider? CreateOpenAiProvider(ILoggerFactory loggerFactory, string apiKey)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        var type = Type.GetType("Aura.Providers.Llm.OpenAiLlmProvider, Aura.Providers");
        if (type == null)
        {
            _logger.LogWarning("OpenAiLlmProvider type not found");
            return null;
        }

        // Use generic CreateLogger<T> method via reflection
        var createLoggerMethod = typeof(LoggerFactoryExtensions)
            .GetMethod("CreateLogger", new[] { typeof(ILoggerFactory) });
        if (createLoggerMethod == null)
        {
            _logger.LogWarning("CreateLogger<T> method not found");
            return null;
        }
        
        var genericMethod = createLoggerMethod.MakeGenericMethod(type);
        var logger = genericMethod.Invoke(null, new object[] { loggerFactory });

        return (ILlmProvider)Activator.CreateInstance(
            type,
            logger,
            httpClient,
            apiKey,
            "gpt-4o-mini"
        )!;
    }

    private ILlmProvider? CreateAzureOpenAiProvider(ILoggerFactory loggerFactory, string apiKey, string endpoint)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        var type = Type.GetType("Aura.Providers.Llm.AzureOpenAiLlmProvider, Aura.Providers");
        if (type == null)
        {
            _logger.LogWarning("AzureOpenAiLlmProvider type not found");
            return null;
        }

        // Use generic CreateLogger<T> method via reflection
        var createLoggerMethod = typeof(LoggerFactoryExtensions)
            .GetMethod("CreateLogger", new[] { typeof(ILoggerFactory) });
        if (createLoggerMethod == null)
        {
            _logger.LogWarning("CreateLogger<T> method not found");
            return null;
        }
        
        var genericMethod = createLoggerMethod.MakeGenericMethod(type);
        var logger = genericMethod.Invoke(null, new object[] { loggerFactory });

        return (ILlmProvider)Activator.CreateInstance(
            type,
            logger,
            httpClient,
            apiKey,
            endpoint,
            "gpt-4"
        )!;
    }

    private ILlmProvider? CreateGeminiProvider(ILoggerFactory loggerFactory, string apiKey)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        var type = Type.GetType("Aura.Providers.Llm.GeminiLlmProvider, Aura.Providers");
        if (type == null)
        {
            _logger.LogWarning("GeminiLlmProvider type not found");
            return null;
        }

        // Use generic CreateLogger<T> method via reflection
        var createLoggerMethod = typeof(LoggerFactoryExtensions)
            .GetMethod("CreateLogger", new[] { typeof(ILoggerFactory) });
        if (createLoggerMethod == null)
        {
            _logger.LogWarning("CreateLogger<T> method not found");
            return null;
        }
        
        var genericMethod = createLoggerMethod.MakeGenericMethod(type);
        var logger = genericMethod.Invoke(null, new object[] { loggerFactory });

        return (ILlmProvider)Activator.CreateInstance(
            type,
            logger,
            httpClient,
            apiKey,
            "gemini-pro"
        )!;
    }
}
