using System;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.Generation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class ScriptCacheServiceTests
{
    private readonly ScriptCacheService _cacheService;

    public ScriptCacheServiceTests()
    {
        _cacheService = new ScriptCacheService(NullLogger<ScriptCacheService>.Instance);
    }

    [Fact]
    public void GetCachedScript_ReturnsNullWhenNotCached()
    {
        var brief = new Brief("Test Topic", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");

        var cached = _cacheService.GetCachedScript(brief, planSpec, "OpenAI", "gpt-4");

        Assert.Null(cached);
    }

    [Fact]
    public void CacheScript_AndRetrieve_ReturnsScript()
    {
        var brief = new Brief("Test Topic", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");
        
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene { Number = 1, Narration = "Test", Duration = TimeSpan.FromSeconds(5), Transition = TransitionType.Cut }
            },
            TotalDuration = TimeSpan.FromSeconds(5),
            Metadata = new ScriptMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                ProviderName = "OpenAI",
                ModelUsed = "gpt-4"
            }
        };

        _cacheService.CacheScript(brief, planSpec, "OpenAI", "gpt-4", script);

        var cached = _cacheService.GetCachedScript(brief, planSpec, "OpenAI", "gpt-4");

        Assert.NotNull(cached);
        Assert.Equal("Test Script", cached.Title);
    }

    [Fact]
    public void ClearCache_RemovesAllEntries()
    {
        var brief = new Brief("Test Topic", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");
        
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new(),
            Metadata = new ScriptMetadata { ProviderName = "OpenAI" }
        };

        _cacheService.CacheScript(brief, planSpec, "OpenAI", "gpt-4", script);
        _cacheService.ClearCache();

        var cached = _cacheService.GetCachedScript(brief, planSpec, "OpenAI", "gpt-4");

        Assert.Null(cached);
    }

    [Fact]
    public void ClearProviderCache_RemovesOnlyProviderEntries()
    {
        var brief = new Brief("Test Topic", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");
        
        var script1 = new Script { Title = "OpenAI Script", Scenes = new(), Metadata = new ScriptMetadata { ProviderName = "OpenAI" } };
        var script2 = new Script { Title = "RuleBased Script", Scenes = new(), Metadata = new ScriptMetadata { ProviderName = "RuleBased" } };

        _cacheService.CacheScript(brief, planSpec, "OpenAI", "gpt-4", script1);
        _cacheService.CacheScript(brief, planSpec, "RuleBased", "template", script2);

        _cacheService.ClearProviderCache("OpenAI");

        var cached1 = _cacheService.GetCachedScript(brief, planSpec, "OpenAI", "gpt-4");
        var cached2 = _cacheService.GetCachedScript(brief, planSpec, "RuleBased", "template");

        Assert.Null(cached1);
        Assert.NotNull(cached2);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        var brief = new Brief("Test Topic", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");
        
        var script = new Script { Title = "Test", Scenes = new(), Metadata = new ScriptMetadata { ProviderName = "OpenAI" } };

        _cacheService.CacheScript(brief, planSpec, "OpenAI", "gpt-4", script);

        var stats = _cacheService.GetStatistics();

        Assert.Equal(1, stats.TotalEntries);
        Assert.Equal(1, stats.ValidEntries);
        Assert.Equal(0, stats.ExpiredEntries);
    }

    [Fact]
    public void DifferentParameters_ProduceDifferentCacheKeys()
    {
        var brief1 = new Brief("Topic 1", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var brief2 = new Brief("Topic 2", null, null, "Casual", "en", Aspect.Widescreen16x9);
        var planSpec = new PlanSpec(TimeSpan.FromSeconds(60), Pacing.Conversational, Density.Balanced, "Modern");
        
        var script = new Script { Title = "Test", Scenes = new(), Metadata = new ScriptMetadata { ProviderName = "OpenAI" } };

        _cacheService.CacheScript(brief1, planSpec, "OpenAI", "gpt-4", script);

        var cached = _cacheService.GetCachedScript(brief2, planSpec, "OpenAI", "gpt-4");

        Assert.Null(cached);
    }
}
