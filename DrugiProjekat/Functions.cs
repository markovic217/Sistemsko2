using DrugiProjekat.Properties;
using static DrugiProjekat.Program;

namespace DrugiProjekat
{
    static class Functions
    {
        public static string letterArray = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string wordsPath = @"C:\Users\gornj\Desktop\Sistemsko\DrugiProjekat\DrugiProjekat\Resources\5000words.txt";
        public static string PartOfSpeech(string word)
        {
            string? wordLine =
            File.ReadLines(wordsPath)
            .FirstOrDefault(l => l.Contains(word.ToLower()));
            var PoS = wordLine?.Split("\t");
            var pos = PoS?[2];
            string p;
            if (pos == null)
                p = "undefined";
            else
                p = pos;

            return p;
        }

        public static IEnumerable<string> SplittingToken(string token)
        {
            var punctuation = token.Where(Char.IsPunctuation).Distinct().ToArray();
            IEnumerable<string> words = token.Split().Select(x => x.Trim(punctuation));
            return words;
        }

        public static List<PoSClass> getMostUsedWords()
        {
            List<PoSClass> posList = new List<PoSClass>();
            File.ReadLines(@"C:\Users\gornj\Desktop\Sistemsko\DrugiProjekat\DrugiProjekat\Resources\5000words.txt")
                .Take(300)
                .ToList()
                .ForEach(line => { string[] splitLine = line.Split("\t"); posList.Add(new PoSClass(splitLine[1], splitLine[2])); });
            return posList;
        }

        public static bool filterA(string comment)
        {
            for (int i = 0; i <= 8; i++)
                if (comment.StartsWith(letterArray[i]))
                    return true;
            return false;
        }

        public static bool filterB(string comment)
        {
            for (int i = 9; i <= 17; i++)
                if (comment.StartsWith(letterArray[i]))
                    return true;
            return false;
        }

        public static bool filterC(string comment)
        {
            for (int i = 18; i <= 25; i++)
                if (comment.StartsWith(letterArray[i]))
                    return true;
            return false;
        }
    }
}