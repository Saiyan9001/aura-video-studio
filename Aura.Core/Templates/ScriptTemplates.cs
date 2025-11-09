using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura.Core.Templates;

/// <summary>
/// Pre-built script templates for offline video generation.
/// Provides professional content for common video types without requiring LLM APIs.
/// </summary>
public static class ScriptTemplates
{
    /// <summary>
    /// Gets a template-based script for the specified video type and topic.
    /// </summary>
    public static string GenerateFromTemplate(VideoType videoType, string topic, int targetWordCount)
    {
        var template = videoType switch
        {
            VideoType.ProductDemo => GenerateProductDemoScript(topic, targetWordCount),
            VideoType.Tutorial => GenerateTutorialScript(topic, targetWordCount),
            VideoType.Marketing => GenerateMarketingScript(topic, targetWordCount),
            VideoType.Educational => GenerateEducationalScript(topic, targetWordCount),
            VideoType.Welcome => GenerateWelcomeScript(topic, targetWordCount),
            _ => GenerateGeneralScript(topic, targetWordCount)
        };
        
        return template;
    }
    
    /// <summary>
    /// Determines video type from topic keywords.
    /// </summary>
    public static VideoType DetermineVideoType(string topic)
    {
        var topicLower = topic.ToLowerInvariant();
        
        if (topicLower.Contains("demo") || topicLower.Contains("product") || 
            topicLower.Contains("feature") || topicLower.Contains("showcase"))
        {
            return VideoType.ProductDemo;
        }
        
        if (topicLower.Contains("tutorial") || topicLower.Contains("how to") || 
            topicLower.Contains("guide") || topicLower.Contains("learn"))
        {
            return VideoType.Tutorial;
        }
        
        if (topicLower.Contains("marketing") || topicLower.Contains("promo") || 
            topicLower.Contains("launch") || topicLower.Contains("announce"))
        {
            return VideoType.Marketing;
        }
        
        if (topicLower.Contains("education") || topicLower.Contains("teach") || 
            topicLower.Contains("explain") || topicLower.Contains("understand"))
        {
            return VideoType.Educational;
        }
        
        if (topicLower.Contains("welcome") || topicLower.Contains("intro") || 
            topicLower.Contains("getting started") || topicLower.Contains("aura"))
        {
            return VideoType.Welcome;
        }
        
        return VideoType.General;
    }
    
    private static string GenerateProductDemoScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Introduction");
        sections.Add($"Welcome to {topic}. Today we're excited to show you the key features and capabilities that make this solution stand out. Let's dive in and see what makes this product special.");
        sections.Add("");
        
        sections.Add("## Key Features");
        sections.Add($"The standout features of {topic} are designed with your needs in mind. Each feature has been carefully crafted to deliver maximum value and ease of use. You'll find that every aspect works seamlessly together.");
        sections.Add("");
        
        if (NeedsExtraContent(sections, targetWordCount))
        {
            sections.Add("## How It Works");
            sections.Add("The workflow is intuitive and straightforward. From your initial setup to advanced usage, everything is designed to feel natural. The interface guides you through each step, making even complex tasks feel simple.");
            sections.Add("");
        }
        
        sections.Add("## Conclusion");
        sections.Add($"That wraps up our demonstration of {topic}. We've covered the essential features and shown you how it can benefit your workflow. Try it out for yourself and discover what it can do for you.");
        
