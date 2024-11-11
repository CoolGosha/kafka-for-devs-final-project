using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System;

namespace GisConsumes.Net
{
    public class Unit
    {
        public required string UnitErpId { get; set; }

        public required string Name { get; set; }

        public required string Imei { get; set; }

        public required string Pu { get; set; }
    }

    public class UnitsGroup
    {
        public required string GroupId { get; set; }

        public required string Name { get; set; }

        public required string Pu { get; set; }
    }

    public class Order
    {
        public required string OrderErpId { get; set; }

        public required string Imei { get; set; }

        public required DateTime BeginAt { get; set; }

        public required DateTime EndAt { get; set; }
    }

    public class Message
    {
        public required string Imei { get; set; }

        public required double Lat { get; set; }

        public required double Lon { get; set; }

        public required DateTime Created { get; set; }

        public override string ToString()
        {
            return $"Imei: {Imei}, Latitude: {Lat}, Longitude: {Lon}, Created: {Created}";
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            string schemaRegistryUrl = "192.168.81.253:8081";

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };

            var unitsConfig = new ConsumerConfig
            {
                BootstrapServers = "192.168.81.253:19092, 192.168.81.253:29092, 192.168.81.253:39092",
                GroupId = $"consumer_group-{Guid.NewGuid()}",
                GroupInstanceId = $"CONSUMER-{Guid.NewGuid()}",
                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 5000,
                MaxPollIntervalMs = 10000,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AllowAutoCreateTopics = false,
                MaxPartitionFetchBytes = 81920
            };

            Dictionary<string, Unit> units = new Dictionary<string, Unit>();
            Dictionary<string, bool> unitsInRoutes = new Dictionary<string, bool>();

