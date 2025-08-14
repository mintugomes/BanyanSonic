using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SonicConnect
{
    public enum SonicChannel
    {
        ingest = 1,
        search = 2,
        control = 3
    }
    public class Base
    {
        protected static int _delaytime; // Default sleep time in milliseconds
        protected static int _tryCounter;
        protected static Connection _connection;

        protected static async Task<(bool, string)> ExecuteCommandAsync(SonicChannel channel, string command)
        {
            string Response = "";
            bool success = false;

            string SearchCommandID = string.Empty;

            if (!command.Contains('\n'))
                throw new ArgumentException("Command must end with a newline character.");
  
            await _connection.Writer.WriteLineAsync(command);

            (success, Response) = await GetResponseAsync(command);

            if(Response.StartsWith("Disconnected"))
            {
                _connection.Dispose(); //Should it be closed?
                throw new IOException("The remote server closed the connection.");
            }

            return (success, Response);
        }

        protected static async Task<(bool, string)> GetResponseAsync(string command)
        {
            bool success = false;
            string response = "";
            string line = "";
            //response = await connection.Reader.ReadLineAsync();
            string ID = "";
            
            if(command.StartsWith("SUGGEST"))
            {
                (success, response) = await GetResponseForQuerySuggest("SUGGEST");
            }
            else if(command.StartsWith("QUERY"))
            {
                (success, response) = await GetResponseForQuerySuggest("QUERY");
            }
            else if (command.StartsWith("PUSH"))
            {
                (success, response) = await LookForResponseAsync("OK");
            }
            else if (command.StartsWith("START"))
            {
                (success, response) = await LookForResponseAsync("STARTED");
            }
            else if (command.StartsWith("QUIT"))
            {
                (success, response) = await LookForResponseAsync("ENDED quit");
            }
            else if (command.StartsWith("FLUSH"))
            {
                (success, response) = await LookForResponseAsync("RESULT");
            }
            else
            {
                (success, response) = await LookForResponseAsync(null);
            }

            return (success, response);
        }

        protected static async Task<(bool, string)> LookForResponseAsync(string token)
        {
            string response = "";
            bool success = false;
            string? line = "";

            if (token == null || token.Trim().Length == 0)
                token = ""; // Default token to look for

            while (!line.StartsWith(token) || (token == "" && line.Trim().Length == 0))
            {
                await Task.Delay(_delaytime); // Ensure the delay is asynchronous

                //line = await _connection.Reader.ReadLineAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_connection.ResponseTimeOutInMSec)); // 10 sec timeout

                line = await _connection.Reader.ReadLineAsync(cts.Token);
                if (line == null)
                {
                    response = "Disconnected. The remote server closed the connection.";
                    throw new IOException(response);
                }

                line = line ?? "";
                _tryCounter--;
                if (_tryCounter == 0)
                    break;
            }

            if (line.StartsWith(token) || (token == "" && line.Trim().Length > 0))
            {
                response = line;
                success = true;
            }
            else
                response = $"Failed! Token '{token}' Not Found";

            return (success, response);
        }

        protected static async Task<(bool, string)> GetResponseForQuerySuggest(string QueryOrSuggest)
        {
            bool success = false;
            string line = "";
            string ID = "";
            string response = "";

            (success, line) = await LookForResponseAsync("PENDING ");

            if (line.StartsWith("PENDING "))
            {
                ID = line.Split(' ')[1];
            }
            else
            {
                response = $"No response received for {QueryOrSuggest} command.";
                return (success, response);
            }

            line = "";
            //Now that 'PENDING' is found
            (success, line) = await LookForResponseAsync($"EVENT {QueryOrSuggest} {ID} ");

            if (success)
                response = line.Replace($"EVENT {QueryOrSuggest} {ID} ", "");
            else
            {
                response = $"No response received for EVENT {QueryOrSuggest} command.";
            }

            return (success, response);
        }

        protected static async Task<(bool, string)> StartChannelAsync(SonicChannel channel)
        {
            string Response = "";
            bool success = false;

            _connection.Open();
            Response = await _connection.Reader.ReadLineAsync(); // Read the initial response from the server
            Debug.WriteLine($"Connection.Open : {Response}");
            if (!Response.StartsWith("CONNECTED "))
            {
                throw new IOException("Failed to connect to the server: " + Response);
            }

            var command = $"START {channel.ToString()} {_connection.Password}\n";
            (success, Response) = await ExecuteCommandAsync(channel, command);
            Debug.WriteLine($"START {channel.ToString()}: " + Response);
            return (success, Response);
        }

        protected static async Task<(bool, string)> PINGAsync(SonicChannel channel)
        {
            var command = "PING\n";
            var Response = await ExecuteCommandAsync(channel, command);
            return Response;
        }

        protected static async Task<(bool, string)> HELPAsync(SonicChannel channel, string? manual)
        {
            string strManual = string.Empty;
            if (manual != null)
                strManual = $" [{manual}]";

            var command = $"HELP{strManual}\n";
            var Response = await ExecuteCommandAsync(channel, command);
            return Response;
        }
        protected static async Task<(bool, string)> QUITAsync(SonicChannel channel)
        {
            var command = "QUIT\n";
            var Response = await ExecuteCommandAsync(channel, command);
            if (Response.Item2 == "ENDED quit")
            {
                _connection.Dispose();
                return Response;
            }
            else
            {
               Response.Item2 = "QUIT command failed: " + Response.Item2;
            }
            return Response;
        }
    }
    public class IngestChannel: Base
    {
        public int DelayTime
        {
            get { return _delaytime; }
            set { _delaytime = value; }
        }
        public int NumberOfAttempt
        {
            get { return _tryCounter; }
            set { _tryCounter = value; }
        }
        public IngestChannel(string host, int port, string password) : this(host, port, password, 50) { }
        public IngestChannel(string host, int port, string password,int DelayTime = 50, int NumberOfAttempt = 5)
        {
            _connection = new Connection(host, port, password);
            _delaytime = DelayTime;
            _tryCounter = NumberOfAttempt;
        }
        public async Task<(bool, string)> StartChannelAsync()
        {
            var Response = await StartChannelAsync(SonicChannel.ingest);
            Debug.WriteLine($"Start Ingest Channel: {Response.Item2}");
            return Response;
        }
        public async Task<(bool, string)> PUSHAsync(Document document)
        {
            string command = $"PUSH {document.Collection} {document.Bucket} {document.Id} \"{document.Text}\"\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> PUSHAsync(string collection, string bucket, string id, string text)
        {
            var command = $"PUSH {collection} {bucket} {id} \"{text}\"\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }

        public async Task<(bool, string)> POPAsync(string collection)
        {
            var command = $"POP {collection}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> POPAsync(string collection, string bucket)
        {
            var command = $"POP {collection} {bucket}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> POPAsync(string collection, string bucket, string id)
        {
            var command = $"POP {collection} {bucket} {id}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> COUNTAsync(string collection, string bucket)
        {
            var command = $"COUNT {collection} {bucket}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> FLUSH_CollectionAsync(string collection)
        {
            var command = $"FLUSHC {collection}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> FLUSH_BucketAsync(string collection, string bucket)
        {
            var command = $"FLUSHB {collection} {bucket}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }
        public async Task<(bool, string)> FLUSH_ObjectAsync(string collection, string bucket, string id)
        {
            var command = $"FLUSHO {collection} {bucket} {id}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.ingest, command);
            return Response;
        }

        public async Task<(bool, string)> HELPAsync(string? manual)
        {
            return await Base.HELPAsync(SonicChannel.ingest, manual);
        }

        public async Task<(bool, string)> PINGAsync(string? manual)
        {
            return await Base.PINGAsync(SonicChannel.ingest);
        }

        public Task<(bool, string)> QUITAsync() => QUITAsync(null);
        public async Task<(bool, string)> QUITAsync(string? manual)
        {
            return await Base.QUITAsync(SonicChannel.ingest);
        }
    }

    public class SearchChannel:Base
    {
        public int DelayTime
        {
            get { return _delaytime; }
            set { _delaytime = value; }
        }
        public int NumberOfAttempt
        {
            get { return _tryCounter; }
            set { _tryCounter = value; }
        }

        public SearchChannel(string host, int port, string password) : this(host, port, password, 50, 5) { }
        public SearchChannel(string host, int port, string password, int DelayTime = 50, int NumberOfAttempt = 5)
        {
            _connection = new Connection(host, port, password);                                                                                                                                     
            _delaytime = DelayTime;
            _tryCounter = NumberOfAttempt;
        }                                                                                                                                                              
        public async Task<(bool, string)> StartChannelAsync()
        {
            var Response = await StartChannelAsync(SonicChannel.search);
            Debug.WriteLine($"Start Search Channel: {Response.Item2}");
            return Response;
        }

        public Task<(bool, string)> QUERYAsync(string collection, string bucket, string terms) => QUERYAsync(collection, bucket, terms, null);
        public async Task<(bool, string)> QUERYAsync(string collection, string bucket, string terms, int? limit=null)
        {
            string strLimit = string.Empty;
            if (limit != null && limit > 0)
                strLimit = $" [LIMIT({limit.ToString()})]";

            var command = $"QUERY {collection} {bucket} \"{terms}\"{strLimit}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.search, command);
            if (Response.Item2.Length <= 0)
                Response.Item1 = false;
            return Response;
        }

        public Task<(bool, string)> SUGGESTAsync(string collection, string bucket, string word) => SUGGESTAsync(collection, bucket, word, null);
        public async Task<(bool, string)> SUGGESTAsync(string collection, string bucket, string word, int? limit)
        {
            string strLimit = string.Empty;
            if (limit != null && limit > 0)
                strLimit = $" [LIMIT({limit.ToString()})]";

            var command = $"SUGGEST {collection} {bucket} \"{word}\"{strLimit}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.search, command);
            return Response;
        }

        public Task<(bool, string)> LISTAsync(string password, string collection, string bucket) => LISTAsync(password, collection, bucket, null);
        public async Task<(bool, string)> LISTAsync(string password, string collection, string bucket, int? limit)
        {
            string strLimit = string.Empty;
            if (limit > 0)
                strLimit = $" [LIMIT({limit.ToString()})]";

            var command = $"LIST {collection} {bucket}{strLimit}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.search, command);
            return Response;
        }
        public async Task<(bool, string)> HELPAsync(string? manual)
        {
            return await Base.HELPAsync(SonicChannel.search, manual);
        }

        public async Task<(bool, string)> PINGAsync(string? manual)
        {
            return await Base.PINGAsync(SonicChannel.search);
        }

        public Task<(bool, string)> QUITAsync() => QUITAsync(null);
        public async Task<(bool, string)> QUITAsync(string? manual)
        {
            return await Base.QUITAsync(SonicChannel.ingest);
        }

        /// <summary>
        /// Search for items in a collection and bucket that match a single term and return their IDs as string[]
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="bucket"></param>
        /// <param name="word"></param>
        /// <returns></returns>
        public async Task<string[]> GetObjectsBySingleTerm(string collection, string bucket, string word)
        {
            string[] returnValue = Array.Empty<string>();
            var result = await QUERYAsync(collection, bucket, word);
            if (result.Item1)
            {
                returnValue = result.Item2.Split(' ');
            }
            return returnValue;
        }

        /// <summary>
        /// Search for items in a collection and bucket that contains all the keywords in the Keywords array and return their IDs as string[]. 
        /// Basically it runs the 'AND" operation with the keywords.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="bucket"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public async Task<string[]> GetObjectsForAllKeywords(string collection, string bucket, string[] keywords)
        {
            string terms = string.Join(" ", keywords);

            var result = await QUERYAsync(collection, bucket, terms);
            if (result.Item1)
            {
                return result.Item2.Split(' ');
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Search for items in a collection and bucket that contains any of the keywords in the Keywords array and return their IDs as string[]. 
        /// Basically it runs the 'OR" operation with the keywords.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="bucket"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public async Task<string[]> GetObjectsForAnyKeywords(string collection, string bucket, string[] keywords)
        {
            string[] lemmaIDs = new string[0]; // start empty
            int count = 0;

            foreach (string keyword in keywords)
            {
                var result = await QUERYAsync(collection, bucket, keyword);
                if (result.Item1)
                {
                    var IDs = result.Item2.Split(' ');
                    // Resize to fit new items
                    Array.Resize(ref lemmaIDs, count + IDs.Length);
                    Array.Copy(IDs, 0, lemmaIDs, count, IDs.Length);
                    count += IDs.Length;
                }
            }

            // If you want an array exactly the size of the items collected
            if (lemmaIDs.Length != count)
                Array.Resize(ref lemmaIDs, count);

            return lemmaIDs;
        }

        /// <summary>
        /// Search for items in a collection and bucket that contains any of the keywords in the Keywords array and return their IDs as string[] per matching keyword. 
        /// Basically it runs the 'OR" operation with the keywords and return items group by keywords.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="bucket"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string[]>> GetObjectsForAnyKeywordsPerKeyword(string collection,string bucket,string[] keywords)
        {
            Dictionary<string, string[]> Lemma_IDs = new Dictionary<string, string[]>();
            foreach (string keyword in keywords)
            {
                var result = await QUERYAsync(collection, bucket, keyword);
                if (result.Item1)
                {
                    var IDs = result.Item2.Split(' ').ToArray();
                    Lemma_IDs.Add(keyword, IDs);
                }
            }
            return Lemma_IDs;
        }
    }

    public class ControlChannel:Base
    {
        public int DelayTime
        {
            get { return _delaytime; }
            set { _delaytime = value; }
        }
        public int NumberOfAttempt
        {
            get { return _tryCounter; }
            set { _tryCounter = value; }
        }
        public ControlChannel(string host, int port, string password) : this(host, port, password, 50, 5) { }
        public ControlChannel(string host, int port, string password, int DelayTime = 50, int NumberOfAttempt = 5)
        {
            _connection = new Connection(host, port, password);
            _delaytime = DelayTime;
            _tryCounter = NumberOfAttempt;
        }

        public async Task<(bool, string)> StartChannelAsync()
        {
            var Response = await StartChannelAsync(SonicChannel.control);
            Debug.WriteLine($"Start Control Channel: {Response.Item2}");
            return Response;
        }
        public async Task<(bool, string)> TRIGGERAsync(string action, string? data)
        {
            string strData = string.Empty;
            if (data != null)
                strData = $" [{data}]";

            var command = $"TRIGGER {action}{strData}\n";
            var Response = await ExecuteCommandAsync(SonicChannel.control, command);
            return Response;
        }

        public static async Task<(bool, string)> INFOAsync()
        {
            var command = "INFO\n";
            var Response = await ExecuteCommandAsync(SonicChannel.control, command);
            return Response;
        }
        public async Task<(bool, string)> HELPAsync(string? manual)
        {
            return await Base.HELPAsync(SonicChannel.control, manual);
        }

        public async Task<(bool, string)> PINGAsync(string? manual)
        {
            return await Base.PINGAsync(SonicChannel.control);
        }

        public Task<(bool, string)> QUITAsync() => QUITAsync(null);
        public async Task<(bool, string)> QUITAsync(string? manual)
        {
            return await Base.QUITAsync(SonicChannel.ingest);
        }
    }
}
