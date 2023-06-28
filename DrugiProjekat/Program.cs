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
                        var content = await response.Content.ReadAsStringAsync();
                        dynamic comments = JsonConvert.DeserializeObject(content);
                        //Console.WriteLine(comments["items"]);
                        foreach (var comment in comments["items"])
                        {
                            string post = comment["snippet"]["topLevelComment"]["snippet"]["textOriginal"];
                            commentSubject.OnNext(post);
                        }
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "text/plain";
                        string data = $"Status code: {(int)HttpStatusCode.OK}.\n Uspesno poslat zahtev";
                        byte[] buffer = Encoding.UTF8.GetBytes(data);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.Close();
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

/*string text = @"It's easy to understand for developers and 'plays well with everything'.
            Don't underestimate the importance of toolchain support. Want to spin up a Ruby microservice which speaks HTTP?
            Sinatra and you're done. Go? Whatever the Go HTTP library is and you're done. Need to interact with it from the command line?
        Curl and you're done. How about from an automated testing script written in Ruby? Net:HTTP/HTTParty and you're done.
        Thinking about how to deploy it vis-a-vis firewall/etc? Port 80 and you're done. Need coarse-grained access logging?
        Built into Nginx/etc already; you're done. Uptime monitoring? Expose a /monitor endpoint; provide URL to existing monitoring solution;
        you're done. Deployment orchestration? Use Capistrano/shell scripts/whatever you already use for the app proper and you're done. 
        Encrypted transport? HTTPS and you're done. Auth/auth? A few options that you're very well-acquainted with and are known to be 
        consumable on both ends of the service trivially.
            Edit to note: I'm assuming, in the above, that one is already sold on the benefits of a microservice 
        architecture for one's particular app/team and is deciding on transport layer for the architecture. 
        FWIW, I run ~4 distinct applications, and most of them are comparatively large monolithic Rails apps.
        My company's bookkeeping runs in the same memory space as bingo card PDF generation.
            Things that would tilt me more towards microservices include a very rapid deployment pace, 
        large engineering organizations which multiple distinct teams which each want to be able to control 
        deploys/architecture decisions, particular hotspots in the application which just don't play well with 
        one's main technology stack (as apparently happened in the app featured in this article), etc.";*/