using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace RepeaterProducer.Net
{
    public class RawMessage
    {
        public string Imei { get; set; }

        public double Lat { get; set; }

        public double Lon { get; set; }

        public DateTime Created { get; set; }

        public RawMessage(Random rnd) 
        {
            Imei = $"862531044307{rnd.Next(400, 500):000}";
            Lat = 50 + rnd.NextDouble() * 10;
            Lon = 40 + rnd.NextDouble() * 10;
            Created = DateTime.Now;
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

                ClientId = "repeater_kafka_producer_local_net",

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

            var rawMessageSchema = (RecordSchema)RecordSchema.Parse(
@"{
    ""type"": ""record"",
    ""name"": ""RawMessage"",
    ""fields"": [
        {""name"": ""imei"", ""type"": ""string""},
        {""name"": ""lat"", ""type"": ""double""},
        {""name"": ""lon"", ""type"": ""double""},
        {""name"": ""created"", ""type"": {""type"": ""long"", ""logicalType"": ""timestamp-millis""}},
    ]
}"
            );

            using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
            using (var producer = new ProducerBuilder<string, GenericRecord>(config).SetKeySerializer(Serializers.Utf8).SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry)).Build())
            {
                while (!cancelClicked)
                {
                    RawMessage rawMessage = new RawMessage(rnd);
                    string key = rawMessage.Imei;

                    var rawMessageRecord = new GenericRecord(rawMessageSchema);
                    rawMessageRecord.Add("imei", rawMessage.Imei);
                    rawMessageRecord.Add("lat", rawMessage.Lat);
                    rawMessageRecord.Add("lon", rawMessage.Lon);
                    rawMessageRecord.Add("created", rawMessage.Created);

                    try
                    {
                        var res = producer.ProduceAsync("raw_messages", new Message<string, GenericRecord> { Key = key, Value = rawMessageRecord });
                        Console.WriteLine($"Produced event to topic [raw_messages]: key = {key} value = {rawMessageRecord}");
                    }
                    catch (ProduceException<string, GenericRecord> ex)
                    {
                        Console.WriteLine($"Failed to deliver raw_message message: {ex}");
                    }

                    Thread.Sleep(10);
                }

                producer.Flush();
            }
        }
    }
}
