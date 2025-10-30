using System.Text;
using Aura.Core.Models;
using PacingEnum = Aura.Core.Models.Pacing;
using DensityEnum = Aura.Core.Models.Density;

namespace Aura.Core.AI;

/// <summary>
/// Enhanced prompt templates for AI-guided video creation that produces high-quality,
/// natural-sounding content that doesn't feel AI-generated or like "slop".
/// Supports both static (default) and dynamic (ML-optimized) modes.
/// </summary>
public static class EnhancedPromptTemplates
{
    /// <summary>
    /// Prompt mode for template usage
    /// </summary>
    public enum PromptMode
    {
        /// <summary>
        /// Use static, predefined templates
        /// </summary>
        Static,
        
        /// <summary>
        /// Allow dynamic ML-driven enhancements
        /// </summary>
        Dynamic
    }

    /// <summary>
    /// Current prompt mode (default: Static)
    /// </summary>
    public static PromptMode CurrentMode { get; set; } = PromptMode.Static;

    /// <summary>
    /// Schema mode for scene analysis prompts
    /// </summary>
    public enum SchemaMode
    {
        /// <summary>
        /// Compact schema with essential fields only
        /// </summary>
        Compact,
        
        /// <summary>
        /// Strict schema with explicit JSON structure and validation
        /// </summary>
        StrictSchema
    }

    /// <summary>
    /// Configuration for provider prompts (PR 1 feature flag)
    /// </summary>
    public static class ProviderPromptConfig
    {
        /// <summary>
        /// Enable or disable strict schema mode for scene analysis.
        /// When true, uses explicit JSON schema with few-shot examples.
        /// Default: true (strict schema enabled)
        /// </summary>
        public static bool StrictSchema { get; set; } = true;

        /// <summary>
        /// Voice style configuration for script generation
        /// </summary>
        public static string VoiceStyle { get; set; } = "natural";

        /// <summary>
        /// Number of few-shot examples to include in scene analysis prompts
        /// </summary>
        public static int ExampleCount { get; set; } = 2;
    }

    /// <summary>
    /// System prompt for video script generation with emphasis on quality and naturalness
    /// </summary>
    public static string GetSystemPromptForScriptGeneration()
    {
        return @"You are an expert video creator specializing in social media content that performs exceptionally well. Your expertise includes:

CORE PRINCIPLES:
- Create content that feels authentic, human, and engaging - never robotic or obviously AI-generated
- Focus on storytelling and emotional connection with the audience
- Use natural language patterns, varied sentence structures, and conversational flow
- Include subtle personality and relatable examples
- Build genuine value and insight rather than surface-level content

CONTENT QUALITY STANDARDS:
- Hook viewers in the first 3-5 seconds with intrigue, surprise, or immediate value
- Use the AIDA framework (Attention, Interest, Desire, Action) naturally
- Balance information density with entertainment value
- Include specific, verifiable facts and examples (when appropriate)
- Use pattern interrupts to maintain engagement
- Build to clear payoffs and actionable takeaways
- End with memorable conclusions and clear next steps

PACING & RHYTHM:
- Vary sentence length for natural flow (mix short punchy statements with longer explanatory ones)
- Use transitions that feel organic, not formulaic
- Build momentum through the video with strategic peaks and valleys
- Time reveals and key points for maximum impact

VOICE & TONE:
- Write for the ear, not the eye (how it sounds when spoken aloud)
- Avoid generic phrases like 'in today's video' or 'don't forget to subscribe'
- Use active voice and vivid language
- Match tone to content type (educational should still be engaging, entertainment should have substance)

VISUAL SYNERGY:
- Write with visual storytelling in mind - describe scenes that can be illustrated
- Include natural moments for B-roll, graphics, or demonstrations
- Mark key visual moments in the script structure

AVOID AI DETECTION FLAGS:
- No overly formal or academic language unless the content demands it
- No repetitive sentence structures or formulaic patterns
- No generic lists without context or unique insights
- No excessive adjectives or marketing speak
- No 'AI voice' patterns (e.g., constant rhetorical questions, overuse of 'delve into')";
    }

