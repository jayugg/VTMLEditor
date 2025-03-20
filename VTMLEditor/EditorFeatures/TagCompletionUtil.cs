using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace VTMLEditor.EditorFeatures;

public class TagCompletionUtil
{
    // Regular expression that matches opening, closing, and self\-closing tags.
        // Group 1: slash if closing; Group 2: tag name; Group 3: slash if self‑closing.
        private static readonly Regex TagRegex = new Regex(@"<\s*(/)?\s*(\w+)(?:\s[^>]*?)?(\s*/)?\s*>", RegexOptions.Compiled);

        /// <summary>
        /// Processes the text and inserts closing tags immediately after an opening tag
        /// if there is not already a closing tag following it.
        /// </summary>
        /// <param name="text">The current text content</param>
        /// <returns>The text with closing tag(s) inserted next to their associated opening tags.</returns>
        public static string GetAutoCompletion(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var result = new StringBuilder();
            int lastIndex = 0;

            foreach (Match match in TagRegex.Matches(text))
            {
                // Append text between previous match and this tag.
                result.Append(text.Substring(lastIndex, match.Index - lastIndex));
                result.Append(match.Value);
                
                // Check if the tag is an opening tag, not self‐closing.
                bool isClosing = !string.IsNullOrEmpty(match.Groups[1].Value);
                bool isSelfClosing = !string.IsNullOrEmpty(match.Groups[3].Value);
                string tagName = match.Groups[2].Value;

                if (!isClosing && !isSelfClosing)
                {
                    // Determine the position after the tag and test for an immediate closing tag.
                    int afterTag = match.Index + match.Length;
                    string remainingText = text.Substring(afterTag).TrimStart();
                    string expectedClosing = $"</{tagName}>";
                    
                    if (!remainingText.StartsWith(expectedClosing, StringComparison.OrdinalIgnoreCase))
                    {
                        // No immediate closing tag found; insert one.
                        result.Append(expectedClosing);
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            result.Append(text.Substring(lastIndex));
            return result.ToString();
        }
        
        /// <summary>
        /// Applies auto‑completion by inserting closing tags immediately after their associated opening tags.
        /// </summary>
        /// <param name="text">The current text content</param>
        /// <returns>The modified text with auto‑completion applied.</returns>
        public static string ApplyAutoCompletion(string text)
        {
            return GetAutoCompletion(text);
        }
}