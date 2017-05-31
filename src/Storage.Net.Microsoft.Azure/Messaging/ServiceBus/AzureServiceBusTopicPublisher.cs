﻿#if NETFULL
using System;
using Storage.Net.Messaging;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Storage.Net.Microsoft.Azure.Messaging.ServiceBus
{
   /// <summary>
   /// Represents queues as Azure Service Bus Topics. Note that you must have at least one subscription
   /// for messages not to be lost. Subscriptions represent <see cref="AzureServiceBusTopicReceiver"/>
   /// in this library
   /// </summary>
   public class AzureServiceBusTopicPublisher : AsyncMessagePublisher
   {
      private NamespaceManager _nsMgr;
      private readonly string _connectionString;
      private readonly string _topicName;
      private TopicClient _client;

      /// <summary>
      /// Creates an instance of Azure Service Bus Topic publisher
      /// </summary>
      /// <param name="connectionString">Service Bus connection string</param>
      /// <param name="topicName">Name of the Service Bus topic</param>
      public AzureServiceBusTopicPublisher(string connectionString, string topicName)
      {
         _connectionString = connectionString;
         _nsMgr = NamespaceManager.CreateFromConnectionString(connectionString);
         _topicName = topicName;

         TopicHelper.PrepareTopic(_nsMgr, topicName);
         _client = TopicClient.CreateFromConnectionString(_connectionString, _topicName);
      }

      /// <summary>
      /// Sends a <see cref="BrokeredMessage"/> with passed content
      /// </summary>
      public override async Task PutMessagesAsync(IEnumerable<QueueMessage> messages)
      {
         if (messages == null) return;
         IEnumerable<BrokeredMessage> bms = messages.Select(Converter.ToBrokeredMessage);
         await _client.SendBatchAsync(bms);
      }
   }
}
#endif