    /// <summary>
    /// Build a detailed user prompt for script generation with content awareness
    /// </summary>
    public static string BuildScriptGenerationPrompt(Brief brief, PlanSpec spec, string? additionalContext = null)
    {
        var sb = new StringBuilder();

        // Topic and core details
        sb.AppendLine($"CREATE A VIDEO SCRIPT:");
        sb.AppendLine($"Topic: {brief.Topic}");
        sb.AppendLine();

        // Target specifications
        sb.AppendLine($"TARGET SPECIFICATIONS:");
        sb.AppendLine($"- Duration: {spec.TargetDuration.TotalMinutes:F1} minutes ({EstimateWordCount(spec):N0} words)");
        sb.AppendLine($"- Tone: {brief.Tone}");
        sb.AppendLine($"- Pacing: {GetPacingDescription(spec.Pacing)}");
        sb.AppendLine($"- Content Density: {GetDensityDescription(spec.Density)}");
        sb.AppendLine($"- Language: {brief.Language}");
        
        if (!string.IsNullOrEmpty(brief.Audience))
        {
            sb.AppendLine($"- Target Audience: {brief.Audience}");
        }

        if (!string.IsNullOrEmpty(brief.Goal))
        {
            sb.AppendLine($"- Content Goal: {brief.Goal}");
        }

        sb.AppendLine();

        // Content creation guidelines specific to this video
        sb.AppendLine($"SPECIFIC GUIDELINES FOR THIS VIDEO:");
        sb.AppendLine(GetToneSpecificGuidelines(brief.Tone));
        sb.AppendLine();

        // Add context if provided
        if (!string.IsNullOrEmpty(additionalContext))
        {
            sb.AppendLine($"ADDITIONAL CONTEXT:");
            sb.AppendLine(additionalContext);
            sb.AppendLine();
        }

        // Structure requirements
        sb.AppendLine($"REQUIRED STRUCTURE:");
        sb.AppendLine($"# [Compelling, Specific Title - Not Generic]");
        sb.AppendLine();
        sb.AppendLine($"## Hook (First 3-5 seconds)");
        sb.AppendLine($"[Immediately grab attention with intrigue, surprise, or promised value. Be specific, not vague.]");
        sb.AppendLine();
        sb.AppendLine($"## Introduction (Next 10-15 seconds)");
        sb.AppendLine($"[Build context and preview the journey. Create anticipation.]");
        sb.AppendLine();
        sb.AppendLine($"## [3-5 Content Sections with Descriptive Headers]");
        sb.AppendLine($"[Each section should:");
        sb.AppendLine($" - Have a clear purpose and payoff");
        sb.AppendLine($" - Include specific examples or demonstrations");
        sb.AppendLine($" - Maintain momentum toward the conclusion");
        sb.AppendLine($" - Suggest visual moments with [VISUAL: description]]");
        sb.AppendLine();
        sb.AppendLine($"## Conclusion");
        sb.AppendLine($"[Powerful summary and clear call-to-action. Leave viewers with lasting value.]");
        sb.AppendLine();

        // Quality requirements
        sb.AppendLine($"QUALITY REQUIREMENTS:");
        sb.AppendLine($"- Every sentence must serve the story and add value");
        sb.AppendLine($"- Use specific, memorable examples and analogies");
        sb.AppendLine($"- Vary sentence length and structure naturally");
        sb.AppendLine($"- Include 2-3 pattern interrupts or surprising turns");
        sb.AppendLine($"- Mark key visual moments with [VISUAL: brief description]");
        sb.AppendLine($"- Ensure the script sounds natural when read aloud");
        sb.AppendLine($"- Build emotional resonance appropriate to the topic");

        return sb.ToString();
    }

