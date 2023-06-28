using NLTKSharp;
using System.Reflection.Metadata;
using System.Xml;
using System.Runtime.Caching;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Net;
using System.Reactive;
using System.Text;
using System.Runtime.CompilerServices;
//using System.Text.Json;

namespace DrugiProjekat
{
    internal class Program
    {
        static readonly object writeLock = new object();
        static readonly string server = "http://localhost:5050/";
        static MemoryCache cache = new MemoryCache("Memory Cache");
        public class PoSClass
        {
            public string Word { get; }
            public string? PoS { get; }
            public PoSClass(string word, string poS)
            {
                Word = word;
                PoS = poS;
            }
            public override string ToString()
            {
                return Word + "\t" + PoS;
            }
        }

        public class CommentStream : IObservable<string>
        {
            private readonly Subject<string> commentSubject;
            public CommentStream()
            {
                commentSubject = new Subject<string>();
            }
            public void GetComments(string videoId, HttpListenerContext context)
            {
                string apiKey = "AIzaSyAyDMPdbLoNYUS-a-gKAmWxnkZQoi-8XoU";
                HttpClient client = new HttpClient();
                var url = $"https://youtube.googleapis.com/youtube/v3/commentThreads?part=snippet%2Creplies&videoId={videoId}&key={apiKey}";
                Task streamingTask = Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"ThreadId: {Thread.CurrentThread.ManagedThreadId}");
                        Console.WriteLine(url);
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        Task responseTask = Task.Run(() =>
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.ContentType = "text/plain";
                            string data = $"Status code: {(int)HttpStatusCode.OK}.\n Uspesno poslat zahtev";
                            byte[] buffer = Encoding.UTF8.GetBytes(data);
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            context.Response.Close();
                        });
                        var content = await response.Content.ReadAsStringAsync();
                        dynamic comments = JsonConvert.DeserializeObject(content);
                        //Console.WriteLine(comments["items"]);
                        foreach (var comment in comments["items"])
                        {
                            string post = comment["snippet"]["topLevelComment"]["snippet"]["textOriginal"];
                            commentSubject.OnNext(post);
                        }
                        
                    }
                    catch (HttpRequestException e)
                    {               
                        Console.WriteLine($"Doslo je do greske: {e.Message}");
                        context.Response.StatusCode = (int)e.StatusCode;
                        context.Response.ContentType = "text/plain";
                        string data = $"Status code: {(int)e.StatusCode}.\n {e.Message}";
                        byte[] buffer = Encoding.UTF8.GetBytes(data);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.Close();
                    }
                });
            }
            public IDisposable Subscribe(IObserver<string> observer)
            {
                return commentSubject.Subscribe(observer);
            }
            public void OnCompleted()
            {
                commentSubject.OnCompleted();
            }
        }
        public class CommentObserver : IObserver<string>
        {
            string name;
            public CommentObserver(string name)
            {
                this.name = name;
            }

            public void OnNext(string post)
            {

                Console.WriteLine($"ThreadId: {Thread.CurrentThread.ManagedThreadId}, name: {name}");
                List<PoSClass> posList = new List<PoSClass>();
                List<string> token = SentenceTokenizer.Tokenize(post);
                PoSClass posClass;
                foreach (string t in token)
                {
                    IEnumerable<string> words = Functions.SplittingToken(t);

                    foreach (string word in words)
                    {
                        CacheItem cacheItem = cache.GetCacheItem(word);
                        if (cacheItem != null)
                        {
                            posClass = new PoSClass(word, cacheItem.Value.ToString());
                            //Console.WriteLine(pos.ToString());
                            posList.Add(posClass);
                        }
                        else
                        {
                            if (word != "")
                            {
                                string pos = Functions.PartOfSpeech(word);
                                posClass = new PoSClass(word, pos);
                                posList.Add(posClass);
                                //Console.WriteLine(posClass.ToString());
                                Task cachingTask = Task.Run(() =>
                                {
                                    cacheItem = new CacheItem(word, pos);
                                    CacheItemPolicy cacheItemPolicy = new CacheItemPolicy();
                                    cacheItemPolicy.SlidingExpiration = new TimeSpan(0, 15, 0);
                                    cache.Add(cacheItem, cacheItemPolicy);
                                });

                            }
                        }

                    }
                }
                lock (writeLock)
                {
                    posList.ForEach((posClass) => { Console.WriteLine(posClass.ToString()); });
                }
            }
            public void OnError(Exception e)
            {
                Console.WriteLine($"Doslo je do greske: {e.Message}");
            }
            public void OnCompleted()
            {
                Console.WriteLine("Zavrsavam citanje!");
            }
        }

        static void Main(string[] args)
        {
            List<PoSClass> poSList = Functions.getMostUsedWords();
            poSList.ForEach((pos) =>
            {
                CacheItem cacheItem = new CacheItem(pos.Word, pos.PoS);
                CacheItemPolicy cacheItemPolicy = new CacheItemPolicy();
                cacheItemPolicy.Priority = CacheItemPriority.NotRemovable;
                cache.Add(cacheItem, cacheItemPolicy);
            });
            CommentStream commentStream = new CommentStream();
            CommentObserver commentObserver1 = new CommentObserver("Observer1");
            CommentObserver commentObserver2 = new CommentObserver("Observer2");
            CommentObserver commentObserver3 = new CommentObserver("Observer3");
            var filterStreamA = commentStream.Where(comment => Functions.filterA(comment));
            var filterStreamB = commentStream.Where(comment => Functions.filterB(comment));
            var filterStreamC = commentStream.Where(comment => Functions.filterC(comment));

            //commentStream.SubscribeOn<string>(Scheduler.Default).ObserveOn<string>(Scheduler.Default).Subscribe(commentObserver1);
            filterStreamA.SubscribeOn(Scheduler.Default).ObserveOn(Scheduler.Default).Subscribe(commentObserver1);
            filterStreamB.SubscribeOn(Scheduler.Default).ObserveOn(Scheduler.Default).Subscribe(commentObserver2);
            filterStreamC.SubscribeOn(Scheduler.Default).ObserveOn(Scheduler.Default).Subscribe(commentObserver3);

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(server);

            listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                string videoId = context.Request.Url.ToString().Substring(server.Length);
                Task task = Task.Run(() =>
                {
                    commentStream.GetComments(videoId, context);                  
                });
            }
        }
    }
}
