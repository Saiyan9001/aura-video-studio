using System;
using System.Collections.Generic;
using System.Text;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.PromptManagement;

/// <summary>
/// Builds sophisticated, context-aware prompts for high-quality script generation
/// Implements video type specific prompting with multi-shot examples
/// </summary>
public class AdvancedScriptPromptBuilder
{
    private readonly ILogger<AdvancedScriptPromptBuilder> _logger;

    public AdvancedScriptPromptBuilder(ILogger<AdvancedScriptPromptBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Build a comprehensive system prompt for script generation
    /// </summary>
    public string BuildSystemPrompt(VideoType videoType, Brief brief, PlanSpec planSpec)
    {
        _logger.LogDebug("Building system prompt for video type: {VideoType}", videoType);

        var prompt = new StringBuilder();
        
        prompt.AppendLine("You are an expert video script writer specializing in creating engaging, audience-focused content.");
        prompt.AppendLine();
        
        prompt.AppendLine(GetVideoTypeGuidelines(videoType));
        prompt.AppendLine();
        
        prompt.AppendLine(GetStructureGuidelines(planSpec));
        prompt.AppendLine();
        
        prompt.AppendLine(GetQualityStandards());
        
        return prompt.ToString();
    }

    /// <summary>
    /// Build user prompt with context injection
    /// </summary>
    public string BuildUserPrompt(Brief brief, PlanSpec planSpec, VideoType videoType)
    {
        _logger.LogDebug("Building user prompt for topic: {Topic}", brief.Topic);

        var prompt = new StringBuilder();
        
        prompt.AppendLine("Create a video script with the following requirements:");
        prompt.AppendLine();
        
        prompt.AppendLine($"**Topic**: {brief.Topic}");
        prompt.AppendLine($"**Target Audience**: {brief.Audience ?? "General audience"}");
        prompt.AppendLine($"**Goal**: {brief.Goal ?? "Inform and engage"}");
        prompt.AppendLine($"**Tone**: {brief.Tone}");
        prompt.AppendLine($"**Duration**: {planSpec.TargetDuration.TotalSeconds} seconds");
        prompt.AppendLine($"**Pacing**: {planSpec.Pacing}");
        prompt.AppendLine($"**Style**: {planSpec.Style}");
        prompt.AppendLine();

        if (videoType == VideoType.Marketing)
        {
            prompt.AppendLine("**Marketing Requirements**:");
            prompt.AppendLine("- Include a strong hook in the first 3 seconds");
            prompt.AppendLine("- Build emotional connection with the audience");
            prompt.AppendLine("- Include clear call-to-action at the end");
            prompt.AppendLine("- Focus on benefits, not just features");
            prompt.AppendLine();
        }
        else if (videoType == VideoType.Educational)
        {
            prompt.AppendLine("**Educational Requirements**:");
            prompt.AppendLine("- Start with clear learning objectives");
            prompt.AppendLine("- Include specific examples to illustrate concepts");
            prompt.AppendLine("- Build from simple to complex");
            prompt.AppendLine("- End with a summary of key takeaways");
            prompt.AppendLine();
        }
        else if (videoType == VideoType.Entertainment)
        {
            prompt.AppendLine("**Entertainment Requirements**:");
            prompt.AppendLine("- Create a compelling story arc with setup, conflict, and resolution");
            prompt.AppendLine("- Build tension and anticipation");
            prompt.AppendLine("- Include emotional beats");
            prompt.AppendLine("- End with a satisfying conclusion");
            prompt.AppendLine();
        }

        prompt.AppendLine(GetJsonSchemaInstructions());
        
        return prompt.ToString();
    }

    /// <summary>
    /// Build refinement prompt for improving existing script
    /// </summary>
    public string BuildRefinementPrompt(string originalScript, List<string> weaknesses, VideoType videoType)
    {
        _logger.LogDebug("Building refinement prompt with {Count} weaknesses", weaknesses.Count);

        var prompt = new StringBuilder();
        
        prompt.AppendLine("Improve the following video script by addressing these specific issues:");
        prompt.AppendLine();
        
        foreach (var weakness in weaknesses)
        {
            prompt.AppendLine($"- {weakness}");
        }
        prompt.AppendLine();
        
        prompt.AppendLine("**Original Script**:");
        prompt.AppendLine(originalScript);
        prompt.AppendLine();
        
        prompt.AppendLine("Maintain the core message and structure, but enhance:");
        prompt.AppendLine("- Clarity and flow");
        prompt.AppendLine("- Engagement and hooks");
        prompt.AppendLine("- Audience connection");
        prompt.AppendLine("- Visual storytelling cues");
        
        if (videoType == VideoType.Marketing)
        {
            prompt.AppendLine("- Emotional appeal and benefits focus");
            prompt.AppendLine("- Call-to-action strength");
        }
        
        return prompt.ToString();
    }

    /// <summary>
    /// Build hook optimization prompt
    /// </summary>
    public string BuildHookOptimizationPrompt(string currentHook, Brief brief, int targetSeconds)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine($"Optimize this video hook to grab attention in the first {targetSeconds} seconds:");
        prompt.AppendLine();
        prompt.AppendLine($"**Current Hook**: {currentHook}");
        prompt.AppendLine();
        prompt.AppendLine($"**Topic**: {brief.Topic}");
        prompt.AppendLine($"**Audience**: {brief.Audience ?? "General"}");
        prompt.AppendLine();
        prompt.AppendLine("Use proven attention-grabbing techniques:");
        prompt.AppendLine("- Pattern interruption: Start with something unexpected");
        prompt.AppendLine("- Curiosity gap: Tease valuable information");
        prompt.AppendLine("- Bold promise: Make a clear value proposition");
        prompt.AppendLine("- Question hook: Pose an intriguing question");
        prompt.AppendLine("- Statistic shock: Use a surprising number");
        prompt.AppendLine();
        prompt.AppendLine("Return ONLY the optimized hook (2-3 sentences max).");
        
        return prompt.ToString();
    }

