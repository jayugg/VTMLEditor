using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Vintagestory.API.Client;

namespace VTMLEditor.TextHighlighting;

/// <summary>
/// VTML token types for simple syntax highlighting.
/// </summary>
public enum VtmlTokenType
{
    Text,
    TagDelimiter,  // Opening (<) and closing (>) delimiters.
    TagName,       // Element names.
    AttributeName,
    EqualsSign,
    AttributeValue
}

/// <summary>
/// Represents a VTML token with its content and type.
/// </summary>
public class VtmlToken
{
    public string Content { get; set; }
    public VtmlTokenType TokenType { get; set; }
}

/// <summary>
/// Provides tokenization methods for VTML source code.
/// </summary>
public static class VtmlTokenizer
{
    /// <summary>
    /// Tokenizes a VTML string into tokens.
    /// </summary>
    /// <param name="text">The VTML source text.</param>
    /// <returns>A list of <see cref="VtmlToken"/> objects.</returns>
    public static List<VtmlToken> Tokenize(string text)
    {
        var tokens = new List<VtmlToken>();
        int lastPos = 0;
        // Use regex to locate VTML tags.
        var tagRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);
        foreach (Match tagMatch in tagRegex.Matches(text))
        {
            // Add text preceding the tag.
            if (tagMatch.Index > lastPos)
            {
                tokens.Add(new VtmlToken
                {
                    Content = text.Substring(lastPos, tagMatch.Index - lastPos),
                    TokenType = VtmlTokenType.Text
                });
            }
            // Add the opening tag delimiter.
            tokens.Add(new VtmlToken
            {
                Content = "<",
                TokenType = VtmlTokenType.TagDelimiter
            });
            // Process the tag content.
            string innerContent = tagMatch.Value.Substring(1, tagMatch.Value.Length - 2);
            tokens.AddRange(TokenizeTagContent(innerContent));
            // Add the closing tag delimiter.
            tokens.Add(new VtmlToken
            {
                Content = ">",
                TokenType = VtmlTokenType.TagDelimiter
            });
            lastPos = tagMatch.Index + tagMatch.Length;
        }
        // Add remaining text after the last tag.
        if (lastPos < text.Length)
        {
            tokens.Add(new VtmlToken
            {
                Content = text.Substring(lastPos),
                TokenType = VtmlTokenType.Text
            });
        }
        return tokens;
    }

    /// <summary>
    /// Processes the inner content of a tag into tokens.
    /// Extracts the tag name and attributes, preserving whitespace.
    /// </summary>
    /// <param name="content">The inner content of a tag.</param>
    /// <returns>A list of <see cref="VtmlToken"/> tokens.</returns>
    private static List<VtmlToken> TokenizeTagContent(string content)
    {
        var tokens = new List<VtmlToken>();
        int pos = 0;

        // Match tag name and its leading whitespace.
        var tagNameMatch = Regex.Match(content, @"^(\s*)(\S+)");
        if (tagNameMatch.Success)
        {
            // Add any leading whitespace.
            if (!string.IsNullOrEmpty(tagNameMatch.Groups[1].Value))
            {
                tokens.Add(new VtmlToken
                {
                    Content = tagNameMatch.Groups[1].Value,
                    TokenType = VtmlTokenType.Text
                });
            }
            // Add the tag name using TagName token type.
            tokens.Add(new VtmlToken
            {
                Content = tagNameMatch.Groups[2].Value,
                TokenType = VtmlTokenType.TagName
            });
            pos = tagNameMatch.Index + tagNameMatch.Length;
        }

        // Regex to match attributes.
        // Group 1: whitespace before attribute name.
        // Group 2: attribute name.
        // Group 3: equals sign (including surrounding whitespace).
        // Group 4: attribute value (with quotes).
        var attrRegex = new Regex(@"(\s*)(\S+)(\s*=\s*)(""[^""]*"")", RegexOptions.Compiled);
        while (pos < content.Length)
        {
            var attrMatch = attrRegex.Match(content, pos);
            if (attrMatch.Success && attrMatch.Index == pos)
            {
                // Add whitespace before the attribute.
                if (!string.IsNullOrEmpty(attrMatch.Groups[1].Value))
                {
                    tokens.Add(new VtmlToken
                    {
                        Content = attrMatch.Groups[1].Value,
                        TokenType = VtmlTokenType.Text
                    });
                }
                // Attribute name.
                tokens.Add(new VtmlToken
                {
                    Content = attrMatch.Groups[2].Value,
                    TokenType = VtmlTokenType.AttributeName
                });
                // Equals sign.
                tokens.Add(new VtmlToken
                {
                    Content = attrMatch.Groups[3].Value,
                    TokenType = VtmlTokenType.EqualsSign
                });
                // Attribute value.
                tokens.Add(new VtmlToken
                {
                    Content = attrMatch.Groups[4].Value,
                    TokenType = VtmlTokenType.AttributeValue
                });
                pos += attrMatch.Length;
            }
            else
            {
                // Add any remaining content as text.
                tokens.Add(new VtmlToken
                {
                    Content = content.Substring(pos),
                    TokenType = VtmlTokenType.Text
                });
                break;
            }
        }
        return tokens;
    }

    /// <summary>
    /// Tokenizes an array of text lines and splits tokens that cross line boundaries.
    /// </summary>
    /// <param name="lines">The array of text lines.</param>
    /// <returns>A list of token lists, one for each line.</returns>
    public static List<List<VtmlToken>> Tokenize(TextLine[] lines)
    {
        // Join full text from all lines using newline.
        string fullText = lines.Join(line => line.Text, "\n");
        List<VtmlToken> fullTextTokens = Tokenize(fullText);
        var linesTokens = new List<List<VtmlToken>>();
        var currentLineTokens = new List<VtmlToken>();

        foreach (var token in fullTextTokens)
        {
            if (token.Content.Contains("\n"))
            {
                string[] parts = token.Content.Split('\n');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0)
                    {
                        if (parts[i].Length > 0)
                        {
                            currentLineTokens.Add(new VtmlToken
                            {
                                Content = parts[i],
                                TokenType = token.TokenType
                            });
                        }
                    }
                    else
                    {
                        linesTokens.Add(new List<VtmlToken>(currentLineTokens));
                        currentLineTokens.Clear();
                        if (parts[i].Length > 0)
                        {
                            currentLineTokens.Add(new VtmlToken
                            {
                                Content = parts[i],
                                TokenType = token.TokenType
                            });
                        }
                    }
                }
            }
            else
            {
                currentLineTokens.Add(token);
            }
        }
        linesTokens.Add(new List<VtmlToken>(currentLineTokens));
        return linesTokens;
    }
}