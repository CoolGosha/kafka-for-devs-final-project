using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace ErpProducer.Net
{
    public class Unit
    {
        public string UnitErpId { get; set; }

        public string Name { get; set; }

        public string Imei { get; set; }

        public string Pu { get; set; }

        public DateTime Changed { get; set; }

        public Unit(Random rnd)
        {
            string idPart = rnd.Next(400, 500).ToString("000");
            UnitErpId = $"000000000010000{idPart}";
            Name = $"k{rnd.Next(1, 999):000}fk31rus";
            Imei = $"862531044307{idPart}";
            Pu = rnd.Next(1, 99).ToString("00");
            Changed = DateTime.Now;
        }
    }

    public class Order
    {
        public string OrderErpId { get; set; }

        public string Imei { get; set; }

        public DateTime BeginAt { get; set; }

        public DateTime EndAt { get; set; }

        public DateTime Changed { get; set; }

        public Order(Random rnd)
        {
            Changed = DateTime.Now;
            OrderErpId = $"{Changed.Ticks:00000000000000000000}";
            Imei = $"862531044307{rnd.Next(400, 500):000}";
            Changed = DateTime.Now;
            BeginAt = Changed.AddSeconds(rnd.Next(100));
            EndAt = BeginAt.AddSeconds(rnd.Next(300));
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Random rnd = new Random();

            string schemaRegistryUrl = "127.0.0.1:8081";

            var config = new ProducerConfig
            {
                BootstrapServers = "127.0.0.1:19092, 127.0.0.1:29092, 127.0.0.1:39092",
                
                ClientId = "erp_kafka_producer_local_net",

                Acks = Acks.All,

                LingerMs = 20,

                MaxInFlight = 5,
                MessageMaxBytes = 1048576,
                SocketReceiveBufferBytes = 1048576,
                SocketSendBufferBytes = 1048576,
                EnableIdempotence = true,

                CompressionType = CompressionType.Gzip,

                BatchSize = 64 * 1024,
            };

            bool cancelClicked = false;

            Console.CancelKeyPress += (_, e) => {
                Console.WriteLine("^C");
                e.Cancel = true; // prevent the process from terminating.
                cancelClicked = true;
            };

            var unitSchema = (RecordSchema)RecordSchema.Parse(
@"{
    ""type"": ""record"",
    ""name"": ""Unit"",
    ""fields"": [
        {""name"": ""unit_erp_id"", ""type"": ""string""},
        {""name"": ""name"", ""type"": ""string""},
        {""name"": ""imei"", ""type"": ""string""},
        {""name"": ""pu"", ""type"": ""string""},
        {""name"": ""changed"", ""type"": {""type"": ""long"", ""logicalType"": ""timestamp-millis""}},
    ]
}"
            );

            var orderSchema = (RecordSchema)RecordSchema.Parse(
@"{
    ""type"": ""record"",
    ""name"": ""Order"",
    ""fields"": [
        {""name"": ""order_erp_id"", ""type"": ""string""},
        {""name"": ""imei"", ""type"": ""string""},
        {""name"": ""changed"", ""type"": {""type"": ""long"", ""logicalType"": ""timestamp-millis""}},
        {""name"": ""begin_at"", ""type"": {""type"": ""long"", ""logicalType"": ""timestamp-millis""}},
        {""name"": ""end_at"", ""type"": {""type"": ""long"", ""logicalType"": ""timestamp-millis""}},
    ]
}"
            );

            using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
            using (var producer = new ProducerBuilder<string, GenericRecord>(config).SetKeySerializer(Serializers.Utf8).SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry)).Build())
            {
                while (!cancelClicked)
                {
                    if (rnd.Next(100) < 10)
                    {
                        Unit unit = new Unit(rnd);
                        string key = unit.UnitErpId;
                        
                        var unitRecord = new GenericRecord(unitSchema);
                        unitRecord.Add("unit_erp_id", unit.UnitErpId);
                        unitRecord.Add("name", unit.Name);
                        unitRecord.Add("imei", unit.Imei);
                        unitRecord.Add("pu", unit.Pu);
                        unitRecord.Add("changed", unit.Changed);

                        try
                        {
                            var res = producer.ProduceAsync("units", new Message<string, GenericRecord> { Key = key, Value = unitRecord });
                            Console.WriteLine($"Produced event to topic [units]: key = {key} value = {unitRecord}");
                        }
                        catch (ProduceException<string, GenericRecord> ex)
                        {
                            Console.WriteLine($"Failed to deliver unit message: {ex}");
                        }
                    }

                    if (rnd.Next(100) < 70)
                    {
                        Order order = new Order(rnd);
                        string key = order.OrderErpId;

                        var orderRecord = new GenericRecord(orderSchema);
                        orderRecord.Add("order_erp_id", order.OrderErpId);
                        orderRecord.Add("imei", order.Imei);
                        orderRecord.Add("changed", order.Changed);
                        orderRecord.Add("begin_at", order.BeginAt);
                        orderRecord.Add("end_at", order.EndAt);


                        try
                        {
                            var res = producer.ProduceAsync("orders", new Message<string, GenericRecord> { Key = key, Value = orderRecord });
                            Console.WriteLine($"Produced event to topic [orders]: key = {key} value = {orderRecord}"); 
                        }
                        catch (ProduceException<string, GenericRecord> ex)
                        {
                            Console.WriteLine($"Failed to deliver order message: {ex}");
                        }
                    }

                    Thread.Sleep(1000);
                }

                producer.Flush();
            }
        }
    }
}
