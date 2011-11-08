using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;

namespace Ncqrs.Eventing.Storage.MongoDB
{
    public class MongoStoredEvent
    {
        public long EventSequence { get; set; }
        public Guid EventSourceId { get; set; }
        public Guid EventIdentifier { get; set; }      
        public DateTime EventTimeStamp { get; set; }
        public Version Version { get; set; }
        public object Data { get; set; }

        
    }
}
