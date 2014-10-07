using Funq;
using ServiceStack.Common.Web;
using ServiceStack.Messaging;
using ServiceStack.Redis;
using ServiceStack.Redis.Messaging;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceInterface;
using ServiceStack.WebHost.Endpoints;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace SS39Q
{
    public class AppHost : AppHostBase
    {
        public AppHost() : base("MyService", typeof (AppHost).Assembly)
        {
        }

        public override void Configure(Container container)
        {
            SetConfig(new EndpointHostConfig {DebugMode = true});

            container.Register<IRedisClientsManager>(x => new PooledRedisClientManager(new[] { "localhost:6379" }));
            container.Register<IMessageService>(new RedisMqServer(container.Resolve<IRedisClientsManager>()));
            container.Register(container.Resolve<IMessageService>().MessageFactory.CreateMessageQueueClient());

            using (var mqService = container.Resolve<IMessageService>())
            {
                //mqService.RegisterHandler<Hello>(ServiceController.ExecuteMessage);
                mqService.RegisterHandler<Hello>(message =>
                {
                    Hello hello = message.GetBody();
                    Processor.Process(hello.Timeout, hello.Name);
                    return null;
                });
                mqService.Start();
            }
        }
    }

    [Route("/hello/{Name}/{Timeout}")]
    public class Hello
    {
        public int Timeout { get; set; }
        public string Name { get; set; }
    }
    
    public class HelloService : Service
    {
        public IMessageQueueClient QueueClient { get; set; }

        public object Any(Hello request)
        {
            QueueClient.Publish(request);
            return new HttpResult(HttpStatusCode.OK, 
                "Request received. Your data will be processed soon." + request.Name);
        }
    }

    public static class Processor
    {
        private const string FILE = @"C:\temp\file.txt";
        
        public static void Process(int timeout, string name)
        {
            string message = string.Format("[{0}] Hello world {1}\n", DateTime.Now, name);
            File.AppendAllText(FILE, message);
            Thread.Sleep(timeout);
            Debug.Write(message);
        }
    }
}