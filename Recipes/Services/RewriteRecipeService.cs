using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Recipes.Services
{
    public class RewriteRecipeService
    {
        private readonly Dictionary<string, List<string>> _synonyms;
        private readonly Random _rnd = new Random();

        public RewriteRecipeService()
        {
            _synonyms = LoadSynonyms();
        }

        // ------------------------------------------------------
        // PUBLIC ENTRY POINT
        // ------------------------------------------------------
        public string RewriteText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            string text = input;

            text = ReplaceSynonyms(text);
            text = ImproveCulinaryTone(text);
            text = InjectRandomAdjectives(text);
            text = EnhanceVerbs(text);
            text = RestructureSentences(text);
            text = ShuffleSentencePatterns(text);
            text = CleanupFormatting(text);

            return text.Trim();
        }

        // ------------------------------------------------------
        // LOAD synonyms.json
        // ------------------------------------------------------
        private Dictionary<string, List<string>> LoadSynonyms()
        {
            try
            {
                string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                string jsonPath = Path.Combine(dataFolder, "synonyms.json");

                if (!File.Exists(jsonPath))
                    return new Dictionary<string, List<string>>();

                string json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                       ?? new Dictionary<string, List<string>>();
            }
            catch
            {
                return new Dictionary<string, List<string>>();
            }
        }

        // ------------------------------------------------------
        // SYNONYM REPLACER (synonyms.json)
        // ------------------------------------------------------
        private string ReplaceSynonyms(string text)
        {
            if (_synonyms == null || _synonyms.Count == 0)
                return text;

            foreach (var pair in _synonyms)
            {
                string find = pair.Key;
                List<string> options = pair.Value;

                if (options.Count == 0)
                    continue;

                string replacement = options[_rnd.Next(options.Count)];

                text = Regex.Replace(
                    text,
                    $@"\b{Regex.Escape(find)}\b",
                    replacement,
                    RegexOptions.IgnoreCase
                );
            }

            return text;
        }

        // ------------------------------------------------------
        // CULINARY TONE ENHANCER (new)
        // Makes instructions more "chef-like"
        // ------------------------------------------------------
        private string ImproveCulinaryTone(string text)
        {
            var replacements = new Dictionary<string, string>
            {
                { "mix", "combine" },
                { "stir", "stir gently" },
                { "add", "fold in" },
                { "put", "place" },
                { "cook", "cook over moderate heat" },
                { "boil", "bring to a gentle boil" },
                { "fry", "pan-fry lightly" },
                { "slice", "slice thinly" },
                { "chop", "finely chop" },
                { "heat", "warm" },
                { "pour", "slowly pour" },
                { "season", "season generously" },
                { "serve", "serve immediately" }
            };

            foreach (var r in replacements)
            {
                text = Regex.Replace(text, $@"\b{r.Key}\b", r.Value, RegexOptions.IgnoreCase);
            }

            return text;
        }

        // ------------------------------------------------------
        // VERB REFINER (modern culinary verbs)
        // ------------------------------------------------------
        private string EnhanceVerbs(string text)
        {
            var upgrades = new Dictionary<string, string>
            {
                { "mix well", "mix thoroughly" },
                { "stir well", "stir until smooth" },
                { "cook until", "cook until nicely developed" },
                { "bake for", "bake until golden and fragrant for" },
                { "let it cool", "allow it to cool completely" }
            };

            foreach (var u in upgrades)
            {
                text = Regex.Replace(text, u.Key, u.Value, RegexOptions.IgnoreCase);
            }

            return text;
        }

        // ------------------------------------------------------
        // RANDOM ADJECTIVES
        // ------------------------------------------------------
        private static readonly string[] Adjectives = new[]
        {
            "fresh", "aromatic", "rich", "savory", "light",
            "tender", "zesty", "warm", "flavor-packed", "hearty",
            "smooth", "velvety", "wholesome", "lively", "fragrant",
            "crispy", "golden", "balanced", "subtle", "juicy",
            "silky", "soft", "bright", "comforting", "tasty"
        };

        private string InjectRandomAdjectives(string text)
        {
            var keywords = new[]
            {
                "chicken","tomato","onion","garlic",
                "sauce","bread","butter","pasta","salad",
                "rice","soup","herbs","oil","cheese"
            };

            var words = text.Split(' ').ToList();

            for (int i = 0; i < words.Count; i++)
            {
                string clean = words[i].ToLower().Trim(',', '.', ';', ':');

                if (keywords.Contains(clean) && _rnd.NextDouble() < 0.20)
                {
                    string adj = Adjectives[_rnd.Next(Adjectives.Length)];
                    words[i] = adj + " " + words[i];
                }
            }

            return string.Join(" ", words);
        }

        // ------------------------------------------------------
        // SENTENCE RESTRUCTURING (same logic but smoother)
        // ------------------------------------------------------
        private string RestructureSentences(string text)
        {
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .ToList();

            for (int i = 0; i < sentences.Count; i++)
            {
                string s = sentences[i].Trim();

                // Clause flip
                if (s.Contains(",") && _rnd.NextDouble() < 0.25)
                {
                    var parts = s.Split(',');
                    if (parts.Length >= 2)
                    {
                        sentences[i] =
                            $"{parts[1].Trim()} after {parts[0].Trim('.')}.".Trim();
                        continue;
                    }
                }

                // Break long lines
                if (s.Length > 90 && _rnd.NextDouble() < 0.25)
                {
                    int mid = s.IndexOf(' ', s.Length / 2);
                    if (mid > 0)
                    {
                        sentences[i] = s[..mid] + ". " + s[(mid + 1)..].TrimStart();
                        continue;
                    }
                }

                // Merge small ones
                if (i < sentences.Count - 1 &&
                    s.Length < 40 &&
                    _rnd.NextDouble() < 0.25)
                {
                    sentences[i] =
                        $"{s} Additionally, {sentences[i + 1].Trim().Trim('.')}.";
                    sentences.RemoveAt(i + 1);
                }
            }

            return string.Join(" ", sentences);
        }

        // ------------------------------------------------------
        // SENTENCE SHUFFLING
        // ------------------------------------------------------
        private string ShuffleSentencePatterns(string text)
        {
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .ToList();

            if (sentences.Count > 3)
            {
                var first = sentences[0];
                sentences.RemoveAt(0);
                sentences.Insert(sentences.Count - 1, first);
            }

            return string.Join(" ", sentences);
        }

        // ------------------------------------------------------
        // CLEANUP
        // ------------------------------------------------------
        private string CleanupFormatting(string text)
        {
            text = text.Replace("\r", "");
            while (text.Contains("  "))
                text = text.Replace("  ", " ");

            return text.Trim();
        }

        // ------------------------------------------------------
        // TITLE REWRITER — STRONG & NATURAL
        // ------------------------------------------------------
        public string RewriteTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return title;

            string newTitle = ReplaceSynonyms(title);

            // Add rich descriptive tone
            string[] descriptors =
            {
                "Homestyle", "Classic", "Rustic", "Authentic",
                "Cozy", "Aromatic", "Golden", "Hearty",
                "Flavorful", "Freshly Made", "Creamy", "Rich"
            };

            if (_rnd.NextDouble() < 0.55)
            {
                string word = descriptors[_rnd.Next(descriptors.Length)];
                newTitle = $"{word} {newTitle}";
            }

            // Optional reordering
            if (_rnd.NextDouble() < 0.30)
            {
                var words = newTitle.Split(' ');
                if (words.Length > 2)
                {
                    var last = words.Last();
                    newTitle = $"{last} {string.Join(" ", words.Take(words.Length - 1))}";
                }
            }

            return Regex.Replace(newTitle, "\\s+", " ").Trim();
        }
    }
}