    /// <summary>
    /// Build scene regeneration prompt with context
    /// </summary>
    public string BuildSceneRegenerationPrompt(
        int sceneNumber, 
        string currentScene, 
        string previousSceneContext, 
        string nextSceneContext,
        string improvementGoal)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine($"Regenerate Scene {sceneNumber} to improve: {improvementGoal}");
        prompt.AppendLine();
        prompt.AppendLine("**Context**:");
        prompt.AppendLine($"Previous Scene: {previousSceneContext}");
        prompt.AppendLine($"Current Scene: {currentScene}");
        prompt.AppendLine($"Next Scene: {nextSceneContext}");
        prompt.AppendLine();
        prompt.AppendLine("Ensure smooth transitions and narrative flow with surrounding scenes.");
        prompt.AppendLine("Maintain consistent tone and pacing.");
        
        return prompt.ToString();
    }

    /// <summary>
    /// Build prompt for generating script variations
    /// </summary>
    public string BuildVariationPrompt(string originalScript, string variationFocus)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine("Create an alternative version of this script with the following focus:");
        prompt.AppendLine($"**Variation Focus**: {variationFocus}");
        prompt.AppendLine();
        prompt.AppendLine("**Original Script**:");
        prompt.AppendLine(originalScript);
        prompt.AppendLine();
        prompt.AppendLine("Maintain the core message but explore a different:");
        prompt.AppendLine("- Opening approach");
        prompt.AppendLine("- Story structure");
        prompt.AppendLine("- Tone and voice");
        prompt.AppendLine("- Examples and analogies");
        
        return prompt.ToString();
    }

    /// <summary>
    /// Get multi-shot examples for the video type
    /// </summary>
    public string GetMultiShotExamples(VideoType videoType)
    {
        return videoType switch
        {
            VideoType.Educational => GetEducationalExamples(),
            VideoType.Marketing => GetMarketingExamples(),
            VideoType.Entertainment => GetEntertainmentExamples(),
            _ => string.Empty
        };
    }

    private string GetVideoTypeGuidelines(VideoType videoType)
    {
        return videoType switch
        {
            VideoType.Educational => @"**Educational Video Guidelines**:
- Start with a clear learning objective that answers ""What will viewers learn?""
- Use the explanation-example-practice pattern
- Include concrete examples to illustrate abstract concepts
- Build complexity gradually (simple → intermediate → advanced)
- Summarize key takeaways at the end
- Use analogies to connect new concepts to familiar ones
- Maintain appropriate pacing for the target audience's knowledge level",

            VideoType.Marketing => @"**Marketing Video Guidelines**:
- Hook viewers within 3 seconds with a problem, question, or bold statement
- Focus on benefits and outcomes, not just features
- Build emotional connection through storytelling
- Use social proof or authority when relevant
- Create urgency or scarcity where appropriate
- Include a clear, specific call-to-action
- Speak directly to the target audience's pain points and desires",

            VideoType.Entertainment => @"**Entertainment Video Guidelines**:
- Create a compelling story arc: Setup → Conflict → Resolution
- Build tension and anticipation throughout
- Include emotional peaks and valleys
- Use vivid, sensory language
- Create relatable characters or situations
- Include unexpected moments or plot twists
- End with a satisfying payoff or twist",

            _ => @"**General Video Guidelines**:
- Start with a strong hook to grab attention
- Maintain clear narrative flow
- Use conversational, engaging language
- Include visual storytelling cues
- End with a memorable conclusion"
        };
    }

    private string GetStructureGuidelines(PlanSpec planSpec)
    {
        var sceneDuration = planSpec.TargetDuration.TotalSeconds / GetRecommendedSceneCount(planSpec);
        
        return $@"**Structure Guidelines**:
- Duration: {planSpec.TargetDuration.TotalSeconds} seconds total
- Recommended scenes: {GetRecommendedSceneCount(planSpec)}
- Average scene length: {sceneDuration:F1} seconds
- Pacing: {planSpec.Pacing} (adjust sentence complexity and information density accordingly)
- Style: {planSpec.Style}

**Scene Format**:
Each scene should include:
- Narration: The spoken script (conversational, easy to read aloud)
- Visual: Specific visual description for image generation
- Timing: Appropriate for the amount of spoken content";
    }

    private string GetQualityStandards()
    {
        return @"**Quality Standards**:
- Reading speed: 150-160 words per minute (natural speaking pace)
- Sentence length: Vary between 10-20 words for natural rhythm
- Vocabulary: Appropriate for target audience
- Transitions: Smooth connections between scenes
- Visual prompts: Specific, actionable descriptions for image generation
- Engagement: Every scene should advance the narrative or add value";
    }

    private string GetJsonSchemaInstructions()
    {
        return @"**Output Format**:
Return a JSON object with this structure:
{
  ""title"": ""Video Title"",
  ""scenes"": [
    {
      ""number"": 1,
      ""narration"": ""The spoken script for this scene"",
      ""visualPrompt"": ""Specific visual description: e.g., 'Professional office setting with person working at modern desk'"",
      ""durationSeconds"": 5.0,
      ""transition"": ""fade"" // Options: cut, fade, dissolve
    }
  ]
}";
    }

    private int GetRecommendedSceneCount(PlanSpec planSpec)
    {
        var duration = planSpec.TargetDuration.TotalSeconds;
        
        return planSpec.Pacing switch
        {
            Pacing.Chill => Math.Max(2, (int)(duration / 12)),
            Pacing.Conversational => Math.Max(3, (int)(duration / 8)),
            Pacing.Fast => Math.Max(4, (int)(duration / 5)),
            _ => Math.Max(3, (int)(duration / 8))
        };
    }

    private string GetEducationalExamples()
    {
        return @"**Example: Educational Script Structure**

Scene 1 (Hook + Learning Objective):
""Ever wonder why some videos go viral while others get zero views? In the next 60 seconds, you'll learn the three psychological triggers that make content irresistible.""

Scene 2 (Concept + Example):
""The first trigger is curiosity. When you create an information gap, viewers can't help but watch. For example, starting with 'The secret ingredient is...' makes people lean in.""

Scene 3 (Application):
""Try this in your next video: Open with a question your audience is dying to answer. Then tease that you'll reveal the answer at the end.""";
    }

    private string GetMarketingExamples()
    {
        return @"**Example: Marketing Script Structure**

Scene 1 (Problem Hook):
""Spending hours editing videos but getting no results? You're not alone. 93% of creators waste time on the wrong things.""

Scene 2 (Solution):
""That's why we built Aura. Generate professional videos in minutes, not hours. No editing skills required.""

Scene 3 (Social Proof + CTA):
""Join 10,000+ creators who've reclaimed their time. Start your free trial today—no credit card needed.""";
    }

    private string GetEntertainmentExamples()
    {
        return @"**Example: Entertainment Script Structure**

Scene 1 (Setup):
""It started as a normal Tuesday morning. I had no idea my life was about to change forever.""

Scene 2 (Conflict):
""That's when I saw the message. Three words that made my heart stop: 'We need to talk.'""

Scene 3 (Resolution):
""Looking back now, that moment of panic led to the best decision I ever made. Let me tell you why...""";
    }
}

/// <summary>
/// Video type classification for prompt specialization
/// </summary>
public enum VideoType
{
    General,
    Educational,
    Marketing,
    Entertainment,
    Tutorial,
    Documentary,
    Vlog,
    ProductReview,
    Explainer
}
