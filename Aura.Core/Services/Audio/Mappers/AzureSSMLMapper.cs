using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Aura.Core.Models;
using Aura.Core.Models.Audio;
using Aura.Core.Models.Voice;
using EmphasisLevel = Aura.Core.Models.Voice.EmphasisLevel;

namespace Aura.Core.Services.Audio.Mappers;

/// <summary>
/// SSML mapper for Azure Cognitive Services TTS
/// Supports comprehensive SSML with styles, emotions, and prosody
/// </summary>
public class AzureSSMLMapper : ISSMLMapper
{
    public VoiceProvider Provider => VoiceProvider.Azure;

    public ProviderSSMLConstraints GetConstraints()
    {
        return new ProviderSSMLConstraints
        {
            SupportedTags = new HashSet<string>
            {
                "speak", "voice", "prosody", "break", "emphasis", "say-as",
                "phoneme", "sub", "mstts:express-as", "mstts:silence", "audio"
            },
            SupportedProsodyAttributes = new HashSet<string>
            {
                "rate", "pitch", "volume", "contour"
            },
            RateRange = (0.5, 2.0),
            PitchRange = (-50.0, 50.0),
            VolumeRange = (0.0, 100.0),
            MaxPauseDurationMs = 5000,
            SupportsTimingMarkers = true,
            MaxTextLength = 10000
        };
    }

    public string MapToSSML(
        string text,
        ProsodyAdjustments adjustments,
        VoiceSpec voiceSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\"");
        sb.AppendLine("       xmlns:mstts=\"https://www.w3.org/2001/mstts\" xml:lang=\"en-US\">");

        sb.Append("  <voice name=\"").Append(XmlEscape(voiceSpec.VoiceName)).AppendLine("\">");

        var hasProsody = Math.Abs(adjustments.Rate - 1.0) > 0.01 ||
                        Math.Abs(adjustments.Pitch) > 0.01 ||
                        Math.Abs(adjustments.Volume - 1.0) > 0.01;

        if (hasProsody)
        {
            sb.Append("    <prosody");
            
            if (Math.Abs(adjustments.Rate - 1.0) > 0.01)
            {
                var ratePercent = (int)((adjustments.Rate - 1.0) * 100);
                sb.Append($" rate=\"{ratePercent:+0;-0;0}%\"");
            }

            if (Math.Abs(adjustments.Pitch) > 0.01)
            {
                var pitchHz = adjustments.Pitch * 50.0;
                sb.Append($" pitch=\"{pitchHz:+0.0;-0.0;0.0}Hz\"");
            }

            if (Math.Abs(adjustments.Volume - 1.0) > 0.01)
            {
                var volumePercent = (int)(adjustments.Volume * 100);
                sb.Append($" volume=\"{volumePercent}\"");
            }

            sb.AppendLine(">");
        }

        var processedText = ProcessTextWithEmphasisAndPauses(text, adjustments);
        sb.Append("      ").AppendLine(processedText);

        if (hasProsody)
        {
            sb.AppendLine("    </prosody>");
        }

        sb.AppendLine("  </voice>");
        sb.AppendLine("</speak>");

