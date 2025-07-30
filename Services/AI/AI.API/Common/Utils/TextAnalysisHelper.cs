using System.Text.RegularExpressions;

namespace AI.API.Common.Utils
{
    public static class TextAnalysisHelper
    {
        public static TextAnalysisResult AnalyzeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new TextAnalysisResult();
            }

            return new TextAnalysisResult
            {
                CharacterCount = text.Length,
                WordCount = CountWords(text),
                SentenceCount = CountSentences(text),
                ParagraphCount = CountParagraphs(text),
                LineCount = CountLines(text),
                WhitespaceCount = CountWhitespace(text),
                SpecialCharacterCount = CountSpecialCharacters(text),
                DigitCount = CountDigits(text),
                UppercaseCount = CountUppercase(text),
                LowercaseCount = CountLowercase(text),
                AverageWordsPerSentence = CalculateAverageWordsPerSentence(text),
                ReadabilityScore = CalculateReadabilityScore(text),
                ComplexityLevel = DetermineComplexityLevel(text),
                EstimatedReadingTime = EstimateReadingTime(text),
                LanguageHints = DetectLanguageHints(text),
                TopWords = GetTopWords(text, 10),
                SentimentHint = AnalyzeSentimentHint(text)
            };
        }

        private static int CountWords(string text)
        {
            return Regex.Matches(text, @"\b\w+\b").Count;
        }

        private static int CountSentences(string text)
        {
            return Regex.Matches(text, @"[.!?]+").Count;
        }

        private static int CountParagraphs(string text)
        {
            return text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static int CountLines(string text)
        {
            return text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static int CountWhitespace(string text)
        {
            return text.Count(char.IsWhiteSpace);
        }

        private static int CountSpecialCharacters(string text)
        {
            return text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        }

        private static int CountDigits(string text)
        {
            return text.Count(char.IsDigit);
        }

        private static int CountUppercase(string text)
        {
            return text.Count(char.IsUpper);
        }

        private static int CountLowercase(string text)
        {
            return text.Count(char.IsLower);
        }

        private static double CalculateAverageWordsPerSentence(string text)
        {
            var wordCount = CountWords(text);
            var sentenceCount = CountSentences(text);
            return sentenceCount > 0 ? (double)wordCount / sentenceCount : 0;
        }

        private static double CalculateReadabilityScore(string text)
        {
            // Simplified Flesch Reading Ease formula
            var words = CountWords(text);
            var sentences = CountSentences(text);
            var syllables = EstimateSyllables(text);

            if (sentences == 0 || words == 0) return 0;

            return 206.835 - (1.015 * words / sentences) - (84.6 * syllables / words);
        }

        private static int EstimateSyllables(string text)
        {
            // Simple syllable estimation
            var words = Regex.Matches(text, @"\b\w+\b");
            var totalSyllables = 0;

            foreach (Match word in words)
            {
                var syllables = Regex.Matches(word.Value.ToLower(), @"[aeiouy]+").Count;
                if (syllables == 0) syllables = 1; // Every word has at least one syllable
                totalSyllables += syllables;
            }

            return totalSyllables;
        }

        private static string DetermineComplexityLevel(string text)
        {
            var readabilityScore = CalculateReadabilityScore(text);
            
            return readabilityScore switch
            {
                >= 90 => "Very Easy",
                >= 80 => "Easy",
                >= 70 => "Fairly Easy",
                >= 60 => "Standard",
                >= 50 => "Fairly Difficult",
                >= 30 => "Difficult",
                _ => "Very Difficult"
            };
        }

        private static TimeSpan EstimateReadingTime(string text)
        {
            var wordCount = CountWords(text);
            var averageWordsPerMinute = 200; // Average reading speed
            var minutes = (double)wordCount / averageWordsPerMinute;
            return TimeSpan.FromMinutes(minutes);
        }

        private static List<string> DetectLanguageHints(string text)
        {
            var hints = new List<string>();

            // Simple language detection based on character patterns
            if (Regex.IsMatch(text, @"[а-яё]", RegexOptions.IgnoreCase))
                hints.Add("Russian");
            
            if (Regex.IsMatch(text, @"[àáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿ]", RegexOptions.IgnoreCase))
                hints.Add("European");
            
            if (Regex.IsMatch(text, @"[一-龯]"))
                hints.Add("Chinese");
            
            if (Regex.IsMatch(text, @"[ひらがなカタカナ]"))
                hints.Add("Japanese");
            
            if (Regex.IsMatch(text, @"[가-힣]"))
                hints.Add("Korean");

            if (hints.Count == 0)
                hints.Add("English/Latin");

            return hints;
        }

        private static List<WordFrequency> GetTopWords(string text, int count)
        {
            var words = Regex.Matches(text.ToLower(), @"\b\w{3,}\b") // Words with 3+ characters
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => !IsStopWord(w))
                .GroupBy(w => w)
                .Select(g => new WordFrequency { Word = g.Key, Count = g.Count() })
                .OrderByDescending(wf => wf.Count)
                .Take(count)
                .ToList();

            return words;
        }

        private static bool IsStopWord(string word)
        {
            var stopWords = new HashSet<string>
            {
                "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
                "from", "up", "about", "into", "through", "during", "before", "after", "above",
                "below", "between", "among", "this", "that", "these", "those", "is", "are", "was",
                "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will",
                "would", "could", "should", "may", "might", "must", "can", "shall"
            };

            return stopWords.Contains(word);
        }

        private static string AnalyzeSentimentHint(string text)
        {
            var positiveWords = new[] { "good", "great", "excellent", "amazing", "wonderful", "fantastic", "love", "like", "happy", "joy" };
            var negativeWords = new[] { "bad", "terrible", "awful", "hate", "dislike", "sad", "angry", "frustrated", "disappointed", "horrible" };

            var words = text.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            var positiveCount = words.Count(w => positiveWords.Contains(w));
            var negativeCount = words.Count(w => negativeWords.Contains(w));

            if (positiveCount > negativeCount) return "Positive";
            if (negativeCount > positiveCount) return "Negative";
            return "Neutral";
        }
    }

    public class TextAnalysisResult
    {
        public int CharacterCount { get; set; }
        public int WordCount { get; set; }
        public int SentenceCount { get; set; }
        public int ParagraphCount { get; set; }
        public int LineCount { get; set; }
        public int WhitespaceCount { get; set; }
        public int SpecialCharacterCount { get; set; }
        public int DigitCount { get; set; }
        public int UppercaseCount { get; set; }
        public int LowercaseCount { get; set; }
        public double AverageWordsPerSentence { get; set; }
        public double ReadabilityScore { get; set; }
        public string ComplexityLevel { get; set; } = string.Empty;
        public TimeSpan EstimatedReadingTime { get; set; }
        public List<string> LanguageHints { get; set; } = new();
        public List<WordFrequency> TopWords { get; set; } = new();
        public string SentimentHint { get; set; } = string.Empty;
    }

    public class WordFrequency
    {
        public string Word { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
