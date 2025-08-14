using SonicConnect;
using BanyanWordNet;


namespace BanyanSearch
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            SonicConnect.Document[] documents = GetDocuments("books", "romance", @"C:\The Banyan\BanyanSearchEngine\Books\OEBPS\Text");
            dynamic result;
            /* 
            //Ingesting New Documents: Open Ingest channel->PUSH documents into sonic for indexing-> QUIT from Ingest Channel
            //Optional : Flush all the content 
            IngestChannel ingest = new IngestChannel("localhost", 1491, "SecretPassword", 10, 5);
            result = await ingest.StartChannelAsync();

            //Clean start. flush all objects from collection "books"
            result = await ingest.FLUSH_CollectionAsync("books");
            Console.WriteLine($"FLUSH : " + result.Item2);

            //Indexing documents
            foreach (var doc in documents)
            {
                result = await ingest.PUSHAsync(doc);
                Console.WriteLine($"PUSH : " + result.Item2);
            }

            result = await ingest.QUITAsync();
            Console.WriteLine($"QUIT from Ingest : " + result.Item2);
            */

            SearchChannel search = new SearchChannel("localhost", 1491, "SecretPassword");
            search.DelayTime = 0;
            result = await search.StartChannelAsync();

            //await SuggestAsUserTypes(search);

            Console.WriteLine($"QUERY : " + result.Item2);


            //Searching for a lemma words for a given word
            string word = "puppy";
            string[] lemmas = WordNet.GetLemmas(word, WordNet.SearchType.Hypernym);
            //string terms = String.Join(" ", lemmas).Trim();

            Console.WriteLine($"Lemmas for '{word}': {String.Join(' ', lemmas)}\n");

            //result = await search.QUERYAsync("books", "romance", terms);
            //OR
            //Dictionary<string, string[]>  Lemma_IDs = await search.GetObjectsForAnyKeywordsPerKeyword("books", "romance", lemmas);
            //foreach (var item in Lemma_IDs) Console.WriteLine($"Lemma: {item.Key}, IDs: {string.Join(", ", item.Value)}");
            //OR
            //string[] IDs = await search.GetObjectsBySingleTerm("books", "romance", word);
            //Console.WriteLine($"IDs for '{word}': {string.Join(", ", IDs)}\n");
            //OR
            string[] IDs = await search.GetObjectsForAnyKeywords("books", "romance", lemmas);
            Console.WriteLine($"IDs for '{word}': {string.Join(", ", IDs)}\n");
            //OR
            //string[] IDs = await search.GetObjectsForAllKeywords("books", "romance", lemmas);
            //Console.WriteLine($"IDs for '{word}': {string.Join(", ", IDs)}\n");

            result = await search.QUITAsync();
            Console.WriteLine($"QUIT from Search : " + result.Item2);
        }
        static SonicConnect.Document[] GetDocuments(string collection,string bucket,string FileDirectory)
        {
            SonicConnect.Document[] docs = new SonicConnect.Document[] {
            new SonicConnect.Document("books", "romance", "book1", "The quick brown fox jumps over the lazy dog."),
            new SonicConnect.Document("books", "romance", "book2", "A fast brown fox is really energetic."),
            new SonicConnect.Document("books", "romance", "book3", "The dog slept all day."),
            new SonicConnect.Document("books", "romance", "book4", "Red apple and green apple are healthy.")
            };
            return docs;


            DirectoryInfo contentDir = new DirectoryInfo(FileDirectory);
            FileInfo[] Files = contentDir.GetFiles("*.xhtml", SearchOption.AllDirectories);
            SonicConnect.Document[] documents = new SonicConnect.Document[Files.Length];

            //string bookTitle = String.Empty;
            string bookContent = String.Empty;
            foreach (var file in Files)
            {
                bookContent = File.ReadAllText(contentDir.FullName + "\\" + file.Name);
                documents[Array.IndexOf(Files, file)] = new SonicConnect.Document(collection, bucket, file.Name, bookContent);
            }

            return documents;
        }

        static async Task SuggestAsUserTypes(SearchChannel search)
        {
            Console.WriteLine("Type your search query (press Enter to search, Esc to exit):");
            string currentInput = string.Empty;

            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine($"\nFinal Search: {currentInput}");
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("\nExited.");
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (currentInput.Length > 0)
                    {
                        currentInput = currentInput[..^1];
                        Console.Write("\b \b"); // remove last char from console
                    }
                }
                else
                {
                    currentInput += keyInfo.KeyChar;
                    Console.Write(keyInfo.KeyChar);
                }

                // Get auto-suggestions asynchronously
                //var suggestions = await search.QUERYAsync("books", "romance", currentInput);
                var suggestions = await search.SUGGESTAsync("books", "romance", currentInput);

                // Show suggestions below current line
                Console.WriteLine();
                Console.WriteLine("Suggestions:");
                //foreach (var s in suggestions)
                    Console.WriteLine($" - {suggestions}");

                // Restore cursor to typing position
                Console.Write(currentInput);
            }
        }
    }
}
