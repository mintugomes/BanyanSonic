using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BanyanSearch.Program;

namespace BanyanWordNet
{
    public class WordNet
    {
        public static string WordNetDBPath { get; } = "Data Source=Assets/sqlite-31.db";

        public enum SearchType
        {
            Synonym = 1,
            Hypernym = 2,
            Supernym = 3
        }

        public static string synonymQuery { get; } = @"
                        SELECT w2.lemma
                        FROM words w1
                        JOIN senses s1 ON w1.wordid = s1.wordid AND w1.lemma = @word
                        JOIN senses s2 ON s1.synsetid = s2.synsetid
                        JOIN words w2 ON s2.wordid = w2.wordid
                        WHERE w2.wordid != w1.wordid;";

        public static string hypernymQuery { get; } = @"
                        SELECT w2.lemma
                        FROM words w1
                        JOIN senses s1 ON w1.wordid = s1.wordid and w1.lemma = @word
                        JOIN synsets ss1 ON s1.synsetid = ss1.synsetid
                        JOIN semlinks sl ON ss1.synsetid = sl.synset1id
                        JOIN linktypes l ON sl.linkid=l.linkid and l.link='hypernym'
                        JOIN synsets ss2 ON sl.synset2id = ss2.synsetid
                        JOIN senses s2 ON ss2.synsetid = s2.synsetid
                        JOIN words w2 ON s2.wordid = w2.wordid
                        WHERE w2.wordid != w1.wordid";

        public static string supernymQuery { get; } = @"
                        SELECT w2.lemma--, lt.link
                        FROM words w1
                        JOIN senses s1 ON w1.wordid = s1.wordid and w1.lemma = @word
                        JOIN synsets ss1 ON s1.synsetid = ss1.synsetid
                        JOIN semlinks sl ON ss1.synsetid = sl.synset1id
                        JOIN linktypes lt ON sl.linkid = lt.linkid
                        JOIN synsets ss2 ON sl.synset2id = ss2.synsetid
                        JOIN senses s2 ON ss2.synsetid = s2.synsetid
                        JOIN words w2 ON s2.wordid = w2.wordid
                        where w2.wordid != w1.wordid";

        public static string[] GetLemmas(string word, SearchType searchType)
        {
            word = word.ToLower();
            try
            {
                string dbPath = WordNet.WordNetDBPath;

                using var connection = new SqliteConnection(dbPath);
                connection.Open();

                string query = searchType switch
                {
                    SearchType.Synonym => WordNet.synonymQuery,
                    SearchType.Hypernym => WordNet.hypernymQuery,
                    SearchType.Supernym => WordNet.supernymQuery,
                    _ => throw new ArgumentException("Invalid search type")
                };

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@word", word);

                using var reader = command.ExecuteReader();

                string[] temp = new string[100]; // Initial capacity
                int tempCount = 0;

                // Add the original word
                temp[tempCount++] = word.Replace(' ', '_');

                // Read from DB
                while (reader.Read())
                {
                    if (tempCount == temp.Length)
                    {
                        Array.Resize(ref temp, temp.Length + 20);
                    }

                    temp[tempCount++] = reader.GetString(0).Trim().Replace(' ', '_');
                }

                // Step 2: Sort the array
                Array.Sort(temp, 0, tempCount);

                // Step 3: Deduplicate into a new array
                string[] deduplicated = new string[tempCount];
                int dedupCount = 0;
                string? last = null;

                for (int i = 0; i < tempCount; i++)
                {
                    if (temp[i] != last)
                    {
                        deduplicated[dedupCount++] = temp[i];
                        last = temp[i];
                    }
                }

                // Optional: Resize deduplicated array to actual count
                Array.Resize(ref deduplicated, dedupCount);
                return deduplicated;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception(ex.Message);
            }
        }
    }
}