        return sb.ToString();
    }

    public SSMLValidationResult Validate(string ssml)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<SSMLRepairSuggestion>();

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(ssml);

            var rootNode = doc.DocumentElement;
            if (rootNode?.Name != "speak")
            {
                errors.Add("Root element must be <speak>");
                suggestions.Add(new SSMLRepairSuggestion(
                    "Missing speak root",
                    "Wrap content in <speak> element",
                    true));
            }

            var voiceNodes = doc.GetElementsByTagName("voice");
            if (voiceNodes.Count == 0)
            {
                warnings.Add("No <voice> element specified");
            }

            var prosodyNodes = doc.GetElementsByTagName("prosody");
            foreach (XmlNode node in prosodyNodes)
            {
                ValidateProsodyAttributes(node, warnings, suggestions);
            }

            var breakNodes = doc.GetElementsByTagName("break");
            foreach (XmlNode node in breakNodes)
            {
                ValidateBreakAttributes(node, warnings, suggestions);
            }
        }
        catch (XmlException ex)
        {
            errors.Add($"Invalid XML: {ex.Message}");
            suggestions.Add(new SSMLRepairSuggestion(
                "XML parsing error",
                "Check for unclosed tags and proper escaping",
                false));
        }

        return new SSMLValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            RepairSuggestions = suggestions
        };
    }

    public string AutoRepair(string ssml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(ssml);

            if (doc.DocumentElement?.Name != "speak")
            {
                var speakNode = doc.CreateElement("speak");
                speakNode.SetAttribute("version", "1.0");
                speakNode.SetAttribute("xmlns", "http://www.w3.org/2001/10/synthesis");
                speakNode.SetAttribute("xml:lang", "en-US");
                
                while (doc.FirstChild != null)
                {
                    var child = doc.FirstChild;
                    doc.RemoveChild(child);
                    speakNode.AppendChild(child);
                }
                
                doc.AppendChild(speakNode);
            }

            return doc.OuterXml;
        }
        catch
        {
            var text = System.Text.RegularExpressions.Regex.Replace(ssml, "<[^>]+>", "");
            text = System.Net.WebUtility.HtmlDecode(text);
            
            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{XmlEscape(text)}</speak>";
        }
    }

    public async Task<int> EstimateDurationAsync(
        string ssml,
        VoiceSpec voiceSpec,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var text = System.Text.RegularExpressions.Regex.Replace(ssml, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        
        var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        var baseWordsPerMinute = 150.0;
        var adjustedWpm = baseWordsPerMinute * voiceSpec.Rate;
        
        var baseDurationSeconds = (wordCount / adjustedWpm) * 60.0;
        
        var breakMatches = System.Text.RegularExpressions.Regex.Matches(ssml, @"<break\s+time=""(\d+)(ms|s)""");
        var totalBreakMs = 0.0;
        
        foreach (System.Text.RegularExpressions.Match match in breakMatches)
        {
            if (double.TryParse(match.Groups[1].Value, out var value))
            {
                var unit = match.Groups[2].Value;
                totalBreakMs += unit == "s" ? value * 1000 : value;
            }
        }
        
        var totalDurationMs = (baseDurationSeconds * 1000) + totalBreakMs;
        
        return (int)Math.Max(100, totalDurationMs);
    }

    private string ProcessTextWithEmphasisAndPauses(string text, ProsodyAdjustments adjustments)
    {
        var result = XmlEscape(text);
        
        if (adjustments.Pauses != null && adjustments.Pauses.Any())
        {
            var sortedPauses = adjustments.Pauses.OrderByDescending(kvp => kvp.Key).ToList();
            
            foreach (var pause in sortedPauses)
            {
                if (pause.Key >= 0 && pause.Key <= text.Length)
                {
                    var adjustedPos = CalculateAdjustedPosition(text, pause.Key, result);
                    if (adjustedPos >= 0 && adjustedPos <= result.Length)
                    {
                        result = result.Insert(adjustedPos, $"<break time=\"{pause.Value}ms\"/>");
                    }
                }
            }
        }
        
        if (adjustments.Emphasis != null && adjustments.Emphasis.Any())
        {
            var sortedEmphasis = adjustments.Emphasis.OrderByDescending(e => e.StartPosition).ToList();
            
            foreach (var emphasis in sortedEmphasis)
            {
                if (emphasis.StartPosition >= 0 && 
                    emphasis.StartPosition + emphasis.Length <= text.Length)
                {
                    var emphText = text.Substring(emphasis.StartPosition, emphasis.Length);
                    var escapedEmphText = XmlEscape(emphText);
                    
                    var startPos = CalculateAdjustedPosition(text, emphasis.StartPosition, result);
                    if (startPos >= 0)
                    {
                        var endPos = startPos + escapedEmphText.Length;
                        if (endPos <= result.Length && result.Substring(startPos, escapedEmphText.Length) == escapedEmphText)
                        {
                            var level = emphasis.Level switch
                            {
                                EmphasisLevel.Strong => "strong",
                                EmphasisLevel.Moderate => "moderate",
                                EmphasisLevel.Reduced => "reduced",
                                _ => "moderate"
                            };
                            
                            result = result.Remove(startPos, escapedEmphText.Length);
                            result = result.Insert(startPos, $"<emphasis level=\"{level}\">{escapedEmphText}</emphasis>");
                        }
                    }
                }
            }
        }
        
        return result;
    }

    private int CalculateAdjustedPosition(string originalText, int originalPosition, string escapedText)
    {
        var adjustedPos = 0;
        var originalPos = 0;
        
        while (originalPos < originalPosition && adjustedPos < escapedText.Length)
        {
            var origChar = originalText[originalPos];
            
            if (origChar == '&')
            {
                if (escapedText.Substring(adjustedPos).StartsWith("&amp;"))
                {
                    adjustedPos += 5;
                }
                else if (escapedText.Substring(adjustedPos).StartsWith("&apos;"))
                {
                    adjustedPos += 6;
                }
                else
                {
                    adjustedPos++;
                }
            }
            else if (origChar == '<' && escapedText.Substring(adjustedPos).StartsWith("&lt;"))
            {
                adjustedPos += 4;
            }
            else if (origChar == '>' && escapedText.Substring(adjustedPos).StartsWith("&gt;"))
            {
                adjustedPos += 4;
            }
            else if (origChar == '"' && escapedText.Substring(adjustedPos).StartsWith("&quot;"))
            {
                adjustedPos += 6;
            }
            else if (origChar == '\'' && escapedText.Substring(adjustedPos).StartsWith("&apos;"))
            {
                adjustedPos += 6;
            }
            else
            {
                adjustedPos++;
            }
            
            originalPos++;
        }
        
        return adjustedPos;
    }

    private void ValidateProsodyAttributes(XmlNode node, List<string> warnings, List<SSMLRepairSuggestion> suggestions)
    {
        var constraints = GetConstraints();
        
        if (node.Attributes == null) return;

        var rateAttr = node.Attributes["rate"];
        if (rateAttr != null)
        {
            var rateValue = ParseRate(rateAttr.Value);
            if (rateValue < constraints.RateRange.Min || rateValue > constraints.RateRange.Max)
            {
                warnings.Add($"Rate {rateValue} outside valid range [{constraints.RateRange.Min}, {constraints.RateRange.Max}]");
            }
        }

        var pitchAttr = node.Attributes["pitch"];
        if (pitchAttr != null)
        {
            var pitchValue = ParsePitch(pitchAttr.Value);
            if (pitchValue < constraints.PitchRange.Min || pitchValue > constraints.PitchRange.Max)
            {
                warnings.Add($"Pitch {pitchValue} outside valid range [{constraints.PitchRange.Min}, {constraints.PitchRange.Max}]");
            }
        }
    }

    private void ValidateBreakAttributes(XmlNode node, List<string> warnings, List<SSMLRepairSuggestion> suggestions)
    {
        var constraints = GetConstraints();
        
        if (node.Attributes == null) return;

        var timeAttr = node.Attributes["time"];
        if (timeAttr != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(timeAttr.Value, @"^(\d+)(ms|s)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            {
                var unit = match.Groups[2].Value;
                var durationMs = unit == "s" ? value * 1000 : value;
                
                if (durationMs > constraints.MaxPauseDurationMs)
                {
                    warnings.Add($"Break duration {durationMs}ms exceeds maximum {constraints.MaxPauseDurationMs}ms");
                }
            }
        }
    }

    private double ParseRate(string rateValue)
    {
        if (rateValue.EndsWith("%"))
        {
            if (double.TryParse(rateValue.TrimEnd('%'), out var percent))
            {
                return 1.0 + (percent / 100.0);
            }
        }
        else if (double.TryParse(rateValue, out var rate))
        {
            return rate;
        }
        
        return 1.0;
    }

    private double ParsePitch(string pitchValue)
    {
        if (pitchValue.EndsWith("Hz"))
        {
            if (double.TryParse(pitchValue.TrimEnd('H', 'z'), out var hz))
            {
                return hz;
            }
        }
        else if (pitchValue.EndsWith("%"))
        {
            if (double.TryParse(pitchValue.TrimEnd('%'), out var percent))
            {
                return percent;
            }
        }
        else if (double.TryParse(pitchValue, out var pitch))
        {
            return pitch;
        }
        
        return 0.0;
    }

    private string XmlEscape(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