            var consumeUnitsTask = Task.Run(() =>
            {
                using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
                using (var consumer = new ConsumerBuilder<string, GenericRecord>(unitsConfig).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync()).Build())
                {
                    consumer.Subscribe("units");
                    int cnt = 0;
                    try
                    {
                        while (true)
                        {
                            try
                            {
                                var cr = consumer.Consume(cts.Token);
                                var avroUnit = cr.Message.Value;
                                if (avroUnit != null)
                                {
                                    Unit unit = new Unit()
                                    {
                                        UnitErpId = (string)avroUnit.GetValue(0),
                                        Name = (string)avroUnit.GetValue(1),
                                        Imei = (string)avroUnit.GetValue(2),
                                        Pu = (string)avroUnit.GetValue(3)
                                    };
                                    if (units.ContainsKey(unit.Imei))
                                    {
                                        units[unit.Imei] = unit;
                                    }
                                    else
                                    {
                                        units.Add(unit.Imei, unit);
                                        if (!unitsInRoutes.ContainsKey(unit.Imei))
                                        {
                                            unitsInRoutes.Add(unit.Imei, false);
                                        }
                                    }
                                }
                                cnt++;
                            }
                            catch (ConsumeException e)
                            {
                                Console.WriteLine($"Consume error: {e.Error.Reason}");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (cnt > 0)
                        {
                            consumer.Commit();
                        }
                    }
                    finally
                    {
                        consumer.Close();
                    }
                }
            });

            var unitsGroupsConfig = new ConsumerConfig
            {
                BootstrapServers = "192.168.81.253:19092, 192.168.81.253:29092, 192.168.81.253:39092",
                GroupId = $"consumer_group-{Guid.NewGuid()}",
                GroupInstanceId = $"CONSUMER-{Guid.NewGuid()}",
                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 5000,
                MaxPollIntervalMs = 10000,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AllowAutoCreateTopics = false,
                MaxPartitionFetchBytes = 81920
            };

            Dictionary<string, UnitsGroup> unitsGroups = new Dictionary<string, UnitsGroup>();

            var consumeUnitsGroupsTask = Task.Run(() =>
            {
                using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
                using (var consumer = new ConsumerBuilder<string, GenericRecord>(unitsGroupsConfig).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync()).Build())
                {
                    consumer.Subscribe("units_groups");
                    int cnt = 0;
                    try
                    {
                        while (true)
                        {
                            var cr = consumer.Consume(cts.Token);
                            var avroUnitsGroup = cr.Message.Value;
                            if (avroUnitsGroup != null)
                            {
                                UnitsGroup unitsGroup = new UnitsGroup()
                                {
                                    GroupId = (string)avroUnitsGroup.GetValue(0),
                                    Name = (string)avroUnitsGroup.GetValue(1),
                                    Pu = (string)avroUnitsGroup.GetValue(2)
                                };
                                if (unitsGroups.ContainsKey(unitsGroup.GroupId))
                                {
                                    unitsGroups[unitsGroup.GroupId] = unitsGroup;
                                }
                                else
                                {
                                    unitsGroups.Add(unitsGroup.GroupId, unitsGroup);
                                }
                            }
                            cnt++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (cnt > 0)
                        {
                            consumer.Commit();
                        }
                    }
                    finally
                    {
                        consumer.Close();
                    }
                }
            });

            var ordersConfig = new ConsumerConfig
            {
                BootstrapServers = "192.168.81.253:19092, 192.168.81.253:29092, 192.168.81.253:39092",
                GroupId = $"consumer_group-{Guid.NewGuid()}",
                GroupInstanceId = $"CONSUMER-{Guid.NewGuid()}",
                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 5000,
                MaxPollIntervalMs = 10000,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AllowAutoCreateTopics = false,
                MaxPartitionFetchBytes = 81920
            };

            Dictionary<string, Order> orders = new Dictionary<string, Order>();

            var consumeOrdersTask = Task.Run(() =>
            {
                using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
                using (var consumer = new ConsumerBuilder<string, GenericRecord>(ordersConfig).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync()).Build())
                {
                    consumer.Subscribe("orders");
                    int cnt = 0;
                    try
                    {
                        while (true)
                        {
                            var cr = consumer.Consume(cts.Token);
                            var avroOrder = cr.Message.Value;
                            if (avroOrder != null)
                            {
                                Order order = new Order()
                                {
                                    OrderErpId = (string)avroOrder.GetValue(0),
                                    Imei = (string)avroOrder.GetValue(1),
                                    BeginAt = ((DateTime)avroOrder.GetValue(2)).ToLocalTime(),
                                    EndAt = ((DateTime)avroOrder.GetValue(3)).ToLocalTime()
                                };
                                if (order.EndAt > DateTime.Now)
                                {
                                    orders.Add(order.OrderErpId, order);
                                }
                            }
                            cnt++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (cnt > 0)
                        {
                            consumer.Commit();
                        }
                    }
                    finally
                    {
                        consumer.Close();
                    }
                }
            });

            var messagesConfig = new ConsumerConfig
            {
                BootstrapServers = "192.168.81.253:19092, 192.168.81.253:29092, 192.168.81.253:39092",
                GroupId = $"consumer_group-{Guid.NewGuid()}",
                GroupInstanceId = $"CONSUMER-{Guid.NewGuid()}",
                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 5000,
                MaxPollIntervalMs = 10000,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AllowAutoCreateTopics = false,
                MaxPartitionFetchBytes = 81920
            };

            Thread.Sleep(10000);
            
            using (var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = schemaRegistryUrl }))
            using (var consumer = new ConsumerBuilder<string, GenericRecord>(messagesConfig).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync()).Build())
            {

                consumer.Subscribe("messages");
                int cnt = 0;
                try
                {
                    while (true)
                    {  
                        cts.CancelAfter(TimeSpan.FromSeconds(100)); // Здесь я указываю, сколько времени консьюмер будет ожидать новых сообщений
                        var cr = consumer.Consume(cts.Token);
                        var avroMessage = cr.Message.Value;
                        if (avroMessage != null)
                        {
                            Message message = new Message()
                            {
                                Imei = (string)avroMessage.GetValue(0),
                                Lat = (double)avroMessage.GetValue(1),
                                Lon = (double)avroMessage.GetValue(2),
                                Created = ((DateTime)avroMessage.GetValue(3)).ToLocalTime()
                            };
                            //Console.WriteLine(message.ToString());

                            try
                            {
                                if (unitsInRoutes[message.Imei])
                                {
                                    Console.WriteLine($"Message from unit en route received. {message}");
                                }
                                else
                                {
                                    Unit unit = units[message.Imei];
                                    bool isInGroup = false;
                                    foreach (var unitGroup in unitsGroups)
                                    {
                                        if (unit.Pu == unitGroup.Value.Pu)
                                        {
                                            isInGroup = true;
                                            break;
                                        }
                                    }
                                    if (isInGroup)
                                    {
                                        //Console.WriteLine($"Unit {unit.Imei}, ordersCount = {orders.Count}");
                                        foreach (var order in orders)
                                        {
                                            if ((message.Imei == order.Value.Imei) && (message.Created >= order.Value.BeginAt))
                                            {
                                                unitsInRoutes[message.Imei] = true;
                                                Console.WriteLine($"Unit {unit.Imei} set en route!");
                                                Console.WriteLine($"Message from unit en route received. {message}");
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }

                        List<string> ordersToDelete = new List<string>();
                        foreach (var order in orders)
                        {
                            if (order.Value.EndAt < DateTime.Now)
                            {
                                ordersToDelete.Add(order.Key);
                            }
                        }
                        foreach (string orderToDelete in ordersToDelete)
                        {
                            unitsInRoutes[orders[orderToDelete].Imei] = false;
                            Console.WriteLine($"Unit {orders[orderToDelete].Imei} unset en route!");
                            orders.Remove(orderToDelete);
                        }

                        cnt++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ctrl-C was pressed or timeout expired.
                    if (cnt > 0)
                    {
                        consumer.Commit();
                    }
                }
                finally
                {
                    consumer.Close();
                }
            }
        }
    }
}