    /// <summary>
    /// Get tone-specific creative guidelines
    /// </summary>
    private static string GetToneSpecificGuidelines(string tone)
    {
        return tone.ToLowerInvariant() switch
        {
            "informative" or "educational" => @"- Lead with curiosity and discovery, not dry facts
- Use the 'curiosity gap' technique to maintain engagement
- Include surprising facts or counterintuitive insights
- Make complex topics accessible through clear analogies
- Build understanding progressively, not all at once",

            "narrative" or "storytelling" => @"- Use story structure with clear setup, conflict, and resolution
- Include sensory details and emotional beats
- Create characters or perspectives the audience can connect with
- Use 'show don't tell' principles where possible
- Build suspense and anticipation naturally",

            "humorous" or "entertaining" => @"- Use wit and clever observations, not forced jokes
- Include relatable situations and reactions
- Timing is crucial - set up punchlines properly
- Balance humor with substance
- Use callbacks and running gags if appropriate",

            "dramatic" => @"- Build tension and stakes progressively
- Use vivid, evocative language
- Include emotional peaks and valleys
- Create investment in outcomes
- Use the power of silence and pauses (mark with [PAUSE])",

            "conversational" => @"- Write as if talking to a friend over coffee
- Use 'you' and 'we' to create connection
- Include personal touches and relatable moments
- Ask engaging questions (then answer them)
- Keep energy up with natural enthusiasm",

            "professional" => @"- Maintain authority while remaining accessible
- Use industry-specific insights and expertise
- Include data and examples that demonstrate credibility
- Balance formality with engagement
- Provide actionable, professional-grade insights",

            _ => @"- Focus on authentic, engaging delivery
- Use natural language patterns
- Include specific, memorable content
- Maintain viewer interest throughout
- Provide clear value and takeaways"
        };
    }

    /// <summary>
    /// Get pacing description for better AI understanding
    /// </summary>
    private static string GetPacingDescription(PacingEnum pacing)
    {
        return pacing switch
        {
            PacingEnum.Chill => "Relaxed and contemplative (~120-140 WPM), with longer pauses for reflection",
            PacingEnum.Conversational => "Natural conversation pace (~150-165 WPM), comfortable and engaging",
            PacingEnum.Fast => "Energetic and dynamic (~180-200 WPM), keeping momentum high",
            _ => "Standard conversational pace"
        };
    }

    /// <summary>
    /// Get density description for better AI understanding
    /// </summary>
    private static string GetDensityDescription(DensityEnum density)
    {
        return density switch
        {
            DensityEnum.Sparse => "Light and accessible, with plenty of breathing room between concepts",
            DensityEnum.Balanced => "Good balance of depth and accessibility, moderately detailed",
            DensityEnum.Dense => "Information-rich and detailed, for engaged audiences seeking depth",
            _ => "Balanced information density"
        };
    }

    /// <summary>
    /// Estimate word count based on duration and pacing
    /// </summary>
    private static int EstimateWordCount(PlanSpec spec)
    {
        int baseWpm = spec.Pacing switch
        {
            PacingEnum.Chill => 130,
            PacingEnum.Conversational => 157,
            PacingEnum.Fast => 190,
            _ => 157
        };

        double densityMultiplier = spec.Density switch
        {
            DensityEnum.Sparse => 0.85,
            DensityEnum.Balanced => 1.0,
            DensityEnum.Dense => 1.15,
            _ => 1.0
        };

        return (int)(spec.TargetDuration.TotalMinutes * baseWpm * densityMultiplier);
    }

    /// <summary>
    /// System prompt for visual asset selection with emphasis on coherence and quality
    /// </summary>
    public static string GetSystemPromptForVisualSelection()
    {
        return @"You are an expert visual director for video content. Your role is to select or generate visuals that:

VISUAL QUALITY PRINCIPLES:
- Match the narrative and emotional tone of each scene
- Create visual coherence and consistency throughout the video
- Avoid generic stock footage clichés
- Use visuals that enhance understanding and engagement
- Create visual variety while maintaining a cohesive style

SELECTION CRITERIA:
- Relevance: Direct connection to the script content
- Quality: Professional, well-composed, properly lit
- Emotion: Matches the emotional tone and energy level
- Originality: Avoid overused or cliché imagery
- Coherence: Fits with the overall visual style of the video

VISUAL STORYTELLING:
- Use close-ups for emotional moments
- Use wide shots for context and scope
- Use motion and action to maintain engagement
- Consider color psychology and mood
- Think about transitions between shots

Provide specific, actionable search terms or generation prompts that will yield high-quality, relevant visuals.";
    }

