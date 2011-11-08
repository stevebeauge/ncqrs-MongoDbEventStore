using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Ncqrs.Eventing.Storage.MongoDB
{
    public class MongoCommit
    {
        [BsonId(IdGenerator = typeof(CombGuidGenerator))]
        public Guid CommitId { get; set; }
        
        public List<MongoStoredEvent> Events { get; set;}
    }
}
