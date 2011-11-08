using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Bson.Serialization;
using System.Reflection;

namespace Ncqrs.Eventing.Storage.MongoDB
{
    public class MongoDBEventStore : IEventStore
    {
        private readonly MongoServer _mongoServer;
        private readonly string _databaseName;
        private readonly SafeMode _safeMode;
        private readonly string _collectionName;
        public MongoDBEventStore(
            MongoServer mongoServer, 
            SafeMode safeMode,
            string databaseName = "EventsStore", 
            string collectionName="Commits"
            )
        {
            _mongoServer = mongoServer;
            _databaseName = databaseName;
            _collectionName = collectionName;
            _safeMode = safeMode;
            SetupClassMap();
        }

        private void SetupClassMap()
        {
            BsonClassMap.RegisterClassMap<MongoStoredEvent>(cm =>
            {
                cm.MapProperty(m => m.EventIdentifier);
                cm.MapProperty(m => m.EventSequence);
                cm.MapProperty(m => m.EventSourceId);
                cm.MapProperty(m => m.EventTimeStamp);
                cm.MapProperty(m => m.Version);                
                cm.MapProperty(m => m.Data);
            });
            RegisterAllEventTypes();
        }

        private void RegisterAllEventTypes()
        {
            var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes());
            var eventTypes = allTypes.Where(t => typeof(Event).IsAssignableFrom(t));

            var reg2 = typeof(BsonClassMap);
            var regMethod = reg2.GetMethod("RegisterClassMap", new Type[] {  });
            var regMethodsGeneric = eventTypes.Select(et => regMethod.MakeGenericMethod(et));

            foreach (var registerMethod in regMethodsGeneric)
            {
                registerMethod.Invoke(null, null);
            }
        }
        public CommittedEventStream ReadFrom(Guid eventSourceId, long minVersion, long maxVersion)
        {
            _mongoServer.Connect();

            var db = _mongoServer.GetDatabase(_databaseName);
            var coll = db.GetCollection<MongoCommit>(_collectionName);

            var query = Query.ElemMatch(
                "Events", 
                Query.And(
                    Query.EQ("EventSourceId",eventSourceId),
                    Query.GTE("EventSequence", minVersion),
                    Query.LTE("EventSequence", maxVersion)
                    )
                );

            var commits = coll.Find(query);
            var events = commits.SelectMany(
                c => 
                    c.Events.Where(evt=>evt.EventSourceId == eventSourceId) // Get only events related to the correct source Id
                    .Select(evt=> new { c.CommitId, EventData = evt}) // Build a temp object containing the commit id and the event data
                );

            var result = new CommittedEventStream(
                eventSourceId, 
                events.Select(evt=>new CommittedEvent(
                    evt.CommitId,
                    evt.EventData.EventIdentifier,
                    evt.EventData.EventSourceId,
                    evt.EventData.EventSequence,
                    evt.EventData.EventTimeStamp,
                    evt.EventData.Data,
                    evt.EventData.Version
                    ))
                );

            _mongoServer.Disconnect();

            return result;
        }

        public void Store(UncommittedEventStream eventStream)
        {
            var commit = new MongoCommit
            {
                CommitId = eventStream.CommitId,
                Events = eventStream.Select(evt => new MongoStoredEvent
                {
                    Data = evt.Payload,
                    EventIdentifier = evt.EventIdentifier,
                    EventSequence = evt.EventSequence,
                    EventSourceId = evt.EventSourceId,
                    EventTimeStamp = evt.EventTimeStamp,
                    Version = evt.EventVersion
                }).ToList()
            };

            _mongoServer.Connect();

            var db = _mongoServer.GetDatabase(_databaseName);
            var coll = db.GetCollection<MongoCommit>(_collectionName);

            coll.Save(commit, _safeMode);

            _mongoServer.Disconnect();

        }
    }
}