    /// <summary>
    /// Build a visual selection prompt with content awareness
    /// </summary>
    public static string BuildVisualSelectionPrompt(string sceneHeading, string sceneContent, string tone, int sceneIndex)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"SELECT VISUALS FOR SCENE {sceneIndex + 1}:");
        sb.AppendLine($"Scene Title: {sceneHeading}");
        sb.AppendLine();
        sb.AppendLine($"Script Content:");
        sb.AppendLine(sceneContent);
        sb.AppendLine();
        sb.AppendLine($"Tone: {tone}");
        sb.AppendLine();
        sb.AppendLine($"Provide 3-5 specific search terms or image prompts that would yield visuals for this scene.");
        sb.AppendLine($"Each should be:");
        sb.AppendLine($"- Specific and descriptive (not generic)");
        sb.AppendLine($"- Professional quality");
        sb.AppendLine($"- Emotionally appropriate");
        sb.AppendLine($"- Visually interesting");
        sb.AppendLine();
        sb.AppendLine($"Format: One search term per line, starting with 'VISUAL:'");

        return sb.ToString();
    }

    /// <summary>
    /// System prompt for content quality validation
    /// </summary>
    public static string GetSystemPromptForQualityValidation()
    {
        return @"You are a quality control expert for AI-generated video content. Your role is to identify any signs that content feels:
- AI-generated or robotic
- Generic or 'sloppy'
- Lacking in substance or authenticity
- Using cliché patterns or phrases
- Poorly paced or structured

Provide specific, actionable feedback for improvement, focusing on making content feel more human, engaging, and valuable.";
    }

    /// <summary>
    /// Build a quality validation prompt
    /// </summary>
    public static string BuildQualityValidationPrompt(string script, string contentType)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ANALYZE THIS {contentType.ToUpper()} VIDEO SCRIPT FOR QUALITY:");
        sb.AppendLine();
        sb.AppendLine(script);
        sb.AppendLine();
        sb.AppendLine($"EVALUATION CRITERIA:");
        sb.AppendLine($"1. Authenticity: Does it feel human-written and genuine?");
        sb.AppendLine($"2. Engagement: Will it hold viewer attention throughout?");
        sb.AppendLine($"3. Value: Does it provide real insight or entertainment?");
        sb.AppendLine($"4. Pacing: Is the rhythm and flow natural?");
        sb.AppendLine($"5. Originality: Does it avoid clichés and generic patterns?");
        sb.AppendLine();
        sb.AppendLine($"Provide:");
        sb.AppendLine($"- Overall quality score (0-100)");
        sb.AppendLine($"- Specific issues found (if any)");
        sb.AppendLine($"- Concrete suggestions for improvement");
        sb.AppendLine($"- What works well that should be preserved");

        return sb.ToString();
    }

    /// <summary>
    /// System prompt for scene analysis with explicit JSON schema.
    /// Schema version: v1.0-strict (for PR 2 parser targeting)
    /// </summary>
    public static string GetSystemPromptForSceneAnalysis()
    {
        var schemaMode = ProviderPromptConfig.StrictSchema ? SchemaMode.StrictSchema : SchemaMode.Compact;

        if (schemaMode == SchemaMode.StrictSchema)
        {
            return @"You are a video pacing expert analyzing scenes for optimal timing and transitions.

CRITICAL: Your response must be VALID JSON and NOTHING ELSE. No explanatory text before or after the JSON.

OUTPUT FORMAT - You must return a JSON object with this EXACT schema:
{
  ""importance"": <number 0-100>,
  ""complexity"": <number 0-100>,
  ""emotionalIntensity"": <number 0-100>,
  ""informationDensity"": <string: ""low"" | ""medium"" | ""high"">,
  ""optimalDurationSeconds"": <number>,
  ""transitionType"": <string: ""cut"" | ""fade"" | ""dissolve"">,
  ""reasoning"": <string>
}

FIELD DEFINITIONS:
- importance: How critical is this scene to the video's message (0=skippable, 100=essential)
- complexity: How complex is the information presented (0=simple, 100=highly complex)
- emotionalIntensity: Emotional impact level (0=neutral, 100=highly emotional)
- informationDensity: Amount of information (""low"", ""medium"", or ""high"" ONLY)
- optimalDurationSeconds: Recommended duration in seconds (typically 5-15 seconds)
- transitionType: Recommended transition (""cut"", ""fade"", or ""dissolve"" ONLY)
- reasoning: Brief explanation (1-2 sentences maximum)

ANALYSIS PRINCIPLES:
- High importance scenes deserve longer duration
- Complex information needs more time to process
- High emotional intensity benefits from dissolve/fade transitions
- Dense information requires slower pacing
- Consider flow from previous scene to current scene";
        }
        else
        {
            return @"You are a video pacing expert. Analyze scenes for optimal timing. 
Return your response ONLY as valid JSON with no additional text.";
        }
    }

    /// <summary>
    /// Build scene analysis prompt with optional few-shot examples.
    /// Targets schema version v1.0-strict for downstream parser compatibility.
    /// </summary>
    public static string BuildSceneAnalysisPrompt(
        string sceneText,
        string? previousSceneText,
        string videoGoal)
    {
        var sb = new StringBuilder();
        var schemaMode = ProviderPromptConfig.StrictSchema ? SchemaMode.StrictSchema : SchemaMode.Compact;
        var exampleCount = ProviderPromptConfig.ExampleCount;

        if (schemaMode == SchemaMode.StrictSchema && exampleCount > 0)
        {
            sb.AppendLine("FEW-SHOT EXAMPLES:");
            sb.AppendLine();

            if (exampleCount >= 1)
            {
                sb.AppendLine("Example 1 - Opening hook:");
                sb.AppendLine("Scene: \"Have you ever wondered why your phone battery dies so fast?\"");
                sb.AppendLine("Goal: Educational tech tips");
                sb.AppendLine("Output:");
                sb.AppendLine(@"{
  ""importance"": 95,
  ""complexity"": 20,
  ""emotionalIntensity"": 60,
  ""informationDensity"": ""low"",
  ""optimalDurationSeconds"": 4,
  ""transitionType"": ""cut"",
  ""reasoning"": ""Strong hook requires quick pacing. High importance as it captures attention. Low complexity question with high relatability.""
}");
                sb.AppendLine();
            }

            if (exampleCount >= 2)
            {
                sb.AppendLine("Example 2 - Educational content:");
                sb.AppendLine("Scene: \"The lithium-ion battery works through a chemical reaction between the anode and cathode. When you charge your phone, lithium ions move from the cathode to the anode, storing energy.\"");
                sb.AppendLine("Goal: Educational tech tips");
                sb.AppendLine("Output:");
                sb.AppendLine(@"{
  ""importance"": 75,
  ""complexity"": 80,
  ""emotionalIntensity"": 30,
  ""informationDensity"": ""high"",
  ""optimalDurationSeconds"": 12,
  ""transitionType"": ""dissolve"",
  ""reasoning"": ""Complex technical explanation needs longer duration. High information density requires slower pacing. Dissolve transition helps viewer process information smoothly.""
}");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("YOUR TASK:");
        sb.AppendLine();
        sb.AppendLine($"Analyze this scene and return ONLY the JSON object (no other text):");
        sb.AppendLine();
        sb.AppendLine($"Scene: {sceneText}");
        
        if (!string.IsNullOrEmpty(previousSceneText))
        {
            sb.AppendLine($"Previous scene: {previousSceneText}");
        }
        
        sb.AppendLine($"Video goal: {videoGoal}");
        sb.AppendLine();

        if (schemaMode == SchemaMode.StrictSchema)
        {
            sb.AppendLine("REMEMBER: Return ONLY the JSON object matching the schema. No explanatory text.");
        }
        else
        {
            sb.AppendLine(@"Respond with ONLY the JSON object with fields:
- importance (0-100)
- complexity (0-100)
- emotionalIntensity (0-100)
- informationDensity (""low""|""medium""|""high"")
- optimalDurationSeconds (number)
- transitionType (""cut""|""fade""|""dissolve"")
- reasoning (string)");
        }

        return sb.ToString();
    }
}
