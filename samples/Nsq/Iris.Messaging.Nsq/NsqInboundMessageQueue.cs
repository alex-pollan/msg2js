﻿using Iris.Logging;
using Newtonsoft.Json;
using NsqSharp.Bus.Configuration;
using NsqSharp.Bus.Configuration.BuiltIn;
using System;
using System.Collections.Generic;

namespace Iris.Messaging.Nsq
{
    public class NsqInboundMessageQueue : IInboundMessageQueue, IDisposable 
    {
        private readonly BusConfiguration _busConfiguration;

        public ICollection<Type> MessageTypes { get; } = new List<Type>();

        //TODO: consolidate DI
        public NsqInboundMessageQueue(IMessageDispatcher dispatcher, ILogger logger,
            NsqConfiguration configuration)
        {
            foreach (var item in configuration.MessageTypeTopics.Keys)
            {
                MessageTypes.Add(item);
            }
            
            var structureMapContainer = new StructureMap.Container();
            structureMapContainer.Configure(p =>
            {
                p.Scan(x =>
                {
                    x.TheCallingAssembly();
                    x.WithDefaultConventions();
                });

                p.For<IMessageDispatcher>().Use(dispatcher);
                p.For<ILogger>().Use(logger);
            });

            var messageTopics = new Dictionary<Type, string>(configuration.MessageTypeTopics);
            var handlerChannels = new Dictionary<Type, string>(configuration.MessageHandlerTypeChannels);

            _busConfiguration = new BusConfiguration(
                new StructureMapObjectBuilder(structureMapContainer), // dependency injection container
                new NewtonsoftJsonSerializer(typeof(JsonConvert).Assembly), // message serializer
                new MessageAuditor(), // receives received, started, and failed notifications
                new MessageTypeToTopicProvider( // mapping between .NET message types and topics
                    messageTopics
                ),
                new HandlerTypeToChannelDictionary( // mapping between IHandleMessages<T> implementations and channels
                    handlerChannels
                ),
                preCreateTopicsAndChannels: true, // pre-create topics so we dont have to wait for an nsqlookupd cycle
                defaultNsqLookupdHttpEndpoints: configuration.LookupdHttpEndpoints, // nsqlookupd address
                defaultThreadsPerHandler: 1, // threads per handler. tweak based on use case, see handlers in this project.
                logOnProcessCrash: true
            );
        }

        public void Start()
        {
            _busConfiguration.StartBus();
        }

        public void Dispose()
        {
            _busConfiguration.StopBus();
        }
    }
}