        return string.Join("\n", sections);
    }
    
    private static string GenerateTutorialScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Introduction");
        sections.Add($"In this tutorial, we'll learn about {topic}. By the end of this video, you'll have a solid understanding of the fundamentals and be ready to apply what you've learned. Let's get started.");
        sections.Add("");
        
        sections.Add("## Step 1: Getting Started");
        sections.Add("First, let's cover the basics. Understanding these foundational concepts is crucial for success. Take your time with this section as it sets the stage for everything that follows.");
        sections.Add("");
        
        if (NeedsExtraContent(sections, targetWordCount))
        {
            sections.Add("## Step 2: Key Concepts");
            sections.Add("Now that we have the basics down, let's explore the key concepts in more detail. These principles will help you understand how everything works together. Pay close attention to how each piece connects to the others.");
            sections.Add("");
        }
        
        sections.Add("## Step 3: Putting It All Together");
        sections.Add("Let's bring everything together. You've learned the individual components, and now it's time to see how they work as a complete system. This is where it all starts to make sense.");
        sections.Add("");
        
        sections.Add("## Summary");
        sections.Add($"Great job! You've completed this tutorial on {topic}. Remember to practice what you've learned, and don't hesitate to revisit any sections as needed. Keep learning and growing your skills.");
        
        return string.Join("\n", sections);
    }
    
    private static string GenerateMarketingScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Opening Hook");
        sections.Add($"Are you ready to discover {topic}? This is the game-changer you've been waiting for. In just a few minutes, you'll see why so many people are making the switch. Let's explore what makes this opportunity so compelling.");
        sections.Add("");
        
        sections.Add("## The Problem");
        sections.Add("We all face challenges that slow us down and hold us back. Traditional solutions often fall short, leaving us frustrated and looking for better alternatives. There has to be a better way, and now there is.");
        sections.Add("");
        
        sections.Add("## The Solution");
        sections.Add($"That's where {topic} comes in. This innovative approach solves the problems that have plagued users for too long. It's designed to deliver results quickly and efficiently, without the usual hassles and complications.");
        sections.Add("");
        
        if (NeedsExtraContent(sections, targetWordCount))
        {
            sections.Add("## Why Choose Us");
            sections.Add("What sets us apart is our commitment to excellence and customer satisfaction. We've listened to feedback, refined our approach, and delivered something truly special. The results speak for themselves.");
            sections.Add("");
        }
        
        sections.Add("## Call to Action");
        sections.Add($"Don't wait any longer. Experience {topic} for yourself and see the difference it can make. Join the growing community of satisfied users who have already made the smart choice. Take action today.");
        
        return string.Join("\n", sections);
    }
    
    private static string GenerateEducationalScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Introduction");
        sections.Add($"Welcome to our educational exploration of {topic}. Today we'll examine this subject from multiple perspectives and build a comprehensive understanding together. Learning is a journey, so let's begin.");
        sections.Add("");
        
        sections.Add("## Background and Context");
        sections.Add("To truly understand this topic, we need to look at where it came from and why it matters. The historical context provides valuable insights that help us appreciate its significance today. Let's examine the key developments that shaped current thinking.");
        sections.Add("");
        
        sections.Add("## Core Concepts");
        sections.Add("At the heart of this subject are several fundamental concepts that form the foundation of our understanding. These ideas connect to create a cohesive framework. Let's explore each concept and see how they relate to one another.");
        sections.Add("");
        
        if (NeedsExtraContent(sections, targetWordCount))
        {
            sections.Add("## Real-World Applications");
            sections.Add("Theory becomes meaningful when we see how it applies in practice. Real-world examples demonstrate these principles in action and show their practical value. This is where abstract concepts become concrete and useful.");
            sections.Add("");
        }
        
        sections.Add("## Conclusion");
        sections.Add($"We've covered significant ground in exploring {topic}. The concepts we've discussed form a solid foundation for further learning. Continue to build on this knowledge and explore the subject even deeper. Thank you for learning with us today.");
        
        return string.Join("\n", sections);
    }
    
    private static string GenerateWelcomeScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Welcome");
        sections.Add("Welcome to Aura Video Studio! We're excited to help you create amazing videos with the power of AI. This tool makes video production accessible to everyone, regardless of technical expertise.");
        sections.Add("");
        
        sections.Add("## What You Can Do");
        sections.Add("With Aura Video Studio, you can generate complete videos from just a simple brief. The AI handles script writing, voice narration, visual selection, and video rendering. Everything is automated, yet fully customizable to match your vision.");
        sections.Add("");
        
        sections.Add("## Getting Started");
        sections.Add("Getting started is simple. Just describe your video concept, choose your preferences, and let the AI do the heavy lifting. You can use this Quick Demo feature without any API keys or complex setup. It works right out of the box in offline mode.");
        sections.Add("");
        
        sections.Add("## Key Features");
        sections.Add("Aura Video Studio includes multiple AI providers for maximum flexibility. Script generation, text-to-speech synthesis, image generation, and video rendering all work together seamlessly. The offline mode with rule-based script generation and local TTS ensures you can work even without internet connectivity or API keys.");
        sections.Add("");
        
        sections.Add("## Offline Mode Benefits");
        sections.Add("Running in offline mode means no API costs, complete privacy, and independence from external services. While AI providers offer more sophisticated results, offline mode provides consistent, reliable video generation that works anywhere, anytime. It's perfect for testing, prototyping, and situations where connectivity is limited.");
        sections.Add("");
        
        sections.Add("## Next Steps");
        sections.Add("Now that you've seen what's possible, try creating your own video. Explore the different options, experiment with settings, and discover the full potential of AI-powered video creation. When you're ready, you can configure online providers for even more advanced capabilities. We can't wait to see what you create. Welcome aboard!");
        
        return string.Join("\n", sections);
    }
    
    private static string GenerateGeneralScript(string topic, int targetWordCount)
    {
        var sections = new List<string>();
        
        sections.Add($"# {topic}");
        sections.Add("");
        sections.Add("## Introduction");
        sections.Add($"Today we're exploring {topic}, an interesting subject that deserves our attention. Throughout this video, we'll examine different aspects and perspectives to build a complete picture. Let's dive in and learn together.");
        sections.Add("");
        
        sections.Add("## Main Points");
        sections.Add("There are several key points to consider when thinking about this topic. Each aspect contributes to our overall understanding and helps us see the bigger picture. Let's examine these points one by one.");
        sections.Add("");
        
        if (NeedsExtraContent(sections, targetWordCount))
        {
            sections.Add("## Deeper Analysis");
            sections.Add("Looking more closely, we can identify patterns and connections that might not be immediately obvious. These insights help us develop a nuanced understanding. The details matter, and they reveal important truths about the subject.");
            sections.Add("");
        }
        
        sections.Add("## Conclusion");
        sections.Add($"We've covered important ground in our discussion of {topic}. The insights we've gained provide a foundation for further exploration. Continue to think critically about these ideas and apply what you've learned. Thank you for watching.");
        
        return string.Join("\n", sections);
    }
    
    private static bool NeedsExtraContent(List<string> sections, int targetWordCount)
    {
        var currentContent = string.Join(" ", sections);
        var currentWordCount = currentContent.Split(new[] { ' ', '\n', '\t' }, 
            StringSplitOptions.RemoveEmptyEntries).Length;
        
        return currentWordCount < targetWordCount * 0.7;
    }
}

/// <summary>
/// Video types supported by template system
/// </summary>
public enum VideoType
{
    ProductDemo,
    Tutorial,
    Marketing,
    Educational,
    Welcome,
    General
}
